using System;
using System.Collections.Generic;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Rendering MCP tools: create_text_renderer, create_line_renderer, create_sprite_renderer, create_trail_renderer.
/// </summary>
internal static class RenderingToolHandlers
{

	// ── create_text_renderer ───────────────────────────────────────────────

	internal static object CreateTextRenderer( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Text Renderer" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var tr = go.Components.Create<TextRenderer>();
			tr.Text = OzmiumSceneHelpers.Get( args, "text", tr.Text );
			tr.FontSize = OzmiumSceneHelpers.Get( args, "fontSize", tr.FontSize );
			tr.Scale = OzmiumSceneHelpers.Get( args, "scale", tr.Scale );

			if ( args.TryGetProperty( "color", out var colEl ) && colEl.ValueKind == JsonValueKind.String )
			{
				try { tr.Color = Color.Parse( colEl.GetString() ) ?? default; } catch { }
			}

			if ( args.TryGetProperty( "horizontalAlignment", out var hEl ) && hEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<TextRenderer.HAlignment>( hEl.GetString(), true, out var h ) )
					tr.HorizontalAlignment = h;
			}

			if ( args.TryGetProperty( "verticalAlignment", out var vEl ) && vEl.ValueKind == JsonValueKind.String )
			{
				if ( Enum.TryParse<TextRenderer.VAlignment>( vEl.GetString(), true, out var v ) )
					tr.VerticalAlignment = v;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created TextRenderer '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_line_renderer ───────────────────────────────────────────────

	internal static object CreateLineRenderer( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Line Renderer" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var lr = go.Components.Create<LineRenderer>();
			lr.UseVectorPoints = true;
			lr.VectorPoints = new List<Vector3>();

			if ( args.TryGetProperty( "points", out var ptsEl ) && ptsEl.ValueKind == JsonValueKind.Array )
			{
				foreach ( var ptEl in ptsEl.EnumerateArray() )
				{
					if ( ptEl.ValueKind == JsonValueKind.Object )
					{
						lr.VectorPoints.Add( new Vector3(
							OzmiumSceneHelpers.Get( ptEl, "x", 0f ),
							OzmiumSceneHelpers.Get( ptEl, "y", 0f ),
							OzmiumSceneHelpers.Get( ptEl, "z", 0f ) ) );
					}
				}
			}

			if ( args.TryGetProperty( "color", out var colEl ) && colEl.ValueKind == JsonValueKind.String )
			{
				try { lr.Color = Color.Parse( colEl.GetString() ) ?? default; } catch { }
			}

			if ( args.TryGetProperty( "width", out var wEl ) )
				lr.Width = 5; // Width is a Curve, but we set a default via property

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created LineRenderer '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				pointCount = lr.VectorPoints?.Count ?? 0
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_sprite_renderer ──────────────────────────────────────────────

	internal static object CreateSpriteRenderer( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Sprite Renderer" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			// SpriteRenderer — look up via TypeLibrary (may not exist in all versions)
			var spriteTd = OzmiumWriteHandlers.FindComponentTypeDescription( "SpriteRenderer" );
			if ( spriteTd == null )
				return OzmiumSceneHelpers.Txt( "SpriteRenderer is not available in this S&box version." );

			var comp = go.Components.Create( spriteTd );

			if ( args.TryGetProperty( "color", out var colEl ) && colEl.ValueKind == JsonValueKind.String )
			{
				try
				{
					var prop = spriteTd.TargetType.GetProperty( "Color" );
					if ( prop != null ) prop.SetValue( comp, Color.Parse( colEl.GetString() ) ?? default );
				}
				catch { }
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created SpriteRenderer '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_trail_renderer ───────────────────────────────────────────────

	internal static object CreateTrailRenderer( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Trail Renderer" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var trail = go.Components.Create<TrailRenderer>();
			trail.MaxPoints = OzmiumSceneHelpers.Get( args, "maxPoints", trail.MaxPoints );
			trail.PointDistance = OzmiumSceneHelpers.Get( args, "pointDistance", trail.PointDistance );
			trail.LifeTime = OzmiumSceneHelpers.Get( args, "lifetime", trail.LifeTime );
			trail.Emitting = OzmiumSceneHelpers.Get( args, "emitting", trail.Emitting );

			if ( args.TryGetProperty( "color", out var colEl ) && colEl.ValueKind == JsonValueKind.String )
			{
				try { trail.Color = Color.Parse( colEl.GetString() ) ?? default; } catch { }
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created TrailRenderer '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_model_renderer ──────────────────────────────────────────────

	internal static object CreateModelRenderer( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Model Renderer" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var mr = go.Components.Create<ModelRenderer>();

			if ( args.TryGetProperty( "modelPath", out var mpEl ) && mpEl.ValueKind == JsonValueKind.String )
			{
				var model = Model.Load( mpEl.GetString() );
				if ( model != null ) mr.Model = model;
			}

			if ( args.TryGetProperty( "tint", out var tintEl ) && tintEl.ValueKind == JsonValueKind.String )
			{
				try { mr.Tint = Color.Parse( tintEl.GetString() ) ?? default; } catch { }
			}

			mr.RenderType = OzmiumSceneHelpers.Get( args, "castsShadows", true ) 
				? ModelRenderer.ShadowRenderType.On 
				: ModelRenderer.ShadowRenderType.Off;

			if ( args.TryGetProperty( "bodyGroups", out var bgEl ) && bgEl.ValueKind == JsonValueKind.String )
			{
				if ( ulong.TryParse( bgEl.GetString(), out var bg ) ) mr.BodyGroups = bg;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created ModelRenderer '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				model    = mr.Model?.ResourcePath ?? "null"
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_skinned_model ──────────────────────────────────────────────

	internal static object CreateSkinnedModel( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Skinned Model" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var sk = go.Components.Create<SkinnedModelRenderer>();

			if ( args.TryGetProperty( "modelPath", out var mpEl ) && mpEl.ValueKind == JsonValueKind.String )
			{
				var model = Model.Load( mpEl.GetString() );
				if ( model != null ) sk.Model = model;
			}

			if ( args.TryGetProperty( "tint", out var tintEl ) && tintEl.ValueKind == JsonValueKind.String )
			{
				try { sk.Tint = Color.Parse( tintEl.GetString() ) ?? default; } catch { }
			}

			sk.UseAnimGraph = OzmiumSceneHelpers.Get( args, "useAnimGraph", true );
			sk.CreateBoneObjects = OzmiumSceneHelpers.Get( args, "createBoneObjects", false );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created SkinnedModelRenderer '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				model    = sk.Model?.ResourcePath ?? "null",
				useAnimGraph = sk.UseAnimGraph
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_screen_panel ──────────────────────────────────────────────

	internal static object CreateScreenPanel( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x    = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y    = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z    = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Screen Panel" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var sp = go.Components.Create<ScreenPanel>();
			sp.Opacity = OzmiumSceneHelpers.Get( args, "opacity", 1f );
			sp.Scale = OzmiumSceneHelpers.Get( args, "scale", 1f );
			sp.ZIndex = OzmiumSceneHelpers.Get( args, "zIndex", 100 );
			sp.AutoScreenScale = OzmiumSceneHelpers.Get( args, "autoScreenScale", true );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created ScreenPanel '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				opacity  = sp.Opacity,
				scale    = sp.Scale,
				zIndex   = sp.ZIndex
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

	internal static Dictionary<string, object> SchemaCreateTextRenderer => S( "create_text_renderer",
		"Create a GO with a TextRenderer component for 3D world text.",
		new Dictionary<string, object>
		{
			["x"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                 = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["text"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Text to display." },
			["fontSize"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Font size." },
			["color"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Text color hex." },
			["scale"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World size scale." },
			["horizontalAlignment"] = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Horizontal alignment.",
				["enum"] = new[] { "Left", "Center", "Right" }
			},
			["verticalAlignment"]   = new Dictionary<string, object>
			{
				["type"] = "string", ["description"] = "Vertical alignment.",
				["enum"] = new[] { "Top", "Center", "Bottom" }
			}
		} );

	internal static Dictionary<string, object> SchemaCreateLineRenderer => S( "create_line_renderer",
		"Create a GO with a LineRenderer component for lines/paths.",
		new Dictionary<string, object>
		{
			["x"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["points"] = new Dictionary<string, object>
			{
				["type"] = "array", ["description"] = "Array of Vector3 points {x,y,z}.",
				["items"] = new Dictionary<string, object> { ["type"] = "object",
					["properties"] = new Dictionary<string, object>
					{
						["x"] = new Dictionary<string, object> { ["type"] = "number" },
						["y"] = new Dictionary<string, object> { ["type"] = "number" },
						["z"] = new Dictionary<string, object> { ["type"] = "number" }
					}
				}
			},
			["color"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Line color hex." }
		} );

	internal static Dictionary<string, object> SchemaCreateSpriteRenderer => S( "create_sprite_renderer",
		"Create a GO with a SpriteRenderer for 2D billboards in 3D.",
		new Dictionary<string, object>
		{
			["x"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["color"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sprite color hex." }
		} );

	internal static Dictionary<string, object> SchemaCreateTrailRenderer => S( "create_trail_renderer",
		"Create a GO with a TrailRenderer for object trails.",
		new Dictionary<string, object>
		{
			["x"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["maxPoints"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Maximum trail points." },
			["pointDistance"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Distance between trail points." },
			["lifetime"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Trail point lifetime in seconds." },
			["emitting"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether the trail emits points." },
			["color"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Trail color hex." }
		} );

	internal static Dictionary<string, object> SchemaCreateModelRenderer => S( "create_model_renderer",
		"Create a GO with a ModelRenderer (static model display). Unlike Prop, this is purely visual — no physics, no breakable behavior. Essential for decorative models.",
		new Dictionary<string, object>
		{
			["x"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["modelPath"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Model asset path (e.g. 'models/citizen.vmdl')." },
			["tint"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex (default '#FFFFFF')." },
			["castsShadows"]  = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Cast shadows (default true)." },
			["bodyGroups"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Body group mask (ulong)." }
		} );

	internal static Dictionary<string, object> SchemaCreateSkinnedModel => S( "create_skinned_model",
		"Create a GO with a SkinnedModelRenderer for animated models (NPCs, characters). THE component for animated characters — lets AI set up NPCs and animated props.",
		new Dictionary<string, object>
		{
			["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["modelPath"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Model asset path (e.g. 'models/citizen.vmdl')." },
			["tint"]              = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex." },
			["useAnimGraph"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use animation graph (default true)." },
			["createBoneObjects"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Create bone objects (default false)." }
		} );

	internal static Dictionary<string, object> SchemaCreateScreenPanel => S( "create_screen_panel",
		"Create a GO with a ScreenPanel for 2D HUD-style UI (health bars, score counters, debug overlays). Renders flat to screen, unlike WorldPanel which is 3D.",
		new Dictionary<string, object>
		{
			["x"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["opacity"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Panel opacity (default 1)." },
			["scale"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Panel scale (default 1)." },
			["zIndex"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Z-order index (default 100)." },
			["autoScreenScale"]  = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Auto-scale with screen resolution (default true)." }
		} );

	// ── create_render_entity (Omnibus) ──────────────────────────────────────

	internal static object CreateRenderEntity( JsonElement args )
	{
		string renderType = OzmiumSceneHelpers.Get( args, "renderType", "" );
		return renderType switch
		{
			"TextRenderer" => CreateTextRenderer( args ),
			"LineRenderer" => CreateLineRenderer( args ),
			"SpriteRenderer" => CreateSpriteRenderer( args ),
			"TrailRenderer" => CreateTrailRenderer( args ),
			"ModelRenderer" => CreateModelRenderer( args ),
			"SkinnedModelRenderer" => CreateSkinnedModel( args ),
			"ScreenPanel" => CreateScreenPanel( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown renderType: {renderType}" )
		};
	}

	internal static Dictionary<string, object> SchemaCreateRenderEntity => S( "create_render_entity",
		"Create a rendering entity (TextRenderer, LineRenderer, SpriteRenderer, TrailRenderer, ModelRenderer, SkinnedModelRenderer, ScreenPanel).",
		new Dictionary<string, object>
		{
			["renderType"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Type of renderer.", ["enum"] = new[] { "TextRenderer", "LineRenderer", "SpriteRenderer", "TrailRenderer", "ModelRenderer", "SkinnedModelRenderer", "ScreenPanel" } },
			["x"]                   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["name"]                = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["text"]                = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Text to display." },
			["fontSize"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Font size." },
			["color"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color hex." },
			["tint"]                = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex." },
			["scale"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Size scale." },
			["horizontalAlignment"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Horizontal alignment." },
			["verticalAlignment"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Vertical alignment." },
			["points"]              = new Dictionary<string, object> { ["type"] = "array",  ["description"] = "Array of Vector3 points {x,y,z}.", ["items"] = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object> { ["x"] = new Dictionary<string, object> { ["type"] = "number" }, ["y"] = new Dictionary<string, object> { ["type"] = "number" }, ["z"] = new Dictionary<string, object> { ["type"] = "number" } } } },
			["maxPoints"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Maximum trail points." },
			["pointDistance"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Distance between trail points." },
			["lifetime"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Trail point lifetime in seconds." },
			["emitting"]            = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether the trail emits points." },
			["modelPath"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Model asset path." },
			["castsShadows"]        = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Cast shadows." },
			["bodyGroups"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Body group mask (ulong)." },
			["useAnimGraph"]        = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use animation graph." },
			["createBoneObjects"]   = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Create bone objects." },
			["opacity"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Panel opacity." },
			["zIndex"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Z-order index." },
			["autoScreenScale"]     = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Auto-scale with screen resolution." }
		},
		new[] { "renderType" } );
}
