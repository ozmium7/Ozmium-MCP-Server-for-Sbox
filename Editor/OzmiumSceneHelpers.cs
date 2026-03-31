using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Shared helpers: scene resolution, object tree walking, JSON arg extraction,
/// schema builders, and other utilities used by all handler files.
/// </summary>
internal static class OzmiumSceneHelpers
{
	/// <summary>Shared JSON serialization settings used by all handler files.</summary>
	internal static readonly JsonSerializerOptions JsonSettings = new()
	{
		PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	// ── Scene resolution ────────────────────────────────────────────────────

	/// <summary>
	/// Returns the best available scene:
	/// 1. Game.ActiveScene (game is running)
	/// 2. Active SceneEditorSession.Scene (prefab/scene editor)
	/// 3. First available editor session
	/// 4. null
	/// </summary>
	internal static Scene ResolveScene()
	{
		// Prefer the editor session scene — this is what the user sees in the hierarchy panel.
		try
		{
			var active = SceneEditorSession.Active;
			if ( active?.Scene != null ) return active.Scene;
			foreach ( var s in SceneEditorSession.All )
				if ( s?.Scene != null ) return s.Scene;
		}
		catch { }
		// Fall back to runtime scene (only meaningful during play mode or tests)
		if ( Game.ActiveScene != null ) return Game.ActiveScene;
		return null;
	}

	// ── Tree walking ────────────────────────────────────────────────────────

	internal static IEnumerable<GameObject> WalkAll( Scene scene, bool includeDisabled = true )
	{
		foreach ( var root in scene.Children )
			foreach ( var go in WalkSubtree( root, includeDisabled ) )
				yield return go;
	}

	/// <summary>Name marker that causes MCP to skip an object and its entire subtree.</summary>
	internal const string IgnoreMarker = "(MCP IGNORE)";
	/// <summary>Tag that causes MCP to skip an object and its entire subtree.</summary>
	internal const string IgnoreTag = "mcp_ignore";
	/// <summary>Max children before auto-skipping subtree walk (parent still returned).</summary>
	internal const int MaxAutoWalkChildren = 25;

	internal static IEnumerable<GameObject> WalkSubtree( GameObject root, bool includeDisabled = true )
	{
		if ( !includeDisabled && !root.Enabled ) yield break;
		if ( root.Name != null && root.Name.IndexOf( IgnoreMarker, StringComparison.OrdinalIgnoreCase ) >= 0 ) yield break;
		if ( root.Tags.Has( IgnoreTag ) ) yield break;
		yield return root;
		// Auto-skip children of objects with too many children (performance guard)
		if ( root.Children.Count > MaxAutoWalkChildren ) yield break;
		foreach ( var child in root.Children )
			foreach ( var go in WalkSubtree( child, includeDisabled ) )
				yield return go;
	}

	// ── Object path ─────────────────────────────────────────────────────────

	internal static string GetObjectPath( GameObject go )
	{
		var parts = new List<string>();
		var cur = go;
		while ( cur != null ) { parts.Insert( 0, cur.Name ); cur = cur.Parent; }
		return string.Join( "/", parts );
	}

	// ── Component / tag helpers ─────────────────────────────────────────────

	internal static List<string> GetComponentNames( GameObject go )
		=> go.Components.GetAll().Select( c => c.GetType().Name ).ToList();

	internal static List<string> GetTags( GameObject go )
		=> go.Tags.TryGetAll().ToList();

	// ── Object builders ─────────────────────────────────────────────────────

	internal static Dictionary<string, object> BuildSummary( GameObject go )
	{
		var pos = go.WorldPosition;
		return new Dictionary<string, object>
		{
			["id"]               = go.Id.ToString(),
			["name"]             = go.Name,
			["path"]             = GetObjectPath( go ),
			["enabled"]          = go.Enabled,
			["active"]           = go.Active,
			["tags"]             = GetTags( go ),
			["components"]       = GetComponentNames( go ),
			["position"]         = V3( pos ),
			["childCount"]       = go.Children.Count,
			["isPrefabInstance"] = go.IsPrefabInstance,
			["prefabSource"]     = go.IsPrefabInstance ? go.PrefabInstanceSource : null,
			["isNetworkRoot"]    = go.IsNetworkRoot,
			["networkMode"]      = go.NetworkMode.ToString()
		};
	}

	internal static Dictionary<string, object> BuildDetail( GameObject go, bool recurse = false )
	{
		var comps = new List<Dictionary<string, object>>();
		foreach ( var c in go.Components.GetAll() )
			comps.Add( new Dictionary<string, object> { ["type"] = c.GetType().Name, ["enabled"] = c.Enabled } );

		List<object> children;
		if ( recurse )
			children = go.Children.Select( c => (object)BuildDetail( c, true ) ).ToList();
		else
			children = go.Children.Select( c => (object)new Dictionary<string, object>
			{
				["id"] = c.Id.ToString(), ["name"] = c.Name,
				["enabled"] = c.Enabled, ["components"] = GetComponentNames( c )
			} ).ToList();

		return new Dictionary<string, object>
		{
			["id"]             = go.Id.ToString(),
			["name"]           = go.Name,
			["path"]           = GetObjectPath( go ),
			["enabled"]        = go.Enabled,
			["active"]         = go.Active,
			["tags"]           = GetTags( go ),
			["components"]     = comps,
			["worldTransform"] = new Dictionary<string, object>
			{
				["position"] = V3( go.WorldPosition ),
				["rotation"] = Rot( go.WorldRotation ),
				["scale"]    = V3( go.WorldScale )
			},
			["localTransform"] = new Dictionary<string, object>
			{
				["position"] = V3( go.LocalPosition ),
				["rotation"] = Rot( go.LocalRotation ),
				["scale"]    = V3( go.LocalScale )
			},
			["parent"] = go.Parent != null
				? (object)new Dictionary<string, object> { ["id"] = go.Parent.Id.ToString(), ["name"] = go.Parent.Name }
				: null,
			["children"]         = children,
			["isRoot"]           = go.IsRoot,
			["isNetworkRoot"]    = go.IsNetworkRoot,
			["isPrefabInstance"] = go.IsPrefabInstance,
			["prefabSource"]     = go.IsPrefabInstance ? go.PrefabInstanceSource : null,
			["networkMode"]      = go.NetworkMode.ToString()
		};
	}

	// ── Find by id/name ─────────────────────────────────────────────────────

	internal static GameObject FindGo( Scene scene, string id, string name )
	{
		GameObject target = null;
		if ( !string.IsNullOrEmpty( id ) && Guid.TryParse( id, out var guid ) )
		{
			target = WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid )
			      ?? scene.Children.FirstOrDefault( g => g.Id == guid );
		}
		if ( target == null && !string.IsNullOrEmpty( name ) )
		{
			target = WalkAll( scene, true ).FirstOrDefault( g =>
				string.Equals( g.Name, name, StringComparison.OrdinalIgnoreCase ) )
				?? scene.Children.FirstOrDefault( g =>
				string.Equals( g.Name, name, StringComparison.OrdinalIgnoreCase ) );
		}
		return target;
	}

	/// <summary>
	/// Like FindGo but skips objects that don't have the requested component type.
	/// Resolves name collisions where multiple objects share the same name.
	/// </summary>
	internal static GameObject FindGoWithComponent<T>( Scene scene, string id, string name ) where T : Component
	{
		// If ID provided, try exact match first (ID is unique, no collision possible)
		if ( !string.IsNullOrEmpty( id ) && Guid.TryParse( id, out var guid ) )
		{
			var byId = WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid )
			        ?? scene.Children.FirstOrDefault( g => g.Id == guid );
			if ( byId != null && byId.Components.Get<T>() != null )
				return byId;
		}

		// Name lookup — find first object with this name that has the component
		if ( !string.IsNullOrEmpty( name ) )
		{
			return WalkAll( scene, true ).FirstOrDefault( g =>
				string.Equals( g.Name, name, StringComparison.OrdinalIgnoreCase )
				&& g.Components.Get<T>() != null )
				?? scene.Children.FirstOrDefault( g =>
				string.Equals( g.Name, name, StringComparison.OrdinalIgnoreCase )
				&& g.Components.Get<T>() != null );
		}

		return null;
	}

	// ── Hierarchy line ──────────────────────────────────────────────────────

	internal static void AppendHierarchyLine( StringBuilder sb, GameObject go, int depth, bool showChildren )
	{
		var indent  = new string( ' ', depth * 2 );
		var comps   = GetComponentNames( go );
		var tags    = GetTags( go );
		var compStr = comps.Count > 0 ? $" [{string.Join( ", ", comps )}]" : "";
		var tagStr  = tags.Count > 0 ? $" #{string.Join( " #", tags )}" : "";
		var disStr  = go.Enabled ? "" : " (disabled)";
		var cStr    = showChildren ? $"  children:{go.Children.Count}" : "";
		sb.AppendLine( $"{indent}- {go.Name} (ID: {go.Id}){disStr}{tagStr}{compStr}{cStr}" );
	}

	// ── Formatting ──────────────────────────────────────────────────────────

	internal static Dictionary<string, object> V3( Vector3 v ) => new()
		{ ["x"] = MathF.Round( v.x, 2 ), ["y"] = MathF.Round( v.y, 2 ), ["z"] = MathF.Round( v.z, 2 ) };

	internal static Dictionary<string, object> Rot( Rotation r ) => new()
		{ ["pitch"] = MathF.Round( r.Pitch(), 2 ), ["yaw"] = MathF.Round( r.Yaw(), 2 ), ["roll"] = MathF.Round( r.Roll(), 2 ) };

	// ── JSON helpers (shared across all handler files) ───────────────────

	/// <summary>Wraps a plain text string into the MCP text-result envelope.</summary>
	internal static object Txt( string text ) => new { content = new object[] { new { type = "text", text } } };

	/// <summary>Extracts a typed value from a JsonElement, returning <paramref name="def"/> on missing/invalid.</summary>
	internal static T Get<T>( JsonElement el, string key, T def )
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

	/// <summary>Builds a tool definition object with name, description, and inputSchema.</summary>
	internal static Dictionary<string, object> S( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}

	/// <summary>Shorthand for building a property dictionary from (key, type, description) tuples.</summary>
	internal static Dictionary<string, object> Ps( params (string k, string type, string d)[] fields )
	{
		var d = new Dictionary<string, object>();
		foreach ( var (k, tp, desc) in fields )
			d[k] = new Dictionary<string, object> { ["type"] = tp, ["description"] = desc };
		return d;
	}

	/// <summary>Strips a leading "Assets/" or "assets/" prefix so AssetSystem.FindByPath works.</summary>
	internal static string NormalizePath( string path )
	{
		if ( path == null ) return null;
		if ( path.StartsWith( "Assets/", StringComparison.OrdinalIgnoreCase ) )
			path = path.Substring( "Assets/".Length );
		return path;
	}

	// ── Selection helpers ─────────────────────────────────────────────────

	/// <summary>
	/// Returns the currently selected GameObjects in the editor, using reflection
	/// to access the editor Selection API without hard dependencies.
	/// </summary>
	internal static List<GameObject> GetSelectedGameObjects()
	{
		var result = new List<GameObject>();
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return result;
			var selProp = session.GetType().GetProperty( "Selection",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
			var selObj = selProp?.GetValue( session );
			if ( selObj == null ) return result;
			// SelectionSystem implements IEnumerable<object> — enumerate directly
			if ( selObj is IEnumerable<object> objs )
				foreach ( var o in objs )
					if ( o is GameObject go ) result.Add( go );
		}
		catch { }
		return result;
	}
}
