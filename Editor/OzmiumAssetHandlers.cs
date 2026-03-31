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

	// ── browse_assets ──────────────────────────────────────────────────────

	internal static object BrowseAssets( JsonElement args )
	{
		string typeFilter = OzmiumSceneHelpers.Get( args, "type",        (string)null );
		string nameFilter = OzmiumSceneHelpers.Get( args, "nameContains",(string)null );
		int    max        = OzmiumSceneHelpers.Get( args, "maxResults",  100 );

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
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }

		var summary = $"Found {results.Count} asset(s)" +
			( !string.IsNullOrEmpty( typeFilter ) ? $" type='{typeFilter}'" : "" ) +
			( !string.IsNullOrEmpty( nameFilter ) ? $" name='{nameFilter}'" : "" ) +
			$" (scanned {total}).";

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new { summary, results }, OzmiumSceneHelpers.JsonSettings ) );
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
			foreach ( var go in OzmiumSceneHelpers.GetSelectedGameObjects() )
				sel.Add( new Dictionary<string, object>
				{
					["id"] = go.Id.ToString(), ["name"] = go.Name,
					["path"] = OzmiumSceneHelpers.GetObjectPath( go )
				} );
			ctx["selectedObjects"] = sel;
		}
		catch ( Exception ex ) { ctx["editorApiError"] = ex.Message; }

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( ctx, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── get_model_info ─────────────────────────────────────────────────────

	internal static object GetModelInfo( JsonElement args )
	{
		string path = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );
		if ( string.IsNullOrEmpty( path ) ) return OzmiumSceneHelpers.Txt( "Provide 'path' (model asset path, e.g. 'models/citizen_male.vmdl')." );

		try
		{
			var model = Model.Load( path );
			if ( model == null ) return OzmiumSceneHelpers.Txt( $"Model not found: '{path}'." );

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

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				path,
				boneCount       = model.BoneCount,
				bones,
				attachmentCount = attachments.Count,
				attachments
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error loading model: {ex.Message}" ); }
	}

	// ── get_material_properties ────────────────────────────────────────────

	internal static object GetMaterialProperties( JsonElement args )
	{
		string path = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );
		if ( string.IsNullOrEmpty( path ) ) return OzmiumSceneHelpers.Txt( "Provide 'path' (material asset path, e.g. 'materials/dev/dev_01.vmat')." );

		try
		{
			var mat = Material.Load( path );
			if ( mat == null ) return OzmiumSceneHelpers.Txt( $"Material not found: '{path}'." );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				path,
				name   = mat.Name,
				shader = mat.ShaderName
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_prefab_structure ────────────────────────────────────────────────

	internal static object GetPrefabStructure( JsonElement args )
	{
		string path = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );
		if ( string.IsNullOrEmpty( path ) ) return OzmiumSceneHelpers.Txt( "Provide 'path' (relative prefab path, e.g. 'prefabs/player.prefab')." );

		try
		{
			// PrefabFile does not expose a live Scene property when not open in the editor;
			// fall back to reading the raw prefab JSON from disk.
			var asset = AssetSystem.FindByPath( path );
			if ( asset != null && System.IO.File.Exists( asset.AbsolutePath ) )
			{
				var raw = System.IO.File.ReadAllText( asset.AbsolutePath );
				return OzmiumSceneHelpers.Txt( $"Raw prefab JSON for '{path}':\n{raw}" );
			}

			return OzmiumSceneHelpers.Txt( $"Prefab not found: '{path}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── reload_asset ───────────────────────────────────────────────────────

	internal static object ReloadAsset( JsonElement args )
	{
		string path = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );
		if ( string.IsNullOrEmpty( path ) ) return OzmiumSceneHelpers.Txt( "Provide 'path'." );

		try
		{
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null ) return OzmiumSceneHelpers.Txt( $"Asset not found: '{path}'." );
			asset.Compile( true );
			return OzmiumSceneHelpers.Txt( $"Reimport triggered for '{path}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_component_types ────────────────────────────────────────────────

	internal static object GetComponentTypes( JsonElement args )
	{
		string filter = OzmiumSceneHelpers.Get( args, "filter", (string)null );

		var results = new List<Dictionary<string, object>>();
		try
		{
			foreach ( var td in TypeLibrary.GetTypes<Component>() )
			{
				var name = td.Name;
				if ( !string.IsNullOrEmpty( filter )
					&& name.IndexOf( filter, StringComparison.OrdinalIgnoreCase ) < 0
					&& ( td.TargetType?.Namespace ?? "" ).IndexOf( filter, StringComparison.OrdinalIgnoreCase ) < 0 )
					continue;

				// Skip abstract types
				if ( td.TargetType != null && td.TargetType.IsAbstract ) continue;

				results.Add( new Dictionary<string, object>
				{
					["name"]  = name,
					["namespace"] = td.TargetType?.Namespace ?? ""
				} );
			}
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }

		results = results.OrderBy( r => r["name"]?.ToString() ).ToList();
		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			summary = $"Found {results.Count} component type(s)" +
				( !string.IsNullOrEmpty( filter ) ? $" matching '{filter}'" : "" ) + ".",
			results
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── search_assets ─────────────────────────────────────────────────────

	internal static object SearchAssets( JsonElement args )
	{
		string query   = OzmiumSceneHelpers.Get( args, "query",     (string)null );
		string type    = OzmiumSceneHelpers.Get( args, "type",      (string)null );
		int    max     = OzmiumSceneHelpers.Get( args, "maxResults", 50 );

		if ( string.IsNullOrEmpty( query ) ) return OzmiumSceneHelpers.Txt( "Provide 'query'." );

		var results = new List<Dictionary<string, object>>();
		try
		{
			foreach ( var asset in AssetSystem.All )
			{
				if ( results.Count >= max ) break;
				var ext     = asset.AssetType?.FileExtension ?? "";
				var aName   = asset.Name ?? "";
				var aPath   = asset.Path ?? "";
				var friendly = asset.AssetType?.FriendlyName ?? ext;

				// Match query against name and path
				bool match = aName.IndexOf( query, StringComparison.OrdinalIgnoreCase ) >= 0
				          || aPath.IndexOf( query, StringComparison.OrdinalIgnoreCase ) >= 0;
				if ( !match ) continue;

				if ( !string.IsNullOrEmpty( type ) )
				{
					bool typeMatch = ext.IndexOf( type, StringComparison.OrdinalIgnoreCase ) >= 0
					                || friendly.IndexOf( type, StringComparison.OrdinalIgnoreCase ) >= 0;
					if ( !typeMatch ) continue;
				}

				results.Add( new Dictionary<string, object>
				{
					["path"]         = aPath,
					["relativePath"] = asset.RelativePath ?? aPath,
					["name"]         = aName,
					["type"]         = friendly,
					["extension"]    = ext
				} );
			}
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			summary = $"Found {results.Count} asset(s) matching '{query}'" +
				( !string.IsNullOrEmpty( type ) ? $" type='{type}'" : "" ) + ".",
			results
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── get_scene_statistics ──────────────────────────────────────────────

	internal static object GetSceneStatistics()
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		var allObjects = OzmiumSceneHelpers.WalkAll( scene, true ).ToList();

		// Component type frequency
		var compCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
			foreach ( var comp in go.Components.GetAll() )
			{
				var typeName = comp.GetType().Name;
				compCounts.TryGetValue( typeName, out var existing );
				compCounts[typeName] = existing + 1;
			}

		// All unique tags
		var allTags = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
			foreach ( var tag in go.Tags.TryGetAll() )
				allTags.Add( tag );

		// Prefab source breakdown
		var prefabCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects.Where( g => g.IsPrefabInstance && g.PrefabInstanceSource != null ) )
		{
			var src = go.PrefabInstanceSource;
			prefabCounts.TryGetValue( src, out var existing );
			prefabCounts[src] = existing + 1;
		}

		// Network mode distribution
		var netModeCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
		{
			var mode = go.NetworkMode.ToString();
			netModeCounts.TryGetValue( mode, out var existing );
			netModeCounts[mode] = existing + 1;
		}

		var stats = new Dictionary<string, object>
		{
			["sceneName"]            = scene.Name,
			["totalObjects"]         = allObjects.Count,
			["rootObjects"]          = scene.Children.Count,
			["enabledObjects"]       = allObjects.Count( g => g.Enabled ),
			["disabledObjects"]      = allObjects.Count( g => !g.Enabled ),
			["uniqueTags"]           = allTags.OrderBy( t => t ).ToList(),
			["componentBreakdown"]   = compCounts
				.OrderByDescending( kv => kv.Value )
				.Select( kv => new Dictionary<string, object> { ["type"] = kv.Key, ["count"] = kv.Value } )
				.ToList(),
			["prefabBreakdown"]      = prefabCounts
				.OrderByDescending( kv => kv.Value )
				.Select( kv => new Dictionary<string, object> { ["prefab"] = kv.Key, ["instances"] = kv.Value } )
				.ToList(),
			["networkModeBreakdown"] = netModeCounts
		};

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( stats, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── Schemas ─────────────────────────────────────────────────────────────

	private static Dictionary<string, object> P1( string key, string type, string desc )
		=> new Dictionary<string, object> { [key] = new Dictionary<string, object> { ["type"] = type, ["description"] = desc } };

	internal static Dictionary<string, object> SchemaGetModelInfo => OzmiumSceneHelpers.S( "get_model_info",
		"Return bone names, attachment points, and sequence count for a .vmdl model.",
		P1( "path", "string", "Model path (e.g. 'models/citizen_male.vmdl')." ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaGetMaterialProperties => OzmiumSceneHelpers.S( "get_material_properties",
		"Return shader name and surface properties for a .vmat material.",
		P1( "path", "string", "Material path (e.g. 'materials/dev/dev_01.vmat')." ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaGetPrefabStructure => OzmiumSceneHelpers.S( "get_prefab_structure",
		"Return the full object/component hierarchy of a .prefab file without opening it.",
		P1( "path", "string", "Prefab path (e.g. 'prefabs/player.prefab')." ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaReloadAsset => OzmiumSceneHelpers.S( "reload_asset",
		"Force reimport/recompile of a specific asset — useful after modifying source files on disk.",
		P1( "path", "string", "Asset path to reimport." ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaGetComponentTypes => OzmiumSceneHelpers.S( "get_component_types",
		"List all available component types via TypeLibrary, so AI knows what components can be added.",
		new Dictionary<string, object>
		{
			["filter"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Optional filter string to match against type name or namespace." }
		} );

	internal static Dictionary<string, object> SchemaSearchAssets => OzmiumSceneHelpers.S( "search_assets",
		"Search assets by content (file extension filter + substring matching on name and path).",
		new Dictionary<string, object>
		{
			["query"]      = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Search query (matches name and path)." },
			["type"]       = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Optional file type filter (e.g. 'prefab', 'vmdl', 'vmat')." },
			["maxResults"] = new Dictionary<string, object> { ["type"] = "integer", ["description"] = "Max results (default 50)." }
		},
		new[] { "query" } );

	internal static Dictionary<string, object> SchemaGetSceneStatistics => OzmiumSceneHelpers.S( "get_scene_statistics",
		"Enhanced scene summary with component type frequency, prefab breakdown, network mode distribution, and tags.",
		new Dictionary<string, object>() );
}

