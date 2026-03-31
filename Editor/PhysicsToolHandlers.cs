using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Physics & Collider MCP tools: add_collider, configure_collider, add_rigidbody.
/// </summary>
internal static class PhysicsToolHandlers
{
	// ── add_collider ──────────────────────────────────────────────────────

	internal static object AddCollider( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string ctype = OzmiumSceneHelpers.Get( args, "colliderType", "BoxCollider" );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			Collider collider;
			switch ( ctype.ToLowerInvariant() )
			{
				case "boxcollider":
				case "box":
					{
					var c = go.Components.Create<BoxCollider>();
					if ( args.TryGetProperty( "size", out var szEl ) && szEl.ValueKind == JsonValueKind.Object )
					{
						c.Scale = ParseV3( szEl );
					}
					if ( args.TryGetProperty( "center", out var ctEl ) && ctEl.ValueKind == JsonValueKind.Object )
					{
						c.Center = ParseV3( ctEl );
					}
					collider = c;
					break;
				}
				case "spherecollider":
				case "sphere":
				{
					var c = go.Components.Create<SphereCollider>();
					if ( args.TryGetProperty( "center", out var ctEl ) && ctEl.ValueKind == JsonValueKind.Object )
					{
						c.Center = ParseV3( ctEl );
					}
					c.Radius = OzmiumSceneHelpers.Get( args, "radius", 32f );
					collider = c;
					break;
				}
				case "capsulecollider":
				case "capsule":
				{
					var c = go.Components.Create<CapsuleCollider>();
					c.Start = OzmiumSceneHelpers.Get( args, "start", Vector3.Zero );
					c.End = OzmiumSceneHelpers.Get( args, "end", new Vector3( 0, 64, 0 ) );
					c.Radius = OzmiumSceneHelpers.Get( args, "radius", 16f );
					collider = c;
					break;
				}
				case "modelcollider":
				case "model":
				{
					collider = go.Components.Create<ModelCollider>();
					break;
				}
				default:
					return OzmiumSceneHelpers.Txt( $"Unknown collider type '{ctype}'. Use: BoxCollider, SphereCollider, CapsuleCollider, ModelCollider." );
			}

			// Common collider properties
			if ( args.TryGetProperty( "isTrigger", out var trigEl ) )
				collider.IsTrigger = trigEl.GetBoolean();
			if ( args.TryGetProperty( "friction", out var frEl ) && frEl.ValueKind != JsonValueKind.Null )
				collider.Friction = frEl.GetSingle();
			if ( args.TryGetProperty( "elasticity", out var elEl ) && elEl.ValueKind != JsonValueKind.Null )
				collider.Elasticity = elEl.GetSingle();
			if ( args.TryGetProperty( "surfaceVelocity", out var svEl ) && svEl.ValueKind == JsonValueKind.Object )
				collider.SurfaceVelocity = ParseV3( svEl );

			return OzmiumSceneHelpers.Txt( $"Added {ctype} to '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_collider ───────────────────────────────────────────────────

	internal static object ConfigureCollider( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var collider = go.Components.GetAll().FirstOrDefault( c => c is Collider ) as Collider;
		if ( collider == null ) return OzmiumSceneHelpers.Txt( $"No Collider component found on '{go.Name}'." );

		try
		{
			// BoxCollider-specific
			if ( collider is BoxCollider bc )
			{
				if ( args.TryGetProperty( "size", out var szEl ) && szEl.ValueKind == JsonValueKind.Object )
					bc.Scale = ParseV3( szEl );
				if ( args.TryGetProperty( "center", out var ctEl ) && ctEl.ValueKind == JsonValueKind.Object )
					bc.Center = ParseV3( ctEl );
			}

			// SphereCollider-specific
			if ( collider is SphereCollider sc )
			{
				if ( args.TryGetProperty( "center", out var ctEl ) && ctEl.ValueKind == JsonValueKind.Object )
					sc.Center = ParseV3( ctEl );
				if ( args.TryGetProperty( "radius", out var rEl ) )
					sc.Radius = rEl.GetSingle();
			}

			// CapsuleCollider-specific
			if ( collider is CapsuleCollider cc )
			{
				if ( args.TryGetProperty( "start", out var stEl ) )
				{
					if ( stEl.ValueKind == JsonValueKind.Object ) cc.Start = ParseV3( stEl );
					else if ( stEl.ValueKind == JsonValueKind.Array ) cc.Start = ParseV3FromArr( stEl );
				}
				if ( args.TryGetProperty( "end", out var enEl ) )
				{
					if ( enEl.ValueKind == JsonValueKind.Object ) cc.End = ParseV3( enEl );
					else if ( enEl.ValueKind == JsonValueKind.Array ) cc.End = ParseV3FromArr( enEl );
				}
				if ( args.TryGetProperty( "radius", out var crEl ) )
					cc.Radius = crEl.GetSingle();
			}

			// Common properties
			if ( args.TryGetProperty( "isTrigger", out var trigEl ) )
				collider.IsTrigger = trigEl.GetBoolean();
			if ( args.TryGetProperty( "friction", out var frEl ) && frEl.ValueKind != JsonValueKind.Null )
				collider.Friction = frEl.GetSingle();
			if ( args.TryGetProperty( "elasticity", out var elEl ) && elEl.ValueKind != JsonValueKind.Null )
				collider.Elasticity = elEl.GetSingle();
			if ( args.TryGetProperty( "surfaceVelocity", out var svEl ) && svEl.ValueKind == JsonValueKind.Object )
				collider.SurfaceVelocity = ParseV3( svEl );

			return OzmiumSceneHelpers.Txt( $"Configured {collider.GetType().Name} on '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── add_rigidbody ──────────────────────────────────────────────────────

	internal static object AddRigidbody( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			var rb = go.Components.Create<Rigidbody>();
			rb.MassOverride = OzmiumSceneHelpers.Get( args, "mass", 0f );
			rb.LinearDamping = OzmiumSceneHelpers.Get( args, "linearDamping", 0f );
			rb.AngularDamping = OzmiumSceneHelpers.Get( args, "angularDamping", 0f );
			rb.Gravity = OzmiumSceneHelpers.Get( args, "gravity", true );
			rb.GravityScale = OzmiumSceneHelpers.Get( args, "gravityScale", 1f );

			return OzmiumSceneHelpers.Txt( $"Added Rigidbody to '{go.Name}' (massOverride={rb.MassOverride}, gravity={rb.Gravity})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private static Vector3 ParseV3( JsonElement el )
	{
		float vx = 0, vy = 0, vz = 0;
		if ( el.TryGetProperty( "x", out var xp ) ) vx = xp.GetSingle();
		if ( el.TryGetProperty( "y", out var yp ) ) vy = yp.GetSingle();
		if ( el.TryGetProperty( "z", out var zp ) ) vz = zp.GetSingle();
		return new Vector3( vx, vy, vz );
	}

	private static Vector3 ParseV3FromArr( JsonElement el )
	{
		if ( el.ValueKind != JsonValueKind.Array ) return Vector3.Zero;
		var arr = el.EnumerateArray().ToList();
		return new Vector3(
			arr.Count > 0 ? arr[0].GetSingle() : 0,
			arr.Count > 1 ? arr[1].GetSingle() : 0,
			arr.Count > 2 ? arr[2].GetSingle() : 0 );
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	private static readonly Dictionary<string, object> ColliderTypes = new()
	{
		["type"] = "string",
		["description"] = "Type of collider to add.",
		["enum"] = new[] { "BoxCollider", "SphereCollider", "CapsuleCollider", "ModelCollider" }
	};

	private static readonly Dictionary<string, object> V3Prop = new()
	{
		["type"] = "object", ["description"] = "Vector3 {x, y, z}."
	};

	internal static Dictionary<string, object> SchemaAddCollider => S( "add_collider",
		"Adds a collider component to a GameObject with configured properties.",
		new Dictionary<string, object>
		{
			["id"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["colliderType"]    = ColliderTypes,
			["size"]           = V3Prop,
			["center"]         = V3Prop,
			["radius"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius (SphereCollider/CapsuleCollider)." },
			["start"]          = V3Prop,
			["end"]            = V3Prop,
			["isTrigger"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether this is a trigger volume." },
			["friction"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Friction (0-1)." },
			["elasticity"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Bounciness (0-1)." },
			["surfaceVelocity"] = V3Prop
		},
		new[] { "colliderType" } );

	internal static Dictionary<string, object> SchemaConfigureCollider => S( "configure_collider",
		"Modifies properties on an existing Collider component.",
		new Dictionary<string, object>
		{
			["id"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["size"]           = V3Prop,
			["center"]         = V3Prop,
			["radius"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius." },
			["start"]          = V3Prop,
			["end"]            = V3Prop,
			["isTrigger"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Trigger volume." },
			["friction"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Friction." },
			["elasticity"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Bounciness." },
			["surfaceVelocity"] = V3Prop
		} );

	internal static Dictionary<string, object> SchemaAddRigidbody => S( "add_rigidbody",
		"Adds a Rigidbody component to a GameObject for physics simulation.",
		new Dictionary<string, object>
		{
			["id"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["mass"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Mass override (0 = auto)." },
			["linearDamping"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Linear damping." },
			["angularDamping"]  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Angular damping." },
			["gravity"]        = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable gravity (default true)." },
			["gravityScale"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Gravity scale (default 1)." }
		} );

	private static Dictionary<string, object> S( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}

	// ── create_character_controller ───────────────────────────────────────

	internal static object CreateCharacterController( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Character Controller" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var cc = go.Components.Create<CharacterController>();
			cc.Radius = OzmiumSceneHelpers.Get( args, "radius", 16f );
			cc.Height = OzmiumSceneHelpers.Get( args, "height", 64f );
			cc.StepHeight = OzmiumSceneHelpers.Get( args, "stepHeight", 18f );
			cc.GroundAngle = OzmiumSceneHelpers.Get( args, "groundAngle", 45f );
			cc.Acceleration = OzmiumSceneHelpers.Get( args, "acceleration", 10f );
			cc.Bounciness = OzmiumSceneHelpers.Get( args, "bounciness", 0.3f );

			return OzmiumSceneHelpers.Txt( $"Created CharacterController on '{go.Name}' (radius={cc.Radius}, height={cc.Height})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── add_plane_collider ───────────────────────────────────────────────

	internal static object AddPlaneCollider( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			var plane = go.Components.Create<PlaneCollider>();

			if ( args.TryGetProperty( "scale", out var scEl ) && scEl.ValueKind == JsonValueKind.Object )
			{
				float sx = OzmiumSceneHelpers.Get( scEl, "x", 50f );
				float sy = OzmiumSceneHelpers.Get( scEl, "y", 50f );
				plane.Scale = new Vector2( sx, sy );
			}

			if ( args.TryGetProperty( "center", out var ctEl ) && ctEl.ValueKind == JsonValueKind.Object )
				plane.Center = ParseV3( ctEl );

			if ( args.TryGetProperty( "normal", out var nmEl ) && nmEl.ValueKind == JsonValueKind.Object )
				plane.Normal = ParseV3( nmEl );

			return OzmiumSceneHelpers.Txt( $"Added PlaneCollider to '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── add_hull_collider ────────────────────────────────────────────────

	internal static object AddHullCollider( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string hullType = OzmiumSceneHelpers.Get( args, "hullType", "Box" );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			var hull = go.Components.Create<HullCollider>();

			if ( Enum.TryParse<HullCollider.PrimitiveType>( hullType, true, out var pt ) )
				hull.Type = pt;

			if ( args.TryGetProperty( "center", out var ctEl ) && ctEl.ValueKind == JsonValueKind.Object )
				hull.Center = ParseV3( ctEl );

			if ( hull.Type == HullCollider.PrimitiveType.Box )
			{
				if ( args.TryGetProperty( "size", out var szEl ) && szEl.ValueKind == JsonValueKind.Object )
					hull.BoxSize = ParseV3( szEl );
			}
			else
			{
				hull.Height = OzmiumSceneHelpers.Get( args, "height", 50f );
				hull.Radius = OzmiumSceneHelpers.Get( args, "radius", 25f );
				if ( hull.Type == HullCollider.PrimitiveType.Cone )
					hull.Radius2 = OzmiumSceneHelpers.Get( args, "tipRadius", 0f );
				hull.Slices = OzmiumSceneHelpers.Get( args, "slices", 16 );
			}

			return OzmiumSceneHelpers.Txt( $"Added HullCollider ({hull.Type}) to '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_model_physics ─────────────────────────────────────────────

	internal static object CreateModelPhysics( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Model Physics" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var mp = go.Components.Create<ModelPhysics>();

			if ( args.TryGetProperty( "modelPath", out var mpEl ) && mpEl.ValueKind == JsonValueKind.String )
			{
				var model = Model.Load( mpEl.GetString() );
				if ( model != null ) mp.Model = model;
			}

			mp.MotionEnabled = OzmiumSceneHelpers.Get( args, "motionEnabled", true );

			return OzmiumSceneHelpers.Txt( $"Created ModelPhysics on '{go.Name}' (model={mp.Model?.ResourcePath ?? "null"})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Physics extension schemas ────────────────────────────────────────

	internal static Dictionary<string, object> SchemaCreateCharacterController => S( "create_character_controller",
		"Create a GO with a CharacterController for collision-based movement (NPCs, custom entities). Capsule collision system.",
		new Dictionary<string, object>
		{
			["x"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["radius"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Capsule radius (default 16)." },
			["height"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Capsule height (default 64)." },
			["stepHeight"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max step height (default 18)." },
			["groundAngle"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max ground angle in degrees (default 45)." },
			["acceleration"]  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Movement acceleration (default 10)." },
			["bounciness"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Bounciness (default 0.3)." }
		} );

	internal static Dictionary<string, object> SchemaAddPlaneCollider => S( "add_plane_collider",
		"Add a PlaneCollider to an existing GO (flat ground, walls, floors). Currently missing from the standard collider tools.",
		new Dictionary<string, object>
		{
			["id"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["scale"]  = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Scale {x,y} (default 50,50)." },
			["center"] = V3Prop,
			["normal"] = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Normal direction {x,y,z} (default 0,0,1)." }
		} );

	internal static Dictionary<string, object> SchemaAddHullCollider => S( "add_hull_collider",
		"Add a HullCollider to an existing GO (box/cone/cylinder primitives). Provides shapes not available in standard colliders.",
		new Dictionary<string, object>
		{
			["id"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["hullType"]  = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Hull shape type.",
				["enum"] = new[] { "Box", "Cone", "Cylinder" }
			},
			["center"]    = V3Prop,
			["size"]      = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Box size {x,y,z} for Box type (default 50,50,50)." },
			["height"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Height for Cone/Cylinder type (default 50)." },
			["radius"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius for Cone/Cylinder type (default 25)." },
			["tipRadius"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Tip radius for Cone type (default 0)." },
			["slices"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Sides for Cone/Cylinder (default 16)." }
		} );

	internal static Dictionary<string, object> SchemaCreateModelPhysics => S( "create_model_physics",
		"Create a GO with a ModelPhysics component for ragdolls and physics-driven models with per-bone bodies.",
		new Dictionary<string, object>
		{
			["x"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["modelPath"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Model asset path (e.g. 'models/citizen.vmdl')." },
			["motionEnabled"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable physics motion (default true)." }
		} );

	// ── manage_physics (Omnibus) ──────────────────────────────────────────

	internal static object ManagePhysics( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"add_collider"                => AddCollider( args ),
			"configure_collider"          => ConfigureCollider( args ),
			"add_rigidbody"               => AddRigidbody( args ),
			"create_character_controller" => CreateCharacterController( args ),
			"add_plane_collider"          => AddPlaneCollider( args ),
			"add_hull_collider"           => AddHullCollider( args ),
			"create_model_physics"        => CreateModelPhysics( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: add_collider, configure_collider, add_rigidbody, create_character_controller, add_plane_collider, add_hull_collider, create_model_physics" )
		};
	}

	internal static Dictionary<string, object> SchemaManagePhysics => S( "manage_physics",
		"Manage physics: add/configure colliders, rigidbodies, character controllers, plane/hull colliders, model physics.",
		new Dictionary<string, object>
		{
			["operation"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "add_collider", "configure_collider", "add_rigidbody", "create_character_controller", "add_plane_collider", "add_hull_collider", "create_model_physics" } },
			["id"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["colliderType"]     = ColliderTypes,
			["size"]             = V3Prop,
			["center"]           = V3Prop,
			["radius"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius." },
			["start"]            = V3Prop,
			["end"]              = V3Prop,
			["isTrigger"]        = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Trigger volume." },
			["friction"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Friction (0-1)." },
			["elasticity"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Bounciness (0-1)." },
			["surfaceVelocity"]  = V3Prop,
			["mass"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Mass override (0 = auto)." },
			["linearDamping"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Linear damping." },
			["angularDamping"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Angular damping." },
			["gravity"]          = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable gravity." },
			["gravityScale"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Gravity scale." },
			["height"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Height." },
			["stepHeight"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max step height." },
			["groundAngle"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max ground angle." },
			["acceleration"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Movement acceleration." },
			["bounciness"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Bounciness." },
			["scale"]            = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Scale {x,y} (PlaneCollider)." },
			["normal"]           = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Normal {x,y,z}." },
			["hullType"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Hull shape type.", ["enum"] = new[] { "Box", "Cone", "Cylinder" } },
			["tipRadius"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Tip radius (Cone)." },
			["slices"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Sides for Cone/Cylinder." },
			["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position (create operations)." },
			["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["modelPath"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Model asset path." },
			["motionEnabled"]    = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable physics motion." }
		},
		new[] { "operation" } );
}
