using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Camera MCP tools: create_camera, configure_camera.
/// </summary>
internal static class CameraToolHandlers
{

	// ── create_camera ──────────────────────────────────────────────────────

	internal static object CreateCamera( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float  x     = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y     = OzmiumSceneHelpers.Get( args, "y", 100f );
		float  z     = OzmiumSceneHelpers.Get( args, "z", 0f );
		float  pitch = OzmiumSceneHelpers.Get( args, "pitch", -90f );
		float  yaw   = OzmiumSceneHelpers.Get( args, "yaw", 0f );
		float  roll  = OzmiumSceneHelpers.Get( args, "roll", 0f );
		string name  = OzmiumSceneHelpers.Get( args, "name", "Camera" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );
			go.WorldRotation = Rotation.From( pitch, yaw, roll );

			var cam = go.Components.Create<CameraComponent>();
			cam.FieldOfView = OzmiumSceneHelpers.Get( args, "fov", 60f );
			cam.ZNear = OzmiumSceneHelpers.Get( args, "zNear", 10f );
			cam.ZFar = OzmiumSceneHelpers.Get( args, "zFar", 10000f );
			cam.IsMainCamera = OzmiumSceneHelpers.Get( args, "isMainCamera", true );

			if ( args.TryGetProperty( "orthographic", out var orthoEl ) )
				cam.Orthographic = orthoEl.GetBoolean();
			if ( args.TryGetProperty( "orthographicHeight", out var ohEl ) )
				cam.OrthographicHeight = ohEl.GetSingle();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created Camera '{go.Name}'.",
				id       = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				fov      = cam.FieldOfView
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_camera ────────────────────────────────────────────────────

	internal static object ConfigureCamera( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGoWithComponent<CameraComponent>( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"No object with CameraComponent found (id={id ?? "null"}, name={name ?? "null"})." );

		var cam = go.Components.Get<CameraComponent>();

		try
		{
			if ( args.TryGetProperty( "fov", out var fovEl ) )
				cam.FieldOfView = fovEl.GetSingle();
			if ( args.TryGetProperty( "zNear", out var znEl ) )
				cam.ZNear = znEl.GetSingle();
			if ( args.TryGetProperty( "zFar", out var zfEl ) )
				cam.ZFar = zfEl.GetSingle();
			if ( args.TryGetProperty( "isMainCamera", out var mcEl ) )
				cam.IsMainCamera = mcEl.GetBoolean();
			if ( args.TryGetProperty( "orthographic", out var orthoEl ) )
				cam.Orthographic = orthoEl.GetBoolean();
			if ( args.TryGetProperty( "orthographicHeight", out var ohEl ) )
				cam.OrthographicHeight = ohEl.GetSingle();
			if ( args.TryGetProperty( "backgroundColor", out var bgEl ) && bgEl.ValueKind == JsonValueKind.String )
			{
				try { cam.BackgroundColor = Color.Parse( bgEl.GetString() ) ?? default; } catch { }
			}
			if ( args.TryGetProperty( "priority", out var prEl ) )
				cam.Priority = prEl.GetInt32();

			return OzmiumSceneHelpers.Txt( $"Configured CameraComponent on '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── list_cameras ──────────────────────────────────────────────────────

	internal static object ListCameras()
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		var cameras = new List<Dictionary<string, object>>();
		foreach ( var go in OzmiumSceneHelpers.WalkAll( scene, true ) )
		{
			var cam = go.Components.Get<CameraComponent>();
			if ( cam == null ) continue;

			cameras.Add( new Dictionary<string, object>
			{
				["id"]           = go.Id.ToString(),
				["name"]         = go.Name,
				["enabled"]      = go.Enabled,
				["position"]     = OzmiumSceneHelpers.V3( go.WorldPosition ),
				["fov"]          = cam.FieldOfView,
				["zNear"]        = cam.ZNear,
				["zFar"]         = cam.ZFar,
				["isMainCamera"] = cam.IsMainCamera,
				["orthographic"] = cam.Orthographic
			} );
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			summary = $"{cameras.Count} camera(s) found.",
			cameras
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	private static Dictionary<string, object> S( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}

	internal static Dictionary<string, object> SchemaCreateCamera => S( "create_camera",
		"Creates a GO with a CameraComponent.",
		new Dictionary<string, object>
	{
			["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["pitch"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pitch rotation in degrees." },
			["yaw"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Yaw rotation in degrees." },
			["roll"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Roll rotation in degrees." },
			["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["fov"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Field of view in degrees." },
			["zNear"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Near clip plane distance." },
			["zFar"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Far clip plane distance." },
			["isMainCamera"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether this is the main camera (default true)." },
			["orthographic"]     = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use orthographic projection." },
			["orthographicHeight"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Orthographic height." }
		} );

	internal static Dictionary<string, object> SchemaConfigureCamera => S( "configure_camera",
		"Configures an existing CameraComponent.",
		new Dictionary<string, object>
	{
			["id"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["fov"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Field of view." },
			["zNear"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Near clip plane." },
			["zFar"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Far clip plane." },
			["isMainCamera"]      = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Main camera flag." },
			["orthographic"]     = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Orthographic projection." },
			["orthographicHeight"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Orthographic height." },
			["backgroundColor"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Background color hex." },
			["priority"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Camera priority (higher renders on top)." }
		} );

	// ── manage_camera (Omnibus) ───────────────────────────────────────────

	internal static object ManageCamera( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_camera"    => CreateCamera( args ),
			"configure_camera" => ConfigureCamera( args ),
			"list_cameras"     => ListCameras(),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create_camera, configure_camera, list_cameras" )
		};
	}

	internal static Dictionary<string, object> SchemaManageCamera => S( "manage_camera",
		"Manage cameras: create, configure, and list CameraComponents.",
		new Dictionary<string, object>
		{
			["operation"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "create_camera", "configure_camera", "list_cameras" } },
			["id"]                 = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID (for configure)." },
			["name"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO." },
			["x"]                  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." },
			["y"]                  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." },
			["z"]                  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." },
			["pitch"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pitch rotation in degrees." },
			["yaw"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Yaw rotation in degrees." },
			["roll"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Roll rotation in degrees." },
			["fov"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Field of view." },
			["zNear"]              = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Near clip plane." },
			["zFar"]               = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Far clip plane." },
			["isMainCamera"]        = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Main camera flag." },
			["orthographic"]       = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Orthographic projection." },
			["orthographicHeight"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Orthographic height." },
			["backgroundColor"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Background color hex." },
			["priority"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Camera priority." }
		},
		new[] { "operation" } );
}
