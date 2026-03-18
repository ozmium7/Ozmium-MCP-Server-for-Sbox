using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// HTTP/SSE server that exposes MCP tools to AI clients.
/// Handles transport concerns only — tool logic lives in ToolHandlers,
/// schemas in ToolDefinitions, and scene helpers in SceneQueryHelpers.
/// </summary>
public static class McpServer
{
	[ConVar( "mcp_server_port", ConVarFlags.Saved )]
	public static int Port { get; set; } = 8098;

	// ── GUI events & state ─────────────────────────────────────────────────
	public static event Action OnServerStateChanged;
	public static event Action<string> OnLogMessage;
	public static bool IsRunning  => _listener != null && _listener.IsListening;
	public static int  SessionCount => _sessions.Count;

	private static void LogInfo( string msg )  { Log.Info( msg );  OnLogMessage?.Invoke( msg ); }
	private static void LogError( string msg ) { Log.Error( msg ); OnLogMessage?.Invoke( $"[ERROR] {msg}" ); }

	// ── Internal state ─────────────────────────────────────────────────────
	private static HttpListener _listener;
	private static CancellationTokenSource _cts;
	private static readonly ConcurrentDictionary<string, McpSession> _sessions = new();

	internal static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition      = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented               = false
	};

	// ── Lifecycle ──────────────────────────────────────────────────────────

	public static void StartServer()
	{
		if ( _listener != null && _listener.IsListening )
		{
			Log.Info( "MCP Server is already running" );
			return;
		}

		try
		{
			_listener = new HttpListener();
			_listener.Prefixes.Add( $"http://localhost:{Port}/" );
			_listener.Prefixes.Add( $"http://127.0.0.1:{Port}/" );
			_listener.Start();

			_cts = new CancellationTokenSource();
			Task.Run( () => ListenLoop( _cts.Token ) );

			LogInfo( $"Started Model Context Protocol Server on port {Port}" );
			OnServerStateChanged?.Invoke();
		}
		catch ( Exception ex )
		{
			LogError( $"Failed to start MCP Server: {ex.Message}" );
		}
	}

	public static void StopServer()
	{
		_cts?.Cancel();
		_listener?.Stop();
		_listener?.Close();
		_listener = null;

		foreach ( var session in _sessions.Values )
		{
			session.Tcs.TrySetResult( true );
			try { session.SseResponse?.Close(); } catch { }
		}
		_sessions.Clear();

		LogInfo( "Stopped Model Context Protocol Server" );
		OnServerStateChanged?.Invoke();
	}

	// ── HTTP listen loop ───────────────────────────────────────────────────

	private static async Task ListenLoop( CancellationToken token )
	{
		while ( !token.IsCancellationRequested && _listener != null && _listener.IsListening )
		{
			try
			{
				var context = await _listener.GetContextAsync();
				_ = Task.Run( () => HandleContext( context ), token );
			}
			catch ( Exception ex ) when ( ex is not ObjectDisposedException )
			{
				Log.Error( $"Error in MCP listen loop: {ex.Message}" );
			}
		}
	}

	private static async Task HandleContext( HttpListenerContext context )
	{
		var req = context.Request;
		var res = context.Response;

		res.Headers.Add( "Access-Control-Allow-Origin",  "*" );
		res.Headers.Add( "Access-Control-Allow-Methods", "GET, POST, OPTIONS" );
		res.Headers.Add( "Access-Control-Allow-Headers", "*" );

		if ( req.HttpMethod == "OPTIONS" ) { res.StatusCode = 200; res.Close(); return; }

		try
		{
			if      ( req.Url.AbsolutePath == "/sse"     && req.HttpMethod == "GET"  ) await HandleSse( req, res );
			else if ( req.Url.AbsolutePath == "/message" && req.HttpMethod == "POST" ) await HandleMessage( req, res );
			else    { res.StatusCode = 404; res.Close(); }
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error handling MCP request: {ex.Message}" );
			res.StatusCode = 500;
			res.Close();
		}
	}

	// ── SSE connection ─────────────────────────────────────────────────────

	private static async Task HandleSse( HttpListenerRequest req, HttpListenerResponse res )
	{
		var sessionId = Guid.NewGuid().ToString();
		var session   = new McpSession { SessionId = sessionId, SseResponse = res };
		_sessions[sessionId] = session;

		res.ContentType = "text/event-stream";
		res.Headers.Add( "Cache-Control", "no-cache" );
		res.Headers.Add( "Connection",    "keep-alive" );

		try
		{
			var msg    = $"event: endpoint\ndata: /message?sessionId={sessionId}\n\n";
			var buffer = Encoding.UTF8.GetBytes( msg );
			await res.OutputStream.WriteAsync( buffer, 0, buffer.Length );
			await res.OutputStream.FlushAsync();

			LogInfo( $"Created new MCP SSE session: {sessionId}" );
			OnServerStateChanged?.Invoke();

			await session.Tcs.Task; // keep alive until closed
		}
		catch ( Exception ex ) { LogError( $"SSE connection error: {ex.Message}" ); }
		finally
		{
			_sessions.TryRemove( sessionId, out _ );
			try { res.Close(); } catch { }
			LogInfo( $"Closed MCP SSE session: {sessionId}" );
			OnServerStateChanged?.Invoke();
		}
	}

	// ── Message (JSON-RPC) ─────────────────────────────────────────────────

	private static async Task HandleMessage( HttpListenerRequest req, HttpListenerResponse res )
	{
		var sessionId = req.QueryString["sessionId"];
		if ( string.IsNullOrEmpty( sessionId ) || !_sessions.TryGetValue( sessionId, out var session ) )
		{
			res.StatusCode = 400; res.Close(); return;
		}

		using var reader = new StreamReader( req.InputStream, Encoding.UTF8 );
		var body = await reader.ReadToEndAsync();

		try
		{
			using var doc  = JsonDocument.Parse( body );
			var root       = doc.RootElement;
			string method  = root.TryGetProperty( "method", out var m ) ? m.GetString() : null;
			object id      = null;

			if ( root.TryGetProperty( "id", out var idProp ) )
			{
				if      ( idProp.ValueKind == JsonValueKind.Number ) id = idProp.GetInt32();
				else if ( idProp.ValueKind == JsonValueKind.String ) id = idProp.GetString();
			}

			res.StatusCode = 202;
			res.Close();

			if ( id != null )
			{
				var bodyCopy = body;
				_ = Task.Run( async () => await ProcessRpcRequest( session, id, method, bodyCopy ) );
			}
			else if ( method == "notifications/initialized" )
			{
				session.Initialized = true;
				LogInfo( $"MCP Session {sessionId} initialized." );
				OnServerStateChanged?.Invoke();
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error parsing JSON-RPC: {ex.Message}" );
		}
	}

	// ── RPC dispatch ───────────────────────────────────────────────────────

	private static async Task ProcessRpcRequest( McpSession session, object id, string method, string rawBody )
	{
		object result = null;
		object error  = null;

		using var doc  = JsonDocument.Parse( rawBody );
		var root       = doc.RootElement;

		try
		{
			if ( method == "initialize" )
			{
				result = new
				{
					protocolVersion = "2024-11-05",
					capabilities    = new { tools = new { listChanged = true } },
					serverInfo      = new { name = "SboxMcpServer", version = "1.1.0" }
				};
			}
			else if ( method == "tools/list" )
			{
				result = new { tools = ToolDefinitions.All };
			}
			else if ( method == "tools/call" )
			{
				var args     = root.TryGetProperty( "params", out var p ) && p.TryGetProperty( "arguments", out var a ) ? a : default;
				var toolName = root.GetProperty( "params" ).GetProperty( "name" ).GetString();

				result = toolName switch
				{
					"get_scene_hierarchy"    => ToolHandlers.GetSceneHierarchy( args ),
					"find_game_objects"      => ToolHandlers.FindGameObjects( args, JsonOptions ),
					"get_game_object_details"=> ToolHandlers.GetGameObjectDetails( args, JsonOptions ),
					"get_scene_summary"      => ToolHandlers.GetSceneSummary( JsonOptions ),
					"run_console_command"    => ToolHandlers.RunConsoleCommand( args ),
					_                        => throw new InvalidOperationException( $"Tool '{toolName}' not found" )
				};

				LogInfo( $"Tool: {toolName}" );
			}
			else
			{
				error = new { code = -32601, message = $"Method '{method}' not found" };
			}
		}
		catch ( ArgumentException ex )
		{
			// Invalid parameters (e.g. missing required arg)
			error = new { code = -32602, message = ex.Message };
		}
		catch ( Exception ex )
		{
			error = new { code = -32603, message = $"Internal error: {ex.Message}" };
		}

		var response = new { jsonrpc = "2.0", id, result, error };
		var json     = JsonSerializer.Serialize( response, JsonOptions );
		await SendSseEvent( session, "message", json );
	}

	// ── SSE write ─────────────────────────────────────────────────────────

	private static async Task SendSseEvent( McpSession session, string eventName, string data )
	{
		if ( session.SseResponse == null || !session.SseResponse.OutputStream.CanWrite ) return;
		try
		{
			var msg    = $"event: {eventName}\ndata: {data}\n\n";
			var buffer = Encoding.UTF8.GetBytes( msg );
			await session.SseResponse.OutputStream.WriteAsync( buffer, 0, buffer.Length );
			await session.SseResponse.OutputStream.FlushAsync();
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to send SSE event to session {session.SessionId}: {ex.Message}" );
		}
	}
}
