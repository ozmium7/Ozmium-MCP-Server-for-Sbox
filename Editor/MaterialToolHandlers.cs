using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Material parameter editing MCP tools.
/// Uses Material.Attributes (RenderAttributes) for shader parameter overrides,
/// and ModelRenderer.SetMaterialOverride for per-model material swapping.
/// </summary>
internal static class MaterialToolHandlers
{

	// ── set_param ──────────────────────────────────────────────────────────

	private static object SetParam( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", (string)null );
		string paramName    = OzmiumSceneHelpers.Get( args, "paramName", (string)null );
		string paramType    = OzmiumSceneHelpers.Get( args, "paramType", (string)null );

		if ( string.IsNullOrEmpty( materialPath ) ) return OzmiumSceneHelpers.Txt( "Provide 'materialPath'." );
		if ( string.IsNullOrEmpty( paramName ) ) return OzmiumSceneHelpers.Txt( "Provide 'paramName'." );

		var mat = Material.Load( OzmiumSceneHelpers.NormalizePath( materialPath ) );
		if ( mat == null )
		{
			// Try creating a copy so we can edit it non-destructively
			mat = Material.Load( materialPath );
			if ( mat == null )
				return OzmiumSceneHelpers.Txt( $"Material not found: '{materialPath}'." );
		}

		// Always create a copy to avoid modifying the shared asset
		var copy = mat.CreateCopy();
		if ( copy == null ) return OzmiumSceneHelpers.Txt( "Failed to create material copy." );

		try
		{
			if ( !args.TryGetProperty( "value", out var valEl ) )
				return OzmiumSceneHelpers.Txt( "Provide 'value'." );

			switch ( paramType?.ToLowerInvariant() )
			{
				case "float":
				{
					float v = valEl.ValueKind == JsonValueKind.Number ? valEl.GetSingle() : float.Parse( valEl.GetString() ?? "0" );
					copy.Attributes.Set( paramName, v );
					break;
				}
				case "color":
				{
					var colorStr = valEl.ValueKind == JsonValueKind.String ? valEl.GetString() : valEl.GetRawText();
					var color = Color.Parse( colorStr ) ?? Color.White;
					copy.Attributes.Set( paramName, new Vector4( color.r, color.g, color.b, color.a ) );
					break;
				}
				case "vector3":
				{
					float vx = 0, vy = 0, vz = 0;
					if ( valEl.ValueKind == JsonValueKind.Object )
					{
						if ( valEl.TryGetProperty( "x", out var xp ) ) vx = xp.GetSingle();
						if ( valEl.TryGetProperty( "y", out var yp ) ) vy = yp.GetSingle();
						if ( valEl.TryGetProperty( "z", out var zp ) ) vz = zp.GetSingle();
					}
					copy.Attributes.Set( paramName, new Vector3( vx, vy, vz ) );
					break;
				}
				case "int":
				{
					int v = valEl.ValueKind == JsonValueKind.Number ? valEl.GetInt32() : int.Parse( valEl.GetString() ?? "0" );
					copy.Attributes.Set( paramName, v );
					break;
				}
				case "bool":
				{
					bool v = valEl.ValueKind == JsonValueKind.True || (valEl.ValueKind == JsonValueKind.String && bool.Parse( valEl.GetString() ));
					copy.Attributes.Set( paramName, v );
					break;
				}
				default:
				{
					// Auto-detect from JSON type
					if ( valEl.ValueKind == JsonValueKind.Number )
						copy.Attributes.Set( paramName, valEl.GetSingle() );
					else if ( valEl.ValueKind == JsonValueKind.True || valEl.ValueKind == JsonValueKind.False )
						copy.Attributes.Set( paramName, valEl.GetBoolean() );
					else if ( valEl.ValueKind == JsonValueKind.String )
					{
						var s = valEl.GetString();
						if ( Color.Parse( s ) != null )
						{
							var c = Color.Parse( s ) ?? Color.White;
							copy.Attributes.Set( paramName, new Vector4( c.r, c.g, c.b, c.a ) );
						}
						else
						{
							copy.Attributes.Set( paramName, s );
						}
					}
					break;
				}
			}

			return OzmiumSceneHelpers.Txt( $"Set '{paramName}' (type={paramType ?? "auto"}) on material copy of '{materialPath}'. Copy name: '{copy.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_texture ────────────────────────────────────────────────────────

	private static object SetTexture( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", (string)null );
		string paramName    = OzmiumSceneHelpers.Get( args, "paramName", (string)null );
		string texturePath  = OzmiumSceneHelpers.Get( args, "texturePath", (string)null );

		if ( string.IsNullOrEmpty( materialPath ) ) return OzmiumSceneHelpers.Txt( "Provide 'materialPath'." );
		if ( string.IsNullOrEmpty( paramName ) ) return OzmiumSceneHelpers.Txt( "Provide 'paramName'." );
		if ( string.IsNullOrEmpty( texturePath ) ) return OzmiumSceneHelpers.Txt( "Provide 'texturePath'." );

		var mat = Material.Load( OzmiumSceneHelpers.NormalizePath( materialPath ) );
		if ( mat == null ) mat = Material.Load( materialPath );
		if ( mat == null ) return OzmiumSceneHelpers.Txt( $"Material not found: '{materialPath}'." );

		try
		{
			var copy = mat.CreateCopy();
			if ( copy == null ) return OzmiumSceneHelpers.Txt( "Failed to create material copy." );

			var tex = Texture.Load( OzmiumSceneHelpers.NormalizePath( texturePath ) );
			if ( tex == null ) tex = Texture.Load( texturePath );
			if ( tex == null ) return OzmiumSceneHelpers.Txt( $"Texture not found: '{texturePath}'." );

			copy.Attributes.Set( paramName, tex );

			return OzmiumSceneHelpers.Txt( $"Set texture '{paramName}' = '{texturePath}' on material copy of '{materialPath}'. Copy name: '{copy.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_params ─────────────────────────────────────────────────────────

	private static object GetParams( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", (string)null );
		if ( string.IsNullOrEmpty( materialPath ) ) return OzmiumSceneHelpers.Txt( "Provide 'materialPath'." );

		var mat = Material.Load( OzmiumSceneHelpers.NormalizePath( materialPath ) );
		if ( mat == null ) mat = Material.Load( materialPath );
		if ( mat == null ) return OzmiumSceneHelpers.Txt( $"Material not found: '{materialPath}'." );

		try
		{
			var info = new Dictionary<string, object>
			{
				["name"] = mat.Name,
				["path"] = materialPath
			};

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Material info for '{materialPath}'.",
				name = mat.Name,
				path = materialPath,
				note = "Use set_param to override shader parameters on a copy. Common params: g_vColorTint (color), F_BASE_COLOR (color), F_ROUGHNESS (float), F_METALLIC (float)."
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_model_override ─────────────────────────────────────────────────

	private static object SetModelOverride( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string target = OzmiumSceneHelpers.Get( args, "target", (string)null );
		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", (string)null );

		if ( string.IsNullOrEmpty( target ) ) return OzmiumSceneHelpers.Txt( "Provide 'target' (attribute target name, e.g. 'skin')." );
		if ( string.IsNullOrEmpty( materialPath ) ) return OzmiumSceneHelpers.Txt( "Provide 'materialPath'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var mr = go.Components.Get<ModelRenderer>();
		if ( mr == null ) return OzmiumSceneHelpers.Txt( $"No ModelRenderer on '{go.Name}'." );

		try
		{
			var mat = Material.Load( OzmiumSceneHelpers.NormalizePath( materialPath ) );
			if ( mat == null ) mat = Material.Load( materialPath );
			if ( mat == null ) return OzmiumSceneHelpers.Txt( $"Material not found: '{materialPath}'." );

			mr.SetMaterialOverride( mat, target );

			return OzmiumSceneHelpers.Txt( $"Set material override on '{go.Name}' for target '{target}' = '{materialPath}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── clear_model_overrides ──────────────────────────────────────────────

	private static object ClearModelOverrides( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var mr = go.Components.Get<ModelRenderer>();
		if ( mr == null ) return OzmiumSceneHelpers.Txt( $"No ModelRenderer on '{go.Name}'." );

		try
		{
			mr.ClearMaterialOverrides();
			return OzmiumSceneHelpers.Txt( $"Cleared all material overrides on '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── manage_material (Omnibus) ──────────────────────────────────────────

	internal static object ManageMaterial( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"set_param"            => SetParam( args ),
			"set_texture"          => SetTexture( args ),
			"get_params"           => GetParams( args ),
			"set_model_override"   => SetModelOverride( args ),
			"clear_model_overrides" => ClearModelOverrides( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: set_param, set_texture, get_params, set_model_override, clear_model_overrides" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageMaterial
	{
		get
		{
			var props = new Dictionary<string, object>();
			props["operation"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "set_param", "set_texture", "get_params", "set_model_override", "clear_model_overrides" } };
			props["materialPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Material asset path." };
			props["paramName"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Shader parameter name (e.g. g_vColorTint, F_ROUGHNESS)." };
			props["paramType"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Parameter type: float, color, vector3, int, bool, texture. Auto-detected if omitted.", ["enum"] = new[] { "float", "color", "vector3", "int", "bool", "texture" } };
			props["value"]       = new Dictionary<string, object> { ["description"] = "Value to set (number, string, bool, {x,y,z} for vector3, or hex color string)." };
			props["texturePath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Texture asset path (set_texture)." };
			props["id"]          = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of GO with ModelRenderer (set_model_override/clear)." };
			props["name"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of GO with ModelRenderer." };
			props["target"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Attribute target name for model override (e.g. 'skin')." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object> { ["name"] = "manage_material", ["description"] = "Edit material shader parameters and override materials on ModelRenderers. Create material copies with custom params, swap textures, override model materials by target.", ["inputSchema"] = schema };
		}
	}
}
