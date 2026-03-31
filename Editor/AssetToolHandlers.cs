using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handlers for asset-browsing and editor-context MCP tools:
/// browse_assets and get_editor_context.
/// </summary>
internal static class AssetToolHandlers
{
	// ── browse_assets ──────────────────────────────────────────────────────

	internal static object BrowseAssets( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string typeFilter = null;
		string nameFilter = null;
		int    maxResults = 100;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "type",        out var tp ) ) typeFilter = tp.GetString();
			if ( args.TryGetProperty( "nameContains", out var np ) ) nameFilter = np.GetString();
			if ( args.TryGetProperty( "maxResults",   out var mr ) ) maxResults = Math.Clamp( mr.GetInt32(), 1, 500 );
		}

		var results   = new List<Dictionary<string, object>>();
		int totalSeen = 0;

		try
		{
			foreach ( var asset in AssetSystem.All )
			{
				totalSeen++;

				var assetType = asset.AssetType?.FileExtension ?? "";
				var assetName = asset.Name ?? "";

				// Type filter: match by file extension or friendly type name (case-insensitive)
				if ( !string.IsNullOrEmpty( typeFilter ) )
				{
					bool typeMatch =
						assetType.IndexOf( typeFilter, StringComparison.OrdinalIgnoreCase ) >= 0 ||
						( asset.AssetType?.FriendlyName ?? "" ).IndexOf( typeFilter, StringComparison.OrdinalIgnoreCase ) >= 0;
					if ( !typeMatch ) continue;
				}

				// Name filter: match basename (case-insensitive)
				if ( !string.IsNullOrEmpty( nameFilter ) &&
					assetName.IndexOf( nameFilter, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;

				if ( results.Count >= maxResults ) break;

				results.Add( new Dictionary<string, object>
				{
					["path"]       = asset.Path ?? "",
					["name"]       = assetName,
					["type"]       = asset.AssetType?.FriendlyName ?? assetType,
					["extension"]  = assetType,
					["relativePath"] = asset.RelativePath ?? asset.Path ?? ""
				} );
			}
		}
		catch ( Exception ex )
		{
			return OzmiumSceneHelpers.Txt( $"Error enumerating assets: {ex.Message}" );
		}

		var summary = $"Found {results.Count} asset(s)" +
			( !string.IsNullOrEmpty( typeFilter ) ? $" of type '{typeFilter}'" : "" ) +
			( !string.IsNullOrEmpty( nameFilter ) ? $" matching '{nameFilter}'" : "" ) +
			$" (scanned {totalSeen} total).";

		var json = JsonSerializer.Serialize( new { summary, results }, jsonOptions );
		return OzmiumSceneHelpers.Txt( json );
	}

	// ── get_editor_context ─────────────────────────────────────────────────

	internal static object GetEditorContext( JsonSerializerOptions jsonOptions )
	{
		var ctx = new Dictionary<string, object>();

		// Active game scene (in-play)
		ctx["activeGameScene"] = Game.ActiveScene?.Name;

		// Editor session info
		try
		{
			var sessions = new List<Dictionary<string, object>>();
			foreach ( var session in SceneEditorSession.All )
			{
				if ( session == null ) continue;
				sessions.Add( new Dictionary<string, object>
				{
					["isActive"]    = session == SceneEditorSession.Active,
					["sceneName"]   = session.Scene?.Name,
					["isPrefab"]    = session.Scene?.Name?.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) ?? false,
					["objectCount"] = session.Scene != null
						? session.Scene.Children.Count
						: 0
				} );
			}
			ctx["editorSessions"]       = sessions;
			ctx["activeSessionScene"]   = SceneEditorSession.Active?.Scene?.Name;

			// Selection
			var sel = new List<Dictionary<string, object>>();
			foreach ( var go in OzmiumSceneHelpers.GetSelectedGameObjects() )
			{
				sel.Add( new Dictionary<string, object>
				{
					["id"]   = go.Id.ToString(),
					["name"] = go.Name,
					["path"] = OzmiumSceneHelpers.GetObjectPath( go )
				} );
			}
			ctx["selectedObjects"] = sel;
		}
		catch ( Exception ex )
		{
			ctx["editorApiError"] = ex.Message;
		}

		var json = JsonSerializer.Serialize( ctx, jsonOptions );
		return OzmiumSceneHelpers.Txt( json );
	}
}
