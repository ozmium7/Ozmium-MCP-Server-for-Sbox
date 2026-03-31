using System;
using System.Collections.Generic;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Game MCP tools: create_spawn_point, create_trigger_hurt, create_envmap_probe.
/// </summary>
internal static class GameToolHandlers
{

	// ── create_spawn_point ───────────────────────────────────────────────

	internal static object CreateSpawnPoint( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Spawn Point" );
		string color = OzmiumSceneHelpers.Get( args, "color", "#E3510D" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var sp = go.Components.Create<SpawnPoint>();
			try { sp.Color = Color.Parse( color ) ?? default; } catch { }

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created SpawnPoint '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_trigger_hurt ────────────────────────────────────────────────

	internal static object CreateTriggerHurt( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		GameObject go;
		if ( !string.IsNullOrEmpty( id ) || !string.IsNullOrEmpty( name ) )
		{
			go = OzmiumSceneHelpers.FindGo( scene, id, name );
			if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found. Provide an existing GO with a Collider." );
		}
		else
		{
			go = scene.CreateObject();
			go.Name = "Trigger Hurt";
		}

		try
		{
			// Add a collider if one doesn't exist
			if ( go.Components.Get<Collider>() == null )
				go.Components.Create<BoxCollider>();

			var th = go.Components.GetOrCreate<TriggerHurt>();
			th.Damage = OzmiumSceneHelpers.Get( args, "damage", th.Damage );
			th.Rate = OzmiumSceneHelpers.Get( args, "rate", th.Rate );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created TriggerHurt on '{go.Name}'.",
				id       = go.Id.ToString(),
				damage   = th.Damage,
				rate     = th.Rate
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_envmap_probe ──────────────────────────────────────────────

	internal static object CreateEnvmapProbe( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Envmap Probe" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var probe = go.Components.Create<EnvmapProbe>();

			if ( args.TryGetProperty( "mode", out var modeEl ) && modeEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<EnvmapProbe.EnvmapProbeMode>( modeEl.GetString(), true, out var mode ) )
					probe.Mode = mode;
			}

			if ( args.TryGetProperty( "resolution", out var resEl ) && resEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<EnvmapProbe.CubemapResolution>( resEl.GetString(), true, out var res ) )
					probe.Resolution = res;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created EnvmapProbe '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				mode      = probe.Mode.ToString(),
				resolution = probe.Resolution.ToString()
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_prop ──────────────────────────────────────────────────────────

	internal static object CreateProp( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Prop" );
		string modelPath = OzmiumSceneHelpers.Get( args, "modelPath", (string)null );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var prop = go.Components.Create<Prop>();

			if ( !string.IsNullOrEmpty( modelPath ) )
			{
				var model = Model.Load( modelPath );
				if ( model != null ) prop.Model = model;
			}

			prop.Health = OzmiumSceneHelpers.Get( args, "health", prop.Health );
			prop.IsStatic = OzmiumSceneHelpers.Get( args, "isStatic", prop.IsStatic );
			prop.StartAsleep = OzmiumSceneHelpers.Get( args, "startAsleep", prop.StartAsleep );

			if ( args.TryGetProperty( "tint", out var tintEl ) && tintEl.ValueKind == JsonValueKind.String )
			{
				try { prop.Tint = Color.Parse( tintEl.GetString() ) ?? default; } catch { }
			}

			if ( args.TryGetProperty( "bodyGroups", out var bgEl ) && bgEl.ValueKind == JsonValueKind.String )
			{
				if ( ulong.TryParse( bgEl.GetString(), out var bg ) ) prop.BodyGroups = bg;
			}

			if ( args.TryGetProperty( "materialGroup", out var mgEl ) && mgEl.ValueKind == JsonValueKind.String )
			{
				prop.MaterialGroup = mgEl.GetString();
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created Prop '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				model    = prop.Model?.ResourcePath ?? "null",
				health   = prop.Health,
				isStatic = prop.IsStatic
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_decal ────────────────────────────────────────────────────────

	internal static object CreateDecal( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Decal" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var decal = go.Components.Create<Decal>();

			if ( args.TryGetProperty( "sizeX", out var sxEl ) )
				decal.Size = new Vector2( sxEl.GetSingle(), args.TryGetProperty( "sizeY", out var syEl ) ? syEl.GetSingle() : decal.Size.y );
			else if ( args.TryGetProperty( "size", out var sizeEl ) && sizeEl.ValueKind == JsonValueKind.Object )
			{
				decal.Size = new Vector2(
					OzmiumSceneHelpers.Get( sizeEl, "x", decal.Size.x ),
					OzmiumSceneHelpers.Get( sizeEl, "y", decal.Size.y ) );
			}

			decal.Depth = OzmiumSceneHelpers.Get( args, "depth", decal.Depth );
			decal.Looped = OzmiumSceneHelpers.Get( args, "looped", decal.Looped );
			decal.Transient = OzmiumSceneHelpers.Get( args, "transient", decal.Transient );
			decal.AttenuationAngle = OzmiumSceneHelpers.Get( args, "attenuationAngle", decal.AttenuationAngle );
			decal.SortLayer = OzmiumSceneHelpers.Get( args, "sortLayer", (uint)decal.SortLayer );

			if ( args.TryGetProperty( "colorTint", out var ctEl ) && ctEl.ValueKind == JsonValueKind.String )
			{
				try { decal.ColorTint = Color.Parse( ctEl.GetString() ) ?? default; } catch { }
			}

			if ( args.TryGetProperty( "lifeTime", out var ltEl ) )
				decal.LifeTime = ltEl.GetSingle();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created Decal '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_world_panel ──────────────────────────────────────────────────

	internal static object CreateWorldPanel( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "World Panel" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var wp = go.Components.Create<WorldPanel>();
			wp.RenderScale = OzmiumSceneHelpers.Get( args, "renderScale", 1.0f );
			wp.LookAtCamera = OzmiumSceneHelpers.Get( args, "lookAtCamera", false );
			wp.InteractionRange = OzmiumSceneHelpers.Get( args, "interactionRange", 1000f );

			if ( args.TryGetProperty( "panelSize", out var psEl ) && psEl.ValueKind == JsonValueKind.Object )
			{
				wp.PanelSize = new Vector2(
					OzmiumSceneHelpers.Get( psEl, "x", 512f ),
					OzmiumSceneHelpers.Get( psEl, "y", 512f ) );
			}

			if ( args.TryGetProperty( "horizontalAlign", out var haEl ) && haEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<WorldPanel.HAlignment>( haEl.GetString(), true, out var ha ) )
					wp.HorizontalAlign = ha;
			}

			if ( args.TryGetProperty( "verticalAlign", out var vaEl ) && vaEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<WorldPanel.VAlignment>( vaEl.GetString(), true, out var va ) )
					wp.VerticalAlign = va;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created WorldPanel '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				panelSize = new { wp.PanelSize.x, wp.PanelSize.y }
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_fire_damage ─────────────────────────────────────────────────

	internal static object CreateFireDamage( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		GameObject go;
		if ( !string.IsNullOrEmpty( id ) || !string.IsNullOrEmpty( name ) )
		{
			go = OzmiumSceneHelpers.FindGo( scene, id, name );
			if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );
		}
		else
		{
			go = scene.CreateObject();
			go.Name = "Fire Damage";
		}

		try
		{
			var fd = go.Components.GetOrCreate<FireDamage>();
			fd.DamagePerSecond = OzmiumSceneHelpers.Get( args, "damagePerSecond", fd.DamagePerSecond );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created FireDamage on '{go.Name}'.",
				id       = go.Id.ToString(),
				damagePerSecond = fd.DamagePerSecond
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

	internal static Dictionary<string, object> SchemaCreateSpawnPoint => S( "create_spawn_point",
		"Create a SpawnPoint component for player spawning.",
		new Dictionary<string, object>
		{
			["x"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["color"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Spawn point color hex (default '#E3510D')." }
		} );

	internal static Dictionary<string, object> SchemaCreateTriggerHurt => S( "create_trigger_hurt",
		"Create a TriggerHurt volume that deals damage. Requires a Collider on the GO.",
		new Dictionary<string, object>
		{
			["id"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of existing GO." },
			["name"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name of existing GO." },
			["damage"]  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Damage per tick." },
			["rate"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Seconds between damage ticks." }
		} );

	internal static Dictionary<string, object> SchemaCreateEnvmapProbe => S( "create_envmap_probe",
		"Create an EnvmapProbe for environment reflections.",
		new Dictionary<string, object>
	{
			["x"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["mode"]       = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Probe mode.",
				["enum"] = new[] { "Baked", "Realtime", "CustomTexture" }
			},
			["resolution"] = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Cubemap resolution.",
				["enum"] = new[] { "Small", "Medium", "Large", "Huge" }
			}
		} );

	// ── Game extension schemas ────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaCreateProp => S( "create_prop",
		"Create a GO with a Prop component — the fundamental S&box game object combining model, physics, and breakable behavior.",
		new Dictionary<string, object>
		{
			["x"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["modelPath"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Model asset path (e.g. 'models/citizen.vmdl')." },
			["health"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Prop health (0 = use model default)." },
			["tint"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex (default '#FFFFFF')." },
			["isStatic"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Static prop (no dynamic physics)." },
			["startAsleep"]   = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Start physics asleep." },
			["bodyGroups"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Body group mask (ulong)." },
			["materialGroup"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Material group name." }
		} );

	internal static Dictionary<string, object> SchemaCreateDecal => S( "create_decal",
		"Create a GO with a Decal component for projecting textures onto surfaces (bullet holes, graffiti, signs).",
		new Dictionary<string, object>
		{
			["x"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["sizeX"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Decal width (default 1)." },
			["sizeY"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Decal height (default 1)." },
			["depth"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Projection depth (default 8)." },
			["colorTint"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex." },
			["lifeTime"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Lifetime in seconds (0 = infinite)." },
			["looped"]           = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Repeat forever." },
			["transient"]        = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Auto-remove when max decals exceeded." },
			["attenuationAngle"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Angle fade (0-1, default 1)." },
			["sortLayer"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Render sort layer (default 0)." }
		} );

	internal static Dictionary<string, object> SchemaCreateWorldPanel => S( "create_world_panel",
		"Create a GO with a WorldPanel component for 3D in-world UI (signs, HUDs, screens, billboards).",
		new Dictionary<string, object>
		{
			["x"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["renderScale"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Render scale (default 1.0)." },
			["lookAtCamera"]     = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Billboard toward camera." },
			["panelSize"]        = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Panel size {x,y} (default 512,512)." },
			["horizontalAlign"]  = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Horizontal alignment.",
				["enum"] = new[] { "Left", "Center", "Right" }
			},
			["verticalAlign"]    = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Vertical alignment.",
				["enum"] = new[] { "Top", "Center", "Bottom" }
			},
			["interactionRange"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max interaction distance (default 1000)." }
		} );

	internal static Dictionary<string, object> SchemaCreateFireDamage => S( "create_fire_damage",
		"Create a GO with a FireDamage component for fire/burn damage zones (lava, fire traps).",
		new Dictionary<string, object>
		{
			["id"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of existing GO." },
			["name"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name of existing GO." },
			["damagePerSecond"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Damage per second (default 20)." }
		} );

	// ── create_hitbox ─────────────────────────────────────────────────────

	internal static object CreateHitbox( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Hitbox" );
		string shape = OzmiumSceneHelpers.Get( args, "shape", "Sphere" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var hb = go.Components.Create<ManualHitbox>();

			if ( Enum.TryParse<ManualHitbox.HitboxShape>( shape, true, out var hs ) )
				hb.Shape = hs;

			hb.Radius = OzmiumSceneHelpers.Get( args, "radius", 10f );

			if ( args.TryGetProperty( "centerA", out var caEl ) && caEl.ValueKind == JsonValueKind.Object )
				hb.CenterA = new Vector3(
					OzmiumSceneHelpers.Get( caEl, "x", 0f ),
					OzmiumSceneHelpers.Get( caEl, "y", 0f ),
					OzmiumSceneHelpers.Get( caEl, "z", 0f ) );

			if ( args.TryGetProperty( "centerB", out var cbEl ) && cbEl.ValueKind == JsonValueKind.Object )
				hb.CenterB = new Vector3(
					OzmiumSceneHelpers.Get( cbEl, "x", 0f ),
					OzmiumSceneHelpers.Get( cbEl, "y", 32f ),
					OzmiumSceneHelpers.Get( cbEl, "z", 0f ) );

			if ( args.TryGetProperty( "hitboxTags", out var htEl ) && htEl.ValueKind == JsonValueKind.String )
			{
				foreach ( var tag in htEl.GetString().Split( ',', StringSplitOptions.RemoveEmptyEntries ) )
					hb.HitboxTags.Add( tag.Trim() );
			}

			if ( args.TryGetProperty( "targetId", out var tidEl ) && tidEl.ValueKind == JsonValueKind.String )
			{
				var targetGo = OzmiumSceneHelpers.FindGo( scene, tidEl.GetString(), null );
				if ( targetGo != null ) hb.Target = targetGo;
			}
			else if ( args.TryGetProperty( "targetName", out var tnEl ) && tnEl.ValueKind == JsonValueKind.String )
			{
				var targetGo = OzmiumSceneHelpers.FindGo( scene, null, tnEl.GetString() );
				if ( targetGo != null ) hb.Target = targetGo;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created ManualHitbox '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				shape    = hb.Shape.ToString(),
				radius   = hb.Radius
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_chair ──────────────────────────────────────────────────────

	internal static object CreateChair( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Chair" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var chair = go.Components.Create<BaseChair>();

			if ( args.TryGetProperty( "sitPose", out var spEl ) && spEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<BaseChair.AnimatorSitPose>( spEl.GetString(), true, out var pose ) )
					chair.SitPose = pose;
			}

			chair.SitHeight = OzmiumSceneHelpers.Get( args, "sitHeight", 0f );
			chair.TooltipTitle = OzmiumSceneHelpers.Get( args, "tooltipTitle", "Sit" );
			chair.TooltipIcon = OzmiumSceneHelpers.Get( args, "tooltipIcon", "airline_seat_recline_normal" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created BaseChair '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				sitPose  = chair.SitPose.ToString()
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_dresser ────────────────────────────────────────────────────

	internal static object CreateDresser( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		GameObject go;
		if ( !string.IsNullOrEmpty( id ) || !string.IsNullOrEmpty( name ) )
		{
			go = OzmiumSceneHelpers.FindGo( scene, id, name );
			if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );
		}
		else
		{
			go = scene.CreateObject();
			go.Name = "Dresser";
		}

		try
		{
			var dresser = go.Components.GetOrCreate<Dresser>();

			if ( args.TryGetProperty( "source", out var srcEl ) && srcEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<Dresser.ClothingSource>( srcEl.GetString(), true, out var src ) )
					dresser.Source = src;
			}

			dresser.ManualHeight = OzmiumSceneHelpers.Get( args, "manualHeight", 0.5f );
			dresser.ManualTint = OzmiumSceneHelpers.Get( args, "manualTint", 0.5f );
			dresser.ManualAge = OzmiumSceneHelpers.Get( args, "manualAge", 0.5f );
			dresser.ApplyHeightScale = OzmiumSceneHelpers.Get( args, "applyHeightScale", true );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created Dresser on '{go.Name}'.",
				id       = go.Id.ToString(),
				source   = dresser.Source.ToString()
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_gib ────────────────────────────────────────────────────────

	internal static object CreateGib( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Gib" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var gib = go.Components.Create<Gib>();

			if ( args.TryGetProperty( "modelPath", out var mpEl ) && mpEl.ValueKind == JsonValueKind.String )
			{
				var model = Model.Load( mpEl.GetString() );
				if ( model != null ) gib.Model = model;
			}

			gib.FadeTime = OzmiumSceneHelpers.Get( args, "fadeTime", 5f );
			gib.IsStatic = OzmiumSceneHelpers.Get( args, "isStatic", false );

			if ( args.TryGetProperty( "tint", out var tintEl ) && tintEl.ValueKind == JsonValueKind.String )
			{
				try { gib.Tint = Color.Parse( tintEl.GetString() ) ?? default; } catch { }
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created Gib '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				fadeTime = gib.FadeTime,
				model    = gib.Model?.ResourcePath ?? "null"
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Game extension schemas (batch 2) ─────────────────────────────────

	internal static Dictionary<string, object> SchemaCreateHitbox => S( "create_hitbox",
		"Create a GO with a ManualHitbox for custom damage zones on NPCs/props. Supports sphere, capsule, box, and cylinder shapes.",
		new Dictionary<string, object>
		{
			["x"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["shape"]       = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Hitbox shape.",
				["enum"] = new[] { "Sphere", "Capsule", "Box", "Cylinder" }
			},
			["radius"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Hitbox radius (default 10)." },
			["centerA"]     = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Center A point {x,y,z} (default 0,0,0)." },
			["centerB"]     = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Center B point {x,y,z} (default 0,32,0)." },
			["hitboxTags"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Comma-separated hitbox tags." },
			["targetId"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of the target GameObject." },
			["targetName"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of the target GameObject." }
		} );

	internal static Dictionary<string, object> SchemaCreateChair => S( "create_chair",
		"Create a GO with a BaseChair component for sittable furniture. Players can interact to sit down.",
		new Dictionary<string, object>
		{
			["x"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["sitPose"]      = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Sit pose animation.",
				["enum"] = new[] { "Standing", "Chair", "ChairForward", "ChairCrossed", "KneelingOpen", "Kneeling", "Ground", "GroundCrossed" }
			},
			["sitHeight"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Sit height offset (default 0)." },
			["tooltipTitle"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tooltip title (default 'Sit')." },
			["tooltipIcon"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tooltip icon name (default 'airline_seat_recline_normal')." }
		} );

	internal static Dictionary<string, object> SchemaCreateDresser => S( "create_dresser",
		"Add a Dresser component to a GO for NPC clothing/appearance setup. Handles clothing, skin color, and height for citizen models. Critical for NPC creation.",
		new Dictionary<string, object>
		{
			["id"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of existing GO." },
			["name"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name of existing GO." },
			["source"]          = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Clothing source.",
				["enum"] = new[] { "Manual", "LocalUser", "OwnerConnection" }
			},
			["manualHeight"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual height (0-1, default 0.5)." },
			["manualTint"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual skin tint (0-1, default 0.5)." },
			["manualAge"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual age (0-1, default 0.5)." },
			["applyHeightScale"]= new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Apply height scale (default true)." }
		} );

	internal static Dictionary<string, object> SchemaCreateGib => S( "create_gib",
		"Create a GO with a Gib component — a prop that fades and self-destructs after a delay. Essential for death effects, explosions, breakable objects.",
		new Dictionary<string, object>
		{
			["x"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["modelPath"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Model asset path (e.g. 'models/gibs/wood_gib01.vmdl')." },
			["tint"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex." },
			["fadeTime"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Seconds before auto-fade (default 5)." },
			["isStatic"]   = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Static gib (default false)." }
		} );

	// ── create_game_entity (Omnibus) ────────────────────────────────────────

	internal static object CreateGameEntity( JsonElement args )
	{
		string entityType = OzmiumSceneHelpers.Get( args, "entityType", "" );
		return entityType switch
		{
			"SpawnPoint" => CreateSpawnPoint( args ),
			"TriggerHurt" => CreateTriggerHurt( args ),
			"EnvmapProbe" => CreateEnvmapProbe( args ),
			"Prop" => CreateProp( args ),
			"Decal" => CreateDecal( args ),
			"WorldPanel" => CreateWorldPanel( args ),
			"FireDamage" => CreateFireDamage( args ),
			"ManualHitbox" => CreateHitbox( args ),
			"BaseChair" => CreateChair( args ),
			"Dresser" => CreateDresser( args ),
			"Gib" => CreateGib( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown entityType: {entityType}" )
		};
	}

	internal static Dictionary<string, object> SchemaCreateGameEntity => S( "create_game_entity",
		"Create a specific game entity (SpawnPoint, TriggerHurt, EnvmapProbe, Prop, Decal, WorldPanel, FireDamage, ManualHitbox, BaseChair, Dresser, Gib).",
		new Dictionary<string, object>
		{
			["entityType"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Type of entity to create.", ["enum"] = new[] { "SpawnPoint", "TriggerHurt", "EnvmapProbe", "Prop", "Decal", "WorldPanel", "FireDamage", "ManualHitbox", "BaseChair", "Dresser", "Gib" } },
			["id"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of existing GO (for some components)." },
			["x"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["modelPath"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Model asset path." },
			["tint"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex." },
			["colorTint"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Decal color tint hex." },
			["health"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Prop health." },
			["isStatic"]        = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Static prop/gib." },
			["startAsleep"]     = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Start physics asleep." },
			["damage"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Damage per tick (TriggerHurt)." },
			["rate"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Seconds between damage ticks (TriggerHurt)." },
			["damagePerSecond"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Damage per second (FireDamage)." },
			["mode"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Probe mode." },
			["resolution"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Cubemap resolution." },
			["sizeX"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Decal width." },
			["sizeY"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Decal height." },
			["depth"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Projection depth." },
			["lifeTime"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Lifetime in seconds." },
			["fadeTime"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Seconds before auto-fade (Gib)." },
			["looped"]          = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Repeat forever." },
			["transient"]       = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Auto-remove when max decals exceeded." },
			["attenuationAngle"]= new Dictionary<string, object> { ["type"] = "number", ["description"] = "Angle fade." },
			["sortLayer"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Render sort layer." },
			["renderScale"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Render scale (WorldPanel)." },
			["lookAtCamera"]    = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Billboard toward camera (WorldPanel)." },
			["panelSize"]       = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Panel size {x,y}." },
			["horizontalAlign"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Horizontal alignment." },
			["verticalAlign"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Vertical alignment." },
			["interactionRange"]= new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max interaction distance." },
			["shape"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Hitbox shape." },
			["radius"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Hitbox radius." },
			["centerA"]         = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Center A point {x,y,z}." },
			["centerB"]         = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Center B point {x,y,z}." },
			["hitboxTags"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Comma-separated hitbox tags." },
			["targetId"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of the target GameObject." },
			["targetName"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of the target GameObject." },
			["sitPose"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sit pose animation." },
			["sitHeight"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Sit height offset." },
			["tooltipTitle"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tooltip title." },
			["tooltipIcon"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tooltip icon name." },
			["source"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Clothing source." },
			["manualHeight"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual height." },
			["manualTint"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual skin tint." },
			["manualAge"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual age." },
			["applyHeightScale"]= new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Apply height scale." }
		},
		new[] { "entityType" } );
}
