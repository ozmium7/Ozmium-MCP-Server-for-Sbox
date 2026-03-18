using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json.Serialization;
using System.Text.Json;
using System.Net;
using System.Text;
using System.IO;
using System.Collections.Concurrent;
using System.Linq;
using Editor;
using Sandbox;

namespace SboxMcpServer;

public class McpSession
{
	public string SessionId { get; set; }
	public HttpListenerResponse SseResponse { get; set; }
	public TaskCompletionSource<bool> Tcs { get; set; } = new TaskCompletionSource<bool>();
	public bool Initialized { get; set; }
}

public static class McpServer
{
	[ConVar("mcp_server_port", ConVarFlags.Saved)]
	public static int Port { get; set; } = 8098;

	// GUI Events & Properties
	public static event Action OnServerStateChanged;
	public static event Action<string> OnLogMessage;
	public static bool IsRunning => _listener != null && _listener.IsListening;
	public static int SessionCount => _sessions.Count;
	
	private static void LogInfo(string msg) {
		Log.Info(msg);
		OnLogMessage?.Invoke(msg);
	}
	
	private static void LogError(string msg) {
		Log.Error(msg);
		OnLogMessage?.Invoke($"[ERROR] {msg}");
	}

	private static HttpListener _listener;
	private static CancellationTokenSource _cts;
	private static readonly ConcurrentDictionary<string, McpSession> _sessions = new();

	private static readonly JsonSerializerOptions _jsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
		WriteIndented = false
	};

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
		
		foreach (var session in _sessions.Values)
		{
			session.Tcs.TrySetResult(true);
			try { session.SseResponse?.Close(); } catch { }
		}
		_sessions.Clear();

		LogInfo( "Stopped Model Context Protocol Server" );
		OnServerStateChanged?.Invoke();
	}

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

		res.Headers.Add( "Access-Control-Allow-Origin", "*" );
		res.Headers.Add( "Access-Control-Allow-Methods", "GET, POST, OPTIONS" );
		res.Headers.Add( "Access-Control-Allow-Headers", "*" );

		if ( req.HttpMethod == "OPTIONS" )
		{
			res.StatusCode = 200;
			res.Close();
			return;
		}

		try
		{
			if ( req.Url.AbsolutePath == "/sse" && req.HttpMethod == "GET" )
			{
				await HandleSse( req, res );
			}
			else if ( req.Url.AbsolutePath == "/message" && req.HttpMethod == "POST" )
			{
				await HandleMessage( req, res );
			}
			else
			{
				res.StatusCode = 404;
				res.Close();
			}
		}
		catch ( Exception ex )
		{
			Log.Error( $"Error handling MCP request: {ex.Message}" );
			res.StatusCode = 500;
			res.Close();
		}
	}

	private static async Task HandleSse( HttpListenerRequest req, HttpListenerResponse res )
	{
		var sessionId = Guid.NewGuid().ToString();
		var session = new McpSession { SessionId = sessionId, SseResponse = res };
		_sessions[sessionId] = session;

		res.ContentType = "text/event-stream";
		res.Headers.Add( "Cache-Control", "no-cache" );
		res.Headers.Add( "Connection", "keep-alive" );

		try
		{
			var endpointUrl = $"/message?sessionId={sessionId}";
			var message = $"event: endpoint\ndata: {endpointUrl}\n\n";
			var buffer = Encoding.UTF8.GetBytes( message );
			
			await res.OutputStream.WriteAsync( buffer, 0, buffer.Length );
			await res.OutputStream.FlushAsync();

			LogInfo($"Created new MCP SSE session: {sessionId}");
			OnServerStateChanged?.Invoke();

			// Keep connection open
			await session.Tcs.Task;
		}
		catch ( Exception ex )
		{
			LogError( $"SSE connection error: {ex.Message}" );
		}
		finally
		{
			_sessions.TryRemove( sessionId, out _ );
			try { res.Close(); } catch { }
			LogInfo($"Closed MCP SSE session: {sessionId}");
			OnServerStateChanged?.Invoke();
		}
	}

	private static async Task HandleMessage( HttpListenerRequest req, HttpListenerResponse res )
	{
		var sessionId = req.QueryString["sessionId"];
		if ( string.IsNullOrEmpty( sessionId ) || !_sessions.TryGetValue( sessionId, out var session ) )
		{
			res.StatusCode = 400;
			res.Close();
			return;
		}

		using var reader = new StreamReader( req.InputStream, Encoding.UTF8 );
		var body = await reader.ReadToEndAsync();

		try
		{
			using var doc = JsonDocument.Parse( body );
			var root = doc.RootElement;
			
			string method = root.TryGetProperty("method", out var m) ? m.GetString() : null;
			object id = null;
			
			if (root.TryGetProperty("id", out var idProp))
			{
				if (idProp.ValueKind == JsonValueKind.Number) id = idProp.GetInt32();
				else if (idProp.ValueKind == JsonValueKind.String) id = idProp.GetString();
			}

			if (id != null)
			{
				// It's a request — close HTTP response immediately (202), process async
				res.StatusCode = 202;
				res.Close();

				// Pass the raw body string — ProcessRpcRequest owns the JsonDocument
				var bodyCopy = body;
				_ = Task.Run( async () => await ProcessRpcRequest( session, id, method, bodyCopy ) );
			}
			else
			{
				// It's a notification
				res.StatusCode = 202;
				res.Close();
				if (method == "notifications/initialized")
				{
					session.Initialized = true;
					LogInfo($"MCP Session {sessionId} initialized.");
					OnServerStateChanged?.Invoke();
				}
			}
		}
		catch (Exception ex)
		{
			Log.Error($"Error parsing JSON-RPC: {ex.Message}");
			res.StatusCode = 400;
			res.Close();
		}
	}

	private static async Task ProcessRpcRequest( McpSession session, object id, string method, string rawBody )
	{
		object result = null;
		object error = null;

		using var doc = JsonDocument.Parse( rawBody );
		var root = doc.RootElement;

		try
		{
			if ( method == "initialize" )
			{
				result = new
				{
					protocolVersion = "2024-11-05",
					capabilities = new { tools = new { listChanged = true } },
					serverInfo = new { name = "SboxMcpServer", version = "1.0.0" }
				};
			}
			else if ( method == "tools/list" )
			{
				// Use explicit object array to avoid CS0826
				var toolList = new object[]
				{
					new Dictionary<string, object> {
						["name"] = "get_scene_hierarchy",
						["description"] = "Lists all GameObjects in the current active scene.",
						["inputSchema"] = new Dictionary<string, object> {
							["type"] = "object",
							["properties"] = new Dictionary<string, object>()
						}
					},
					new Dictionary<string, object> {
						["name"] = "run_console_command",
						["description"] = "Runs a console command in the S&box editor.",
						["inputSchema"] = new Dictionary<string, object> {
							["type"] = "object",
							["properties"] = new Dictionary<string, object> {
								["command"] = new Dictionary<string, object> {
									["type"] = "string",
									["description"] = "The console command to run."
								}
							},
							["required"] = new[] { "command" }
						}
					}
				};
				result = new { tools = toolList };
			}
			else if ( method == "tools/call" )
			{
				var args = root.TryGetProperty("params", out var p) && p.TryGetProperty("arguments", out var a) ? a : default;
				var toolName = root.GetProperty("params").GetProperty("name").GetString();
				
				if (toolName == "get_scene_hierarchy")
				{
					var sceneStr = "";
					var scene = Game.ActiveScene;
					if (scene != null) {
						sceneStr = $"Active Scene: {scene.Name}\n";
						foreach (var go in scene.GetAllObjects(true)) {
							sceneStr += $"- {go.Name} (ID: {go.Id})\n";
						}
					} else {
						sceneStr = "No active scene.";
					}
					LogInfo( $"Tool: get_scene_hierarchy called" );
					result = new {
						content = new object[] {
							new { type = "text", text = sceneStr }
						}
					};
				}
				else if (toolName == "run_console_command")
				{
					var cmd = args.GetProperty("command").GetString();
					Sandbox.ConsoleSystem.Run(cmd);
					LogInfo( $"Tool: run_console_command({cmd})" );
					result = new {
						content = new object[] {
							new { type = "text", text = $"Ran command: {cmd}" }
						}
					};
				}
				else
				{
					error = new { code = -32601, message = $"Tool {toolName} not found" };
				}
			}
			else
			{
				error = new { code = -32601, message = $"Method {method} not found" };
			}
		}
		catch ( Exception ex )
		{
			error = new { code = -32603, message = $"Internal error: {ex.Message}" };
		}

		var responseObj = new { jsonrpc = "2.0", id = id, result = result, error = error };
		var json = JsonSerializer.Serialize( responseObj, _jsonOptions );

		await SendSseEvent( session, "message", json );
	}

	private static async Task SendSseEvent( McpSession session, string eventName, string data )
	{
		if ( session.SseResponse == null || !session.SseResponse.OutputStream.CanWrite ) return;

		try
		{
			var message = $"event: {eventName}\ndata: {data}\n\n";
			var buffer = Encoding.UTF8.GetBytes( message );
			await session.SseResponse.OutputStream.WriteAsync( buffer, 0, buffer.Length );
			await session.SseResponse.OutputStream.FlushAsync();
		}
		catch ( Exception ex )
		{
			Log.Warning( $"Failed to send SSE event to session {session.SessionId}: {ex.Message}" );
		}
	}
}
