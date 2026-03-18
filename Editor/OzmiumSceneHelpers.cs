using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Shared helpers: scene resolution, object tree walking, and JSON object builders.
/// </summary>
internal static class OzmiumSceneHelpers
{
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
		if ( Game.ActiveScene != null ) return Game.ActiveScene;
		try
		{
			var active = SceneEditorSession.Active;
			if ( active?.Scene != null ) return active.Scene;
			foreach ( var s in SceneEditorSession.All )
				if ( s?.Scene != null ) return s.Scene;
		}
		catch { }
		return null;
	}

	// ── Tree walking ────────────────────────────────────────────────────────

	internal static IEnumerable<GameObject> WalkAll( Scene scene, bool includeDisabled = true )
	{
		foreach ( var root in scene.Children )
			foreach ( var go in WalkSubtree( root, includeDisabled ) )
				yield return go;
	}

	internal static IEnumerable<GameObject> WalkSubtree( GameObject root, bool includeDisabled = true )
	{
		if ( !includeDisabled && !root.Enabled ) yield break;
		yield return root;
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
}
