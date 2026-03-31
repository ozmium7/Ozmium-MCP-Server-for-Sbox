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
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

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
			return OzmiumSceneHelpers.Txt( $"Selected '{go.Name}' (ID: {go.Id})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── open_asset ──────────────────────────────────────────────────────────

	internal static object OpenAsset( JsonElement args )
	{
		string path = OzmiumSceneHelpers.Get( args, "path", (string)null );
		if ( string.IsNullOrEmpty( path ) ) return OzmiumSceneHelpers.Txt( "Provide 'path'." );

		try
		{
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null ) return OzmiumSceneHelpers.Txt( $"Asset not found: '{path}'." );
			asset.OpenInEditor();
			return OzmiumSceneHelpers.Txt( $"Opened '{path}' in editor." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_play_state ──────────────────────────────────────────────────────

	internal static object GetPlayState()
	{
		var session = SceneEditorSession.Active;
		var state = session?.IsPlaying == true ? "Playing" : "Stopped";
		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new { playState = state }, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── start_play_mode ─────────────────────────────────────────────────────

	internal static object StartPlayMode()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session." );
			if ( session.IsPlaying ) return OzmiumSceneHelpers.Txt( "Already playing." );
			session.SetPlaying( session.Scene );
			return OzmiumSceneHelpers.Txt( "Play mode started." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error starting play mode: {ex.Message}" ); }
	}

	// ── stop_play_mode ──────────────────────────────────────────────────────

	internal static object StopPlayMode()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session." );
			if ( !session.IsPlaying ) return OzmiumSceneHelpers.Txt( "Already stopped." );
			session.StopPlaying();
			return OzmiumSceneHelpers.Txt( "Play mode stopped." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error stopping play mode: {ex.Message}" ); }
	}

	// ── get_editor_log ──────────────────────────────────────────────────────

	internal static object GetEditorLog( JsonElement args )
	{
		int lines = OzmiumSceneHelpers.Get( args, "lines", 50 );
		var recent = _log.TakeLast( lines ).ToList();
		return OzmiumSceneHelpers.Txt( string.Join( "\n", recent ) );
	}

	// ── list_console_commands ───────────────────────────────────────────────

	internal static object ListConsoleCommands( JsonElement args )
	{
		string filter = OzmiumSceneHelpers.Get( args, "filter", (string)null );
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

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new { summary = $"Found {entries.Count} [ConVar] entries{( !string.IsNullOrEmpty( filter ) ? $" matching '{filter}'" : "" )}.", entries, skippedAssemblies = Array.Empty<string>() }, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── run_console_command ─────────────────────────────────────────────────

	internal static object RunConsoleCommand( JsonElement args )
	{
		var cmd   = args.GetProperty( "command" ).GetString()?.Trim() ?? "";
		var parts = cmd.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		if ( parts.Length == 0 ) return OzmiumSceneHelpers.Txt( "Provide a command." );

		var cmdName = parts[0];
		string current = null;
		try { current = Sandbox.ConsoleSystem.GetValue( cmdName ); } catch { }

		if ( current == null )
			return OzmiumSceneHelpers.Txt( $"Unknown convar '{cmdName}'. Only [ConVar] properties are supported." );

		if ( parts.Length == 1 ) return OzmiumSceneHelpers.Txt( $"{cmdName} = {current}" );

		var newVal = string.Join( " ", parts.Skip( 1 ) );
		Sandbox.ConsoleSystem.SetValue( cmdName, newVal );
		string readback = null;
		try { readback = Sandbox.ConsoleSystem.GetValue( cmdName ); } catch { }
		return OzmiumSceneHelpers.Txt( $"Set {cmdName} = {readback ?? newVal}" );
	}

	// ── get_selected_objects ──────────────────────────────────────────────

	internal static object GetSelectedObjects()
	{
		var selected = OzmiumSceneHelpers.GetSelectedGameObjects();
		var results = selected.Select( go => OzmiumSceneHelpers.BuildSummary( go ) ).ToList();
		var summary = $"{results.Count} object(s) selected.";
		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new { summary, results }, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── set_selected_objects ──────────────────────────────────────────────

	internal static object SetSelectedObjects( JsonElement args )
	{
		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as a string array of GUIDs." );

		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session active." );

			var selProp = session.GetType().GetProperty( "Selection",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
			var selObj = selProp?.GetValue( session );
			if ( selObj == null ) return OzmiumSceneHelpers.Txt( "Selection API not available." );

			// Clear then Add each object — SelectionSystem has Set(object) and Add(object),
			// not Set(IEnumerable). Using Add after Clear supports multi-select.
			var clearMethod = selObj.GetType().GetMethod( "Clear" );
			clearMethod?.Invoke( selObj, null );

			var addMethod = selObj.GetType().GetMethod( "Add", new[] { typeof( object ) } );
			if ( addMethod == null ) return OzmiumSceneHelpers.Txt( "Selection.Add not available." );

			var scene = OzmiumSceneHelpers.ResolveScene();
			if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

			int count = 0;
			foreach ( var idEl in idsEl.EnumerateArray() )
			{
				var guidStr = idEl.GetString();
				if ( !string.IsNullOrEmpty( guidStr ) && Guid.TryParse( guidStr, out var guid ) )
				{
					var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
					if ( go != null )
					{
						addMethod.Invoke( selObj, new object[] { go } );
						count++;
					}
				}
			}
			return OzmiumSceneHelpers.Txt( $"Selected {count} object(s)." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── clear_selection ──────────────────────────────────────────────────

	internal static object ClearSelection()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session active." );

			var selProp = session.GetType().GetProperty( "Selection",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
			var selObj = selProp?.GetValue( session );
			if ( selObj == null ) return OzmiumSceneHelpers.Txt( "Selection API not available." );

			var clearMethod = selObj.GetType().GetMethod( "Clear" );
			clearMethod?.Invoke( selObj, null );

			return OzmiumSceneHelpers.Txt( "Selection cleared." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── select_by_tag ──────────────────────────────────────────────────

	internal static object SelectByTag( JsonElement args )
	{
		string tag = OzmiumSceneHelpers.Get( args, "tag", (string)null );
		if ( string.IsNullOrEmpty( tag ) ) return OzmiumSceneHelpers.Txt( "Provide 'tag'." );

		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session active." );

			var selProp = session.GetType().GetProperty( "Selection",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
			var selObj = selProp?.GetValue( session );
			if ( selObj == null ) return OzmiumSceneHelpers.Txt( "Selection API not available." );

			var clearMethod = selObj.GetType().GetMethod( "Clear" );
			clearMethod?.Invoke( selObj, null );

			var addMethod = selObj.GetType().GetMethod( "Add", new[] { typeof( object ) } );
			if ( addMethod == null ) return OzmiumSceneHelpers.Txt( "Selection.Add not available." );

			int count = 0;
			foreach ( var go in OzmiumSceneHelpers.WalkAll( scene, true ) )
			{
				if ( go.Tags.Contains( tag ) )
				{
					addMethod.Invoke( selObj, new object[] { go } );
					count++;
				}
			}

			return OzmiumSceneHelpers.Txt( $"Selected {count} object(s) with tag '{tag}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── select_children ────────────────────────────────────────────────

	internal static object SelectChildren( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var parent = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( parent == null ) return OzmiumSceneHelpers.Txt( $"Parent not found: id='{id}' name='{name}'." );

		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session active." );

			var selProp = session.GetType().GetProperty( "Selection",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
			var selObj = selProp?.GetValue( session );
			if ( selObj == null ) return OzmiumSceneHelpers.Txt( "Selection API not available." );

			var clearMethod = selObj.GetType().GetMethod( "Clear" );
			clearMethod?.Invoke( selObj, null );

			var addMethod = selObj.GetType().GetMethod( "Add", new[] { typeof( object ) } );
			if ( addMethod == null ) return OzmiumSceneHelpers.Txt( "Selection.Add not available." );

			int count = 0;
			foreach ( var go in OzmiumSceneHelpers.WalkSubtree( parent, true ) )
			{
				// Skip the parent itself, select only children/descendants
				if ( go.Id == parent.Id ) continue;
				addMethod.Invoke( selObj, new object[] { go } );
				count++;
			}

			return OzmiumSceneHelpers.Txt( $"Selected {count} child(ren) of '{parent.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaSelectGameObject => OzmiumSceneHelpers.S( "select_game_object",
		"Select a GameObject in the editor hierarchy and viewport.",
		new Dictionary<string, object>
		{
			["id"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." }
		} );

	internal static Dictionary<string, object> SchemaOpenAsset => OzmiumSceneHelpers.S( "open_asset",
		"Open an asset in its default editor (scene, prefab, material, etc.).",
		new Dictionary<string, object> { ["path"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Asset path to open." } },
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaGetPlayState => OzmiumSceneHelpers.S( "get_play_state",
		"Returns the current play state: 'Playing' or 'Stopped'.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaStartPlayMode => OzmiumSceneHelpers.S( "start_play_mode",
		"Press the Play button in the editor.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaStopPlayMode => OzmiumSceneHelpers.S( "stop_play_mode",
		"Press the Stop button in the editor.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaGetEditorLog => OzmiumSceneHelpers.S( "get_editor_log",
		"Return recent log lines captured from the editor output.",
		new Dictionary<string, object> { ["lines"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Number of recent lines (default 50)." } } );

	internal static Dictionary<string, object> SchemaGetSelectedObjects => OzmiumSceneHelpers.S( "get_selected_objects",
		"Return the currently selected objects in the editor.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaSetSelectedObjects => OzmiumSceneHelpers.S( "set_selected_objects",
		"Select multiple objects at once.",
		new Dictionary<string, object>
		{
			["ids"] = new Dictionary<string, object>
			{
				["type"] = "array",
				["description"] = "Array of GUID strings to select.",
				["items"] = new Dictionary<string, object> { ["type"] = "string" }
			}
		},
		new[] { "ids" } );

	internal static Dictionary<string, object> SchemaClearSelection => OzmiumSceneHelpers.S( "clear_selection",
		"Clear the editor selection.",
		new Dictionary<string, object>() );

	// ── frame_selection ──────────────────────────────────────────────────

	private static BBox GetGameObjectBounds( GameObject go )
	{
		// Try collider bounds first (most accurate)
		var collider = go.Components.GetAll().FirstOrDefault( c => c is Collider ) as Collider;
		if ( collider != null )
			return collider.GetWorldBounds();

		// Fallback: use position with a small extent
		return BBox.FromPositionAndSize( go.WorldPosition, 1f );
	}

	private static BBox CombineBounds( IEnumerable<GameObject> objects )
	{
		var first = true;
		BBox result = default;
		foreach ( var obj in objects )
		{
			var b = GetGameObjectBounds( obj );
			if ( first ) { result = b; first = false; }
			else { result = new BBox( Vector3.Min( result.Mins, b.Mins ), Vector3.Max( result.Maxs, b.Maxs ) ); }
		}
		return result;
	}

	internal static object FrameSelection( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session." );

			BBox bounds;

			// If specific objects are given, compute their combined bounds
			if ( args.TryGetProperty( "ids", out var idsEl ) && idsEl.ValueKind == JsonValueKind.Array )
			{
				var objects = new List<GameObject>();
				foreach ( var idEl in idsEl.EnumerateArray() )
				{
					var guidStr = idEl.GetString();
					if ( !string.IsNullOrEmpty( guidStr ) && Guid.TryParse( guidStr, out var guid ) )
					{
						var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
						if ( go != null ) objects.Add( go );
					}
				}
				if ( objects.Count == 0 ) return OzmiumSceneHelpers.Txt( "No matching objects found." );

				bounds = CombineBounds( objects );
			}
			else
			{
				// Default: use current selection
				var selected = OzmiumSceneHelpers.GetSelectedGameObjects().ToList();
				if ( selected.Count == 0 ) return OzmiumSceneHelpers.Txt( "No selection to frame." );

				bounds = CombineBounds( selected );
			}

			// Use reflection to call FrameTo(in BBox) on the session
			var frameMethod = session.GetType().GetMethod( "FrameTo",
				new[] { typeof( BBox ) } );
			if ( frameMethod == null ) return OzmiumSceneHelpers.Txt( "FrameTo not available on this session type." );

			frameMethod.Invoke( session, new object[] { bounds } );
			return OzmiumSceneHelpers.Txt( $"Framed selection to {bounds}." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── save_scene_as ──────────────────────────────────────────────────

	internal static object SaveSceneAs( JsonElement args )
	{
		string path = OzmiumSceneHelpers.Get( args, "path", (string)null );
		if ( string.IsNullOrEmpty( path ) ) return OzmiumSceneHelpers.Txt( "Provide 'path' for the new scene file." );

		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session." );

			// Use reflection: ISceneEditorSession.Save(bool forceSaveAs)
			// The Save method with forceSaveAs=true triggers Save As dialog-like behavior.
			// However, the API doesn't directly accept a path parameter — it uses the engine's
			// file dialog. We'll note this limitation.
			var saveMethod = session.GetType().GetMethod( "Save", new[] { typeof( bool ) } );
			if ( saveMethod != null )
			{
				saveMethod.Invoke( session, new object[] { true } );
				return OzmiumSceneHelpers.Txt( $"Save As dialog opened. Note: S&box API does not support programmatic path; user must choose '{path}' in the dialog." );
			}

			return OzmiumSceneHelpers.Txt( "Save method not available on this session type." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_scene_unsaved ───────────────────────────────────────────────

	internal static object GetSceneUnsaved()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				hasUnsavedChanges = false,
				message = "No editor session active."
			}, OzmiumSceneHelpers.JsonSettings ) );

			var hasUnsaved = session.HasUnsavedChanges;
			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				hasUnsavedChanges = hasUnsaved,
				message = hasUnsaved ? "Scene has unsaved changes." : "Scene is saved."
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── break_from_prefab ───────────────────────────────────────────────

	internal static object BreakFromPrefab( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

		try
		{
			go.BreakFromPrefab();
			return OzmiumSceneHelpers.Txt( $"Broke '{go.Name}' from its prefab source." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── update_from_prefab ──────────────────────────────────────────────

	internal static object UpdateFromPrefab( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

		try
		{
			go.UpdateFromPrefab();
			return OzmiumSceneHelpers.Txt( $"Updated '{go.Name}' from its prefab source." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Schemas (extensions) ─────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaFrameSelection => OzmiumSceneHelpers.S( "frame_selection",
		"Focus the editor camera on the current selection or specified objects.",
		new Dictionary<string, object>
		{
			["ids"] = new Dictionary<string, object>
			{
				["type"] = "array", ["description"] = "Optional GUID array. If omitted, uses current selection.",
				["items"] = new Dictionary<string, object> { ["type"] = "string" }
			}
		} );

	internal static Dictionary<string, object> SchemaSaveSceneAs => OzmiumSceneHelpers.S( "save_scene_as",
		"Save the current scene to a new file path (opens Save As dialog).",
		new Dictionary<string, object>
		{
			["path"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Desired file path." }
		} );

	internal static Dictionary<string, object> SchemaGetSceneUnsaved => OzmiumSceneHelpers.S( "get_scene_unsaved",
		"Check if the current scene has unsaved changes.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaBreakFromPrefab => OzmiumSceneHelpers.S( "break_from_prefab",
		"Break a prefab instance's connection to its source prefab.",
		new Dictionary<string, object>
		{
			["id"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." }
		} );

	internal static Dictionary<string, object> SchemaUpdateFromPrefab => OzmiumSceneHelpers.S( "update_from_prefab",
		"Update a prefab instance to match its source prefab.",
		new Dictionary<string, object>
		{
			["id"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." }
		} );

	// ── manage_selection (Omnibus) ────────────────────────────────────────

	internal static object ManageSelection( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"select"          => SelectGameObject( args ),
			"select_many"     => SetSelectedObjects( args ),
			"clear"           => ClearSelection(),
			"get_selected"    => GetSelectedObjects(),
			"select_by_tag"   => SelectByTag( args ),
			"select_children" => SelectChildren( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: select, select_many, clear, get_selected, select_by_tag, select_children" )
		};
	}

	internal static Dictionary<string, object> SchemaManageSelection => OzmiumSceneHelpers.S( "manage_selection",
		"Manage editor selection: select objects, select many, clear selection, get current selection, select by tag, or select children.",
		new Dictionary<string, object>
	{
		["operation"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "select", "select_many", "clear", "get_selected", "select_by_tag", "select_children" } },
		["id"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID (for select, select_children operations)." },
		["name"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name (for select, select_children operations)." },
		["ids"]       = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of GUIDs (for select_many).", ["items"] = new Dictionary<string, object> { ["type"] = "string" } },
		["tag"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tag to match (for select_by_tag)." }
	},
	new[] { "operation" } );

	// ── get_scene_info ──────────────────────────────────────────────────

	internal static object GetSceneInfo()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				scenePath = (string)null,
				sceneName = (string)null,
				message = "No editor session active."
			}, OzmiumSceneHelpers.JsonSettings ) );

			var scene = session.Scene;
			string resourcePath = scene?.Source?.ResourcePath;
			string sceneName = scene?.Name;

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				scenePath = resourcePath,
				sceneName = sceneName,
				isPrefabSession = session.IsPrefabSession
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── manage_editor_state (Omnibus) ────────────────────────────────────

	internal static object ManageEditorState( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"start_play" => StartPlayMode(),
			"stop_play"  => StopPlayMode(),
			"get_play_state" => GetPlayState(),
			"save_scene" => OzmiumWriteHandlers.SaveScene(),
			"get_scene_info" => GetSceneInfo(),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: start_play, stop_play, get_play_state, save_scene, get_scene_info" )
		};
	}

	internal static Dictionary<string, object> SchemaManageEditorState => OzmiumSceneHelpers.S( "manage_editor_state",
		"Manage editor state: start/stop play mode, get play state, save scene, get scene info.",
		new Dictionary<string, object>
	{
		["operation"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "start_play", "stop_play", "get_play_state", "save_scene", "get_scene_info" } }
	},
	new[] { "operation" } );
}
