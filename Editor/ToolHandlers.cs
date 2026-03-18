using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Implements the logic for each MCP tool call.
/// Each public method corresponds to one tool name and returns a result object
/// suitable for wrapping in a JSON-RPC response, or throws on hard errors.
/// </summary>
internal static class ToolHandlers
{
	// ── get_scene_hierarchy ────────────────────────────────────────────────

	internal static object GetSceneHierarchy( JsonElement args )
	{
		bool rootOnly = args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "rootOnly", out var roP ) && roP.GetBoolean();

		bool includeDisabled = !( args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "includeDisabled", out var idP ) && !idP.GetBoolean() );

		var sb    = new StringBuilder();
		var scene = Game.ActiveScene;

		if ( scene == null )
		{
			sb.Append( "No active scene." );
		}
		else
		{
			sb.AppendLine( $"Scene: {scene.Name}" );

			if ( rootOnly )
			{
				foreach ( var go in scene.Children )
				{
					if ( !includeDisabled && !go.Enabled ) continue;
					AppendHierarchyLine( sb, go, 0, showChildCount: true );
				}
			}
			else
			{
				void Walk( GameObject go, int depth )
				{
					if ( !includeDisabled && !go.Enabled ) return;
					AppendHierarchyLine( sb, go, depth, showChildCount: false );
					foreach ( var child in go.Children )
						Walk( child, depth + 1 );
				}
				foreach ( var go in scene.Children )
					Walk( go, 0 );
			}
		}

		return TextResult( sb.ToString() );
	}

	// ── find_game_objects ──────────────────────────────────────────────────

	internal static object FindGameObjects( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string nameContains = null;
		string hasTag       = null;
		string hasComponent = null;
		bool   enabledOnly  = false;
		int    maxResults   = 50;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "nameContains", out var nc ) ) nameContains = nc.GetString();
			if ( args.TryGetProperty( "hasTag",       out var ht ) ) hasTag       = ht.GetString();
			if ( args.TryGetProperty( "hasComponent", out var hc ) ) hasComponent = hc.GetString();
			if ( args.TryGetProperty( "enabledOnly",  out var eo ) ) enabledOnly  = eo.GetBoolean();
			if ( args.TryGetProperty( "maxResults",   out var mr ) ) maxResults   = Math.Clamp( mr.GetInt32(), 1, 500 );
		}

		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		var matches = new List<Dictionary<string, object>>();
		foreach ( var go in scene.GetAllObjects( true ) )
		{
			if ( matches.Count >= maxResults ) break;
			if ( enabledOnly && !go.Enabled ) continue;
			if ( !string.IsNullOrEmpty( nameContains ) &&
				go.Name.IndexOf( nameContains, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			if ( !string.IsNullOrEmpty( hasTag ) && !go.Tags.Has( hasTag ) ) continue;
			if ( !string.IsNullOrEmpty( hasComponent ) )
			{
				bool found = go.Components.GetAll().Any( c =>
					c.GetType().Name.IndexOf( hasComponent, StringComparison.OrdinalIgnoreCase ) >= 0 );
				if ( !found ) continue;
			}
			matches.Add( SceneQueryHelpers.BuildObjectSummary( go ) );
		}

		var totalAll = scene.GetAllObjects( true ).Count();
		var summary  = $"Found {matches.Count} matching object(s) (searched {totalAll} total).";
		if ( matches.Count >= maxResults )
			summary += $" Result limit ({maxResults}) reached — refine your filters for more specific results.";

		var json = JsonSerializer.Serialize( new { summary, results = matches }, jsonOptions );
		return TextResult( json );
	}

	// ── get_game_object_details ────────────────────────────────────────────

	internal static object GetGameObjectDetails( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string idStr   = null;
		string nameStr = null;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "id",   out var idP   ) ) idStr   = idP.GetString();
			if ( args.TryGetProperty( "name", out var nameP ) ) nameStr = nameP.GetString();
		}

		if ( string.IsNullOrEmpty( idStr ) && string.IsNullOrEmpty( nameStr ) )
			throw new ArgumentException( "Provide either 'id' or 'name'." );

		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		GameObject target = null;

		if ( !string.IsNullOrEmpty( idStr ) && Guid.TryParse( idStr, out var guid ) )
			target = scene.GetAllObjects( true ).FirstOrDefault( g => g.Id == guid );

		if ( target == null && !string.IsNullOrEmpty( nameStr ) )
			target = scene.GetAllObjects( true ).FirstOrDefault( g =>
				string.Equals( g.Name, nameStr, StringComparison.OrdinalIgnoreCase ) );

		if ( target == null )
			return TextResult( $"No GameObject found matching id='{idStr}' name='{nameStr}'." );

		var json = JsonSerializer.Serialize( SceneQueryHelpers.BuildObjectDetail( target ), jsonOptions );
		return TextResult( json );
	}

	// ── get_scene_summary ──────────────────────────────────────────────────

	internal static object GetSceneSummary( JsonSerializerOptions jsonOptions )
	{
		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		var allObjects  = scene.GetAllObjects( true ).ToList();
		var rootObjects = scene.Children.ToList();
		int totalCount  = allObjects.Count;
		int rootCount   = rootObjects.Count;
		int enabledCount = allObjects.Count( g => g.Enabled );

		// Component type frequency
		var compCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
		{
			foreach ( var comp in go.Components.GetAll() )
			{
				var typeName = comp.GetType().Name;
				compCounts.TryGetValue( typeName, out var existing );
				compCounts[typeName] = existing + 1;
			}
		}
		var topComponents = compCounts
			.OrderByDescending( kv => kv.Value )
			.Select( kv => new Dictionary<string, object> { ["type"] = kv.Key, ["count"] = kv.Value } )
			.ToList();

		// All unique tags
		var allTags = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
			foreach ( var tag in go.Tags.TryGetAll() )
				allTags.Add( tag );

		// Root object quick list
		var rootNames = rootObjects.Select( g => new Dictionary<string, object>
		{
			["name"]       = g.Name,
			["id"]         = g.Id.ToString(),
			["enabled"]    = g.Enabled,
			["childCount"] = g.Children.Count,
			["components"] = SceneQueryHelpers.GetComponentNames( g )
		} ).ToList();

		var summary = new Dictionary<string, object>
		{
			["sceneName"]          = scene.Name,
			["totalObjects"]       = totalCount,
			["rootObjects"]        = rootCount,
			["enabledObjects"]     = enabledCount,
			["disabledObjects"]    = totalCount - enabledCount,
			["uniqueTags"]         = allTags.OrderBy( t => t ).ToList(),
			["componentBreakdown"] = topComponents,
			["rootObjectList"]     = rootNames
		};

		var json = JsonSerializer.Serialize( summary, jsonOptions );
		return TextResult( json );
	}

	// ── run_console_command ────────────────────────────────────────────────

	internal static object RunConsoleCommand( JsonElement args )
	{
		var cmd = args.GetProperty( "command" ).GetString();
		Sandbox.ConsoleSystem.Run( cmd );
		return TextResult( $"Ran command: {cmd}" );
	}

	// ── Shared helpers ─────────────────────────────────────────────────────

	/// <summary>Wraps a plain text string in the MCP content envelope.</summary>
	internal static object TextResult( string text ) => new
	{
		content = new object[] { new { type = "text", text } }
	};

	private static void AppendHierarchyLine( StringBuilder sb, GameObject go, int depth, bool showChildCount )
	{
		var indent  = new string( ' ', depth * 2 );
		var comps   = SceneQueryHelpers.GetComponentNames( go );
		var tags    = SceneQueryHelpers.GetTags( go );
		var compStr = comps.Count > 0 ? $" [{string.Join( ", ", comps )}]" : "";
		var tagStr  = tags.Count  > 0 ? $" #{string.Join( " #", tags )}" : "";
		var disStr  = go.Enabled ? "" : " (disabled)";
		var childStr = showChildCount ? $"  children:{go.Children.Count}" : "";
		sb.AppendLine( $"{indent}- {go.Name} (ID: {go.Id}){disStr}{tagStr}{compStr}{childStr}" );
	}
}
