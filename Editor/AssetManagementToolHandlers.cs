using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Omnibus handler for asset management operations:
/// create_asset, rename_asset, move_asset, delete_asset, find_unused_assets.
/// </summary>
internal static class AssetManagementToolHandlers
{
	// ── create_asset ────────────────────────────────────────────────────────

	private static object CreateAsset( JsonElement args )
	{
		string type = OzmiumSceneHelpers.Get( args, "type", (string)null );
		string path = OzmiumSceneHelpers.Get( args, "path", (string)null );

		if ( string.IsNullOrEmpty( type ) )
			return OzmiumSceneHelpers.Txt( "Provide 'type' — asset type extension (e.g. 'vmat', 'prefab', 'scene', 'vsnd')." );

		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' — save location for the new asset (e.g. 'materials/my_material.vmat')." );

		// Ensure the path has the right extension
		if ( !path.EndsWith( $".{type}", StringComparison.OrdinalIgnoreCase ) )
			path = $"{path}.{type}";

		try
		{
			// Verify the asset type is valid
			var assetType = AssetType.FromExtension( type );
			if ( assetType == null )
				return OzmiumSceneHelpers.Txt( $"Unknown asset type '{type}'. Common types: vmat, prefab, scene, vsnd, vmdl." );

			// Check if asset already exists
			var existing = AssetSystem.FindByPath( path );
			if ( existing != null )
				return OzmiumSceneHelpers.Txt( $"Asset already exists at '{path}'. Use a different path or delete it first." );

			// Resolve to absolute path for CreateResource
			string absolutePath = path;
			if ( !System.IO.Path.IsPathRooted( absolutePath ) )
			{
				// Resolve relative to current project
				var project = Project.Current;
				if ( project == null )
					return OzmiumSceneHelpers.Txt( "No active project found. Provide an absolute path." );

				absolutePath = System.IO.Path.Combine( project.GetAssetsPath() ?? project.GetRootPath(), path );
			}

			// Ensure parent directory exists
			var dir = System.IO.Path.GetDirectoryName( absolutePath );
			if ( !string.IsNullOrEmpty( dir ) && !System.IO.Directory.Exists( dir ) )
				System.IO.Directory.CreateDirectory( dir );

			var asset = AssetSystem.CreateResource( type, absolutePath );
			if ( asset == null )
				return OzmiumSceneHelpers.Txt( $"Failed to create asset. Verify the path is valid: '{absolutePath}'." );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message      = $"Created {assetType.FriendlyName} asset at '{asset.Path}'.",
				path         = asset.Path,
				absolutePath = asset.AbsolutePath,
				type         = assetType.FriendlyName,
				extension    = assetType.FileExtension
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error creating asset: {ex.Message}" ); }
	}

	// ── rename_asset ────────────────────────────────────────────────────────

	private static object RenameAsset( JsonElement args )
	{
		string path    = OzmiumSceneHelpers.Get( args, "path", (string)null );
		string newName = OzmiumSceneHelpers.Get( args, "newName", (string)null );

		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' — the current asset path." );

		if ( string.IsNullOrEmpty( newName ) )
			return OzmiumSceneHelpers.Txt( "Provide 'newName' — the new file name (without extension)." );

		try
		{
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null )
				return OzmiumSceneHelpers.Txt( $"Asset not found: '{path}'. Use browse_assets to find valid paths." );

			string oldName = asset.Name;
			bool success = EditorUtility.RenameAsset( asset, newName );

			if ( !success )
				return OzmiumSceneHelpers.Txt( $"Failed to rename '{oldName}' to '{newName}'. The name may be invalid or already taken." );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Renamed '{oldName}' to '{newName}'.",
				oldName,
				newName,
				newPath = asset.Path
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error renaming asset: {ex.Message}" ); }
	}

	// ── move_asset ──────────────────────────────────────────────────────────

	private static object MoveAsset( JsonElement args )
	{
		string path      = OzmiumSceneHelpers.Get( args, "path", (string)null );
		string directory = OzmiumSceneHelpers.Get( args, "directory", (string)null );
		bool   overwrite = OzmiumSceneHelpers.Get( args, "overwrite", true );

		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' — the current asset path." );

		if ( string.IsNullOrEmpty( directory ) )
			return OzmiumSceneHelpers.Txt( "Provide 'directory' — the target directory to move the asset into." );

		try
		{
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null )
				return OzmiumSceneHelpers.Txt( $"Asset not found: '{path}'." );

			// Resolve directory to absolute if needed
			string absDir = directory;
			if ( !System.IO.Path.IsPathRooted( absDir ) )
			{
				var project = Project.Current;
				if ( project != null )
					absDir = System.IO.Path.Combine( project.GetAssetsPath() ?? project.GetRootPath(), directory );
			}

			if ( !System.IO.Directory.Exists( absDir ) )
				System.IO.Directory.CreateDirectory( absDir );

			string oldPath = asset.Path;
			EditorUtility.MoveAssetToDirectory( asset, absDir, overwrite );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Moved '{oldPath}' to '{directory}'.",
				oldPath,
				newPath   = asset.Path,
				directory
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error moving asset: {ex.Message}" ); }
	}

	// ── delete_asset ────────────────────────────────────────────────────────

	private static object DeleteAsset( JsonElement args )
	{
		string path = OzmiumSceneHelpers.Get( args, "path", (string)null );

		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' — the asset path to delete." );

		try
		{
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null )
				return OzmiumSceneHelpers.Txt( $"Asset not found: '{path}'." );

			// Check dependants to warn about breaking references
			var dependants = asset.GetDependants( false );
			int dependantCount = dependants?.Count ?? 0;

			string warning = "";
			if ( dependantCount > 0 )
			{
				var depNames = dependants.Take( 5 ).Select( d => d.Path ).ToList();
				warning = $" Warning: {dependantCount} asset(s) reference this asset ({string.Join( ", ", depNames )}{(dependantCount > 5 ? "..." : "")}).";
			}

			// Use the Asset.Delete() method which sends to recycle bin
			asset.Delete();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Deleted '{path}' (sent to recycle bin).{warning}",
				path,
				dependantsAffected = dependantCount
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error deleting asset: {ex.Message}" ); }
	}

	// ── find_unused_assets ──────────────────────────────────────────────────

	private static object FindUnusedAssets( JsonElement args )
	{
		string typeFilter = OzmiumSceneHelpers.Get( args, "type", (string)null );
		int    maxResults = OzmiumSceneHelpers.Get( args, "maxResults", 100 );

		try
		{
			var allAssets = AssetSystem.All;
			if ( allAssets == null )
				return OzmiumSceneHelpers.Txt( "No assets available." );

			var unused = new List<object>();
			int scanned = 0;

			foreach ( var asset in allAssets )
			{
				// Skip trivial/generated child assets
				if ( asset.IsTrivialChild || asset.IsProcedural || asset.IsTransient || asset.IsCloud )
					continue;

				// Filter by type if specified
				if ( !string.IsNullOrEmpty( typeFilter ) )
				{
					var assetType = asset.AssetType;
					if ( assetType == null ) continue;

					bool matches = assetType.FileExtension.Equals( typeFilter, StringComparison.OrdinalIgnoreCase ) ||
						assetType.FriendlyName.IndexOf( typeFilter, StringComparison.OrdinalIgnoreCase ) >= 0;

					if ( !matches ) continue;
				}

				scanned++;

				// Check if anything depends on this asset
				var dependants = asset.GetDependants( false );
				if ( dependants == null || dependants.Count == 0 )
				{
					unused.Add( new
					{
						path      = asset.Path,
						name      = asset.Name,
						type      = asset.AssetType?.FriendlyName ?? "Unknown",
						extension = asset.AssetType?.FileExtension ?? "?"
					} );

					if ( unused.Count >= maxResults ) break;
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				summary = $"Found {unused.Count} unused asset(s) out of {scanned} scanned.",
				unused
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error scanning: {ex.Message}" ); }
	}

	// ── Omnibus dispatcher ──────────────────────────────────────────────────

	internal static object ManageAssets( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_asset"       => CreateAsset( args ),
			"rename_asset"       => RenameAsset( args ),
			"move_asset"         => MoveAsset( args ),
			"delete_asset"       => DeleteAsset( args ),
			"find_unused_assets" => FindUnusedAssets( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation '{operation}'. Valid: create_asset, rename_asset, move_asset, delete_asset, find_unused_assets." )
		};
	}

	// ── Schema ──────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageAssets
	{
		get
		{
			var props = new Dictionary<string, object>
			{
				["operation"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The asset management operation to perform.",
					["enum"]        = new[] { "create_asset", "rename_asset", "move_asset", "delete_asset", "find_unused_assets" }
				},
				["type"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Asset type extension for create_asset (e.g. 'vmat', 'prefab', 'scene', 'vsnd', 'vmdl') or type filter for find_unused_assets."
				},
				["path"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Asset path. For create_asset: save location (e.g. 'materials/my_material.vmat'). For rename_asset/move_asset/delete_asset: current asset path."
				},
				["newName"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "New file name without extension (rename_asset)."
				},
				["directory"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Target directory to move asset into (move_asset). Can be relative to project assets or absolute."
				},
				["overwrite"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "Overwrite if asset exists at target (move_asset, default true)."
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum results to return (find_unused_assets, default 100)."
				}
			};

			var schema = new Dictionary<string, object>
			{
				["type"]       = "object",
				["properties"] = props,
				["required"]   = new[] { "operation" }
			};

			return new Dictionary<string, object>
			{
				["name"]        = "manage_assets",
				["description"] = "Asset management: create a new game resource file like a material, prefab, or scene (create_asset), rename an asset and update all references (rename_asset), move an asset to a different directory (move_asset), safely delete an asset to recycle bin with dependant warnings (delete_asset), or scan for assets with no references to find cleanup candidates (find_unused_assets).",
				["inputSchema"] = schema
			};
		}
	}
}
