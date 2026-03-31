using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;
using InsideGeometryBehaviorType = Sandbox.IndirectLightVolume.InsideGeometryBehavior;

namespace SboxMcpServer;

/// <summary>
/// Lighting MCP tools: create_light, configure_light, create_sky_box, set_sky_box, create_ambient_light.
/// </summary>
internal static class LightingToolHandlers
{

	// ── create_light ──────────────────────────────────────────────────────

	internal static object CreateLight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string type = OzmiumSceneHelpers.Get( args, "type", "PointLight" );
		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		float  pitch = OzmiumSceneHelpers.Get( args, "pitch", 0f );
		float  yaw   = OzmiumSceneHelpers.Get( args, "yaw", 0f );
		float  roll  = OzmiumSceneHelpers.Get( args, "roll", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", (string)null );

		try
		{
			var go = scene.CreateObject();
			go.WorldPosition = new Vector3( x, y, z );
			go.WorldRotation = Rotation.From( pitch, yaw, roll );

			Component light;
			switch ( type.ToLowerInvariant() )
				{
				case "pointlight":
				case "point":
					light = go.Components.Create<PointLight>();
					go.Name = name ?? "Point Light";
					break;
				case "spotlight":
				case "spot":
					light = go.Components.Create<SpotLight>();
					go.Name = name ?? "Spot Light";
					break;
				case "directionallight":
				case "directional":
					light = go.Components.Create<DirectionalLight>();
					go.Name = name ?? "Directional Light";
					break;
				default:
					return OzmiumSceneHelpers.Txt( $"Unknown light type '{type}'. Use: PointLight, SpotLight, DirectionalLight." );
			}

			// Apply optional light color
			if ( args.TryGetProperty( "color", out var colEl ) && colEl.ValueKind == JsonValueKind.String )
			{
				var colorStr = colEl.GetString();
				if ( !string.IsNullOrEmpty( colorStr ) )
				{
					try
					{
						var color = Color.Parse( colorStr ) ?? default;
						var prop = light.GetType().GetProperty( "LightColor" );
						prop?.SetValue( light, color );
					}
					catch { }
				}
			}

			// Apply optional shadows
			if ( args.TryGetProperty( "shadows", out var shEl ) && shEl.ValueKind == JsonValueKind.False )
			{
				var prop = light.GetType().GetProperty( "Shadows" );
				prop?.SetValue( light, false );
			}

			// Set radius and attenuation for point/spot lights
			if ( light is PointLight pl )
			{
				pl.Radius = OzmiumSceneHelpers.Get( args, "radius", pl.Radius );
				pl.Attenuation = OzmiumSceneHelpers.Get( args, "attenuation", pl.Attenuation );
			}
			else if ( light is SpotLight sl )
			{
				sl.Radius = OzmiumSceneHelpers.Get( args, "radius", sl.Radius );
				sl.Attenuation = OzmiumSceneHelpers.Get( args, "attenuation", sl.Attenuation );
				sl.ConeOuter = OzmiumSceneHelpers.Get( args, "coneOuter", sl.ConeOuter );
				sl.ConeInner = OzmiumSceneHelpers.Get( args, "coneInner", sl.ConeInner );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created {type} '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_light ────────────────────────────────────────────────────

	internal static object ConfigureLight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var light = go.Components.GetAll().FirstOrDefault( c => c is Light ) as Light;
		if ( light == null ) return OzmiumSceneHelpers.Txt( $"No Light component found on '{go.Name}'." );

		try
		{
			if ( args.TryGetProperty( "color", out var colEl ) && colEl.ValueKind == JsonValueKind.String )
			{
				var c = Color.Parse( colEl.GetString() ) ?? default;
				var p = light.GetType().GetProperty( "LightColor" );
				p?.SetValue( light, c );
			}

			if ( args.TryGetProperty( "shadows", out var shEl ) )
			{
				var p = light.GetType().GetProperty( "Shadows" );
				p?.SetValue( light, shEl.GetBoolean() );
			}

			if ( args.TryGetProperty( "radius", out var radEl ) )
			{
				var p = light.GetType().GetProperty( "Radius" );
				if ( p != null ) p.SetValue( light, radEl.GetSingle() );
			}

			if ( args.TryGetProperty( "attenuation", out var attEl ) )
			{
				var p = light.GetType().GetProperty( "Attenuation" );
				if ( p != null ) p.SetValue( light, attEl.GetSingle() );
			}

			if ( args.TryGetProperty( "coneOuter", out var coEl ) )
				{
				var p = light.GetType().GetProperty( "ConeOuter" );
				if ( p != null ) p.SetValue( light, coEl.GetSingle() );
			}

			if ( args.TryGetProperty( "coneInner", out var ciEl ) )
			{
				var p = light.GetType().GetProperty( "ConeInner" );
				if ( p != null ) p.SetValue( light, ciEl.GetSingle() );
			}

			if ( args.TryGetProperty( "fogMode", out var fmEl ) && fmEl.ValueKind == JsonValueKind.String )
			{
				var p = light.GetType().GetProperty( "FogMode" );
				if ( p != null )
				{
					var val = Enum.Parse( p.PropertyType, fmEl.GetString(), ignoreCase: true );
					p.SetValue( light, val );
				}
			}

			if ( args.TryGetProperty( "fogStrength", out var fsEl ) )
			{
				var p = light.GetType().GetProperty( "FogStrength" );
				if ( p != null ) p.SetValue( light, fsEl.GetSingle() );
			}

			return OzmiumSceneHelpers.Txt( $"Configured light on '{go.Name}' ({light.GetType().Name})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_sky_box ──────────────────────────────────────────────────────

	internal static object CreateSkyBox( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name = OzmiumSceneHelpers.Get( args, "name", "Sky Box" );
		string skyMaterial = OzmiumSceneHelpers.Get( args, "skyMaterial", "materials/skybox/skybox_day_01.vmat" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var sky = go.Components.Create<SkyBox2D>();

			if ( !string.IsNullOrEmpty( skyMaterial ) )
				{
				var mat = Material.Load( skyMaterial );
				if ( mat != null ) sky.SkyMaterial = mat;
			}

			if ( args.TryGetProperty( "tint", out var tintEl ) && tintEl.ValueKind == JsonValueKind.String )
			{
				try { sky.Tint = Color.Parse( tintEl.GetString() ) ?? default; } catch { }
			}

			if ( args.TryGetProperty( "skyIndirectLighting", out var iblEl ) )
			{
				sky.SkyIndirectLighting = iblEl.GetBoolean();
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created SkyBox '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_sky_box ──────────────────────────────────────────────────────────

	internal static object SetSkyBox( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var sky = go.Components.GetAll().FirstOrDefault( c => c is SkyBox2D ) as SkyBox2D;
		if ( sky == null ) return OzmiumSceneHelpers.Txt( $"No SkyBox2D component found on '{go.Name}'." );

		try
		{
			if ( args.TryGetProperty( "skyMaterial", out var matEl ) && matEl.ValueKind == JsonValueKind.String )
			{
				var mat = Material.Load( matEl.GetString() );
				if ( mat != null ) ((SkyBox2D)sky).SkyMaterial = mat;
			}

			if ( args.TryGetProperty( "tint", out var tintEl ) && tintEl.ValueKind == JsonValueKind.String )
			{
				try { ((SkyBox2D)sky).Tint = Color.Parse( tintEl.GetString() ) ?? default; } catch { }
			}

			if ( args.TryGetProperty( "skyIndirectLighting", out var iblEl ) )
			{
				((SkyBox2D)sky).SkyIndirectLighting = iblEl.GetBoolean();
			}

			return OzmiumSceneHelpers.Txt( $"Updated SkyBox on '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_ambient_light ─────────────────────────────────────────────────

	internal static object CreateAmbientLight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name = OzmiumSceneHelpers.Get( args, "name", "Ambient Light" );
		string color = OzmiumSceneHelpers.Get( args, "color", "Gray" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var amb = go.Components.Create<AmbientLight>();
			try { amb.Color = Color.Parse( color ) ?? default; } catch { }

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created AmbientLight '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> Schema( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}

	internal static readonly Dictionary<string, object> LightTypes = new()
	{
		["type"] = "string",
		["description"] = "Type of light to create.",
		["enum"] = new[] { "PointLight", "SpotLight", "DirectionalLight" }
	};

	internal static readonly Dictionary<string, object> ColorProp = new()
	{
		["type"] = "string", ["description"] = "Light color hex string (e.g. '#FF8800')."
	};

	internal static readonly Dictionary<string, object> FogModes = new()
	{
		["type"] = "string",
		["description"] = "Fog influence mode.",
		["enum"] = new[] { "Disabled", "Enabled", "WithoutShadows" }
	};

	internal static Dictionary<string, object> SchemaCreateLight => Schema( "create_light",
		"Creates a GO with a light component (PointLight/SpotLight/DirectionalLight).",
		new Dictionary<string, object>
		{
			["type"]    = LightTypes,
			["x"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["pitch"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pitch rotation in degrees." },
			["yaw"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Yaw rotation in degrees." },
			["roll"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Roll rotation in degrees." },
			["name"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["color"]   = ColorProp,
			["shadows"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Cast shadows (default true)." },
			["radius"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Light radius (PointLight/SpotLight)." },
			["attenuation"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Light attenuation (PointLight/SpotLight)." },
			["coneOuter"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Outer cone angle (SpotLight)." },
			["coneInner"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Inner cone angle (SpotLight)." }
		},
		new[] { "type" } );

	internal static Dictionary<string, object> SchemaConfigureLight => Schema( "configure_light",
		"Sets properties on an existing Light component on a GameObject.",
		new Dictionary<string, object>
		{
			["id"]            = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["color"]         = ColorProp,
			["shadows"]       = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Cast shadows." },
			["radius"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Light radius." },
			["attenuation"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Light attenuation." },
			["coneOuter"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Outer cone angle (SpotLight)." },
			["coneInner"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Inner cone angle (SpotLight)." },
			["fogMode"]       = FogModes,
			["fogStrength"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Fog strength (0-1)." }
		} );

	internal static Dictionary<string, object> SchemaCreateSkyBox => Schema( "create_sky_box",
		"Creates a GO with a SkyBox2D component for sky rendering.",
		new Dictionary<string, object>
		{
			["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["skyMaterial"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sky material path (e.g. 'materials/skybox/skybox_day_01.vmat')." },
			["tint"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tint color hex string." },
			["skyIndirectLighting"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use sky for indirect lighting (default true)." }
		} );

	internal static Dictionary<string, object> SchemaSetSkyBox => Schema( "set_sky_box",
		"Configures an existing SkyBox2D component.",
		new Dictionary<string, object>
		{
			["id"]                = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["skyMaterial"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sky material path." },
			["tint"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tint color hex string." },
			["skyIndirectLighting"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use sky for indirect lighting." }
		} );

	internal static Dictionary<string, object> SchemaCreateAmbientLight => Schema( "create_ambient_light",
		"Creates/updates a scene-level AmbientLight for global ambient illumination.",
		new Dictionary<string, object>
		{
			["x"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["color"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Ambient color (default 'Gray')." }
		} );

	// ── create_indirect_light_volume ──────────────────────────────────────

	internal static object CreateIndirectLightVolume( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name = OzmiumSceneHelpers.Get( args, "name", "Indirect Light Volume" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var ilg = go.Components.Create<IndirectLightVolume>();

			if ( args.TryGetProperty( "size", out var szEl ) && szEl.ValueKind == JsonValueKind.Object )
			{
				ilg.Bounds = BBox.FromPositionAndSize( 0,
					new Vector3(
						OzmiumSceneHelpers.Get( szEl, "x", 512f ),
						OzmiumSceneHelpers.Get( szEl, "y", 512f ),
						OzmiumSceneHelpers.Get( szEl, "z", 512f ) ) );
			}

			ilg.ProbeDensity = OzmiumSceneHelpers.Get( args, "probeDensity", 8 );
			ilg.NormalBias = OzmiumSceneHelpers.Get( args, "normalBias", 5f );
			ilg.Contrast = OzmiumSceneHelpers.Get( args, "contrast", 1f );

			if ( args.TryGetProperty( "insideGeometryBehavior", out var igbEl ) && igbEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<InsideGeometryBehaviorType>( igbEl.GetString(), true, out var igb ) )
				{
					// Use reflection to find the correct property name, as it changed from the enum name 'InsideGeometryBehavior'
					var behaviorProp = typeof( IndirectLightVolume ).GetProperties()
						.FirstOrDefault( p => p.PropertyType == typeof( InsideGeometryBehaviorType ) );
					behaviorProp?.SetValue( ilg, igb );
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created IndirectLightVolume '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				probeDensity = ilg.ProbeDensity
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	internal static Dictionary<string, object> SchemaCreateIndirectLightVolume => Schema( "create_indirect_light_volume",
		"Create a GO with an IndirectLightVolume (DDGI) for dynamic global illumination. Places a probe grid for real-time bounce light.",
		new Dictionary<string, object>
		{
			["x"]                       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]                     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["size"]                     = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Volume size {x,y,z} (default 512,512,512)." },
			["probeDensity"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Probe density (default 8)." },
			["normalBias"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Normal bias (default 5)." },
			["contrast"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "GI contrast (default 1)." },
			["insideGeometryBehavior"]   = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Behavior when probes are inside geometry.",
				["enum"] = new[] { "Deactivate", "Relocate" }
			}
		} );

	// ── manage_lighting (Omnibus) ──────────────────────────────────────────

	internal static object ManageLighting( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_light"               => CreateLight( args ),
			"configure_light"            => ConfigureLight( args ),
			"create_sky_box"             => CreateSkyBox( args ),
			"set_sky_box"                => SetSkyBox( args ),
			"create_ambient_light"       => CreateAmbientLight( args ),
			"create_indirect_light_volume" => CreateIndirectLightVolume( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create_light, configure_light, create_sky_box, set_sky_box, create_ambient_light, create_indirect_light_volume" )
		};
	}

	internal static Dictionary<string, object> SchemaManageLighting => Schema( "manage_lighting",
		"Manage lighting: create/configure lights, sky boxes, ambient lights, and indirect light volumes.",
		new Dictionary<string, object>
		{
			["operation"]                = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "create_light", "configure_light", "create_sky_box", "set_sky_box", "create_ambient_light", "create_indirect_light_volume" } },
			["id"]                       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID (for configure operations)." },
			["name"]                     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO or exact name." },
			["type"]                     = LightTypes,
			["x"]                        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["pitch"]                    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pitch rotation in degrees." },
			["yaw"]                      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Yaw rotation in degrees." },
			["roll"]                     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Roll rotation in degrees." },
			["color"]                    = ColorProp,
			["shadows"]                  = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Cast shadows." },
			["radius"]                   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Light radius." },
			["attenuation"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Light attenuation." },
			["coneOuter"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Outer cone angle." },
			["coneInner"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Inner cone angle." },
			["fogMode"]                  = FogModes,
			["fogStrength"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Fog strength (0-1)." },
			["skyMaterial"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sky material path." },
			["tint"]                     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tint color hex string." },
			["skyIndirectLighting"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use sky for indirect lighting." },
			["size"]                     = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Volume size {x,y,z}." },
			["probeDensity"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Probe density." },
			["normalBias"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Normal bias." },
			["contrast"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "GI contrast." },
			["insideGeometryBehavior"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Behavior when probes inside geometry.", ["enum"] = new[] { "Deactivate", "Relocate" } }
		},
		new[] { "operation" } );
}
