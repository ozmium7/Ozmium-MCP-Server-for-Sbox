using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Omnibus handler for prefab management operations:
/// create_prefab, open_prefab, apply_to_prefab, revert_to_prefab,
/// get_instance_overrides, list_prefab_templates, break_instance,
/// update_instance, clone_prefab, get_prefab_info, set_prefab_menu.
/// </summary>
internal static class PrefabToolHandlers
{
	/// <summary>
	/// Walks up the hierarchy to find the outermost prefab instance root.
	/// </summary>
	private static GameObject FindPrefabRoot( GameObject go )
	{
		var cur = go;
		while ( cur != null )
		{
			if ( cur.IsPrefabInstanceRoot )
			{
				if ( cur.Parent != null && cur.Parent.IsPrefabInstance )
				{
					cur = cur.Parent;
					continue;
				}
				return cur;
			}
			cur = cur.Parent;
		}
		return go;
	}

	// ── create_prefab ───────────────────────────────────────────────────────

	private static object CreatePrefab( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string path = OzmiumSceneHelpers.Get( args, "path", (string)null );

		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' — the save location for the prefab (e.g. 'prefabs/my_object.prefab')." );

		if ( !path.EndsWith( ".prefab", StringComparison.OrdinalIgnoreCase ) )
			path += ".prefab";

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null )
			return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

		try
		{
			EditorUtility.Prefabs.ConvertGameObjectToPrefab( go, path );
			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message      = $"Created prefab from '{go.Name}' at '{path}'.",
				id           = go.Id.ToString(),
				prefabSource = go.IsPrefabInstance ? go.PrefabInstanceSource : path,
				path
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error creating prefab: {ex.Message}" ); }
	}

	// ── open_prefab ─────────────────────────────────────────────────────────

	private static object OpenPrefab( JsonElement args )
	{
		string path = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );
		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' (prefab asset path, e.g. 'prefabs/player.prefab')." );

		try
		{
			var prefabFile = ResourceLibrary.Get<PrefabFile>( path );
			if ( prefabFile == null )
				return OzmiumSceneHelpers.Txt( $"Prefab not found: '{path}'. Use browse_assets with type='prefab' to find valid paths." );

			EditorScene.OpenPrefab( prefabFile );
			return OzmiumSceneHelpers.Txt( $"Opened prefab '{path}' for editing." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error opening prefab: {ex.Message}" ); }
	}

	// ── apply_to_prefab ─────────────────────────────────────────────────────

	private static object ApplyToPrefab( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null )
			return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

		if ( !go.IsPrefabInstance )
			return OzmiumSceneHelpers.Txt( $"'{go.Name}' is not a prefab instance." );

		try
		{
			var root = FindPrefabRoot( go );
			var source = root.PrefabInstanceSource;
			EditorUtility.Prefabs.WriteInstanceToPrefab( root );
			return OzmiumSceneHelpers.Txt( $"Applied changes from '{go.Name}' back to prefab '{source}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error applying to prefab: {ex.Message}" ); }
	}

	// ── revert_to_prefab ────────────────────────────────────────────────────

	private static object RevertToPrefab( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null )
			return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

		if ( !go.IsPrefabInstance )
			return OzmiumSceneHelpers.Txt( $"'{go.Name}' is not a prefab instance." );

		try
		{
			EditorUtility.Prefabs.RevertInstanceToPrefab( FindPrefabRoot( go ) );
			return OzmiumSceneHelpers.Txt( $"Reverted '{go.Name}' to its prefab source." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error reverting: {ex.Message}" ); }
	}

	// ── get_instance_overrides ──────────────────────────────────────────────

	private static object GetInstanceOverrides( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null )
			return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

		if ( !go.IsPrefabInstance )
			return OzmiumSceneHelpers.Txt( $"'{go.Name}' is not a prefab instance." );

		try
		{
			var root = FindPrefabRoot( go );
			var isModified = EditorUtility.Prefabs.IsInstanceModified( root );

			var modifiedObjects = new List<Dictionary<string, object>>();
			foreach ( var obj in OzmiumSceneHelpers.WalkSubtree( root ) )
			{
				var goModified = EditorUtility.Prefabs.IsGameObjectInstanceModified( obj );
				var isAdded    = EditorUtility.Prefabs.IsGameObjectAddedToInstance( obj );

				var modifiedComponents = new List<Dictionary<string, object>>();
				foreach ( var comp in obj.Components.GetAll() )
				{
					var compModified = EditorUtility.Prefabs.IsComponentInstanceModified( comp );
					var compAdded    = EditorUtility.Prefabs.IsComponentAddedToInstance( comp );
					if ( compModified || compAdded )
					{
						modifiedComponents.Add( new Dictionary<string, object>
						{
							["type"]     = comp.GetType().Name,
							["modified"] = compModified,
							["added"]    = compAdded
						} );
					}
				}

				if ( goModified || isAdded || modifiedComponents.Count > 0 )
				{
					modifiedObjects.Add( new Dictionary<string, object>
					{
						["id"]         = obj.Id.ToString(),
						["name"]       = obj.Name,
						["modified"]   = goModified,
						["added"]      = isAdded,
						["components"] = modifiedComponents
					} );
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				prefabSource = root.PrefabInstanceSource,
				isModified,
				modifiedObjects
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── list_prefab_templates ───────────────────────────────────────────────

	private static object ListPrefabTemplates( JsonElement args )
	{
		try
		{
			var templates = EditorUtility.Prefabs.GetTemplates().ToList();
			var results = new List<Dictionary<string, object>>();
			foreach ( var t in templates )
			{
				results.Add( new Dictionary<string, object>
				{
					["path"]     = t.ResourcePath,
					["menuPath"] = t.MenuPath,
					["menuIcon"] = t.MenuIcon
				} );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				summary   = $"Found {results.Count} prefab template(s).",
				templates = results
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── break_instance ──────────────────────────────────────────────────────

	private static object BreakInstance( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null )
			return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

		if ( !go.IsPrefabInstance )
			return OzmiumSceneHelpers.Txt( $"'{go.Name}' is not a prefab instance." );

		try
		{
			var root = FindPrefabRoot( go );
			var source = root.PrefabInstanceSource;
			root.BreakFromPrefab();
			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Broke '{root.Name}' from prefab '{source}'. It is now an independent GameObject.",
				id      = root.Id.ToString()
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── update_instance ─────────────────────────────────────────────────────

	private static object UpdateInstance( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null )
			return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

		if ( !go.IsPrefabInstance )
			return OzmiumSceneHelpers.Txt( $"'{go.Name}' is not a prefab instance." );

		try
		{
			var root = FindPrefabRoot( go );
			root.UpdateFromPrefab();
			return OzmiumSceneHelpers.Txt( $"Updated '{root.Name}' from prefab '{root.PrefabInstanceSource}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── clone_prefab ────────────────────────────────────────────────────────

	private static object ClonePrefab( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string path     = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );
		float  x        = OzmiumSceneHelpers.Get( args, "x", 0f );
		float  y        = OzmiumSceneHelpers.Get( args, "y", 0f );
		float  z        = OzmiumSceneHelpers.Get( args, "z", 0f );
		string cloneName = OzmiumSceneHelpers.Get( args, "cloneName", (string)null );

		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' (prefab asset path)." );

		try
		{
			var prefabFile = ResourceLibrary.Get<PrefabFile>( path );
			if ( prefabFile == null )
				return OzmiumSceneHelpers.Txt( $"Prefab not found: '{path}'." );

			var transform = new Transform( new Vector3( x, y, z ) );

			GameObject parent = null;
			if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var pguid ) )
				parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == pguid );

			var instance = GameObject.Clone( prefabFile, transform, parent, true, cloneName );
			if ( instance == null )
				return OzmiumSceneHelpers.Txt( "Failed to clone prefab." );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message      = $"Cloned prefab '{path}' as '{instance.Name}'.",
				id           = instance.Id.ToString(),
				name         = instance.Name,
				position     = OzmiumSceneHelpers.V3( instance.WorldPosition ),
				prefabSource = instance.PrefabInstanceSource
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error cloning prefab: {ex.Message}" ); }
	}

	// ── get_prefab_info ─────────────────────────────────────────────────────

	private static object GetPrefabInfo( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string path = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );

		// If id/name provided, get info from scene instance
		if ( !string.IsNullOrEmpty( id ) || !string.IsNullOrEmpty( name ) )
		{
			if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

			var go = OzmiumSceneHelpers.FindGo( scene, id, name );
			if ( go == null )
				return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'." );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				name                       = go.Name,
				id                         = go.Id.ToString(),
				isPrefabInstance           = go.IsPrefabInstance,
				isPrefabInstanceRoot       = go.IsPrefabInstanceRoot,
				isOutermostPrefabRoot      = go.IsPrefabInstanceRoot && FindPrefabRoot( go ) == go,
				isNestedPrefabInstanceRoot = go.IsPrefabInstanceRoot && FindPrefabRoot( go ) != go,
				prefabSource               = go.PrefabInstanceSource,
				isModified                 = go.IsPrefabInstance ? EditorUtility.Prefabs.IsInstanceModified( FindPrefabRoot( go ) ) : false,
				position                   = OzmiumSceneHelpers.V3( go.WorldPosition ),
				path                       = OzmiumSceneHelpers.GetObjectPath( go )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}

		// If path provided, get info from the prefab file itself
		if ( !string.IsNullOrEmpty( path ) )
		{
			try
			{
				var prefabFile = ResourceLibrary.Get<PrefabFile>( path );
				if ( prefabFile == null )
					return OzmiumSceneHelpers.Txt( $"Prefab not found: '{path}'." );

				var rootJson = prefabFile.RootObject;
				string rootName = rootJson?.TryGetPropertyValue( "Name", out var n ) == true ? n?.ToString() : "Unknown";

				var childCount = 0;
				var componentCount = 0;
				if ( rootJson?.TryGetPropertyValue( "Children", out var children ) == true && children is System.Text.Json.Nodes.JsonArray childArr )
					childCount = childArr.Count;
				if ( rootJson?.TryGetPropertyValue( "Components", out var comps ) == true && comps is System.Text.Json.Nodes.JsonArray compArr )
					componentCount = compArr.Count;

				return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
				{
					path           = prefabFile.ResourcePath,
					rootName,
					showInMenu     = prefabFile.ShowInMenu,
					menuPath       = prefabFile.MenuPath,
					menuIcon       = prefabFile.MenuIcon,
					childCount,
					componentCount
				}, OzmiumSceneHelpers.JsonSettings ) );
			}
			catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
		}

		return OzmiumSceneHelpers.Txt( "Provide 'id'/'name' (scene instance) or 'path' (prefab file) to inspect." );
	}

	// ── set_prefab_menu ─────────────────────────────────────────────────────

	private static object SetPrefabMenu( JsonElement args )
	{
		string path     = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );
		bool   showMenu = OzmiumSceneHelpers.Get( args, "showInMenu", true );
		string menuPath = OzmiumSceneHelpers.Get( args, "menuPath", (string)null );
		string menuIcon = OzmiumSceneHelpers.Get( args, "menuIcon", (string)null );

		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' (prefab asset path)." );

		try
		{
			var prefabFile = ResourceLibrary.Get<PrefabFile>( path );
			if ( prefabFile == null )
				return OzmiumSceneHelpers.Txt( $"Prefab not found: '{path}'." );

			prefabFile.ShowInMenu = showMenu;
			if ( menuPath != null ) prefabFile.MenuPath = menuPath;
			if ( menuIcon != null ) prefabFile.MenuIcon = menuIcon;

			// Save changes to disk
			var asset = AssetSystem.FindByPath( path );
			if ( asset != null )
				asset.SaveToDisk( prefabFile );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message    = $"Updated prefab menu settings for '{path}'.",
				showInMenu = prefabFile.ShowInMenu,
				menuPath   = prefabFile.MenuPath,
				menuIcon   = prefabFile.MenuIcon
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── find_prefab_instances ───────────────────────────────────────────────

	private static object FindPrefabInstances( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string prefabPath = OzmiumSceneHelpers.Get( args, "prefabPath", (string)null );
		int    maxResults = OzmiumSceneHelpers.Get( args, "maxResults", 100 );
		bool   enabledOnly = OzmiumSceneHelpers.Get( args, "enabledOnly", false );

		try
		{
			var allObjects = OzmiumSceneHelpers.WalkAll( scene, !enabledOnly );

			// If no path specified, get a breakdown of all prefabs in scene
			if ( string.IsNullOrEmpty( prefabPath ) )
			{
				var prefabCounts = new Dictionary<string, int>();
				foreach ( var go in allObjects )
				{
					if ( !go.IsPrefabInstanceRoot || FindPrefabRoot( go ) != go ) continue;
					var source = go.PrefabInstanceSource ?? "(unknown)";
					prefabCounts.TryGetValue( source, out var count );
					prefabCounts[source] = count + 1;
				}

				var breakdown = prefabCounts
					.OrderByDescending( kvp => kvp.Value )
					.Select( kvp => new { prefab = kvp.Key, count = kvp.Value } )
					.ToList();

				return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
				{
					summary   = $"Found {breakdown.Count} unique prefab(s) in scene.",
					totalInstances = breakdown.Sum( b => b.count ),
					breakdown
				}, OzmiumSceneHelpers.JsonSettings ) );
			}

			// Find instances of a specific prefab
			var instances = new List<object>();
			foreach ( var go in allObjects )
			{
				if ( !go.IsPrefabInstanceRoot || FindPrefabRoot( go ) != go ) continue;
				var source = go.PrefabInstanceSource;
				if ( source == null || source.IndexOf( prefabPath, StringComparison.OrdinalIgnoreCase ) < 0 )
					continue;

				instances.Add( new
				{
					id         = go.Id.ToString(),
					name       = go.Name,
					position   = OzmiumSceneHelpers.V3( go.WorldPosition ),
					isModified = EditorUtility.Prefabs.IsInstanceModified( go ),
					path       = OzmiumSceneHelpers.GetObjectPath( go )
				} );

				if ( instances.Count >= maxResults ) break;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				summary    = $"Found {instances.Count} instance(s) of '{prefabPath}'.",
				prefabPath,
				instances
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── update_all_instances ────────────────────────────────────────────────

	private static object UpdateAllInstances( JsonElement args )
	{
		string path = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );

		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'path' (prefab asset path) to update all instances of." );

		try
		{
			var prefabFile = ResourceLibrary.Get<PrefabFile>( path );
			if ( prefabFile == null )
				return OzmiumSceneHelpers.Txt( $"Prefab not found: '{path}'." );

			EditorScene.UpdatePrefabInstances( prefabFile );
			return OzmiumSceneHelpers.Txt( $"Updated all instances of '{path}' across open scenes." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Omnibus dispatcher ──────────────────────────────────────────────────

	internal static object ManagePrefabs( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_prefab"         => CreatePrefab( args ),
			"open_prefab"           => OpenPrefab( args ),
			"apply_to_prefab"       => ApplyToPrefab( args ),
			"revert_to_prefab"      => RevertToPrefab( args ),
			"get_instance_overrides"=> GetInstanceOverrides( args ),
			"list_prefab_templates" => ListPrefabTemplates( args ),
			"break_instance"        => BreakInstance( args ),
			"update_instance"       => UpdateInstance( args ),
			"clone_prefab"          => ClonePrefab( args ),
			"get_prefab_info"       => GetPrefabInfo( args ),
			"set_prefab_menu"       => SetPrefabMenu( args ),
			"find_prefab_instances" => FindPrefabInstances( args ),
			"update_all_instances"  => UpdateAllInstances( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation '{operation}'. Valid: create_prefab, open_prefab, apply_to_prefab, revert_to_prefab, get_instance_overrides, list_prefab_templates, break_instance, update_instance, clone_prefab, get_prefab_info, set_prefab_menu, find_prefab_instances, update_all_instances." )
		};
	}

	// ── Schema ──────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManagePrefabs
	{
		get
		{
			var props = new Dictionary<string, object>
			{
				["operation"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The prefab operation to perform.",
					["enum"]        = new[]
					{
						"create_prefab", "open_prefab", "apply_to_prefab", "revert_to_prefab",
						"get_instance_overrides", "list_prefab_templates", "break_instance",
						"update_instance", "clone_prefab", "get_prefab_info", "set_prefab_menu",
						"find_prefab_instances", "update_all_instances"
					}
				},
				["id"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "GUID of the target GameObject (for instance operations: create_prefab, apply_to_prefab, revert_to_prefab, get_instance_overrides, break_instance, update_instance, get_prefab_info)."
				},
				["name"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Name of the target GameObject (alternative to id)."
				},
				["path"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Prefab asset path. For create_prefab: save location (e.g. 'prefabs/my_object.prefab'). For open_prefab/clone_prefab/get_prefab_info/set_prefab_menu/update_all_instances: existing prefab path."
				},
				["x"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "World X position (for clone_prefab, default 0)."
				},
				["y"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "World Y position (for clone_prefab, default 0)."
				},
				["z"] = new Dictionary<string, object>
				{
					["type"]        = "number",
					["description"] = "World Z position (for clone_prefab, default 0)."
				},
				["parentId"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Parent GUID for clone_prefab."
				},
				["cloneName"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Custom name for the cloned instance (clone_prefab)."
				},
				["showInMenu"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "Whether the prefab appears in Create GameObject menu (set_prefab_menu, default true)."
				},
				["menuPath"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Menu path in Create GameObject menu (set_prefab_menu, e.g. 'Props/Furniture')."
				},
				["menuIcon"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Icon name for menu entry (set_prefab_menu)."
				},
				["prefabPath"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Prefab path substring to filter (find_prefab_instances). Omit to get a breakdown of all prefabs."
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum results to return (find_prefab_instances, default 100)."
				},
				["enabledOnly"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "Only include enabled instances (find_prefab_instances, default false)."
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
				["name"]        = "manage_prefabs",
				["description"] = "Prefab management: create a prefab from a scene GameObject (create_prefab), open a prefab for editing (open_prefab), apply instance changes back to source prefab (apply_to_prefab), revert instance to match source (revert_to_prefab), inspect overrides on an instance (get_instance_overrides), list prefab templates (list_prefab_templates), break prefab link (break_instance), update instance from source (update_instance), clone/spawn a prefab into the scene (clone_prefab), inspect prefab info from scene instance or file (get_prefab_info), configure prefab menu settings (set_prefab_menu), find all instances of a prefab in the scene (find_prefab_instances), refresh all instances of a prefab across open scenes (update_all_instances).",
				["inputSchema"] = schema
			};
		}
	}
}
