using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Gameplay zone/area management MCP tools: create zone markers with tags,
/// create and configure trigger volumes, tag objects in volumes, list/query zones.
/// </summary>
internal static class ZoneToolHandlers
{

	private static readonly string[] ValidZoneTypes = { "buy_zone", "safe_zone", "jail_area", "no_pvp", "spawn_zone", "no_build" };

	// ── create_zone_marker ─────────────────────────────────────────────────

	private static object CreateZoneMarker( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name = OzmiumSceneHelpers.Get( args, "name", "ZoneMarker" );
		string zoneType = OzmiumSceneHelpers.Get( args, "zoneType", (string)null );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );

		if ( string.IsNullOrEmpty( zoneType ) )
			return OzmiumSceneHelpers.Txt( "Provide 'zoneType'. Valid types: " + string.Join( ", ", ValidZoneTypes ) );

		if ( !ValidZoneTypes.Contains( zoneType.ToLowerInvariant() ) )
			return OzmiumSceneHelpers.Txt( $"Invalid zoneType '{zoneType}'. Valid types: " + string.Join( ", ", ValidZoneTypes ) );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			// Add zone tag
			go.Tags.Add( $"zone:{zoneType.ToLowerInvariant()}" );

			// Add any extra tags
			if ( args.TryGetProperty( "tags", out var tagsEl ) && tagsEl.ValueKind == JsonValueKind.Array )
				foreach ( var t in tagsEl.EnumerateArray() ) go.Tags.Add( t.GetString() );

			// Optional parent
			if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var guid ) )
			{
				var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( parent != null ) go.SetParent( parent );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created zone marker '{go.Name}' with type '{zoneType}'.",
				id = go.Id.ToString(),
				name = go.Name,
				tags = OzmiumSceneHelpers.GetTags( go ),
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_trigger_volume ──────────────────────────────────────────────

	private static object CreateTriggerVolume( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name = OzmiumSceneHelpers.Get( args, "name", "TriggerVolume" );
		float sizeX = OzmiumSceneHelpers.Get( args, "sizeX", 100f );
		float sizeY = OzmiumSceneHelpers.Get( args, "sizeY", 100f );
		float sizeZ = OzmiumSceneHelpers.Get( args, "sizeZ", 100f );
		bool isTrigger = OzmiumSceneHelpers.Get( args, "isTrigger", true );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var td = OzmiumWriteHandlers.FindComponentTypeDescription( "BoxCollider" );
			if ( td == null ) return OzmiumSceneHelpers.Txt( "BoxCollider component type not found." );

			var comp = go.Components.Create( td );
			if ( comp is BoxCollider collider )
			{
				collider.Scale = new Vector3( sizeX, sizeY, sizeZ );
				collider.IsTrigger = isTrigger;
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created trigger volume '{go.Name}'.",
				id = go.Id.ToString(),
				name = go.Name,
				size = new { x = sizeX, y = sizeY, z = sizeZ },
				isTrigger,
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_trigger_volume ───────────────────────────────────────────

	private static object ConfigureTriggerVolume( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var collider = go.Components.Get<BoxCollider>();
		if ( collider == null ) return OzmiumSceneHelpers.Txt( $"No BoxCollider on '{go.Name}'." );

		try
		{
			float sizeX = OzmiumSceneHelpers.Get( args, "sizeX", collider.Scale.x );
			float sizeY = OzmiumSceneHelpers.Get( args, "sizeY", collider.Scale.y );
			float sizeZ = OzmiumSceneHelpers.Get( args, "sizeZ", collider.Scale.z );
			bool isTrigger = OzmiumSceneHelpers.Get( args, "isTrigger", collider.IsTrigger );

			collider.Scale = new Vector3( sizeX, sizeY, sizeZ );
			collider.IsTrigger = isTrigger;

			if ( args.TryGetProperty( "enabled", out var enEl ) )
				go.Enabled = enEl.GetBoolean();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Configured trigger volume '{go.Name}'.",
				id = go.Id.ToString(),
				name = go.Name,
				size = new { x = sizeX, y = sizeY, z = sizeZ },
				isTrigger,
				enabled = go.Enabled
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── tag_objects_in_volume ──────────────────────────────────────────────

	private static object TagObjectsInVolume( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string tagName = OzmiumSceneHelpers.Get( args, "tagName", (string)null );

		if ( string.IsNullOrEmpty( tagName ) ) return OzmiumSceneHelpers.Txt( "Provide 'tagName'." );

		var target = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( target == null ) return OzmiumSceneHelpers.Txt( "Target object not found." );

		try
		{
			var center = target.WorldPosition;
			bool useSphere = OzmiumSceneHelpers.Get( args, "useSphere", false );
			float sphereRadius = OzmiumSceneHelpers.Get( args, "sphereRadius", 100f );

			IEnumerable<GameObject> found;
			if ( useSphere )
			{
				var sphere = new Sphere( center, sphereRadius );
				found = scene.FindInPhysics( sphere );
			}
			else
			{
				float hsx = OzmiumSceneHelpers.Get( args, "sizeX", 100f ) / 2f;
				float hsy = OzmiumSceneHelpers.Get( args, "sizeY", 100f ) / 2f;
				float hsz = OzmiumSceneHelpers.Get( args, "sizeZ", 100f ) / 2f;
				var bbox = new BBox( center - new Vector3( hsx, hsy, hsz ), center + new Vector3( hsx, hsy, hsz ) );
				found = scene.FindInPhysics( bbox );
			}

			int tagged = 0;
			var taggedList = new List<Dictionary<string, object>>();
			foreach ( var go in found )
			{
				if ( go.Id == target.Id ) continue;
				go.Tags.Add( tagName );
				tagged++;
				if ( taggedList.Count < 50 )
					taggedList.Add( new Dictionary<string, object>
					{
						["id"] = go.Id.ToString(),
						["name"] = go.Name,
						["position"] = OzmiumSceneHelpers.V3( go.WorldPosition )
					} );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Tagged {tagged} object(s) with '{tagName}'.",
				center = OzmiumSceneHelpers.V3( center ),
				useSphere,
				sphereRadius = useSphere ? sphereRadius : (object)null,
				tagged,
				taggedObjects = taggedList
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── list_zones ─────────────────────────────────────────────────────────

	private static object ListZones( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string zoneType = OzmiumSceneHelpers.Get( args, "zoneType", (string)null );

		var tagFilter = string.IsNullOrEmpty( zoneType ) ? "zone:" : $"zone:{zoneType.ToLowerInvariant()}";

		var zones = OzmiumSceneHelpers.WalkAll( scene, true )
			.Where( g => g.Tags.TryGetAll().Any( t => t.StartsWith( tagFilter, StringComparison.OrdinalIgnoreCase ) ) )
			.Select( g =>
			{
				var zoneTags = g.Tags.TryGetAll().Where( t => t.StartsWith( "zone:", StringComparison.OrdinalIgnoreCase ) ).ToList();
				return new Dictionary<string, object>
				{
					["id"] = g.Id.ToString(),
					["name"] = g.Name,
					["zoneTypes"] = zoneTags,
					["position"] = OzmiumSceneHelpers.V3( g.WorldPosition ),
					["enabled"] = g.Enabled
				};
			} )
			.ToList();

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			summary = $"Found {zones.Count} zone(s).",
			filter = string.IsNullOrEmpty( zoneType ) ? "all zones" : zoneType,
			zones
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── remove_zone_tag ────────────────────────────────────────────────────

	private static object RemoveZoneTag( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string zoneType = OzmiumSceneHelpers.Get( args, "zoneType", (string)null );

		if ( string.IsNullOrEmpty( zoneType ) )
			return OzmiumSceneHelpers.Txt( "Provide 'zoneType'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var tagToRemove = $"zone:{zoneType.ToLowerInvariant()}";
		bool hadTag = go.Tags.Has( tagToRemove );
		go.Tags.Remove( tagToRemove );

		return OzmiumSceneHelpers.Txt( hadTag
			? $"Removed zone tag '{tagToRemove}' from '{go.Name}'. Remaining tags: {string.Join( ", ", go.Tags.TryGetAll() )}"
			: $"Zone tag '{tagToRemove}' not found on '{go.Name}'. Current tags: {string.Join( ", ", go.Tags.TryGetAll() )}" );
	}

	// ── get_objects_in_zone ────────────────────────────────────────────────

	private static object GetObjectsInZone( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string zoneType = OzmiumSceneHelpers.Get( args, "zoneType", (string)null );
		int maxResults = OzmiumSceneHelpers.Get( args, "maxResults", 50 );

		if ( string.IsNullOrEmpty( zoneType ) )
			return OzmiumSceneHelpers.Txt( "Provide 'zoneType'." );

		// Find zone markers with this type
		var tagFilter = $"zone:{zoneType.ToLowerInvariant()}";
		var zoneMarkers = OzmiumSceneHelpers.WalkAll( scene, true )
			.Where( g => g.Tags.Has( tagFilter ) )
			.ToList();

		if ( zoneMarkers.Count == 0 )
			return OzmiumSceneHelpers.Txt( $"No zone markers with tag '{tagFilter}' found." );

		var allFound = new List<Dictionary<string, object>>();

		foreach ( var marker in zoneMarkers )
		{
			// Use default 100-unit radius around zone marker
			var center = marker.WorldPosition;
			var bbox = new BBox( center - new Vector3( 50, 50, 50 ), center + new Vector3( 50, 50, 50 ) );

			try
			{
				foreach ( var go in scene.FindInPhysics( bbox ) )
				{
					if ( go.Id == marker.Id ) continue;
					allFound.Add( new Dictionary<string, object>
					{
						["id"] = go.Id.ToString(),
						["name"] = go.Name,
						["position"] = OzmiumSceneHelpers.V3( go.WorldPosition ),
						["zone"] = marker.Name
					} );
				}
			}
			catch { }
		}

		var results = allFound.Take( maxResults ).ToList();

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			summary = $"Found {results.Count} object(s) in {zoneMarkers.Count} zone(s) of type '{zoneType}'.",
			zoneType,
			zoneMarkers = zoneMarkers.Select( m => new { id = m.Id.ToString(), name = m.Name, position = OzmiumSceneHelpers.V3( m.WorldPosition ) } ).ToList(),
			totalFound = allFound.Count,
			objects = results
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── manage_zones (Omnibus) ─────────────────────────────────────────────

	internal static object ManageZones( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_zone_marker"       => CreateZoneMarker( args ),
			"create_trigger_volume"    => CreateTriggerVolume( args ),
			"configure_trigger_volume" => ConfigureTriggerVolume( args ),
			"tag_objects_in_volume"    => TagObjectsInVolume( args ),
			"list_zones"               => ListZones( args ),
			"remove_zone_tag"          => RemoveZoneTag( args ),
			"get_objects_in_zone"      => GetObjectsInZone( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create_zone_marker, create_trigger_volume, configure_trigger_volume, tag_objects_in_volume, list_zones, remove_zone_tag, get_objects_in_zone" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageZones
	{
		get
		{
			var stringArrayItem = new Dictionary<string, object> { ["type"] = "string" };
			var props = new Dictionary<string, object>();

			props["operation"] = new Dictionary<string, object>
			{
				["type"] = "string",
				["description"] = "Operation to perform.",
				["enum"] = new[] { "create_zone_marker", "create_trigger_volume", "configure_trigger_volume", "tag_objects_in_volume", "list_zones", "remove_zone_tag", "get_objects_in_zone" }
			};

			// create_zone_marker params
			props["x"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." };
			props["y"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." };
			props["z"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." };
			props["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO (default 'ZoneMarker')." };
			props["zoneType"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Zone type tag: buy_zone, safe_zone, jail_area, no_pvp, spawn_zone, no_build." };
			props["tags"] = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Extra tags to add to zone marker.", ["items"] = stringArrayItem };
			props["parentId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Parent GUID for create_zone_marker." };

			// trigger volume params
			props["sizeX"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box size X (default 100)." };
			props["sizeY"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box size Y (default 100)." };
			props["sizeZ"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box size Z (default 100)." };
			props["isTrigger"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether collider is a trigger volume (default true)." };
			props["enabled"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable state for configure_trigger_volume." };

			// tag_objects_in_volume params
			props["tagName"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tag to apply to objects found in volume." };
			props["useSphere"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Use sphere instead of box for tag_objects_in_volume (default false)." };
			props["sphereRadius"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Sphere radius for tag_objects_in_volume (default 100)." };

			// query params
			props["maxResults"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max results for get_objects_in_zone (default 50)." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object>
			{
				["name"] = "manage_zones",
				["description"] = "Create and manage gameplay zones/areas using tag-based zone markers and trigger volumes. Zones are identified by tags like zone:buy_zone, zone:safe_zone, etc. Create zone markers, trigger volumes, tag objects within volumes, list/query zones.",
				["inputSchema"] = schema
			};
		}
	}
}
