using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Scene serialization, cloning, comparison, and network config MCP tools.
/// Enables scene templates, clone-with-overrides, and data-driven workflows.
/// </summary>
internal static class SceneDataToolHandlers
{

	private static readonly JsonSerializerOptions _pretty = new()
	{
		WriteIndented = true,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	// ── serialize_objects ──────────────────────────────────────────────────

	private static object SerializeObjects( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		bool includeChildren = OzmiumSceneHelpers.Get( args, "includeChildren", true );

		// If no ids provided, use selection
		List<GameObject> objects;
		if ( args.TryGetProperty( "ids", out var idsEl ) && idsEl.ValueKind == JsonValueKind.Array && idsEl.GetArrayLength() > 0 )
		{
			objects = new List<GameObject>();
			foreach ( var el in idsEl.EnumerateArray() )
			{
				var idStr = el.GetString();
				if ( !string.IsNullOrEmpty( idStr ) && Guid.TryParse( idStr, out var guid ) )
				{
					var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
					if ( go != null ) objects.Add( go );
				}
			}
		}
		else
		{
			objects = OzmiumSceneHelpers.GetSelectedGameObjects();
		}

		if ( objects.Count == 0 )
			return OzmiumSceneHelpers.Txt( "No objects to serialize. Provide 'ids' or select objects in the editor." );

		try
		{
			var serialized = new List<Dictionary<string, object>>();
			foreach ( var go in objects )
			{
				var json = go.Serialize();
				serialized.Add( new Dictionary<string, object>
				{
					["id"] = go.Id.ToString(),
					["name"] = go.Name,
					["serialized"] = JsonSerializer.Serialize( json, _pretty )
				} );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Serialized {serialized.Count} object(s).",
				count = serialized.Count,
				includeChildren,
				objects = serialized
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── deserialize_objects ────────────────────────────────────────────────

	private static object DeserializeObjects( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string serializedJson = OzmiumSceneHelpers.Get( args, "serializedJson", (string)null );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );

		if ( string.IsNullOrEmpty( serializedJson ) )
			return OzmiumSceneHelpers.Txt( "Provide 'serializedJson' (JSON string from serialize_objects)." );

		try
		{
			var parsed = JsonSerializer.Deserialize<JsonNode>( serializedJson );
			if ( parsed == null )
				return OzmiumSceneHelpers.Txt( "Failed to parse serializedJson." );

			// Support both array of serialized objects and single object
			IEnumerable<JsonNode> nodes = parsed is JsonArray arr
				? (IEnumerable<JsonNode>)arr
				: new[] { parsed };

			var created = new List<Dictionary<string, object>>();

			foreach ( var node in nodes )
			{
				var jsonObj = node as JsonObject;
				if ( jsonObj == null ) continue;

				var go = scene.CreateObject( false );
				go.Deserialize( jsonObj );

				// Apply optional position override
				if ( args.TryGetProperty( "position", out var posEl ) && posEl.ValueKind == JsonValueKind.Object )
				{
					float x = 0, y = 0, z = 0;
					if ( posEl.TryGetProperty( "x", out var xp ) ) x = xp.GetSingle();
					if ( posEl.TryGetProperty( "y", out var yp ) ) y = yp.GetSingle();
					if ( posEl.TryGetProperty( "z", out var zp ) ) z = zp.GetSingle();
					go.WorldPosition = new Vector3( x, y, z );
				}

				// Optional parent
				if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var parentGuid ) )
				{
					var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == parentGuid );
					if ( parent != null ) go.SetParent( parent );
				}

				go.Enabled = true;

				created.Add( new Dictionary<string, object>
				{
					["id"] = go.Id.ToString(),
					["name"] = go.Name,
					["position"] = OzmiumSceneHelpers.V3( go.WorldPosition )
				} );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Deserialized {created.Count} object(s) into scene.",
				count = created.Count,
				created
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── clone_with_properties ──────────────────────────────────────────────

	private static object CloneWithProperties( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string sourceId = OzmiumSceneHelpers.Get( args, "sourceId", (string)null );
		string sourceName = OzmiumSceneHelpers.Get( args, "sourceName", (string)null );
		string newName = OzmiumSceneHelpers.Get( args, "newName", (string)null );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );

		var sourceGo = OzmiumSceneHelpers.FindGo( scene, sourceId, sourceName );
		if ( sourceGo == null ) return OzmiumSceneHelpers.Txt( "Source object not found." );

		try
		{
			// Serialize → Create → Deserialize (same pattern as DuplicateGameObject)
			var serialized = sourceGo.Serialize();
			var clone = scene.CreateObject( false );
			clone.Deserialize( serialized );

			if ( !string.IsNullOrEmpty( newName ) )
				clone.Name = newName;
			else
			{
				clone.Name = sourceGo.Name;
				clone.MakeNameUnique();
			}

			// Apply optional position override
			if ( args.TryGetProperty( "position", out var posEl ) && posEl.ValueKind == JsonValueKind.Object )
			{
				float x = 0, y = 0, z = 0;
				if ( posEl.TryGetProperty( "x", out var xp ) ) x = xp.GetSingle();
				if ( posEl.TryGetProperty( "y", out var yp ) ) y = yp.GetSingle();
				if ( posEl.TryGetProperty( "z", out var zp ) ) z = zp.GetSingle();
				clone.WorldPosition = new Vector3( x, y, z );
			}

			// Optional parent
			if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var parentGuid ) )
			{
				var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == parentGuid );
				if ( parent != null ) clone.SetParent( parent );
			}

			clone.Enabled = true;

			// Apply property overrides
			var modified = new List<string>();
			var errors = new List<string>();

			if ( args.TryGetProperty( "overrides", out var overridesEl ) && overridesEl.ValueKind == JsonValueKind.Object )
			{
				foreach ( var prop in overridesEl.EnumerateObject() )
				{
					// Format: "componentType.propertyName" or just "propertyName" on first component
					var key = prop.Name;
					var valEl = prop.Value;

					string compType = null;
					string propName = key;

					if ( key.Contains( '.' ) )
					{
						var parts = key.Split( '.', 2 );
						compType = parts[0];
						propName = parts[1];
					}

					// Find component
					var comp = clone.Components.GetAll().FirstOrDefault( c =>
						string.IsNullOrEmpty( compType ) ||
						c.GetType().Name.IndexOf( compType, StringComparison.OrdinalIgnoreCase ) >= 0 );

					if ( comp == null )
					{
						errors.Add( $"No component '{compType}' for override '{key}'." );
						continue;
					}

					var propInfo = comp.GetType().GetProperty( propName,
						System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
					if ( propInfo == null || !propInfo.CanWrite )
					{
						errors.Add( $"Property '{propName}' not found/writable on '{comp.GetType().Name}'." );
						continue;
					}

					try
					{
						object converted = OzmiumWriteHandlers.ConvertJsonValue( valEl, propInfo.PropertyType );
						propInfo.SetValue( comp, converted );
						modified.Add( key );
					}
					catch ( Exception ex ) { errors.Add( $"Error setting '{key}': {ex.Message}" ); }
				}
			}

			var result = new Dictionary<string, object>
			{
				["message"] = $"Cloned '{sourceGo.Name}' as '{clone.Name}' with {modified.Count} override(s).",
				["sourceId"] = sourceGo.Id.ToString(),
				["id"] = clone.Id.ToString(),
				["name"] = clone.Name,
				["position"] = OzmiumSceneHelpers.V3( clone.WorldPosition ),
				["overridesApplied"] = modified
			};
			if ( errors.Count > 0 ) result["errors"] = errors;

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( result, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── compare_objects ────────────────────────────────────────────────────

	private static object CompareObjects( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string idA = OzmiumSceneHelpers.Get( args, "idA", (string)null );
		string nameA = OzmiumSceneHelpers.Get( args, "nameA", (string)null );
		string idB = OzmiumSceneHelpers.Get( args, "idB", (string)null );
		string nameB = OzmiumSceneHelpers.Get( args, "nameB", (string)null );
		bool deep = OzmiumSceneHelpers.Get( args, "deep", false );

		var goA = OzmiumSceneHelpers.FindGo( scene, idA, nameA );
		var goB = OzmiumSceneHelpers.FindGo( scene, idB, nameB );

		if ( goA == null ) return OzmiumSceneHelpers.Txt( $"Object A not found." );
		if ( goB == null ) return OzmiumSceneHelpers.Txt( $"Object B not found." );

		var diffs = new List<Dictionary<string, object>>();

		// Compare name
		if ( !string.Equals( goA.Name, goB.Name, StringComparison.OrdinalIgnoreCase ) )
			diffs.Add( new Dictionary<string, object> { ["field"] = "name", ["a"] = goA.Name, ["b"] = goB.Name } );

		// Compare enabled
		if ( goA.Enabled != goB.Enabled )
			diffs.Add( new Dictionary<string, object> { ["field"] = "enabled", ["a"] = goA.Enabled, ["b"] = goB.Enabled } );

		// Compare tags
		var tagsA = OzmiumSceneHelpers.GetTags( goA ).OrderBy( t => t ).ToList();
		var tagsB = OzmiumSceneHelpers.GetTags( goB ).OrderBy( t => t ).ToList();
		if ( !tagsA.SequenceEqual( tagsB ) )
			diffs.Add( new Dictionary<string, object> { ["field"] = "tags", ["a"] = tagsA, ["b"] = tagsB } );

		// Compare children count
		if ( goA.Children.Count != goB.Children.Count )
			diffs.Add( new Dictionary<string, object> { ["field"] = "childCount", ["a"] = goA.Children.Count, ["b"] = goB.Children.Count } );

		// Compare component types
		var compsA = OzmiumSceneHelpers.GetComponentNames( goA ).OrderBy( t => t ).ToList();
		var compsB = OzmiumSceneHelpers.GetComponentNames( goB ).OrderBy( t => t ).ToList();
		if ( !compsA.SequenceEqual( compsB ) )
			diffs.Add( new Dictionary<string, object> { ["field"] = "components", ["a"] = compsA, ["b"] = compsB } );

		// Compare transform
		var posA = goA.WorldPosition;
		var posB = goB.WorldPosition;
		if ( posA.Distance( posB ) > 0.01f )
			diffs.Add( new Dictionary<string, object> { ["field"] = "position", ["a"] = OzmiumSceneHelpers.V3( posA ), ["b"] = OzmiumSceneHelpers.V3( posB ) } );

		var rotA = goA.WorldRotation;
		var rotB = goB.WorldRotation;
		if ( MathF.Abs( rotA.Angles().pitch - rotB.Angles().pitch ) > 0.01f
			|| MathF.Abs( rotA.Angles().yaw - rotB.Angles().yaw ) > 0.01f
			|| MathF.Abs( rotA.Angles().roll - rotB.Angles().roll ) > 0.01f )
			diffs.Add( new Dictionary<string, object> { ["field"] = "rotation", ["a"] = OzmiumSceneHelpers.Rot( rotA ), ["b"] = OzmiumSceneHelpers.Rot( rotB ) } );

		var sclA = goA.WorldScale;
		var sclB = goB.WorldScale;
		if ( ( sclA - sclB ).Length > 0.01f )
			diffs.Add( new Dictionary<string, object> { ["field"] = "scale", ["a"] = OzmiumSceneHelpers.V3( sclA ), ["b"] = OzmiumSceneHelpers.V3( sclB ) } );

		// Compare network mode
		if ( goA.NetworkMode != goB.NetworkMode )
			diffs.Add( new Dictionary<string, object> { ["field"] = "networkMode", ["a"] = goA.NetworkMode.ToString(), ["b"] = goB.NetworkMode.ToString() } );

		// Deep comparison: serialized JSON diff
		if ( deep )
		{
			try
			{
				var jsonA = JsonSerializer.Serialize( goA.Serialize(), OzmiumSceneHelpers.JsonSettings );
				var jsonB = JsonSerializer.Serialize( goB.Serialize(), OzmiumSceneHelpers.JsonSettings );
				if ( jsonA != jsonB )
					diffs.Add( new Dictionary<string, object>
					{
						["field"] = "serialized",
						["note"] = "Serialized JSON differs (full comparison)",
						["aLength"] = jsonA.Length,
						["bLength"] = jsonB.Length
					} );
			}
			catch { }
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			summary = diffs.Count == 0
				? $"Objects '{goA.Name}' and '{goB.Name}' are identical."
				: $"Found {diffs.Count} difference(s) between '{goA.Name}' and '{goB.Name}'.",
			objectA = new { id = goA.Id.ToString(), name = goA.Name },
			objectB = new { id = goB.Id.ToString(), name = goB.Name },
			differences = diffs
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── batch_set_network_mode ─────────────────────────────────────────────

	private static object BatchSetNetworkMode( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		string modeStr = OzmiumSceneHelpers.Get( args, "networkMode", (string)null );
		if ( string.IsNullOrEmpty( modeStr ) )
			return OzmiumSceneHelpers.Txt( "Provide 'networkMode': Never, Object, or Snapshot." );

		// Parse network mode
		if ( !Enum.TryParse<NetworkMode>( modeStr, ignoreCase: true, out var mode ) )
			return OzmiumSceneHelpers.Txt( $"Invalid networkMode '{modeStr}'. Valid: Never, Object, Snapshot." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		int found = 0, modified = 0;
		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;
			found++;
			go.NetworkMode = mode;
			modified++;
		}

		return OzmiumSceneHelpers.Txt( $"Set NetworkMode={mode} on {modified} object(s) (found {found} of {ids.Count})." );
	}

	// ── get_serialized ─────────────────────────────────────────────────────

	private static object GetSerialized( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		// Support both id/name and idA/nameA (AI sometimes sends compare_objects params)
		string id = OzmiumSceneHelpers.Get( args, "id", (string)null )
		           ?? OzmiumSceneHelpers.Get( args, "idA", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null )
		            ?? OzmiumSceneHelpers.Get( args, "nameA", (string)null );
		bool includeChildren = OzmiumSceneHelpers.Get( args, "includeChildren", true );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'. Use find_game_objects to locate the correct name." );

		try
		{
			var json = go.Serialize();
			var serialized = JsonSerializer.Serialize( json, _pretty );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				id = go.Id.ToString(),
				name = go.Name,
				serialized
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── manage_scene_data (Omnibus) ────────────────────────────────────────

	internal static object ManageSceneData( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"serialize_objects"       => SerializeObjects( args ),
			"deserialize_objects"     => DeserializeObjects( args ),
			"clone_with_properties"   => CloneWithProperties( args ),
			"compare_objects"         => CompareObjects( args ),
			"batch_set_network_mode"  => BatchSetNetworkMode( args ),
			"get_serialized"          => GetSerialized( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: serialize_objects, deserialize_objects, clone_with_properties, compare_objects, batch_set_network_mode, get_serialized" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageSceneData
	{
		get
		{
			var stringArrayItem = new Dictionary<string, object> { ["type"] = "string" };
			var props = new Dictionary<string, object>();

			props["operation"] = new Dictionary<string, object>
			{
				["type"] = "string",
				["description"] = "Operation to perform.",
				["enum"] = new[] { "serialize_objects", "deserialize_objects", "clone_with_properties", "compare_objects", "batch_set_network_mode", "get_serialized" }
			};

			// serialize_objects params
			props["ids"] = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of GUIDs to serialize (omit to use editor selection).", ["items"] = stringArrayItem };
			props["includeChildren"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Include children in serialization (default true)." };

			// deserialize_objects params
			props["serializedJson"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "JSON string from serialize_objects (single object or array)." };
			props["parentId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Optional parent GUID for deserialized objects." };
			props["position"] = new Dictionary<string, object> { ["description"] = "World position {x,y,z} override for deserialized objects." };

			// clone_with_properties params
			props["sourceId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Source GO GUID for clone_with_properties." };
			props["sourceName"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Source GO name for clone_with_properties." };
			props["newName"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the cloned object." };
			props["overrides"] = new Dictionary<string, object> { ["description"] = "Property overrides as {componentType.propertyName: value} or {propertyName: value}." };

			// compare_objects params
			props["idA"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of object A." };
			props["nameA"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of object A." };
			props["idB"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of object B." };
			props["nameB"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of object B." };
			props["deep"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Include serialized JSON comparison (default false)." };

			// batch_set_network_mode params
			props["networkMode"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Network mode: Never, Object, or Snapshot." };

			// get_serialized params
			props["id"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of object for get_serialized." };
			props["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of object for get_serialized." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object>
			{
				["name"] = "manage_scene_data",
				["description"] = "Serialize, deserialize, clone-with-overrides, compare objects, and batch set network modes. Enables scene templates, data-driven workflows, and multiplayer optimization.",
				["inputSchema"] = schema
			};
		}
	}
}
