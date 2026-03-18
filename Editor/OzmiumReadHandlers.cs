using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handlers for all scene-read MCP tools.
/// </summary>
internal static class OzmiumReadHandlers
{
	private static readonly JsonSerializerOptions _json = new()
	{
		PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	// ── get_scene_summary ──────────────────────────────────────────────────

	internal static object GetSceneSummary()
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene. Open a scene or prefab in the editor." );

		var all  = OzmiumSceneHelpers.WalkAll( scene, true ).ToList();
		var root = scene.Children.ToList();

		var compCounts = new Dictionary<string, int>();
		foreach ( var go in all )
			foreach ( var c in go.Components.GetAll() )
			{
				compCounts.TryGetValue( c.GetType().Name, out var n );
				compCounts[c.GetType().Name] = n + 1;
			}

		var allTags = new HashSet<string>();
		foreach ( var go in all )
			foreach ( var t in go.Tags.TryGetAll() ) allTags.Add( t );

		var prefabCounts = new Dictionary<string, int>();
		foreach ( var go in all.Where( g => g.IsPrefabInstance && g.PrefabInstanceSource != null ) )
		{
			prefabCounts.TryGetValue( go.PrefabInstanceSource, out var n );
			prefabCounts[go.PrefabInstanceSource] = n + 1;
		}

		var summary = new Dictionary<string, object>
		{
			["sceneName"]          = scene.Name,
			["totalObjects"]       = all.Count,
			["rootObjects"]        = root.Count,
			["enabledObjects"]     = all.Count( g => g.Enabled ),
			["disabledObjects"]    = all.Count( g => !g.Enabled ),
			["uniqueTags"]         = allTags.OrderBy( t => t ).ToList(),
			["componentBreakdown"] = compCounts.OrderByDescending( kv => kv.Value )
				.Select( kv => new Dictionary<string, object> { ["type"] = kv.Key, ["count"] = kv.Value } ).ToList(),
			["prefabBreakdown"]    = prefabCounts.OrderByDescending( kv => kv.Value )
				.Select( kv => new Dictionary<string, object> { ["prefab"] = kv.Key, ["instances"] = kv.Value } ).ToList(),
			["rootObjectList"]     = root.Select( g => new Dictionary<string, object>
			{
				["name"] = g.Name, ["id"] = g.Id.ToString(),
				["enabled"] = g.Enabled, ["childCount"] = g.Children.Count,
				["components"] = OzmiumSceneHelpers.GetComponentNames( g )
			} ).ToList()
		};

		return Txt( JsonSerializer.Serialize( summary, _json ) );
	}

	// ── get_scene_hierarchy ────────────────────────────────────────────────

	internal static object GetSceneHierarchy( JsonElement args )
	{
		bool rootOnly = Get( args, "rootOnly", false );
		bool inclDisabled = Get( args, "includeDisabled", true );
		string rootId = Get( args, "rootId", (string)null );

		var sb = new StringBuilder();
		var scene = OzmiumSceneHelpers.ResolveScene();

		if ( scene == null ) { sb.Append( "No active scene. Open a scene or prefab in the editor." ); }
		else
		{
			sb.AppendLine( $"Scene: {scene.Name}" );

			if ( !string.IsNullOrEmpty( rootId ) && Guid.TryParse( rootId, out var guid ) )
			{
				var sub = OzmiumSceneHelpers.WalkAll( scene ).FirstOrDefault( g => g.Id == guid );
				if ( sub == null ) sb.Append( $"No object with id='{rootId}'." );
				else Walk( sub, 0, rootOnly, inclDisabled, sb );
			}
			else if ( rootOnly )
			{
				foreach ( var go in scene.Children )
				{
					if ( !inclDisabled && !go.Enabled ) continue;
					OzmiumSceneHelpers.AppendHierarchyLine( sb, go, 0, true );
				}
			}
			else
			{
				foreach ( var go in scene.Children ) Walk( go, 0, false, inclDisabled, sb );
			}
		}
		return Txt( sb.ToString() );
	}

	private static void Walk( GameObject go, int depth, bool rootOnly, bool inclDisabled, StringBuilder sb )
	{
		if ( !inclDisabled && !go.Enabled ) return;
		OzmiumSceneHelpers.AppendHierarchyLine( sb, go, depth, rootOnly );
		if ( !rootOnly )
			foreach ( var child in go.Children ) Walk( child, depth + 1, false, inclDisabled, sb );
	}

	// ── find_game_objects ──────────────────────────────────────────────────

	internal static object FindGameObjects( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene. Open a scene or prefab in the editor." );

		string nameContains  = Get( args, "nameContains",     (string)null );
		string hasTag        = Get( args, "hasTag",           (string)null );
		string hasComponent  = Get( args, "hasComponent",     (string)null );
		string pathContains  = Get( args, "pathContains",     (string)null );
		bool   enabledOnly   = Get( args, "enabledOnly",      false );
		int    maxResults    = Get( args, "maxResults",       50 );

		var matches = new List<Dictionary<string, object>>();
		int searched = 0;
		foreach ( var go in OzmiumSceneHelpers.WalkAll( scene, true ) )
		{
			searched++;
			if ( enabledOnly && !go.Enabled ) continue;
			if ( !string.IsNullOrEmpty( nameContains ) && go.Name.IndexOf( nameContains, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			if ( !string.IsNullOrEmpty( hasTag ) && !go.Tags.Has( hasTag ) ) continue;
			if ( !string.IsNullOrEmpty( hasComponent ) && !go.Components.GetAll().Any( c => c.GetType().Name.IndexOf( hasComponent, StringComparison.OrdinalIgnoreCase ) >= 0 ) ) continue;
			if ( !string.IsNullOrEmpty( pathContains ) && OzmiumSceneHelpers.GetObjectPath( go ).IndexOf( pathContains, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			if ( matches.Count >= maxResults ) break;
			matches.Add( OzmiumSceneHelpers.BuildSummary( go ) );
		}

		return Txt( JsonSerializer.Serialize( new { summary = $"Found {matches.Count} (searched {searched})", results = matches }, _json ) );
	}

	// ── find_game_objects_in_radius ─────────────────────────────────────────

	internal static object FindGameObjectsInRadius( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene. Open a scene or prefab in the editor." );

		float x = Get( args, "x", 0f ), y = Get( args, "y", 0f ), z = Get( args, "z", 0f );
		float radius = Get( args, "radius", 1000f );
		string hasTag = Get( args, "hasTag", (string)null );
		int max = Get( args, "maxResults", 50 );
		var origin = new Vector3( x, y, z );
		float radSq = radius * radius;

		var results = new List<(float d, Dictionary<string, object> s)>();
		foreach ( var go in OzmiumSceneHelpers.WalkAll( scene, true ) )
		{
			if ( !string.IsNullOrEmpty( hasTag ) && !go.Tags.Has( hasTag ) ) continue;
			var diff = go.WorldPosition - origin;
			var dsq  = diff.x * diff.x + diff.y * diff.y + diff.z * diff.z;
			if ( dsq > radSq ) continue;
			results.Add( (MathF.Sqrt( dsq ), OzmiumSceneHelpers.BuildSummary( go )) );
		}
		results.Sort( ( a, b ) => a.d.CompareTo( b.d ) );

		var list = results.Take( max ).Select( r => { r.s["distanceFromOrigin"] = MathF.Round( r.d, 2 ); return r.s; } ).ToList();
		return Txt( JsonSerializer.Serialize( new { summary = $"Found {list.Count} within radius {radius}", results = list }, _json ) );
	}

	// ── get_game_object_details ────────────────────────────────────────────

	internal static object GetGameObjectDetails( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene. Open a scene or prefab in the editor." );

		string id   = Get( args, "id",   (string)null );
		string name = Get( args, "name", (string)null );
		bool recurse = Get( args, "includeChildrenRecursive", false );

		if ( string.IsNullOrEmpty( id ) && string.IsNullOrEmpty( name ) )
			return Txt( "Provide 'id' or 'name'." );

		var target = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( target == null ) return Txt( $"No object found: id='{id}' name='{name}'." );

		return Txt( JsonSerializer.Serialize( OzmiumSceneHelpers.BuildDetail( target, recurse ), _json ) );
	}

	// ── get_component_properties ───────────────────────────────────────────

	internal static object GetComponentProperties( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene. Open a scene or prefab in the editor." );

		string id   = Get( args, "id",            (string)null );
		string name = Get( args, "name",          (string)null );
		string type = Get( args, "componentType", (string)null );

		if ( string.IsNullOrEmpty( type ) ) return Txt( "Provide 'componentType'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return Txt( $"No object found." );

		var comp = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( type, StringComparison.OrdinalIgnoreCase ) >= 0 );
		if ( comp == null ) return Txt( $"No component '{type}' on '{go.Name}'." );

		var props = new Dictionary<string, object>();
		foreach ( var prop in comp.GetType().GetProperties( System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance ) )
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
					string s => (object)s,
					Enum e   => (object)e.ToString(),
					Vector3 v => (object)OzmiumSceneHelpers.V3( v ),
					_        => (object)val.ToString()
				};
			}
			catch { props[prop.Name] = "<error>"; }
		}

		return Txt( JsonSerializer.Serialize( new
		{
			gameObjectId   = go.Id.ToString(),
			gameObjectName = go.Name,
			componentType  = comp.GetType().Name,
			enabled        = comp.Enabled,
			properties     = props
		}, _json ) );
	}

	// ── get_prefab_instances ───────────────────────────────────────────────

	internal static object GetPrefabInstances( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene. Open a scene or prefab in the editor." );

		string prefabPath  = Get( args, "prefabPath",  (string)null );
		bool   enabledOnly = Get( args, "enabledOnly", false );
		int    max         = Get( args, "maxResults",  100 );

		if ( string.IsNullOrEmpty( prefabPath ) )
		{
			var counts = new Dictionary<string, int>();
			foreach ( var go in OzmiumSceneHelpers.WalkAll( scene, true ) )
			{
				if ( !go.IsPrefabInstance || go.PrefabInstanceSource == null ) continue;
				if ( enabledOnly && !go.Enabled ) continue;
				counts.TryGetValue( go.PrefabInstanceSource, out var n );
				counts[go.PrefabInstanceSource] = n + 1;
			}
			var breakdown = counts.OrderByDescending( kv => kv.Value )
				.Select( kv => new Dictionary<string, object> { ["prefab"] = kv.Key, ["instances"] = kv.Value } ).ToList();
			return Txt( JsonSerializer.Serialize( new { summary = $"{counts.Count} unique prefab(s).", breakdown }, _json ) );
		}

		var matches = new List<Dictionary<string, object>>();
		foreach ( var go in OzmiumSceneHelpers.WalkAll( scene, true ) )
		{
			if ( matches.Count >= max ) break;
			if ( !go.IsPrefabInstance || go.PrefabInstanceSource == null ) continue;
			if ( enabledOnly && !go.Enabled ) continue;
			if ( go.PrefabInstanceSource.IndexOf( prefabPath, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			matches.Add( OzmiumSceneHelpers.BuildSummary( go ) );
		}
		return Txt( JsonSerializer.Serialize( new { summary = $"Found {matches.Count} instance(s).", results = matches }, _json ) );
	}

	// ── Helpers ────────────────────────────────────────────────────────────

	internal static object Txt( string text ) => new { content = new object[] { new { type = "text", text } } };

	private static T Get<T>( JsonElement el, string key, T def )
	{
		if ( el.ValueKind == JsonValueKind.Undefined ) return def;
		if ( !el.TryGetProperty( key, out var p ) ) return def;
		try
		{
			var t = typeof( T );
			if ( t == typeof( string ) ) return (T)(object)p.GetString();
			if ( t == typeof( bool ) )   return (T)(object)p.GetBoolean();
			if ( t == typeof( int ) )    return (T)(object)p.GetInt32();
			if ( t == typeof( float ) )  return (T)(object)p.GetSingle();
			return def;
		}
		catch { return def; }
	}
}
