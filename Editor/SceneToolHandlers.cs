using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handlers for scene-inspection MCP tools:
/// get_scene_summary, get_scene_hierarchy, find_game_objects,
/// find_game_objects_in_radius, get_game_object_details,
/// get_component_properties, and get_prefab_instances.
/// </summary>
internal static class SceneToolHandlers
{
	// ── get_scene_summary ──────────────────────────────────────────────────

	internal static object GetSceneSummary( JsonSerializerOptions jsonOptions )
	{
		var scene = ResolveScene();
		if ( scene == null )
			return ToolHandlerBase.TextResult( "No active scene. Open a scene or prefab in the editor." );

		var allObjects  = SceneQueryHelpers.WalkAll( scene, includeDisabled: true ).ToList();
		var rootObjects = scene.Children.ToList();
		int totalCount     = allObjects.Count;
		int rootCount      = rootObjects.Count;
		int enabledCount   = allObjects.Count( g => g.Enabled );
		int disabledCount  = allObjects.Count( g => !g.Enabled );

		// Component type frequency
		var compCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
			foreach ( var comp in go.Components.GetAll() )
			{
				var typeName = comp.GetType().Name;
				compCounts.TryGetValue( typeName, out var existing );
				compCounts[typeName] = existing + 1;
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

		// Prefab source breakdown
		var prefabCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects.Where( g => g.IsPrefabInstance && g.PrefabInstanceSource != null ) )
		{
			var src = go.PrefabInstanceSource;
			prefabCounts.TryGetValue( src, out var existing );
			prefabCounts[src] = existing + 1;
		}
		var prefabBreakdown = prefabCounts
			.OrderByDescending( kv => kv.Value )
			.Select( kv => new Dictionary<string, object> { ["prefab"] = kv.Key, ["instances"] = kv.Value } )
			.ToList();

		// Network mode distribution
		var netModeCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
		{
			var mode = go.NetworkMode.ToString();
			netModeCounts.TryGetValue( mode, out var existing );
			netModeCounts[mode] = existing + 1;
		}

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
			["sceneName"]            = scene.Name,
			["totalObjects"]         = totalCount,
			["rootObjects"]          = rootCount,
			["enabledObjects"]       = enabledCount,
			["disabledObjects"]      = disabledCount,
			["uniqueTags"]           = allTags.OrderBy( t => t ).ToList(),
			["componentBreakdown"]   = topComponents,
			["prefabBreakdown"]      = prefabBreakdown,
			["networkModeBreakdown"] = netModeCounts,
			["rootObjectList"]       = rootNames
		};

		var json = JsonSerializer.Serialize( summary, jsonOptions );
		return ToolHandlerBase.TextResult( json );
	}

	// ── get_scene_hierarchy ────────────────────────────────────────────────

	internal static object GetSceneHierarchy( JsonElement args )
	{
		bool rootOnly = args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "rootOnly", out var roP ) && roP.GetBoolean();

		bool includeDisabled = true;
		if ( args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "includeDisabled", out var idP ) )
			includeDisabled = idP.GetBoolean();

		string rootId = null;
		if ( args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "rootId", out var ridP ) )
			rootId = ridP.GetString();

		var sb    = new StringBuilder();
		var scene = ResolveScene();

		if ( scene == null )
		{
			sb.Append( "No active scene. Open a scene or prefab in the editor." );
		}
		else
		{
			sb.AppendLine( $"Scene: {scene.Name}" );

			if ( !string.IsNullOrEmpty( rootId ) && Guid.TryParse( rootId, out var guid ) )
			{
				var subtreeRoot = SceneQueryHelpers.WalkAll( scene )
					.FirstOrDefault( g => g.Id == guid );

				if ( subtreeRoot == null )
				{
					sb.Append( $"No GameObject found with id='{rootId}'." );
				}
				else
				{
					void WalkSub( GameObject go, int depth )
					{
						if ( !includeDisabled && !go.Enabled ) return;
						ToolHandlerBase.AppendHierarchyLine( sb, go, depth, showChildCount: rootOnly );
						if ( !rootOnly )
							foreach ( var child in go.Children )
								WalkSub( child, depth + 1 );
					}
					WalkSub( subtreeRoot, 0 );
				}
			}
			else if ( rootOnly )
			{
				foreach ( var go in scene.Children )
				{
					if ( !includeDisabled && !go.Enabled ) continue;
					ToolHandlerBase.AppendHierarchyLine( sb, go, 0, showChildCount: true );
				}
			}
			else
			{
				void Walk( GameObject go, int depth )
				{
					if ( !includeDisabled && !go.Enabled ) return;
					ToolHandlerBase.AppendHierarchyLine( sb, go, depth, showChildCount: false );
					foreach ( var child in go.Children )
						Walk( child, depth + 1 );
				}
				foreach ( var go in scene.Children )
					Walk( go, 0 );
			}
		}

		return ToolHandlerBase.TextResult( sb.ToString() );
	}

	// ── find_game_objects ──────────────────────────────────────────────────

	internal static object FindGameObjects( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string nameContains  = null;
		string hasTag        = null;
		string hasComponent  = null;
		string pathContains  = null;
		bool   enabledOnly   = false;
		bool?  isNetworkRoot = null;
		bool?  isPrefabInst  = null;
		int    maxResults    = 50;
		string sortBy        = null;
		float? sortOriginX   = null;
		float? sortOriginY   = null;
		float? sortOriginZ   = null;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "nameContains",     out var nc  ) ) nameContains  = nc.GetString();
			if ( args.TryGetProperty( "hasTag",           out var ht  ) ) hasTag        = ht.GetString();
			if ( args.TryGetProperty( "hasComponent",     out var hc  ) ) hasComponent  = hc.GetString();
			if ( args.TryGetProperty( "pathContains",     out var pc  ) ) pathContains  = pc.GetString();
			if ( args.TryGetProperty( "enabledOnly",      out var eo  ) ) enabledOnly   = eo.GetBoolean();
			if ( args.TryGetProperty( "isNetworkRoot",    out var inr ) ) isNetworkRoot = inr.GetBoolean();
			if ( args.TryGetProperty( "isPrefabInstance", out var ipi ) ) isPrefabInst  = ipi.GetBoolean();
			if ( args.TryGetProperty( "maxResults",       out var mr  ) ) maxResults    = Math.Clamp( mr.GetInt32(), 1, 500 );
			if ( args.TryGetProperty( "sortBy",           out var sb2 ) ) sortBy        = sb2.GetString();
			if ( args.TryGetProperty( "sortOriginX",      out var sox ) ) sortOriginX   = sox.GetSingle();
			if ( args.TryGetProperty( "sortOriginY",      out var soy ) ) sortOriginY   = soy.GetSingle();
			if ( args.TryGetProperty( "sortOriginZ",      out var soz ) ) sortOriginZ   = soz.GetSingle();
		}

		var scene = ResolveScene();
		if ( scene == null )
			return ToolHandlerBase.TextResult( "No active scene. Open a scene or prefab in the editor." );

		var allObjects = SceneQueryHelpers.WalkAll( scene, includeDisabled: true );
		var matches    = new List<Dictionary<string, object>>();
		int totalSearched = 0;

		foreach ( var go in allObjects )
		{
			totalSearched++;
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
			if ( !string.IsNullOrEmpty( pathContains ) )
			{
				var path = SceneQueryHelpers.GetObjectPath( go );
				if ( path.IndexOf( pathContains, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			}
			if ( isNetworkRoot.HasValue && go.IsNetworkRoot != isNetworkRoot.Value ) continue;
			if ( isPrefabInst.HasValue  && go.IsPrefabInstance != isPrefabInst.Value ) continue;

			matches.Add( SceneQueryHelpers.BuildObjectSummary( go ) );
		}

		matches = ApplySorting( matches, sortBy, sortOriginX, sortOriginY, sortOriginZ );

		bool truncated = matches.Count > maxResults;
		if ( truncated ) matches = matches.Take( maxResults ).ToList();

		var summary = $"Found {matches.Count} matching object(s) (searched {totalSearched} total).";
		if ( truncated )
			summary += $" Result limit ({maxResults}) reached — refine your filters for more specific results.";

		var json = JsonSerializer.Serialize( new { summary, results = matches }, jsonOptions );
		return ToolHandlerBase.TextResult( json );
	}

	// ── find_game_objects_in_radius ────────────────────────────────────────

	internal static object FindGameObjectsInRadius( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		float  x           = 0f;
		float  y           = 0f;
		float  z           = 0f;
		float  radius      = 1000f;
		string hasTag      = null;
		string hasComponent = null;
		bool   enabledOnly = false;
		int    maxResults  = 50;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "x",           out var xP  ) ) x           = xP.GetSingle();
			if ( args.TryGetProperty( "y",           out var yP  ) ) y           = yP.GetSingle();
			if ( args.TryGetProperty( "z",           out var zP  ) ) z           = zP.GetSingle();
			if ( args.TryGetProperty( "radius",      out var rP  ) ) radius      = rP.GetSingle();
			if ( args.TryGetProperty( "hasTag",      out var ht  ) ) hasTag      = ht.GetString();
			if ( args.TryGetProperty( "hasComponent",out var hc  ) ) hasComponent = hc.GetString();
			if ( args.TryGetProperty( "enabledOnly", out var eo  ) ) enabledOnly = eo.GetBoolean();
			if ( args.TryGetProperty( "maxResults",  out var mr  ) ) maxResults  = Math.Clamp( mr.GetInt32(), 1, 500 );
		}

		var scene = ResolveScene();
		if ( scene == null )
			return ToolHandlerBase.TextResult( "No active scene. Open a scene or prefab in the editor." );

		var origin     = new Vector3( x, y, z );
		float radiusSq = radius * radius;

		var matches = new List<(float dist, Dictionary<string, object> summary)>();

		foreach ( var go in SceneQueryHelpers.WalkAll( scene, includeDisabled: true ) )
		{
			if ( enabledOnly && !go.Enabled ) continue;
			if ( !string.IsNullOrEmpty( hasTag ) && !go.Tags.Has( hasTag ) ) continue;
			if ( !string.IsNullOrEmpty( hasComponent ) )
			{
				bool found = go.Components.GetAll().Any( c =>
					c.GetType().Name.IndexOf( hasComponent, StringComparison.OrdinalIgnoreCase ) >= 0 );
				if ( !found ) continue;
			}

			var pos    = go.WorldPosition;
			var diff   = pos - origin;
			var distSq = diff.x * diff.x + diff.y * diff.y + diff.z * diff.z;
			if ( distSq > radiusSq ) continue;

			matches.Add( (MathF.Sqrt( distSq ), SceneQueryHelpers.BuildObjectSummary( go )) );
		}

		matches.Sort( ( a, b ) => a.dist.CompareTo( b.dist ) );

		int totalCandidates = matches.Count;
		bool truncated = matches.Count > maxResults;
		var results = matches.Take( maxResults )
			.Select( m =>
			{
				m.summary["distanceFromOrigin"] = MathF.Round( m.dist, 2 );
				return m.summary;
			} )
			.ToList();

		var summary = $"Found {results.Count} object(s) within radius {radius} of ({x},{y},{z}) (searched {totalCandidates} candidates).";
		if ( truncated )
			summary += $" Result limit ({maxResults}) reached.";

		var json = JsonSerializer.Serialize( new { summary, results }, jsonOptions );
		return ToolHandlerBase.TextResult( json );
	}

	// ── get_game_object_details ────────────────────────────────────────────

	internal static object GetGameObjectDetails( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string idStr   = null;
		string nameStr = null;
		bool   recurse = false;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "id",                       out var idP   ) ) idStr   = idP.GetString();
			if ( args.TryGetProperty( "name",                     out var nameP ) ) nameStr = nameP.GetString();
			if ( args.TryGetProperty( "includeChildrenRecursive", out var recP  ) ) recurse = recP.GetBoolean();
		}

		if ( string.IsNullOrEmpty( idStr ) && string.IsNullOrEmpty( nameStr ) )
			throw new ArgumentException( "Provide either 'id' or 'name'." );

		var scene = ResolveScene();
		if ( scene == null )
			return ToolHandlerBase.TextResult( "No active scene. Open a scene or prefab in the editor." );

		var target = FindGameObject( scene, idStr, nameStr );

		if ( target == null )
			return ToolHandlerBase.TextResult( $"No GameObject found matching id='{idStr}' name='{nameStr}'." );

		var json = JsonSerializer.Serialize( SceneQueryHelpers.BuildObjectDetail( target, recurse ), jsonOptions );
		return ToolHandlerBase.TextResult( json );
	}

	// ── get_component_properties ──────────────────────────────────────────

	internal static object GetComponentProperties( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string idStr         = null;
		string nameStr       = null;
		string componentType = null;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "id",            out var idP ) ) idStr         = idP.GetString();
			if ( args.TryGetProperty( "name",          out var nmP ) ) nameStr       = nmP.GetString();
			if ( args.TryGetProperty( "componentType", out var ctP ) ) componentType = ctP.GetString();
		}

		if ( string.IsNullOrEmpty( idStr ) && string.IsNullOrEmpty( nameStr ) )
			throw new ArgumentException( "Provide either 'id' or 'name'." );
		if ( string.IsNullOrEmpty( componentType ) )
			throw new ArgumentException( "Provide 'componentType'." );

		var scene = ResolveScene();
		if ( scene == null )
			return ToolHandlerBase.TextResult( "No active scene. Open a scene or prefab in the editor." );

		var target = FindGameObject( scene, idStr, nameStr );

		if ( target == null )
			return ToolHandlerBase.TextResult( $"No GameObject found matching id='{idStr}' name='{nameStr}'." );

		var comp = target.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( componentType, StringComparison.OrdinalIgnoreCase ) >= 0 );

		if ( comp == null )
			return ToolHandlerBase.TextResult( $"No component matching '{componentType}' found on '{target.Name}'." );

		var props = new Dictionary<string, object>();
		var type  = comp.GetType();

		foreach ( var prop in type.GetProperties( System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance ) )
		{
			if ( !prop.CanRead ) continue;
			try
			{
				var val = prop.GetValue( comp );
				props[prop.Name] = val switch
				{
					null     => null,
					bool b   => (object)b,
					int i    => (object)i,
					float f  => (object)MathF.Round( f, 4 ),
					double d => (object)Math.Round( d, 4 ),
					string s => (object)s,
					Enum e   => (object)e.ToString(),
					Vector3 v => (object)new { x = MathF.Round( v.x, 2 ), y = MathF.Round( v.y, 2 ), z = MathF.Round( v.z, 2 ) },
					_        => (object)val.ToString()
				};
			}
			catch
			{
				props[prop.Name] = "<error reading value>";
			}
		}

		var result = new Dictionary<string, object>
		{
			["gameObjectId"]   = target.Id.ToString(),
			["gameObjectName"] = target.Name,
			["componentType"]  = comp.GetType().Name,
			["enabled"]        = comp.Enabled,
			["properties"]     = props
		};

		var json = JsonSerializer.Serialize( result, jsonOptions );
		return ToolHandlerBase.TextResult( json );
	}

	// ── get_prefab_instances ───────────────────────────────────────────────

	internal static object GetPrefabInstances( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string prefabPath  = null;
		bool   enabledOnly = false;
		int    maxResults  = 100;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "prefabPath",  out var pp ) ) prefabPath  = pp.GetString();
			if ( args.TryGetProperty( "enabledOnly", out var eo ) ) enabledOnly = eo.GetBoolean();
			if ( args.TryGetProperty( "maxResults",  out var mr ) ) maxResults  = Math.Clamp( mr.GetInt32(), 1, 500 );
		}

		var scene = ResolveScene();
		if ( scene == null )
			return ToolHandlerBase.TextResult( "No active scene. Open a scene or prefab in the editor." );

		// No prefabPath — return a full breakdown of all prefab sources
		if ( string.IsNullOrEmpty( prefabPath ) )
		{
			var counts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
			foreach ( var go in SceneQueryHelpers.WalkAll( scene, includeDisabled: true ) )
			{
				if ( !go.IsPrefabInstance || go.PrefabInstanceSource == null ) continue;
				if ( enabledOnly && !go.Enabled ) continue;
				counts.TryGetValue( go.PrefabInstanceSource, out var c );
				counts[go.PrefabInstanceSource] = c + 1;
			}
			var breakdown = counts
				.OrderByDescending( kv => kv.Value )
				.Select( kv => new Dictionary<string, object> { ["prefab"] = kv.Key, ["instances"] = kv.Value } )
				.ToList();
			var bJson = JsonSerializer.Serialize( new { summary = $"{counts.Count} unique prefab(s) in scene.", breakdown }, jsonOptions );
			return ToolHandlerBase.TextResult( bJson );
		}

		// Return instances of a specific prefab
		var matches = new List<Dictionary<string, object>>();
		foreach ( var go in SceneQueryHelpers.WalkAll( scene, includeDisabled: true ) )
		{
			if ( matches.Count >= maxResults ) break;
			if ( !go.IsPrefabInstance ) continue;
			if ( enabledOnly && !go.Enabled ) continue;
			if ( go.PrefabInstanceSource == null ) continue;
			if ( go.PrefabInstanceSource.IndexOf( prefabPath, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			matches.Add( SceneQueryHelpers.BuildObjectSummary( go ) );
		}

		bool truncated = matches.Count >= maxResults;
		var sumStr = $"Found {matches.Count} instance(s) of '{prefabPath}'.";
		if ( truncated ) sumStr += $" Result limit ({maxResults}) reached.";

		var json = JsonSerializer.Serialize( new { summary = sumStr, results = matches }, jsonOptions );
		return ToolHandlerBase.TextResult( json );
	}

	// ── Private helpers ────────────────────────────────────────────────────

	/// <summary>
	/// Returns the best available Scene: the live game scene if playing,
	/// then the active editor session scene (prefab editor, scene editor),
	/// then null if nothing is open.
	/// </summary>
	private static Scene ResolveScene()
	{
		if ( Game.ActiveScene != null ) return Game.ActiveScene;

		// When the game is not running, the editor exposes scenes through
		// SceneEditorSession. Use the active (focused) session if present.
		try
		{
			var session = SceneEditorSession.Active;
			if ( session?.Scene != null ) return session.Scene;

			// Fall back to the first available session
			foreach ( var s in SceneEditorSession.All )
				if ( s?.Scene != null ) return s.Scene;
		}
		catch { /* Editor API unavailable at runtime */ }

		return null;
	}

	/// <summary>
	/// Locates a GameObject by GUID or name, checking WalkAll first then
	/// scene.Children directly to catch disabled root objects.
	/// </summary>
	private static GameObject FindGameObject( Scene scene, string idStr, string nameStr )
	{
		GameObject target = null;

		if ( !string.IsNullOrEmpty( idStr ) && Guid.TryParse( idStr, out var guid ) )
		{
			target = SceneQueryHelpers.WalkAll( scene, includeDisabled: true ).FirstOrDefault( g => g.Id == guid );
			if ( target == null )
				target = scene.Children.FirstOrDefault( g => g.Id == guid );
		}

		if ( target == null && !string.IsNullOrEmpty( nameStr ) )
		{
			target = SceneQueryHelpers.WalkAll( scene, includeDisabled: true ).FirstOrDefault( g =>
				string.Equals( g.Name, nameStr, StringComparison.OrdinalIgnoreCase ) );
			if ( target == null )
				target = scene.Children.FirstOrDefault( g =>
					string.Equals( g.Name, nameStr, StringComparison.OrdinalIgnoreCase ) );
		}

		return target;
	}

	/// <summary>Applies optional sorting to a list of object summary dictionaries.</summary>
	private static List<Dictionary<string, object>> ApplySorting(
		List<Dictionary<string, object>> matches,
		string sortBy,
		float? sortOriginX, float? sortOriginY, float? sortOriginZ )
	{
		if ( string.IsNullOrEmpty( sortBy ) ) return matches;

		if ( sortBy.Equals( "name", StringComparison.OrdinalIgnoreCase ) )
			return matches.OrderBy( m => m["name"]?.ToString() ).ToList();

		if ( sortBy.Equals( "distance", StringComparison.OrdinalIgnoreCase ) &&
			sortOriginX.HasValue && sortOriginY.HasValue && sortOriginZ.HasValue )
		{
			var ox = sortOriginX.Value;
			var oy = sortOriginY.Value;
			var oz = sortOriginZ.Value;
			return matches.OrderBy( m =>
			{
				var pos = (Dictionary<string, object>)m["position"];
				var dx  = (float)(double)pos["x"] - ox;
				var dy  = (float)(double)pos["y"] - oy;
				var dz  = (float)(double)pos["z"] - oz;
				return MathF.Sqrt( dx * dx + dy * dy + dz * dz );
			} ).ToList();
		}

		if ( sortBy.Equals( "componentCount", StringComparison.OrdinalIgnoreCase ) )
			return matches.OrderByDescending( m => ( (List<string>)m["components"] ).Count ).ToList();

		return matches;
	}
}
