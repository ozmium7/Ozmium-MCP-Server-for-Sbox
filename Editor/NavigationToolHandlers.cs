using System;
using System.Collections.Generic;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Navigation MCP tools: create_nav_mesh_agent, create_nav_mesh_link, create_nav_mesh_area.
/// </summary>
internal static class NavigationToolHandlers
{

	// ── create_nav_mesh_agent ──────────────────────────────────────────────

	internal static object CreateNavMeshAgent( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "NavMesh Agent" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var agent = go.Components.Create<NavMeshAgent>();
			agent.Height = OzmiumSceneHelpers.Get( args, "height", agent.Height );
			agent.Radius = OzmiumSceneHelpers.Get( args, "radius", agent.Radius );
			agent.MaxSpeed = OzmiumSceneHelpers.Get( args, "maxSpeed", agent.MaxSpeed );
			agent.Acceleration = OzmiumSceneHelpers.Get( args, "acceleration", agent.Acceleration );
			agent.UpdatePosition = OzmiumSceneHelpers.Get( args, "updatePosition", agent.UpdatePosition );
			agent.UpdateRotation = OzmiumSceneHelpers.Get( args, "updateRotation", agent.UpdateRotation );
			agent.AutoTraverseLinks = OzmiumSceneHelpers.Get( args, "autoTraverseLinks", agent.AutoTraverseLinks );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created NavMeshAgent '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_nav_mesh_link ──────────────────────────────────────────────

	internal static object CreateNavMeshLink( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "NavMesh Link" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var link = go.Components.Create<NavMeshLink>();
			link.IsBiDirectional = OzmiumSceneHelpers.Get( args, "isBiDirectional", link.IsBiDirectional );
			link.ConnectionRadius = OzmiumSceneHelpers.Get( args, "connectionRadius", link.ConnectionRadius );

			if ( args.TryGetProperty( "localStartPosition", out var startPosEl ) && startPosEl.ValueKind == JsonValueKind.Object )
			{
				link.LocalStartPosition = new Vector3(
					OzmiumSceneHelpers.Get( startPosEl, "x", 0f ),
					OzmiumSceneHelpers.Get( startPosEl, "y", 0f ),
					OzmiumSceneHelpers.Get( startPosEl, "z", 0f ) );
			}
			else
			{
				link.LocalStartPosition = OzmiumSceneHelpers.Get( args, "startX", 0f ) * Vector3.Right
					+ OzmiumSceneHelpers.Get( args, "startY", 0f ) * Vector3.Up
					+ OzmiumSceneHelpers.Get( args, "startZ", 0f ) * Vector3.Forward;
			}

			if ( args.TryGetProperty( "localEndPosition", out var endPosEl ) && endPosEl.ValueKind == JsonValueKind.Object )
			{
				link.LocalEndPosition = new Vector3(
					OzmiumSceneHelpers.Get( endPosEl, "x", 0f ),
					OzmiumSceneHelpers.Get( endPosEl, "y", 0f ),
					OzmiumSceneHelpers.Get( endPosEl, "z", 0f ) );
			}
			else
			{
				link.LocalEndPosition = OzmiumSceneHelpers.Get( args, "endX", 0f ) * Vector3.Right
					+ OzmiumSceneHelpers.Get( args, "endY", 0f ) * Vector3.Up
					+ OzmiumSceneHelpers.Get( args, "endZ", 0f ) * Vector3.Forward;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created NavMeshLink '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				startPosition = OzmiumSceneHelpers.V3( link.LocalStartPosition ),
				endPosition   = OzmiumSceneHelpers.V3( link.LocalEndPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_nav_mesh_area ──────────────────────────────────────────────

	internal static object CreateNavMeshArea( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "NavMesh Area" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var area = go.Components.Create<NavMeshArea>();
			area.IsBlocker = OzmiumSceneHelpers.Get( args, "isBlocker", true );

				return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created NavMeshArea '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				isBlocker = area.IsBlocker
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	private static Dictionary<string, object> S( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}

	internal static Dictionary<string, object> SchemaCreateNavMeshAgent => S( "create_nav_mesh_agent",
		"Create a GO with a NavMeshAgent component for AI navigation.",
		new Dictionary<string, object>
		{
			["x"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["height"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Agent height." },
			["radius"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Agent radius." },
			["maxSpeed"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Maximum movement speed." },
			["acceleration"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Movement acceleration." },
			["updatePosition"]    = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Update GO position each frame." },
			["updateRotation"]    = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Face movement direction." },
			["autoTraverseLinks"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Auto-traverse nav links." }
		} );

	internal static Dictionary<string, object> SchemaCreateNavMeshLink => S( "create_nav_mesh_link",
		"Create a NavMeshLink for connecting navigation mesh polygons (ladders, jumps, teleports).",
		new Dictionary<string, object>
		{
			["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["localStartPosition"] = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Start position relative to GO {x,y,z}." },
			["localEndPosition"]   = new Dictionary<string, object> { ["type"] = "object", ["description"] = "End position relative to GO {x,y,z}." },
			["isBiDirectional"]    = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Link is bidirectional (default true)." },
			["connectionRadius"]  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Connection search radius." }
		} );

	internal static Dictionary<string, object> SchemaCreateNavMeshArea => S( "create_nav_mesh_area",
		"Create a NavMeshArea volume that blocks or modifies navmesh generation.",
		new Dictionary<string, object>
		{
			["x"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["isBlocker"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Block navmesh generation in this area (default true)." }
		} );
}
