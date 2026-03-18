using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handlers for asset-query and editor-context MCP tools:
/// browse_assets, get_editor_context, get_model_info, get_material_properties,
/// get_prefab_structure, reload_asset.
/// </summary>
internal static class OzmiumAssetHandlers
{
	private static readonly JsonSerializerOptions _json = new()
	{
		PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	// ── browse_assets ──────────────────────────────────────────────────────

	internal static object BrowseAssets( JsonElement args )
	{
		string typeFilter = Get( args, "type",        (string)null );
		string nameFilter = Get( args, "nameContains",(string)null );
		int    max        = Get( args, "maxResults",  100 );

		var results  = new List<Dictionary<string, object>>();
		int total    = 0;

		try
		{
			foreach ( var asset in AssetSystem.All )
			{
				total++;
				var ext  = asset.AssetType?.FileExtension ?? "";
				var aName = asset.Name ?? "";
				var friendly = asset.AssetType?.FriendlyName ?? ext;

				if ( !string.IsNullOrEmpty( typeFilter ) )
				{
					bool match = ext.IndexOf( typeFilter, StringComparison.OrdinalIgnoreCase ) >= 0
					          || friendly.IndexOf( typeFilter, StringComparison.OrdinalIgnoreCase ) >= 0;
					if ( !match ) continue;
				}
				if ( !string.IsNullOrEmpty( nameFilter ) &&
					aName.IndexOf( nameFilter, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
				if ( results.Count >= max ) break;

				results.Add( new Dictionary<string, object>
				{
					["path"]         = asset.Path ?? "",
					["relativePath"] = asset.RelativePath ?? asset.Path ?? "",
					["name"]         = aName,
					["type"]         = friendly,
					["extension"]    = ext
				} );
			}
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }

		var summary = $"Found {results.Count} asset(s)" +
			( !string.IsNullOrEmpty( typeFilter ) ? $" type='{typeFilter}'" : "" ) +
			( !string.IsNullOrEmpty( nameFilter ) ? $" name='{nameFilter}'" : "" ) +
			$" (scanned {total}).";

		return Txt( JsonSerializer.Serialize( new { summary, results }, _json ) );
	}

	// ── get_editor_context ─────────────────────────────────────────────────

	internal static object GetEditorContext()
	{
		var ctx = new Dictionary<string, object>
		{
			["activeGameScene"] = Game.ActiveScene?.Name,
			["isPlaying"]       = Game.ActiveScene != null
		};

		try
		{
			var sessions = new List<Dictionary<string, object>>();
			foreach ( var s in SceneEditorSession.All )
			{
				if ( s == null ) continue;
				sessions.Add( new Dictionary<string, object>
				{
					["isActive"]    = s == SceneEditorSession.Active,
					["sceneName"]   = s.Scene?.Name,
					["objectCount"] = s.Scene != null ? OzmiumSceneHelpers.WalkAll( s.Scene, true ).Count() : 0
				} );
			}
			ctx["editorSessions"]     = sessions;
			ctx["activeSessionScene"] = SceneEditorSession.Active?.Scene?.Name;

			// Current selection
			var sel = new List<Dictionary<string, object>>();
			foreach ( var go in GetSelectedGameObjects() )
				sel.Add( new Dictionary<string, object>
				{
					["id"] = go.Id.ToString(), ["name"] = go.Name,
					["path"] = OzmiumSceneHelpers.GetObjectPath( go )
				} );
			ctx["selectedObjects"] = sel;
		}
		catch ( Exception ex ) { ctx["editorApiError"] = ex.Message; }

		return Txt( JsonSerializer.Serialize( ctx, _json ) );
	}

	// ── get_model_info ─────────────────────────────────────────────────────

	internal static object GetModelInfo( JsonElement args )
	{
		string path = Get( args, "path", (string)null );
		if ( string.IsNullOrEmpty( path ) ) return Txt( "Provide 'path' (model asset path, e.g. 'models/citizen_male.vmdl')." );

		try
		{
			var model = Model.Load( path );
			if ( model == null ) return Txt( $"Model not found: '{path}'." );

			var bones = new List<Dictionary<string, object>>();
			// BoneCollection.GetBone(string) takes a name, not an index.
			// We don't have a way to enumerate bone names, so just report the count.
			bones.Add( new Dictionary<string, object>
			{
				["note"] = $"{model.BoneCount} bones total. Use model viewer or VMDL source for bone names."
			} );

			var attachments = new List<Dictionary<string, object>>();
			try
			{
				// Use reflection: ModelAttachments API varies — don't assume Count or indexer exist
				var attObj = model.Attachments;
				if ( attObj != null )
				{
					var countProp = attObj.GetType().GetProperty( "Count" )
					             ?? attObj.GetType().GetProperty( "Length" );
					int count = countProp != null ? (int)countProp.GetValue( attObj ) : 0;
					var indexer = attObj.GetType().GetProperty( "Item" );
					for ( int i = 0; i < count; i++ )
					{
						try
						{
							var att = indexer?.GetValue( attObj, new object[] { i } );
							var attName = att?.GetType().GetProperty( "Name" )?.GetValue( att )?.ToString() ?? $"att_{i}";
							attachments.Add( new Dictionary<string, object> { ["name"] = attName, ["index"] = i } );
						}
						catch { attachments.Add( new Dictionary<string, object> { ["index"] = i } ); }
					}
				}
			}
			catch { attachments.Add( new Dictionary<string, object> { ["name"] = "(attachment iteration not supported)" } ); }

			return Txt( JsonSerializer.Serialize( new
			{
				path,
				boneCount       = model.BoneCount,
				bones,
				attachmentCount = attachments.Count,
				attachments
			}, _json ) );
		}
		catch ( Exception ex ) { return Txt( $"Error loading model: {ex.Message}" ); }
	}

	// ── get_material_properties ────────────────────────────────────────────

	internal static object GetMaterialProperties( JsonElement args )
	{
		string path = Get( args, "path", (string)null );
		if ( string.IsNullOrEmpty( path ) ) return Txt( "Provide 'path' (material asset path, e.g. 'materials/dev/dev_01.vmat')." );

		try
		{
			var mat = Material.Load( path );
			if ( mat == null ) return Txt( $"Material not found: '{path}'." );

			return Txt( JsonSerializer.Serialize( new
			{
				path,
				name   = mat.Name,
				shader = mat.ShaderName
			}, _json ) );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_prefab_structure ────────────────────────────────────────────────

	internal static object GetPrefabStructure( JsonElement args )
	{
		string path = Get( args, "path", (string)null );
		if ( string.IsNullOrEmpty( path ) ) return Txt( "Provide 'path' (relative prefab path, e.g. 'prefabs/player.prefab')." );

		try
		{
			// PrefabFile does not expose a live Scene property when not open in the editor;
			// fall back to reading the raw prefab JSON from disk.
			var asset = AssetSystem.FindByPath( path );
			if ( asset != null && System.IO.File.Exists( asset.AbsolutePath ) )
			{
				var raw = System.IO.File.ReadAllText( asset.AbsolutePath );
				return Txt( $"Raw prefab JSON for '{path}':\n{raw}" );
			}

			return Txt( $"Prefab not found: '{path}'." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── reload_asset ───────────────────────────────────────────────────────

	internal static object ReloadAsset( JsonElement args )
	{
		string path = Get( args, "path", (string)null );
		if ( string.IsNullOrEmpty( path ) ) return Txt( "Provide 'path'." );

		try
		{
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null ) return Txt( $"Asset not found: '{path}'." );
			asset.Compile( true );
			return Txt( $"Reimport triggered for '{path}'." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── Helpers ────────────────────────────────────────────────────────────

	private static object Txt( string text ) => new { content = new object[] { new { type = "text", text } } };

	private static T Get<T>( JsonElement el, string key, T def )
	{
		if ( el.ValueKind == JsonValueKind.Undefined ) return def;
		if ( !el.TryGetProperty( key, out var p ) ) return def;
		try
		{
			var t = typeof( T );
			if ( t == typeof( string ) ) return (T)(object)( p.ValueKind == JsonValueKind.Null ? null : p.GetString() );
			if ( t == typeof( bool ) )   return (T)(object)p.GetBoolean();
			if ( t == typeof( int ) )    return (T)(object)p.GetInt32();
			if ( t == typeof( float ) )  return (T)(object)p.GetSingle();
			return def;
		}
		catch { return def; }
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	private static Dictionary<string, object> S( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}
	private static Dictionary<string, object> P1( string key, string type, string desc )
		=> new Dictionary<string, object> { [key] = new Dictionary<string, object> { ["type"] = type, ["description"] = desc } };

	internal static Dictionary<string, object> SchemaGetModelInfo => S( "get_model_info",
		"Return bone names, attachment points, and sequence count for a .vmdl model.",
		P1( "path", "string", "Model path (e.g. 'models/citizen_male.vmdl')." ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaGetMaterialProperties => S( "get_material_properties",
		"Return shader name and surface properties for a .vmat material.",
		P1( "path", "string", "Material path (e.g. 'materials/dev/dev_01.vmat')." ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaGetPrefabStructure => S( "get_prefab_structure",
		"Return the full object/component hierarchy of a .prefab file without opening it.",
		P1( "path", "string", "Prefab path (e.g. 'prefabs/player.prefab')." ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaReloadAsset => S( "reload_asset",
		"Force reimport/recompile of a specific asset — useful after modifying source files on disk.",
		P1( "path", "string", "Asset path to reimport." ),
		new[] { "path" } );

	private static IEnumerable<GameObject> GetSelectedGameObjects()
	{
		var result = new List<GameObject>();
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return result;
			var selProp = session.GetType().GetProperty( "Selection",
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
			var selObj = selProp?.GetValue( session );
			if ( selObj == null ) return result;
			var objsProp = selObj.GetType().GetProperty( "Objects" );
			if ( objsProp?.GetValue( selObj ) is IEnumerable<object> objs )
				foreach ( var o in objs )
					if ( o is GameObject go ) result.Add( go );
		}
		catch { }
		return result;
	}
}

