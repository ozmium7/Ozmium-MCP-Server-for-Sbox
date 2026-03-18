using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Pure scene-data helpers: path building, component/tag enumeration, and
/// the canonical summary/detail object builders used by all tool handlers.
/// </summary>
internal static class SceneQueryHelpers
{
	/// <summary>Returns the scene-path of a GameObject, e.g. "Root/Parent/Child".</summary>
	internal static string GetObjectPath( GameObject go )
	{
		var parts = new List<string>();
		var current = go;
		while ( current != null )
		{
			parts.Insert( 0, current.Name );
			current = current.Parent;
		}
		return string.Join( "/", parts );
	}

	/// <summary>Returns a compact list of component type names for a GameObject.</summary>
	internal static List<string> GetComponentNames( GameObject go )
	{
		var names = new List<string>();
		foreach ( var comp in go.Components.GetAll() )
			names.Add( comp.GetType().Name );
		return names;
	}

	/// <summary>Returns all tags on a GameObject as a list of strings.</summary>
	internal static List<string> GetTags( GameObject go )
	{
		return go.Tags.TryGetAll().ToList();
	}

	/// <summary>
	/// Compact summary used in list results (find_game_objects, get_scene_hierarchy).
	/// </summary>
	internal static Dictionary<string, object> BuildObjectSummary( GameObject go )
	{
		var pos = go.WorldPosition;
		return new Dictionary<string, object>
		{
			["id"]         = go.Id.ToString(),
			["name"]       = go.Name,
			["path"]       = GetObjectPath( go ),
			["enabled"]    = go.Enabled,
			["active"]     = go.Active,
			["tags"]       = GetTags( go ),
			["components"] = GetComponentNames( go ),
			["position"]   = new Dictionary<string, object>
			{
				["x"] = MathF.Round( pos.x, 2 ),
				["y"] = MathF.Round( pos.y, 2 ),
				["z"] = MathF.Round( pos.z, 2 )
			},
			["childCount"]       = go.Children.Count,
			["isPrefabInstance"] = go.IsPrefabInstance,
			["prefabSource"]     = go.IsPrefabInstance ? go.PrefabInstanceSource : null
		};
	}

	/// <summary>
	/// Full detail object used by get_game_object_details.
	/// </summary>
	internal static Dictionary<string, object> BuildObjectDetail( GameObject go )
	{
		var wp = go.WorldPosition;
		var wr = go.WorldRotation;
		var ws = go.WorldScale;
		var lp = go.LocalPosition;
		var lr = go.LocalRotation;
		var ls = go.LocalScale;

		var components = new List<Dictionary<string, object>>();
		foreach ( var comp in go.Components.GetAll() )
		{
			components.Add( new Dictionary<string, object>
			{
				["type"]    = comp.GetType().Name,
				["enabled"] = comp.Enabled
			} );
		}

		var children = go.Children.Select( c => new Dictionary<string, object>
		{
			["id"]         = c.Id.ToString(),
			["name"]       = c.Name,
			["enabled"]    = c.Enabled,
			["components"] = GetComponentNames( c )
		} ).ToList();

		return new Dictionary<string, object>
		{
			["id"]      = go.Id.ToString(),
			["name"]    = go.Name,
			["path"]    = GetObjectPath( go ),
			["enabled"] = go.Enabled,
			["active"]  = go.Active,
			["tags"]    = GetTags( go ),
			["components"] = components,
			["worldTransform"] = new Dictionary<string, object>
			{
				["position"] = Vec3Dict( wp ),
				["rotation"] = RotDict( wr ),
				["scale"]    = Vec3Dict( ws )
			},
			["localTransform"] = new Dictionary<string, object>
			{
				["position"] = Vec3Dict( lp ),
				["rotation"] = RotDict( lr ),
				["scale"]    = Vec3Dict( ls )
			},
			["parent"] = go.Parent != null ? new Dictionary<string, object>
			{
				["id"]   = go.Parent.Id.ToString(),
				["name"] = go.Parent.Name
			} : null,
			["children"]         = children,
			["isRoot"]           = go.IsRoot,
			["isNetworkRoot"]    = go.IsNetworkRoot,
			["isPrefabInstance"] = go.IsPrefabInstance,
			["prefabSource"]     = go.IsPrefabInstance ? go.PrefabInstanceSource : null,
			["networkMode"]      = go.NetworkMode.ToString()
		};
	}

	// ── Private formatting helpers ─────────────────────────────────────────

	private static Dictionary<string, object> Vec3Dict( Vector3 v ) => new()
	{
		["x"] = MathF.Round( v.x, 2 ),
		["y"] = MathF.Round( v.y, 2 ),
		["z"] = MathF.Round( v.z, 2 )
	};

	private static Dictionary<string, object> RotDict( Rotation r ) => new()
	{
		["pitch"] = MathF.Round( r.Pitch(), 2 ),
		["yaw"]   = MathF.Round( r.Yaw(),   2 ),
		["roll"]  = MathF.Round( r.Roll(),  2 )
	};
}
