using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;
using Sandbox.Clutter;

namespace SboxMcpServer;

/// <summary>
/// Effect & environment MCP tools: create_particle_effect, configure_particle_effect,
/// create_fog_volume, configure_post_processing, create_environment_light.
/// </summary>
internal static class EffectToolHandlers
{

	// ── create_particle_effect ──────────────────────────────────────────────

	internal static object CreateParticleEffect( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name = OzmiumSceneHelpers.Get( args, "name", "Particle Effect" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var pe = go.Components.Create<ParticleEffect>();
			pe.MaxParticles = OzmiumSceneHelpers.Get( args, "maxParticles", 1000 );
			pe.Lifetime = OzmiumSceneHelpers.Get( args, "lifetime", 1f );
			pe.TimeScale = OzmiumSceneHelpers.Get( args, "timeScale", 1.0f );
			pe.PreWarm = OzmiumSceneHelpers.Get( args, "preWarm", 0.0f );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created ParticleEffect '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_particle_effect ────────────────────────────────────────────

	internal static object ConfigureParticleEffect( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGoWithComponent<ParticleEffect>( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"No object with ParticleEffect found (id={id ?? "null"}, name={name ?? "null"})." );

		var pe = go.Components.Get<ParticleEffect>();

		try
		{
			if ( args.TryGetProperty( "maxParticles", out var mpEl ) )
				pe.MaxParticles = mpEl.GetInt32();
			if ( args.TryGetProperty( "lifetime", out var ltEl ) )
				pe.Lifetime = ltEl.GetSingle();
			if ( args.TryGetProperty( "timeScale", out var tsEl ) )
				pe.TimeScale = tsEl.GetSingle();
			if ( args.TryGetProperty( "preWarm", out var pwEl ) )
				pe.PreWarm = pwEl.GetSingle();

			return OzmiumSceneHelpers.Txt( $"Configured ParticleEffect on '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_fog_volume ────────────────────────────────────────────────────

	internal static object CreateFogVolume( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x       = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y       = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z       = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name     = OzmiumSceneHelpers.Get( args, "name", "Fog Volume" );
		string fogType  = OzmiumSceneHelpers.Get( args, "fogType", "gradient" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			if ( fogType.Equals( "volumetric", StringComparison.OrdinalIgnoreCase ) )
			{
				var vf = go.Components.Create<VolumetricFogVolume>();
				vf.Strength = OzmiumSceneHelpers.Get( args, "strength", 1.0f );
				vf.FalloffExponent = OzmiumSceneHelpers.Get( args, "falloffExponent", 1.0f );
				vf.Bounds = BBox.FromPositionAndSize( 0, 300 );
			}
			else
				{
				var gf = go.Components.Create<GradientFog>();
				gf.Color = Color.White;
				gf.Height = OzmiumSceneHelpers.Get( args, "height", 100f );
				gf.StartDistance = OzmiumSceneHelpers.Get( args, "startDistance", 0f );
				gf.EndDistance = OzmiumSceneHelpers.Get( args, "endDistance", 1024f );
				gf.FalloffExponent = OzmiumSceneHelpers.Get( args, "falloffExponent", 1.0f );
				gf.VerticalFalloffExponent = OzmiumSceneHelpers.Get( args, "verticalFalloffExponent", 1.0f );

				if ( args.TryGetProperty( "color", out var colEl ) && colEl.ValueKind == JsonValueKind.String )
				{
					try { gf.Color = Color.Parse( colEl.GetString() ) ?? default; } catch { }
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message  = $"Created {fogType} fog on '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_post_processing ────────────────────────────────────────────

	internal static object ConfigurePostProcessing( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			var pp = go.Components.Create<PostProcessVolume>();
			pp.Priority = OzmiumSceneHelpers.Get( args, "priority", 0 );
			pp.BlendWeight = OzmiumSceneHelpers.Get( args, "blendWeight", 1.0f );
			pp.BlendDistance = OzmiumSceneHelpers.Get( args, "blendDistance", 50.0f );
			pp.EditorPreview = OzmiumSceneHelpers.Get( args, "editorPreview", true );

			return OzmiumSceneHelpers.Txt( $"Created PostProcessVolume on '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_environment_light ───────────────────────────────────────────────

	internal static object CreateEnvironmentLight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string sunDir  = OzmiumSceneHelpers.Get( args, "sunDirection", "0 -45 0" );
		string sunCol  = OzmiumSceneHelpers.Get( args, "sunColor", "#FFFFFF" );
		string ambCol  = OzmiumSceneHelpers.Get( args, "ambientColor", "#808080" );
		string skyMat  = OzmiumSceneHelpers.Get( args, "skyMaterial", "materials/skybox/skybox_day_01.vmat" );

		try
		{
			// Parse sun direction as "pitch yaw roll"
			var parts = sunDir.Split( ' ', StringSplitOptions.RemoveEmptyEntries );
			float pitch = 0, yaw = 0, roll = 0;
			if ( parts.Length >= 1 ) float.TryParse( parts[0], out pitch );
			if ( parts.Length >= 2 ) float.TryParse( parts[1], out yaw );
			if ( parts.Length >= 3 ) float.TryParse( parts[2], out roll );

			var sunRot = Rotation.From( pitch, yaw, roll );
			var sunColor = Color.Parse( sunCol ) ?? default;
			var ambColor = Color.Parse( ambCol ) ?? default;

			// Create Directional Light (sun)
			var sun = scene.CreateObject();
			sun.Name = "Sun";
			sun.WorldRotation = sunRot;
			var dl = sun.Components.Create<DirectionalLight>();
			dl.LightColor = sunColor;

			// Create Ambient Light
			var amb = scene.CreateObject();
			amb.Name = "Ambient Light";
			amb.Components.Create<AmbientLight>().Color = ambColor;

			// Create SkyBox2D
			var sky = scene.CreateObject();
			sky.Name = "Sky Box";
			var skyComp = sky.Components.Create<SkyBox2D>();
			var mat = Material.Load( skyMat );
			if ( mat != null ) skyComp.SkyMaterial = mat;
			skyComp.SkyIndirectLighting = true;

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = "Created environment setup (DirectionalLight + AmbientLight + SkyBox2D).",
				sun      = new { id = sun.Id.ToString(), position = OzmiumSceneHelpers.V3( sun.WorldPosition ), rotation = OzmiumSceneHelpers.Rot( sun.WorldRotation ) },
				ambient  = new { id = amb.Id.ToString(), position = OzmiumSceneHelpers.V3( amb.WorldPosition ) },
				skybox   = new { id = sky.Id.ToString(), position = OzmiumSceneHelpers.V3( sky.WorldPosition ), material = skyMat }
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

	private static readonly Dictionary<string, object> FogTypes = new()
	{
		["type"] = "string",
		["description"] = "Type of fog volume.",
		["enum"] = new[] { "gradient", "volumetric" }
	};

	internal static Dictionary<string, object> SchemaCreateParticleEffect => S( "create_particle_effect",
		"Creates a GO with a ParticleEffect component.",
		new Dictionary<string, object>
		{
			["x"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["maxParticles"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max particles." },
			["lifetime"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Particle lifetime in seconds." },
			["timeScale"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Time scale (0-1)." },
			["preWarm"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pre-warm seconds." }
		} );

	internal static Dictionary<string, object> SchemaConfigureParticleEffect => S( "configure_particle_effect",
		"Sets properties on an existing ParticleEffect.",
		new Dictionary<string, object>
		{
			["id"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["maxParticles"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max particles." },
			["lifetime"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Particle lifetime." },
			["timeScale"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Time scale." },
			["preWarm"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pre-warm." }
		} );

	internal static Dictionary<string, object> SchemaCreateFogVolume => S( "create_fog_volume",
		"Creates a GO with a fog volume component (GradientFog or VolumetricFogVolume).",
		new Dictionary<string, object>
	{
			["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["fogType"]           = FogTypes,
			["color"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Fog color (GradientFog)." },
			["strength"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Fog strength (VolumetricFogVolume)." },
			["height"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Fog height (GradientFog)." },
			["startDistance"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Start distance (GradientFog)." },
			["endDistance"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "End distance (GradientFog)." },
			["falloffExponent"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Falloff exponent." },
			["verticalFalloffExponent"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Vertical falloff exponent (GradientFog)." }
		} );

	internal static Dictionary<string, object> SchemaConfigurePostProcessing => S( "configure_post_processing",
		"Creates a PostProcessVolume GO for post-processing effects.",
		new Dictionary<string, object>
		{
			["id"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of existing object." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of existing object." },
			["priority"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Volume priority." },
			["blendWeight"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blend weight (0-1)." },
			["blendDistance"]  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blend distance for soft edges." },
			["editorPreview"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Show preview when selected." }
		} );

	// ── create_beam_effect ────────────────────────────────────────────────────

	internal static object CreateBeamEffect( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Beam Effect" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var beam = go.Components.Create<BeamEffect>();
			beam.Scale = OzmiumSceneHelpers.Get( args, "scale", 32f );
			beam.BeamsPerSecond = OzmiumSceneHelpers.Get( args, "beamsPerSecond", 0f );
			beam.MaxBeams = OzmiumSceneHelpers.Get( args, "maxBeams", 1 );
			beam.Looped = OzmiumSceneHelpers.Get( args, "looped", false );

			if ( args.TryGetProperty( "targetPosition", out var tpEl ) && tpEl.ValueKind == JsonValueKind.Object )
			{
				beam.TargetPosition = new Vector3(
					OzmiumSceneHelpers.Get( tpEl, "x", 0f ),
					OzmiumSceneHelpers.Get( tpEl, "y", 0f ),
					OzmiumSceneHelpers.Get( tpEl, "z", 0f ) );
			}

			if ( !string.IsNullOrEmpty( OzmiumSceneHelpers.Get( args, "targetId", (string)null ) ) ||
			     !string.IsNullOrEmpty( OzmiumSceneHelpers.Get( args, "targetName", (string)null ) ) )
			{
				var targetGo = OzmiumSceneHelpers.FindGo( scene,
					OzmiumSceneHelpers.Get( args, "targetId", (string)null ),
					OzmiumSceneHelpers.Get( args, "targetName", (string)null ) );
				if ( targetGo != null ) beam.TargetGameObject = targetGo;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created BeamEffect '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_verlet_rope ───────────────────────────────────────────────────

	internal static object CreateVerletRope( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Verlet Rope" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var rope = go.Components.Create<VerletRope>();
			rope.SegmentCount = OzmiumSceneHelpers.Get( args, "segmentCount", 16 );
			rope.Slack = OzmiumSceneHelpers.Get( args, "slack", 0f );
			rope.Radius = OzmiumSceneHelpers.Get( args, "radius", 1f );
			rope.Stiffness = OzmiumSceneHelpers.Get( args, "stiffness", 0.7f );
			rope.DampingFactor = OzmiumSceneHelpers.Get( args, "dampingFactor", 0.2f );

			if ( !string.IsNullOrEmpty( OzmiumSceneHelpers.Get( args, "attachmentId", (string)null ) ) ||
			     !string.IsNullOrEmpty( OzmiumSceneHelpers.Get( args, "attachmentName", (string)null ) ) )
			{
				var attachGo = OzmiumSceneHelpers.FindGo( scene,
					OzmiumSceneHelpers.Get( args, "attachmentId", (string)null ),
					OzmiumSceneHelpers.Get( args, "attachmentName", (string)null ) );
				if ( attachGo != null ) rope.Attachment = attachGo;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created VerletRope '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_joint ────────────────────────────────────────────────────────

	internal static object CreateJoint( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name    = OzmiumSceneHelpers.Get( args, "name", "Joint" );
		string jointType = OzmiumSceneHelpers.Get( args, "type", "Fixed" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			Joint joint = jointType.ToLowerInvariant() switch
			{
				"ball"   => go.Components.Create<BallJoint>(),
				"hinge"  => go.Components.Create<HingeJoint>(),
				"slider" => go.Components.Create<SliderJoint>(),
				"spring" => go.Components.Create<SpringJoint>(),
				"wheel"  => go.Components.Create<WheelJoint>(),
				_        => go.Components.Create<FixedJoint>()
			};
			joint.BreakForce = OzmiumSceneHelpers.Get( args, "strength", 1000f );
			joint.BreakTorque = OzmiumSceneHelpers.Get( args, "angularStrength", 1000f );

			if ( !string.IsNullOrEmpty( OzmiumSceneHelpers.Get( args, "bodyId", (string)null ) ) ||
			     !string.IsNullOrEmpty( OzmiumSceneHelpers.Get( args, "bodyName", (string)null ) ) )
			{
				var bodyGo = OzmiumSceneHelpers.FindGo( scene,
					OzmiumSceneHelpers.Get( args, "bodyId", (string)null ),
					OzmiumSceneHelpers.Get( args, "bodyName", (string)null ) );
				if ( bodyGo != null ) joint.Body = bodyGo;
			}

			if ( !string.IsNullOrEmpty( OzmiumSceneHelpers.Get( args, "anchorBodyId", (string)null ) ) ||
			     !string.IsNullOrEmpty( OzmiumSceneHelpers.Get( args, "anchorBodyName", (string)null ) ) )
			{
				var anchorGo = OzmiumSceneHelpers.FindGo( scene,
					OzmiumSceneHelpers.Get( args, "anchorBodyId", (string)null ),
					OzmiumSceneHelpers.Get( args, "anchorBodyName", (string)null ) );
				if ( anchorGo != null ) joint.AnchorBody = anchorGo;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created {jointType}Joint '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				type     = jointType
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Schemas (extensions) ───────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaCreateBeamEffect => S( "create_beam_effect",
		"Creates a GO with a BeamEffect component for laser/energy effects.",
		new Dictionary<string, object>
		{
			["x"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["scale"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Beam scale." },
			["targetPosition"] = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Target position {x,y,z}." },
			["targetId"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of target GO." },
			["targetName"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of target GO." },
			["beamsPerSecond"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Beams per second." },
			["maxBeams"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max simultaneous beams." },
			["looped"]         = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Loop the beam." }
		} );

	internal static Dictionary<string, object> SchemaCreateVerletRope => S( "create_verlet_rope",
		"Creates a GO with a VerletRope component for rope physics.",
		new Dictionary<string, object>
		{
			["x"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["attachmentId"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of attachment GO." },
			["attachmentName"]= new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of attachment GO." },
			["segmentCount"]  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope segment count." },
			["slack"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope slack." },
			["radius"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope radius." },
			["stiffness"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope stiffness (0-1)." },
			["dampingFactor"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope damping (0-1)." }
		} );

	internal static Dictionary<string, object> SchemaCreateJoint => S( "create_joint",
		"Creates a physics joint connecting two bodies. Types: Fixed, Ball, Hinge, Slider, Spring, Wheel.",
		new Dictionary<string, object>
		{
			["x"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["type"]            = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Joint type.",
				["enum"] = new[] { "Fixed", "Ball", "Hinge", "Slider", "Spring", "Wheel" }
			},
			["bodyId"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of body GO." },
			["bodyName"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of body GO." },
			["anchorBodyId"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of anchor GO." },
			["anchorBodyName"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of anchor GO." },
			["strength"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Linear strength." },
			["angularStrength"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Angular strength." }
		} );

	internal static Dictionary<string, object> SchemaCreateEnvironmentLight => S( "create_environment_light",
		"Creates a complete environment lighting setup: DirectionalLight (sun) + AmbientLight + SkyBox2D.",
		new Dictionary<string, object>
	{
			["sunDirection"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sun direction as 'pitch yaw roll'." },
			["sunColor"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sun color hex." },
			["ambientColor"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Ambient color hex." },
			["skyMaterial"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sky material path." }
		} );

	// ── create_clutter ───────────────────────────────────────────────────────

	internal static object CreateClutter( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Clutter" );
		string mode  = OzmiumSceneHelpers.Get( args, "mode", "Volume" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var clutter = go.Components.Create<ClutterComponent>();
			clutter.Seed = OzmiumSceneHelpers.Get( args, "seed", 0 );

			if ( Enum.TryParse<ClutterComponent.ClutterMode>( mode, true, out var cm ) )
				clutter.Mode = cm;

			if ( args.TryGetProperty( "clutterDefinitionPath", out var cdEl ) && cdEl.ValueKind == JsonValueKind.String )
			{
				var asset = AssetSystem.FindByPath( cdEl.GetString() );
				if ( asset != null )
				{
					var def = asset.LoadResource<ClutterDefinition>();
					if ( def != null ) clutter.Clutter = def;
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created Clutter '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				mode     = clutter.Mode.ToString()
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_radius_damage ──────────────────────────────────────────────────

	internal static object CreateRadiusDamage( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Radius Damage" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var rd = go.Components.Create<RadiusDamage>();
			rd.Radius = OzmiumSceneHelpers.Get( args, "radius", 512f );
			rd.DamageAmount = OzmiumSceneHelpers.Get( args, "damageAmount", 100f );
			rd.PhysicsForceScale = OzmiumSceneHelpers.Get( args, "physicsForceScale", 1.0f );
			rd.DamageOnEnabled = OzmiumSceneHelpers.Get( args, "damageOnEnabled", true );
			rd.Occlusion = OzmiumSceneHelpers.Get( args, "occlusion", true );

			if ( args.TryGetProperty( "damageTags", out var dtEl ) && dtEl.ValueKind == JsonValueKind.String )
			{
				foreach ( var tag in dtEl.GetString().Split( ',', StringSplitOptions.RemoveEmptyEntries ) )
				{
					rd.DamageTags.Add( tag.Trim() );
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created RadiusDamage '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				radius   = rd.Radius,
				damage   = rd.DamageAmount
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Effect extension schemas ────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaCreateClutter => S( "create_clutter",
		"Create a GO with a ClutterComponent for scattering vegetation/objects (grass, rocks, debris).",
		new Dictionary<string, object>
		{
			["x"]                    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]                  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["clutterDefinitionPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "ClutterDefinition asset path." },
			["seed"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Random seed (default 0)." },
			["mode"]                 = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Generation mode.",
				["enum"] = new[] { "Volume", "Infinite" }
			}
		} );

	internal static Dictionary<string, object> SchemaCreateRadiusDamage => S( "create_radius_damage",
		"Create a GO with a RadiusDamage component for explosion/area damage effects.",
		new Dictionary<string, object>
		{
			["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["radius"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Damage radius (default 512)." },
			["damageAmount"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Damage amount (default 100)." },
			["physicsForceScale"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Physics force scale (default 1.0)." },
			["damageOnEnabled"]   = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Apply damage on enable (default true)." },
			["occlusion"]         = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Block damage through walls (default true)." },
			["damageTags"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Comma-separated damage tags." }
		} );

	// ── manage_effects (Omnibus) ──────────────────────────────────────────

	internal static object ManageEffects( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_particle_effect"    => CreateParticleEffect( args ),
			"configure_particle_effect" => ConfigureParticleEffect( args ),
			"create_fog_volume"         => CreateFogVolume( args ),
			"configure_post_processing" => ConfigurePostProcessing( args ),
			"create_environment_light"  => CreateEnvironmentLight( args ),
			"create_beam_effect"        => CreateBeamEffect( args ),
			"create_verlet_rope"        => CreateVerletRope( args ),
			"create_joint"              => CreateJoint( args ),
			"create_clutter"            => CreateClutter( args ),
			"create_radius_damage"      => CreateRadiusDamage( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create_particle_effect, configure_particle_effect, create_fog_volume, configure_post_processing, create_environment_light, create_beam_effect, create_verlet_rope, create_joint, create_clutter, create_radius_damage" )
		};
	}

	internal static Dictionary<string, object> SchemaManageEffects => S( "manage_effects",
		"Manage effects & environment: particles, fog, post-processing, environment light, beams, ropes, joints, clutter, radius damage.",
		new Dictionary<string, object>
		{
			["operation"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "create_particle_effect", "configure_particle_effect", "create_fog_volume", "configure_post_processing", "create_environment_light", "create_beam_effect", "create_verlet_rope", "create_joint", "create_clutter", "create_radius_damage" } },
			["id"]                 = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID (for configure operations)." },
			["name"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["x"]                  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["maxParticles"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max particles." },
			["lifetime"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Lifetime in seconds." },
			["timeScale"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Time scale." },
			["preWarm"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pre-warm seconds." },
			["fogType"]            = FogTypes,
			["color"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color hex." },
			["strength"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Fog/strength value." },
			["height"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Fog height." },
			["startDistance"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Start distance." },
			["endDistance"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "End distance." },
			["falloffExponent"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Falloff exponent." },
			["verticalFalloffExponent"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Vertical falloff exponent." },
			["priority"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Volume priority." },
			["blendWeight"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blend weight (0-1)." },
			["blendDistance"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blend distance." },
			["editorPreview"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Show preview when selected." },
			["sunDirection"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sun direction as 'pitch yaw roll'." },
			["sunColor"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sun color hex." },
			["ambientColor"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Ambient color hex." },
			["skyMaterial"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sky material path." },
			["scale"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Scale." },
			["targetPosition"]     = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Target position {x,y,z}." },
			["targetId"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of target GO." },
			["targetName"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of target GO." },
			["beamsPerSecond"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Beams per second." },
			["maxBeams"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max simultaneous beams." },
			["looped"]             = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Loop the beam/effect." },
			["attachmentId"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of attachment GO." },
			["attachmentName"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of attachment GO." },
			["segmentCount"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope segment count." },
			["slack"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope slack." },
			["radius"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius." },
			["stiffness"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope stiffness (0-1)." },
			["dampingFactor"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rope damping (0-1)." },
			["type"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Joint type.", ["enum"] = new[] { "Fixed", "Ball", "Hinge", "Slider", "Spring", "Wheel" } },
			["bodyId"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of body GO." },
			["bodyName"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of body GO." },
			["anchorBodyId"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of anchor GO." },
			["anchorBodyName"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of anchor GO." },
			["strength"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Linear strength." },
			["angularStrength"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Angular strength." },
			["clutterDefinitionPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "ClutterDefinition asset path." },
			["seed"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Random seed." },
			["mode"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Mode (Clutter: Volume/Infinite)." },
			["damageAmount"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Damage amount." },
			["physicsForceScale"]  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Physics force scale." },
			["damageOnEnabled"]    = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Apply damage on enable." },
			["occlusion"]          = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Block damage through walls." },
			["damageTags"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Comma-separated damage tags." }
		},
		new[] { "operation" } );
}
