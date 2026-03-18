using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handlers for editor control MCP tools:
/// select_game_object, open_asset, get_play_state, start/stop play mode,
/// get_editor_log, list_console_commands, run_console_command.
/// </summary>
internal static class OzmiumEditorHandlers
{
	private static readonly JsonSerializerOptions _json = new()
	{
		PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	// Circular log buffer — editor feeds into this from LogMessage
	private static readonly System.Collections.Concurrent.ConcurrentQueue<string> _log
		= new System.Collections.Concurrent.ConcurrentQueue<string>();
	private const int MaxLogLines = 500;

	internal static void AppendLog( string msg )
	{
		_log.Enqueue( msg );
		while ( _log.Count > MaxLogLines ) _log.TryDequeue( out _ );
	}

	// ── select_game_object ──────────────────────────────────────────────────

	internal static object SelectGameObject( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string id   = Get( args, "id",   (string)null );
		string name = Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return Txt( $"Object not found: id='{id}' name='{name}'." );

		try
		{
			var session = SceneEditorSession.Active;
			if ( session != null )
			{
				// Use reflection to access Selection.Set — avoids hard dependency on Selection type
				var selProp = session.GetType().GetProperty( "Selection",
					System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
				var selObj = selProp?.GetValue( session );
				if ( selObj != null )
				{
					var setMethod = selObj.GetType().GetMethod( "Set",
						new[] { typeof( GameObject ) } );
					setMethod?.Invoke( selObj, new object[] { go } );
				}
			}
			return Txt( $"Selected '{go.Name}' (ID: {go.Id})." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── open_asset ──────────────────────────────────────────────────────────

	internal static object OpenAsset( JsonElement args )
	{
		string path = Get( args, "path", (string)null );
		if ( string.IsNullOrEmpty( path ) ) return Txt( "Provide 'path'." );

		try
		{
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null ) return Txt( $"Asset not found: '{path}'." );
			asset.OpenInEditor();
			return Txt( $"Opened '{path}' in editor." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_play_state ──────────────────────────────────────────────────────

	internal static object GetPlayState()
	{
		var session = SceneEditorSession.Active;
		var state = session?.IsPlaying == true ? "Playing" : "Stopped";
		return Txt( JsonSerializer.Serialize( new { playState = state }, _json ) );
	}

	// ── start_play_mode ─────────────────────────────────────────────────────

	internal static object StartPlayMode()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return Txt( "No editor session." );
			if ( session.IsPlaying ) return Txt( "Already playing." );
			session.SetPlaying( session.Scene );
			return Txt( "Play mode started." );
		}
		catch ( Exception ex ) { return Txt( $"Error starting play mode: {ex.Message}" ); }
	}

	// ── stop_play_mode ──────────────────────────────────────────────────────

	internal static object StopPlayMode()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return Txt( "No editor session." );
			if ( !session.IsPlaying ) return Txt( "Already stopped." );
			session.StopPlaying();
			return Txt( "Play mode stopped." );
		}
		catch ( Exception ex ) { return Txt( $"Error stopping play mode: {ex.Message}" ); }
	}

	// ── get_editor_log ──────────────────────────────────────────────────────

	internal static object GetEditorLog( JsonElement args )
	{
		int lines = Get( args, "lines", 50 );
		var recent = _log.TakeLast( lines ).ToList();
		return Txt( string.Join( "\n", recent ) );
	}

	// ── list_console_commands ───────────────────────────────────────────────

	internal static object ListConsoleCommands( JsonElement args )
	{
		string filter = Get( args, "filter", (string)null );
		var entries = new List<Dictionary<string, object>>();

		foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() )
		{
			try
			{
				foreach ( var type in asm.GetTypes() )
					foreach ( var prop in type.GetProperties(
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.NonPublic |
						System.Reflection.BindingFlags.Static ) )
					{
						var attr = prop.GetCustomAttributes( typeof( ConVarAttribute ), false ).FirstOrDefault() as ConVarAttribute;
						if ( attr == null ) continue;
						var cvarName = !string.IsNullOrEmpty( attr.Name ) ? attr.Name : prop.Name.ToLowerInvariant();
						if ( !string.IsNullOrEmpty( filter ) && cvarName.IndexOf( filter, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
						string val = null;
						try { val = Sandbox.ConsoleSystem.GetValue( cvarName ); } catch { }
						entries.Add( new Dictionary<string, object>
						{
							["name"] = cvarName, ["help"] = attr.Help ?? "",
							["flags"] = attr.Flags.ToString(), ["saved"] = attr.Flags.HasFlag( ConVarFlags.Saved ),
							["currentValue"] = val, ["declaringType"] = type.Name
						} );
					}
			}
			catch { }
		}

		entries = entries.GroupBy( e => e["name"]?.ToString() ).Select( g => g.First() )
			.OrderBy( e => e["name"]?.ToString() ).ToList();

		return Txt( JsonSerializer.Serialize( new { summary = $"Found {entries.Count} [ConVar] entries{( !string.IsNullOrEmpty( filter ) ? $" matching '{filter}'" : "" )}.", entries, skippedAssemblies = Array.Empty<string>() }, _json ) );
	}

	// ── run_console_command ─────────────────────────────────────────────────

	internal static object RunConsoleCommand( JsonElement args )
	{
		var cmd   = args.GetProperty( "command" ).GetString()?.Trim() ?? "";
		var parts = cmd.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 ) return Txt( "Provide a command." );

		var cmdName = parts[0];
		string current = null;
		try { current = Sandbox.ConsoleSystem.GetValue( cmdName ); } catch { }

		if ( current == null )
			return Txt( $"Unknown convar '{cmdName}'. Only [ConVar] properties are supported." );

		if ( parts.Length == 1 ) return Txt( $"{cmdName} = {current}" );

		var newVal = string.Join( " ", parts.Skip( 1 ) );
		Sandbox.ConsoleSystem.SetValue( cmdName, newVal );
		string readback = null;
		try { readback = Sandbox.ConsoleSystem.GetValue( cmdName ); } catch { }
		return Txt( $"Set {cmdName} = {readback ?? newVal}" );
	}

	// ── Helpers ────────────────────────────────────────────────────────────

	private static object Txt( string text ) => new { content = new object[] { new { type = "text", text } } };

	private static T Get<T>( JsonElement el, string key, T def )
	{
		if ( el.ValueKind == JsonValueKind.Undefined ) return def;
		if ( !el.TryGetProperty( key, out var p ) ) return def;
		try
		{
			var t = typeof( T );
			if ( t == typeof( string ) ) return (T)(object)( p.ValueKind == JsonValueKind.Null ? null : p.GetString() );
			if ( t == typeof( bool ) )   return (T)(object)p.GetBoolean();
			if ( t == typeof( int ) )    return (T)(object)p.GetInt32();
			if ( t == typeof( float ) )  return (T)(object)p.GetSingle();
			return def;
		}
		catch { return def; }
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	private static Dictionary<string, object> S( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}

	internal static Dictionary<string, object> SchemaSelectGameObject => S( "select_game_object",
		"Select a GameObject in the editor hierarchy and viewport.",
		new Dictionary<string, object>
		{
			["id"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." }
		} );

	internal static Dictionary<string, object> SchemaOpenAsset => S( "open_asset",
		"Open an asset in its default editor (scene, prefab, material, etc.).",
		new Dictionary<string, object> { ["path"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Asset path to open." } },
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaGetPlayState => S( "get_play_state",
		"Returns the current play state: 'Playing' or 'Stopped'.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaStartPlayMode => S( "start_play_mode",
		"Press the Play button in the editor.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaStopPlayMode => S( "stop_play_mode",
		"Press the Stop button in the editor.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaGetEditorLog => S( "get_editor_log",
		"Return recent log lines captured from the editor output.",
		new Dictionary<string, object> { ["lines"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Number of recent lines (default 50)." } } );
}

