using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Bulk object operation MCP tools: batch enable/delete/tag/material/property,
/// and array duplication for creating grids of objects.
/// </summary>
internal static class BatchToolHandlers
{

	// ── batch_enable ───────────────────────────────────────────────────────

	private static object BatchEnable( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		bool enabled = OzmiumSceneHelpers.Get( args, "enabled", true );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		int found = 0, modified = 0;
		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;
			found++;
			go.Enabled = enabled;
			modified++;
		}

		return OzmiumSceneHelpers.Txt( $"Set enabled={enabled} on {modified} object(s) (found {found} of {ids.Count})." );
	}

	// ── batch_delete ───────────────────────────────────────────────────────

	private static object BatchDelete( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		int found = 0, destroyed = 0;
		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;
			found++;
			try { go.Destroy(); destroyed++; } catch { }
		}

		return OzmiumSceneHelpers.Txt( $"Destroyed {destroyed} object(s) (found {found} of {ids.Count})." );
	}

	// ── batch_set_tags ─────────────────────────────────────────────────────

	private static object BatchSetTags( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		int found = 0, modified = 0;

		// set: replace all tags
		if ( args.TryGetProperty( "tags_set", out var setEl ) && setEl.ValueKind == JsonValueKind.Array )
		{
			var tags = new List<string>();
			foreach ( var t in setEl.EnumerateArray() ) tags.Add( t.GetString() );

			foreach ( var idStr in ids )
			{
				if ( !Guid.TryParse( idStr, out var guid ) ) continue;
				var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( go == null ) continue;
				found++;
				go.Tags.RemoveAll();
				foreach ( var t in tags ) go.Tags.Add( t );
				modified++;
			}
		}
		else
		{
			// add tags
			if ( args.TryGetProperty( "tags_add", out var addEl ) && addEl.ValueKind == JsonValueKind.Array )
			{
				var addTags = new List<string>();
				foreach ( var t in addEl.EnumerateArray() ) addTags.Add( t.GetString() );

				foreach ( var idStr in ids )
				{
					if ( !Guid.TryParse( idStr, out var guid ) ) continue;
					var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
					if ( go == null ) continue;
					found++;
					foreach ( var t in addTags ) go.Tags.Add( t );
					modified++;
				}
			}

			// remove tags
			if ( args.TryGetProperty( "tags_remove", out var remEl ) && remEl.ValueKind == JsonValueKind.Array )
			{
				var remTags = new List<string>();
				foreach ( var t in remEl.EnumerateArray() ) remTags.Add( t.GetString() );

				foreach ( var idStr in ids )
				{
					if ( !Guid.TryParse( idStr, out var guid ) ) continue;
					var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
					if ( go == null ) continue;
					foreach ( var t in remTags ) go.Tags.Remove( t );
				}
			}
		}

		return OzmiumSceneHelpers.Txt( $"Modified tags on {modified} object(s) (found {found} of {ids.Count})." );
	}

	// ── batch_set_material ─────────────────────────────────────────────────

	private static object BatchSetMaterial( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", (string)null );
		int faceIndex = OzmiumSceneHelpers.Get( args, "faceIndex", -1 );

		if ( string.IsNullOrEmpty( materialPath ) ) return OzmiumSceneHelpers.Txt( "Provide 'materialPath'." );
		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		var material = MaterialHelper.LoadMaterial( materialPath );
		if ( material == null )
			return OzmiumSceneHelpers.Txt( $"Failed to load material '{materialPath}'." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		int found = 0, modified = 0;
		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;

			var mc = go.Components.Get<MeshComponent>();
			if ( mc == null || mc.Mesh == null ) continue;

			found++;
			if ( faceIndex >= 0 )
			{
				var hFace = mc.Mesh.FaceHandleFromIndex( faceIndex );
				if ( hFace.IsValid )
				{
					mc.Mesh.SetFaceMaterial( hFace, material );
					modified++;
				}
			}
			else
			{
				foreach ( var hf in mc.Mesh.FaceHandles )
					mc.Mesh.SetFaceMaterial( hf, material );
				modified++;
			}
		}

		return OzmiumSceneHelpers.Txt( $"Applied material '{materialPath}' to {modified} mesh(es) (found {found} of {ids.Count})." );
	}

	// ── duplicate_array ────────────────────────────────────────────────────

	private static object DuplicateArray( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string sourceId   = OzmiumSceneHelpers.Get( args, "sourceId",   (string)null );
		string sourceName = OzmiumSceneHelpers.Get( args, "sourceName", (string)null );
		int countX = OzmiumSceneHelpers.Get( args, "countX", 1 );
		int countY = OzmiumSceneHelpers.Get( args, "countY", 1 );
		int countZ = OzmiumSceneHelpers.Get( args, "countZ", 1 );
		float spacingX = OzmiumSceneHelpers.Get( args, "spacingX", 100f );
		float spacingY = OzmiumSceneHelpers.Get( args, "spacingY", 0f );
		float spacingZ = OzmiumSceneHelpers.Get( args, "spacingZ", 100f );

		var sourceGo = OzmiumSceneHelpers.FindGo( scene, sourceId, sourceName );
		if ( sourceGo == null ) return OzmiumSceneHelpers.Txt( "Source object not found." );

		try
		{
			var serialized = sourceGo.Serialize();
			var basePos = sourceGo.WorldPosition;
			var created = new List<Dictionary<string, object>>();

			for ( int ix = 0; ix < countX; ix++ )
			{
				for ( int iy = 0; iy < countY; iy++ )
				{
					for ( int iz = 0; iz < countZ; iz++ )
					{
						// Skip the origin cell (0,0,0) — that's the original
						if ( ix == 0 && iy == 0 && iz == 0 ) continue;

						var clone = scene.CreateObject( false );
						clone.Deserialize( serialized );
						clone.Name = sourceGo.Name;
						clone.MakeNameUnique();
						clone.WorldPosition = basePos + new Vector3( ix * spacingX, iy * spacingY, iz * spacingZ );
						clone.Enabled = true;

						created.Add( new Dictionary<string, object>
						{
							["id"] = clone.Id.ToString(),
							["name"] = clone.Name,
							["position"] = OzmiumSceneHelpers.V3( clone.WorldPosition )
						} );
					}
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created {created.Count} duplicates in {countX}x{countY}x{countZ} grid from '{sourceGo.Name}'.",
				sourceId = sourceGo.Id.ToString(),
				sourceName = sourceGo.Name,
				grid = new { countX, countY, countZ },
				spacing = new { x = spacingX, y = spacingY, z = spacingZ },
				created
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── batch_set_property ─────────────────────────────────────────────────

	private static object BatchSetProperty( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string compType   = OzmiumSceneHelpers.Get( args, "componentType", (string)null );
		string propName   = OzmiumSceneHelpers.Get( args, "propertyName",  (string)null );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );
		if ( string.IsNullOrEmpty( propName ) ) return OzmiumSceneHelpers.Txt( "Provide 'propertyName'." );
		if ( !args.TryGetProperty( "value", out var valEl ) ) return OzmiumSceneHelpers.Txt( "Provide 'value'." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		int found = 0, modified = 0;
		var errors = new List<string>();

		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;
			found++;

			// Find component
			var comp = go.Components.GetAll().FirstOrDefault( c =>
				string.IsNullOrEmpty( compType ) ||
				c.GetType().Name.IndexOf( compType, StringComparison.OrdinalIgnoreCase ) >= 0 );
			if ( comp == null ) { errors.Add( $"No '{compType}' on '{go.Name}'" ); continue; }

			var prop = comp.GetType().GetProperty( propName,
				System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
			if ( prop == null || !prop.CanWrite ) { errors.Add( $"No writable '{propName}' on '{comp.GetType().Name}'" ); continue; }

			try
			{
				object converted = OzmiumWriteHandlers.ConvertJsonValue( valEl, prop.PropertyType );
				prop.SetValue( comp, converted );
				modified++;
			}
			catch ( Exception ex ) { errors.Add( $"Error on '{go.Name}': {ex.Message}" ); }
		}

		var result = new Dictionary<string, object>
		{
			["message"] = $"Set '{propName}' on {modified} component(s) (found {found} of {ids.Count})."
		};
		if ( errors.Count > 0 ) result["errors"] = errors;

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( result, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── batch_reparent ───────────────────────────────────────────────────

	private static object BatchReparent( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		GameObject parentGo = null;
		if ( !string.IsNullOrEmpty( parentId ) && parentId != "null" )
		{
			if ( !Guid.TryParse( parentId, out var parentGuid ) )
				return OzmiumSceneHelpers.Txt( $"Invalid parentId GUID." );
			parentGo = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == parentGuid );
			if ( parentGo == null ) return OzmiumSceneHelpers.Txt( $"Parent '{parentId}' not found." );
		}

		int found = 0, modified = 0;
		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;
			found++;

			try
			{
				go.SetParent( parentGo );
				modified++;
			}
			catch ( Exception ex )
			{
				return OzmiumSceneHelpers.Txt( $"Error reparenting '{go.Name}': {ex.Message}" );
			}
		}

		var parentLabel = parentGo != null ? $"'{parentGo.Name}'" : "scene root";
		return OzmiumSceneHelpers.Txt( $"Reparented {modified} object(s) under {parentLabel} (found {found} of {ids.Count})." );
	}

	// ── batch_operations (Omnibus) ────────────────────────────────────────

	internal static object BatchOperations( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"batch_enable"       => BatchEnable( args ),
			"batch_delete"       => BatchDelete( args ),
			"batch_set_tags"     => BatchSetTags( args ),
			"batch_set_material" => BatchSetMaterial( args ),
			"duplicate_array"    => DuplicateArray( args ),
			"batch_set_property" => BatchSetProperty( args ),
			"batch_reparent"     => BatchReparent( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: batch_enable, batch_delete, batch_set_tags, batch_set_material, duplicate_array, batch_set_property, batch_reparent" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaBatchOperations
	{
		get
		{
			var stringArrayItem = new Dictionary<string, object> { ["type"] = "string" };
			var props = new Dictionary<string, object>();
			props["operation"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "batch_enable", "batch_delete", "batch_set_tags", "batch_set_material", "duplicate_array", "batch_set_property", "batch_reparent" } };
			props["ids"]           = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of GUIDs to operate on.", ["items"] = stringArrayItem };
			props["parentId"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Parent GUID for batch_reparent (omit or 'null' for root)." };
			props["enabled"]       = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enable state for batch_enable (default true)." };
			props["tags_set"]      = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Replace all tags with this list.", ["items"] = stringArrayItem };
			props["tags_add"]      = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Tags to add.", ["items"] = stringArrayItem };
			props["tags_remove"]   = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Tags to remove.", ["items"] = stringArrayItem };
			props["materialPath"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Material path for batch_set_material." };
			props["faceIndex"]     = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Face index (-1 for all, default -1)." };
			props["sourceId"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Source GO GUID for duplicate_array." };
			props["sourceName"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Source GO name for duplicate_array." };
			props["countX"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Grid count X (default 1)." };
			props["countY"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Grid count Y (default 1)." };
			props["countZ"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Grid count Z (default 1)." };
			props["spacingX"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Grid spacing X (default 100)." };
			props["spacingY"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Grid spacing Y (default 0)." };
			props["spacingZ"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Grid spacing Z (default 100)." };
			props["componentType"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Component type substring for batch_set_property." };
			props["propertyName"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Property name for batch_set_property." };
			props["value"]         = new Dictionary<string, object> { ["description"] = "Property value for batch_set_property." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object> { ["name"] = "batch_operations", ["description"] = "Perform bulk operations on multiple objects at once: enable/disable, delete, tag, apply materials, duplicate in grids, set properties, or reparent.", ["inputSchema"] = schema };
		}
	}
}
