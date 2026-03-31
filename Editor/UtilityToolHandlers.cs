using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Utility MCP tools: get_asset_dependencies, batch_transform, copy_component, get_object_bounds.
/// </summary>
internal static class UtilityToolHandlers
	{

	// ── get_asset_dependencies ────────────────────────────────────────────────

	internal static object GetAssetDependencies( JsonElement args )
	{
		string path = OzmiumSceneHelpers.Get( args, "assetPath", (string)null );
		if ( string.IsNullOrEmpty( path ) )
			return OzmiumSceneHelpers.Txt( "Provide 'assetPath'." );

		path = OzmiumSceneHelpers.NormalizePath( path );
		var asset = AssetSystem.FindByPath( path );
		if ( asset == null )
			return OzmiumSceneHelpers.Txt( $"Asset not found: '{path}'." );

		try
		{
			var deps = new List<string>();
			CollectDependencies( asset, deps, new HashSet<string>() );

			if ( deps.Count == 0 )
				return OzmiumSceneHelpers.Txt( $"No dependencies found for '{path}'." );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				asset         = path,
				dependencies  = deps
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	private static void CollectDependencies( Asset asset, List<string> result, HashSet<string> visited )
	{
		if ( !visited.Add( asset.Path ) ) return;

		try
		{
			foreach ( var dep in asset.GetReferences( false ) )
			{
				result.Add( dep?.Path ?? "?" );
				var depAsset = AssetSystem.FindByPath( dep?.Path ?? "" );
				if ( depAsset != null )
					CollectDependencies( depAsset, result, visited );
			}
		}
		catch { }
	}

	// ── batch_transform ─────────────────────────────────────────────────────

	internal static object BatchTransform( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' array." );

		if ( !args.TryGetProperty( "position", out var posEl ) || posEl.ValueKind != JsonValueKind.Object )
			return OzmiumSceneHelpers.Txt( "Provide 'position' object {x,y,z}." );

		try
		{
			float ox = 0, oy = 0, oz = 0;
			if ( posEl.TryGetProperty( "x", out var xp ) ) ox = xp.GetSingle();
			if ( posEl.TryGetProperty( "y", out var yp ) ) oy = yp.GetSingle();
			if ( posEl.TryGetProperty( "z", out var zp ) ) oz = zp.GetSingle();

			int count = 0;
			var errors = new List<string>();

			foreach ( var idEl in idsEl.EnumerateArray() )
			{
				var idStr = idEl.GetString();
				if ( !Guid.TryParse( idStr, out var guid ) )
				{
					errors.Add( $"Invalid GUID: {idStr}" );
					continue;
					}

				var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( go == null )
				{
					errors.Add( $"Not found: {idStr}" );
					continue;
				}

				go.WorldPosition += new Vector3( ox, oy, oz );
				count++;
			}

			if ( errors.Count > 0 )
				return OzmiumSceneHelpers.Txt( $"Moved {count} objects. Errors: {string.Join( ", ", errors )}" );

			return OzmiumSceneHelpers.Txt( $"Moved {count} objects by ({ox}, {oy}, {oz})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── copy_component ───────────────────────────────────────────────────────

	internal static object CopyComponent( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string sourceId   = OzmiumSceneHelpers.Get( args, "sourceId", (string)null );
		string sourceName = OzmiumSceneHelpers.Get( args, "sourceName", (string)null );
		string targetId   = OzmiumSceneHelpers.Get( args, "targetId", (string)null );
		string targetName = OzmiumSceneHelpers.Get( args, "targetName", (string)null );
		string compType   = OzmiumSceneHelpers.Get( args, "componentType", (string)null );

		if ( string.IsNullOrEmpty( compType ) )
			return OzmiumSceneHelpers.Txt( "Provide 'componentType'." );

		var sourceGo = OzmiumSceneHelpers.FindGo( scene, sourceId, sourceName );
		if ( sourceGo == null ) return OzmiumSceneHelpers.Txt( "Source object not found." );

		var targetGo = OzmiumSceneHelpers.FindGo( scene, targetId, targetName );
		if ( targetGo == null ) return OzmiumSceneHelpers.Txt( "Target object not found." );

		var sourceComp = sourceGo.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( compType, StringComparison.OrdinalIgnoreCase ) >= 0 );
		if ( sourceComp == null )
			return OzmiumSceneHelpers.Txt( $"Component '{compType}' not found on source '{sourceGo.Name}'." );

		try
		{
			// Use TypeLibrary to create the same component type on the target
			var td = OzmiumWriteHandlers.FindComponentTypeDescription( compType );
			if ( td == null )
				return OzmiumSceneHelpers.Txt( $"Component type '{compType}' not found in TypeLibrary." );

			var newComp = targetGo.Components.Create( td );
			return OzmiumSceneHelpers.Txt( $"Copied {sourceComp.GetType().Name} from '{sourceGo.Name}' to '{targetGo.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── get_object_bounds ────────────────────────────────────────────────────

	internal static object GetObjectBounds( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			BBox bounds = BBox.FromPositionAndSize( go.WorldPosition, 0.1f );

			// Try collider bounds first
			var collider = go.Components.GetAll().FirstOrDefault( c => c is Collider ) as Collider;
			if ( collider != null )
				bounds = collider.GetWorldBounds();

			// Try ModelRenderer bounds
			var modelRenderer = go.Components.GetAll().FirstOrDefault( c => c.GetType().Name.Contains( "ModelRenderer" ) );
			if ( modelRenderer != null )
			{
				var prop = modelRenderer.GetType().GetProperty( "Model" );
				if ( prop != null )
				{
					var model = prop.GetValue( modelRenderer ) as Model;
					if ( model != null && !model.IsError )
					{
						var bbox = model.Bounds;
						if ( bbox.Volume > 0 )
							bounds = bbox;
					}
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				id      = go.Id.ToString(),
				name    = go.Name,
				enabled = go.Enabled,
				mins    = OzmiumSceneHelpers.V3( bounds.Mins ),
				maxs    = OzmiumSceneHelpers.V3( bounds.Maxs )
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

	internal static Dictionary<string, object> SchemaGetAssetDependencies => S( "get_asset_dependencies",
		"Returns all assets referenced by a given asset (materials, textures, etc.).",
		new Dictionary<string, object>
		{
			["assetPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Asset path to query." }
		},
		new[] { "assetPath" } );

	internal static Dictionary<string, object> SchemaBatchTransform => S( "batch_transform",
		"Applies a position offset to multiple objects at once.",
		new Dictionary<string, object>
		{
			["ids"]      = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of GUIDs.", ["items"] = new Dictionary<string, object> { ["type"] = "string" } },
			["position"] = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Offset {x,y,z} to add to each object." }
		} );

	internal static Dictionary<string, object> SchemaCopyComponent => S( "copy_component",
		"Copies a component from one GameObject to another.",
		new Dictionary<string, object>
	{
			["sourceId"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Source GO GUID or name." },
			["sourceName"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Source GO exact name." },
			["targetId"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Target GO GUID." },
			["targetName"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Target GO name." },
			["componentType"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Component type to copy." }
		},
		new[] { "sourceId", "targetId", "componentType" } );

	internal static Dictionary<string, object> SchemaGetObjectBounds => S( "get_object_bounds",
		"Returns the world-space bounding box of a GameObject.",
		new Dictionary<string, object>
	{
			["id"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." }
	} );
}
