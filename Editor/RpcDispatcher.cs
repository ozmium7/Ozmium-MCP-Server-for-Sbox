using System;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handles JSON-RPC method dispatch for the MCP server.
/// Translates incoming method names into tool handler calls and sends
/// the result back over the SSE connection via McpServer.SendSseEvent.
/// </summary>
internal static class RpcDispatcher
{
	/// <summary>
	/// Parses and dispatches a single JSON-RPC request, then sends the response
	/// as an SSE event on the given session.
	/// </summary>
	internal static async Task ProcessRpcRequest(
		McpSession session,
		object id,
		string method,
		string rawBody,
		JsonSerializerOptions jsonOptions,
		Action<string> logInfo,
		Action<string> logError )
	{
		object result = null;
		object error  = null;

		using var doc = JsonDocument.Parse( rawBody );
		var root      = doc.RootElement;

		try
		{
			if ( method == "initialize" )
			{
				result = new
				{
					protocolVersion = "2024-11-05",
					capabilities    = new { tools = new { listChanged = true } },
					serverInfo      = new { name = "SboxMcpServer", version = "1.2.0" }
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

				// run_console_command is dispatched through its own method so that
				// its try/catch can intercept exceptions thrown by ConsoleSystem.Run
				// on the main thread (nested catches in async methods don't reliably
				// catch these in s&box's sandbox environment).
				if ( toolName == "run_console_command" )
				{
					result = RunConsoleCommandSafe( args );
				}
				else
				{
					// Scene API calls must run on the main thread.
					await GameTask.MainThread();

					result = toolName switch
					{
						// ── Read tools ───────────────────────────────────────────────────────
						"get_scene_summary"           => SceneToolHandlers.GetSceneSummary( jsonOptions ),
						"get_scene_hierarchy"         => SceneToolHandlers.GetSceneHierarchy( args ),
						"find_game_objects"           => SceneToolHandlers.FindGameObjects( args, jsonOptions ),
						"find_game_objects_in_radius" => SceneToolHandlers.FindGameObjectsInRadius( args, jsonOptions ),
						"get_game_object_details"     => SceneToolHandlers.GetGameObjectDetails( args, jsonOptions ),
						"get_component_properties"    => SceneToolHandlers.GetComponentProperties( args, jsonOptions ),
						"get_prefab_instances"        => SceneToolHandlers.GetPrefabInstances( args, jsonOptions ),
						// ── Asset + console ──────────────────────────────────────────────────
						"browse_assets"               => AssetToolHandlers.BrowseAssets( args, jsonOptions ),
						"get_editor_context"          => AssetToolHandlers.GetEditorContext( jsonOptions ),
						"list_console_commands"       => ConsoleToolHandlers.ListConsoleCommands( args, jsonOptions ),
						// ── Write tools ──────────────────────────────────────────────────────
						"create_game_object"          => OzmiumWriteHandlers.CreateGameObject( args ),
						"add_component"               => OzmiumWriteHandlers.AddComponent( args ),
						"remove_component"            => OzmiumWriteHandlers.RemoveComponent( args ),
						"set_component_property"      => OzmiumWriteHandlers.SetComponentProperty( args ),
						"destroy_game_object"         => OzmiumWriteHandlers.DestroyGameObject( args ),
						"reparent_game_object"        => OzmiumWriteHandlers.ReparentGameObject( args ),
						"set_game_object_tags"        => OzmiumWriteHandlers.SetGameObjectTags( args ),
						"instantiate_prefab"          => OzmiumWriteHandlers.InstantiatePrefab( args ),
						"save_scene"                  => OzmiumWriteHandlers.SaveScene(),
						"undo"                        => OzmiumWriteHandlers.Undo(),
						"redo"                        => OzmiumWriteHandlers.Redo(),
						// ── Extended asset tools ─────────────────────────────────────────────
						"get_model_info"              => OzmiumAssetHandlers.GetModelInfo( args ),
						"get_material_properties"     => OzmiumAssetHandlers.GetMaterialProperties( args ),
						"get_prefab_structure"        => OzmiumAssetHandlers.GetPrefabStructure( args ),
						"reload_asset"                => OzmiumAssetHandlers.ReloadAsset( args ),
						// ── Editor control ───────────────────────────────────────────────────
						"select_game_object"          => OzmiumEditorHandlers.SelectGameObject( args ),
						"open_asset"                  => OzmiumEditorHandlers.OpenAsset( args ),
						"get_play_state"              => OzmiumEditorHandlers.GetPlayState(),
						"start_play_mode"             => OzmiumEditorHandlers.StartPlayMode(),
						"stop_play_mode"              => OzmiumEditorHandlers.StopPlayMode(),
						"get_editor_log"              => OzmiumEditorHandlers.GetEditorLog( args ),
						_                             => throw new InvalidOperationException( $"Tool '{toolName}' not found" )
					};
				}

				logInfo( $"Tool: {toolName}" );
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
			logError( $"ProcessRpcRequest catch: method={method} ex={ex.Message}" );

			// For run_console_command, convert engine exceptions into a friendly text result.
			// Parse rawBody fresh since root/doc may be in an uncertain state after the fault.
			if ( method == "tools/call" )
			{
				string toolNameCatch = null;
				string cmdStrCatch   = "?";
				try
				{
					var bodyDoc  = JsonDocument.Parse( rawBody );
					var paramsEl = bodyDoc.RootElement.GetProperty( "params" );
					toolNameCatch = paramsEl.GetProperty( "name" ).GetString();
					if ( paramsEl.TryGetProperty( "arguments", out var argsEl ) &&
						argsEl.TryGetProperty( "command", out var cmdEl ) )
						cmdStrCatch = cmdEl.GetString() ?? "?";
				}
				catch ( Exception parseEx )
				{
					logError( $"ProcessRpcRequest catch parse error: {parseEx.Message}" );
				}

				logError( $"ProcessRpcRequest catch: toolName={toolNameCatch}" );

				if ( toolNameCatch == "run_console_command" )
				{
					result = ToolHandlerBase.TextResult( $"Command failed: {cmdStrCatch}\nError: {ex.Message}" );
					error  = null;
				}
				else
				{
					error = new { code = -32603, message = $"Internal error: {ex.Message}" };
				}
			}
			else
			{
				error = new { code = -32603, message = $"Internal error: {ex.Message}" };
			}
		}

		var response = new { jsonrpc = "2.0", id, result, error };
		var json     = JsonSerializer.Serialize( response, jsonOptions );
		await McpServer.SendSseEvent( session, "message", json );
	}

	/// <summary>
	/// Runs run_console_command in a plain try/catch (no async context) so that
	/// exceptions from ConsoleSystem are catchable.
	/// </summary>
	private static object RunConsoleCommandSafe( JsonElement args )
	{
		var cmdStr = args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty( "command", out var cp )
			? cp.GetString()
			: "";
		try
		{
			return ConsoleToolHandlers.RunConsoleCommand( args );
		}
		catch ( Exception ex )
		{
			return ToolHandlerBase.TextResult( $"Command failed: {cmdStr}\nError: {ex.Message}" );
		}
	}
}
