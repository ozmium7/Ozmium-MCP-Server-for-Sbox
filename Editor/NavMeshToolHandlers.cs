using System;
using System.Collections.Generic;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// NavMesh system management MCP tools: get status, configure generation params,
/// trigger rebuilds, regenerate bounded areas, toggle navmesh.
/// </summary>
internal static class NavMeshToolHandlers
{

	// ── get_navmesh_status ─────────────────────────────────────────────────

	private static object GetNavMeshStatus( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		try
		{
			var navMesh = scene.NavMesh;
			if ( navMesh == null )
				return OzmiumSceneHelpers.Txt( "Scene has no NavMesh." );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				enabled = navMesh.IsEnabled,
				isDirty = navMesh.IsDirty,
				isGenerating = navMesh.IsGenerating,
				agentHeight = navMesh.AgentHeight,
				agentRadius = navMesh.AgentRadius,
				agentStepSize = navMesh.AgentStepSize,
				agentMaxSlope = navMesh.AgentMaxSlope
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_navmesh ──────────────────────────────────────────────────

	private static object ConfigureNavMesh( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		try
		{
			var navMesh = scene.NavMesh;
			if ( navMesh == null )
				return OzmiumSceneHelpers.Txt( "Scene has no NavMesh." );

			if ( args.TryGetProperty( "agentHeight", out var ah ) ) navMesh.AgentHeight = ah.GetSingle();
			if ( args.TryGetProperty( "agentRadius", out var ar ) ) navMesh.AgentRadius = ar.GetSingle();
			if ( args.TryGetProperty( "agentStepSize", out var ass ) ) navMesh.AgentStepSize = ass.GetSingle();
			if ( args.TryGetProperty( "agentMaxSlope", out var ams ) ) navMesh.AgentMaxSlope = ams.GetSingle();
			if ( args.TryGetProperty( "enabled", out var en ) ) navMesh.IsEnabled = en.GetBoolean();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = "NavMesh configured. Call mark_dirty to trigger rebuild.",
				enabled = navMesh.IsEnabled,
				agentHeight = navMesh.AgentHeight,
				agentRadius = navMesh.AgentRadius,
				agentStepSize = navMesh.AgentStepSize,
				agentMaxSlope = navMesh.AgentMaxSlope
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── mark_dirty ─────────────────────────────────────────────────────────

	private static object MarkDirty( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		try
		{
			var navMesh = scene.NavMesh;
			if ( navMesh == null )
				return OzmiumSceneHelpers.Txt( "Scene has no NavMesh." );

			navMesh.SetDirty();

			return OzmiumSceneHelpers.Txt( "NavMesh marked dirty. It will rebuild over the next few frames." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── regenerate_area ────────────────────────────────────────────────────

	private static object RegenerateArea( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 500f );

		try
		{
			var navMesh = scene.NavMesh;
			if ( navMesh == null )
				return OzmiumSceneHelpers.Txt( "Scene has no NavMesh." );

			var physicsWorld = scene.PhysicsWorld;
			if ( physicsWorld == null )
				return OzmiumSceneHelpers.Txt( "No PhysicsWorld on scene (needed for NavMesh generation)." );

			var center = new Vector3( x, y, z );
			var bbox = new BBox( center - new Vector3( radius, radius, radius ), center + new Vector3( radius, radius, radius ) );

			// GenerateTiles is async — we fire and forget. The navmesh rebuilds in the background.
			_ = navMesh.GenerateTiles( physicsWorld, bbox );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = "NavMesh tile generation started (async, bounded area).",
				center = OzmiumSceneHelpers.V3( center ),
				radius
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── toggle_navmesh ─────────────────────────────────────────────────────

	private static object ToggleNavMesh( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		bool enabled = OzmiumSceneHelpers.Get( args, "enabled", true );

		try
		{
			var navMesh = scene.NavMesh;
			if ( navMesh == null )
				return OzmiumSceneHelpers.Txt( "Scene has no NavMesh." );

			navMesh.IsEnabled = enabled;

			return OzmiumSceneHelpers.Txt( $"NavMesh.IsEnabled = {enabled}." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_navmesh_config ─────────────────────────────────────────────────

	private static object GetNavMeshConfig( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		try
		{
			var navMesh = scene.NavMesh;
			if ( navMesh == null )
				return OzmiumSceneHelpers.Txt( "Scene has no NavMesh." );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				enabled = navMesh.IsEnabled,
				isDirty = navMesh.IsDirty,
				isGenerating = navMesh.IsGenerating,
				agentHeight = navMesh.AgentHeight,
				agentRadius = navMesh.AgentRadius,
				agentStepSize = navMesh.AgentStepSize,
				agentMaxSlope = navMesh.AgentMaxSlope,
				notes = "Use configure_navmesh to change params, mark_dirty to rebuild, regenerate_area for bounded regen."
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── manage_navmesh (Omnibus) ───────────────────────────────────────────

	internal static object ManageNavmesh( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"get_navmesh_status" => GetNavMeshStatus( args ),
			"configure_navmesh"  => ConfigureNavMesh( args ),
			"mark_dirty"         => MarkDirty( args ),
			"regenerate_area"    => RegenerateArea( args ),
			"toggle_navmesh"     => ToggleNavMesh( args ),
			"get_navmesh_config" => GetNavMeshConfig( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: get_navmesh_status, configure_navmesh, mark_dirty, regenerate_area, toggle_navmesh, get_navmesh_config" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageNavmesh
	{
		get
		{
			var props = new Dictionary<string, object>();

			props["operation"] = new Dictionary<string, object>
			{
				["type"] = "string",
				["description"] = "Operation to perform.",
				["enum"] = new[] { "get_navmesh_status", "configure_navmesh", "mark_dirty", "regenerate_area", "toggle_navmesh", "get_navmesh_config" }
			};

			// configure_navmesh params
			props["agentHeight"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Agent height (default 64)." };
			props["agentRadius"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Agent radius (default 16)." };
			props["agentStepSize"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Agent max step size (default 18)." };
			props["agentMaxSlope"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Agent max slope in degrees (default 40)." };
			props["enabled"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable/disable navmesh (for configure_navmesh and toggle_navmesh)." };

			// regenerate_area params
			props["x"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X center for regenerate_area." };
			props["y"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y center for regenerate_area." };
			props["z"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z center for regenerate_area." };
			props["radius"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius for bounded regeneration (default 500)." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object>
			{
				["name"] = "manage_navmesh",
				["description"] = "Programmatic NavMesh management: check status, configure agent parameters, trigger rebuilds (mark_dirty for deferred, regenerate_area for bounded), toggle navmesh on/off. Use after building placement or geometry changes to update AI pathfinding.",
				["inputSchema"] = schema
			};
		}
	}
}
