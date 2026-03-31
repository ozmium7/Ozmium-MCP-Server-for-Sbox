using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using HalfEdgeMesh;
using Editor.MeshEditor;

namespace SboxMcpServer;

/// <summary>
/// General-purpose mesh manipulation MCP tools for S&box.
/// Provides tools for creating primitive meshes, editing vertices, faces, and materials,
/// and querying mesh information. Integrates with S&box's native mesh editing system.
/// </summary>
internal static class MeshEditHandlers
{

	// ── create_block ─────────────────────────────────────────────────────────

	/// <summary>
	/// Creates a primitive block using PrimitiveBuilder and PolygonMesh.
	/// This creates proper mesh geometry compatible with S&box's mesh editing tools.
	/// </summary>
	internal static object CreateBlock( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		// Extract parameters
		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		float sizeX = OzmiumSceneHelpers.Get( args, "sizeX", 100f );
		float sizeY = OzmiumSceneHelpers.Get( args, "sizeY", 100f );
		float sizeZ = OzmiumSceneHelpers.Get( args, "sizeZ", 100f );
		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", "materials/dev/reflectivity_30.vmat" );
		string name = OzmiumSceneHelpers.Get( args, "name", "Block" );

		try
		{
			// Create the GameObject
			var gameObject = scene.CreateObject();
			gameObject.Name = name;
			gameObject.WorldPosition = new Vector3( x, y, z );

			// Load material
			var material = MaterialHelper.LoadMaterialOrDefault( materialPath );

			// Create a box primitive using the PrimitiveBuilder pattern
			var boxMins = new Vector3( -sizeX / 2f, -sizeY / 2f, -sizeZ / 2f );
			var boxMaxs = new Vector3( sizeX / 2f, sizeY / 2f, sizeZ / 2f );
			var box = new BBox( boxMins, boxMaxs );

			// Create vertices for a cube (winding matches S&box BlockPrimitive)
			var vertices = new List<Vector3>
			{
				// Front face (z = maxs.z, normal +Z)
				new( boxMins.x, boxMins.y, boxMaxs.z ),
				new( boxMaxs.x, boxMins.y, boxMaxs.z ),
				new( boxMaxs.x, boxMaxs.y, boxMaxs.z ),
				new( boxMins.x, boxMaxs.y, boxMaxs.z ),
				// Back face (z = mins.z, normal -Z)
				new( boxMins.x, boxMaxs.y, boxMins.z ),
				new( boxMaxs.x, boxMaxs.y, boxMins.z ),
				new( boxMaxs.x, boxMins.y, boxMins.z ),
				new( boxMins.x, boxMins.y, boxMins.z ),
				// Top face (y = maxs.y, normal +Y)
				new( boxMaxs.x, boxMaxs.y, boxMins.z ),
				new( boxMins.x, boxMaxs.y, boxMins.z ),
				new( boxMins.x, boxMaxs.y, boxMaxs.z ),
				new( boxMaxs.x, boxMaxs.y, boxMaxs.z ),
				// Bottom face (y = mins.y, normal -Y)
				new( boxMaxs.x, boxMins.y, boxMaxs.z ),
				new( boxMins.x, boxMins.y, boxMaxs.z ),
				new( boxMins.x, boxMins.y, boxMins.z ),
				new( boxMaxs.x, boxMins.y, boxMins.z ),
				// Right face (x = maxs.x, normal +X)
				new( boxMaxs.x, boxMaxs.y, boxMaxs.z ),
				new( boxMaxs.x, boxMins.y, boxMaxs.z ),
				new( boxMaxs.x, boxMins.y, boxMins.z ),
				new( boxMaxs.x, boxMaxs.y, boxMins.z ),
				// Left face (x = mins.x, normal -X)
				new( boxMins.x, boxMaxs.y, boxMins.z ),
				new( boxMins.x, boxMins.y, boxMins.z ),
				new( boxMins.x, boxMins.y, boxMaxs.z ),
				new( boxMins.x, boxMaxs.y, boxMaxs.z )
			};

			// Create new PolygonMesh and populate it
			var mesh = new PolygonMesh();
			var hVertices = mesh.AddVertices( vertices.ToArray() );

			// Define faces using vertex indices (counter-clockwise winding)
			var faceDefinitions = new[]
			{
				new[] { 0, 1, 2, 3 },   // Front
				new[] { 4, 5, 6, 7 },   // Back
				new[] { 8, 9, 10, 11 },  // Top
				new[] { 12, 13, 14, 15 }, // Bottom
				new[] { 16, 17, 18, 19 }, // Right
				new[] { 20, 21, 22, 23 }  // Left
			};

			var hFaces = new List<FaceHandle>();
			foreach ( var faceIndices in faceDefinitions )
			{
				var faceVerts = faceIndices.Select( i => hVertices[i] ).ToArray();
				var hFace = mesh.AddFace( faceVerts );
				hFaces.Add( hFace );
			}

			// Apply material to all faces
			foreach ( var hFace in hFaces )
			{
				mesh.SetFaceMaterial( hFace, material );
			}

			// Align textures to grid and set smoothing
			mesh.TextureAlignToGrid( mesh.Transform );
			mesh.SetSmoothingAngle( 40.0f );

			// Create MeshComponent and assign mesh to trigger RebuildMesh()
			var meshComponent = gameObject.Components.Create<MeshComponent>();
			meshComponent.Mesh = mesh;

			// Add tags
			gameObject.Tags.Add( "mesh" );
			gameObject.Tags.Add( "block" );
			gameObject.Tags.Add( "building" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message  = $"Created block '{name}' ({sizeX}x{sizeY}x{sizeZ}) at ({x}, {y}, {z})",
				id       = gameObject.Id.ToString(),
				name     = gameObject.Name,
				position = OzmiumSceneHelpers.V3( gameObject.WorldPosition ),
				tags     = OzmiumSceneHelpers.GetTags( gameObject ),
				faceCount = hFaces.Count,
				vertexCount = hVertices.Length,
				material = materialPath
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex )
		{
			return OzmiumSceneHelpers.Txt( $"Error creating block: {ex.Message}" );
		}
	}

	// ── set_face_material ─────────────────────────────────────────────────────

	/// <summary>
	/// Applies a material to a specific face or all faces of a mesh.
	/// </summary>
	internal static object SetFaceMaterial( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "gameObjectId", "" );
		string name = OzmiumSceneHelpers.Get( args, "name", "" );
		int faceIndex = OzmiumSceneHelpers.Get( args, "faceIndex", -1 );
		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", "" );

		if ( string.IsNullOrEmpty( materialPath ) )
			return OzmiumSceneHelpers.Txt( "Error: materialPath is required." );

		var gameObject = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( gameObject == null )
			return OzmiumSceneHelpers.Txt( $"Error: GameObject not found (id: {id}, name: {name})." );

		try
		{
			var meshComponent = gameObject.Components.Get<MeshComponent>();
			if ( meshComponent == null )
				return OzmiumSceneHelpers.Txt( $"Error: GameObject '{gameObject.Name}' has no MeshComponent." );

			var mesh = meshComponent.Mesh;
			var material = MaterialHelper.LoadMaterial( materialPath );
			if ( material == null )
				return OzmiumSceneHelpers.Txt( $"Error: Failed to load material '{materialPath}'." );

			if ( faceIndex >= 0 )
			{
				// Apply to specific face
				if ( !MaterialHelper.ApplyMaterialToFace( mesh, faceIndex, material ) )
					return OzmiumSceneHelpers.Txt( $"Error: Invalid face index {faceIndex}." );

				return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
				{
					message = $"Applied material '{materialPath}' to face {faceIndex}",
					gameObjectId = gameObject.Id.ToString(),
					gameObjectName = gameObject.Name,
					faceIndex = faceIndex
				}, OzmiumSceneHelpers.JsonSettings ) );
			}
			else
			{
				// Apply to all faces
				int count = 0;
				foreach ( var hFace in mesh.FaceHandles )
				{
					mesh.SetFaceMaterial( hFace, material );
					count++;
				}

				return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
				{
					message = $"Applied material '{materialPath}' to {count} faces",
					gameObjectId = gameObject.Id.ToString(),
					gameObjectName = gameObject.Name
				}, OzmiumSceneHelpers.JsonSettings ) );
			}
		}
		catch ( Exception ex )
		{
			return OzmiumSceneHelpers.Txt( $"Error setting face material: {ex.Message}" );
		}
	}

	// ── set_texture_parameters ─────────────────────────────────────────────────

	/// <summary>
	/// Sets texture mapping parameters (UV axes and scale) for a specific face.
	/// </summary>
	internal static object SetTextureParameters( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "gameObjectId", "" );
		string name = OzmiumSceneHelpers.Get( args, "name", "" );
		int faceIndex = OzmiumSceneHelpers.Get( args, "faceIndex", -1 );
		float uX = OzmiumSceneHelpers.Get( args, "uAxisX", 1f );
		float uY = OzmiumSceneHelpers.Get( args, "uAxisY", 0f );
		float uZ = OzmiumSceneHelpers.Get( args, "uAxisZ", 0f );
		float vX = OzmiumSceneHelpers.Get( args, "vAxisX", 0f );
		float vY = OzmiumSceneHelpers.Get( args, "vAxisY", 0f );
		float vZ = OzmiumSceneHelpers.Get( args, "vAxisZ", 1f );
		float scaleU = OzmiumSceneHelpers.Get( args, "scaleU", 1f );
		float scaleV = OzmiumSceneHelpers.Get( args, "scaleV", 1f );

		var gameObject = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( gameObject == null )
			return OzmiumSceneHelpers.Txt( $"Error: GameObject not found (id: {id}, name: {name})." );

		try
		{
			var meshComponent = gameObject.Components.Get<MeshComponent>();
			if ( meshComponent == null )
				return OzmiumSceneHelpers.Txt( $"Error: GameObject '{gameObject.Name}' has no MeshComponent." );

			var mesh = meshComponent.Mesh;
			var vAxisU = new Vector3( uX, uY, uZ );
			var vAxisV = new Vector3( vX, vY, vZ );
			var scale = new Vector2( scaleU, scaleV );

			if ( faceIndex >= 0 )
			{
				if ( !MaterialHelper.SetTextureParameters( mesh, faceIndex, vAxisU, vAxisV, scale ) )
					return OzmiumSceneHelpers.Txt( $"Error: Invalid face index {faceIndex}." );
			}
			else
			{
				// Apply to all faces
				foreach ( var hFace in mesh.FaceHandles )
				{
					mesh.SetFaceTextureParameters( hFace, vAxisU, vAxisV, scale );
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = faceIndex >= 0
					? $"Set texture parameters for face {faceIndex}"
					: "Set texture parameters for all faces",
				gameObjectId = gameObject.Id.ToString(),
				gameObjectName = gameObject.Name,
				faceIndex = faceIndex,
				uAxis = OzmiumSceneHelpers.V3( vAxisU ),
				vAxis = OzmiumSceneHelpers.V3( vAxisV ),
				scale = new { x = scaleU, y = scaleV }
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex )
		{
			return OzmiumSceneHelpers.Txt( $"Error setting texture parameters: {ex.Message}" );
		}
	}

	// ── set_vertex_position ────────────────────────────────────────────────────

	/// <summary>
	/// Sets the position of a vertex by handle/index (displacement).
	/// </summary>
	internal static object SetVertexPosition( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "gameObjectId", "" );
		string name = OzmiumSceneHelpers.Get( args, "name", "" );
		int vertexIndex = OzmiumSceneHelpers.Get( args, "vertexIndex", -1 );
		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );

		var gameObject = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( gameObject == null )
			return OzmiumSceneHelpers.Txt( $"Error: GameObject not found (id: {id}, name: {name})." );

		try
		{
			var meshComponent = gameObject.Components.Get<MeshComponent>();
			if ( meshComponent == null )
				return OzmiumSceneHelpers.Txt( $"Error: GameObject '{gameObject.Name}' has no MeshComponent." );

			var mesh = meshComponent.Mesh;
			var hVertex = mesh.VertexHandleFromIndex( vertexIndex );
			if ( !hVertex.IsValid )
				return OzmiumSceneHelpers.Txt( $"Error: Invalid vertex index {vertexIndex}." );

			var newPosition = new Vector3( x, y, z );
			mesh.SetVertexPosition( hVertex, newPosition );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Set vertex {vertexIndex} position to ({x}, {y}, {z})",
				gameObjectId = gameObject.Id.ToString(),
				gameObjectName = gameObject.Name,
				vertexIndex = vertexIndex,
				position = OzmiumSceneHelpers.V3( newPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex )
		{
			return OzmiumSceneHelpers.Txt( $"Error setting vertex position: {ex.Message}" );
		}
	}

	// ── set_vertex_color ────────────────────────────────────────────────────────

	/// <summary>
	/// Sets the color of a vertex (for vertex coloring).
	/// </summary>
	internal static object SetVertexColor( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "gameObjectId", "" );
		string name = OzmiumSceneHelpers.Get( args, "name", "" );
		int vertexIndex = OzmiumSceneHelpers.Get( args, "vertexIndex", -1 );
		float r = OzmiumSceneHelpers.Get( args, "r", 1f );
		float g = OzmiumSceneHelpers.Get( args, "g", 1f );
		float b = OzmiumSceneHelpers.Get( args, "b", 1f );
		float a = OzmiumSceneHelpers.Get( args, "a", 1f );

		var gameObject = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( gameObject == null )
			return OzmiumSceneHelpers.Txt( $"Error: GameObject not found (id: {id}, name: {name})." );

		try
		{
			var meshComponent = gameObject.Components.Get<MeshComponent>();
			if ( meshComponent == null )
				return OzmiumSceneHelpers.Txt( $"Error: GameObject '{gameObject.Name}' has no MeshComponent." );

			var mesh = meshComponent.Mesh;
			var hVertex = mesh.VertexHandleFromIndex( vertexIndex );
			if ( !hVertex.IsValid )
				return OzmiumSceneHelpers.Txt( $"Error: Invalid vertex index {vertexIndex}." );

			// Get the half-edge handle for this vertex
			var hHalfEdge = mesh.HalfEdgeHandleFromIndex( vertexIndex );
			if ( !hHalfEdge.IsValid )
				return OzmiumSceneHelpers.Txt( $"Error: Invalid half-edge for vertex {vertexIndex}." );

			var color = new Color( r, g, b, a );
			mesh.SetVertexColor( hHalfEdge, color );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Set vertex {vertexIndex} color to ({r}, {g}, {b}, {a})",
				gameObjectId = gameObject.Id.ToString(),
				gameObjectName = gameObject.Name,
				vertexIndex = vertexIndex,
				color = new { r, g, b, a }
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex )
		{
			return OzmiumSceneHelpers.Txt( $"Error setting vertex color: {ex.Message}" );
		}
	}

	// ── set_vertex_blend ─────────────────────────────────────────────────────────

	/// <summary>
	/// Sets the blend weight of a vertex (4-channel blend for terrain/texturing).
	/// </summary>
	internal static object SetVertexBlend( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "gameObjectId", "" );
		string name = OzmiumSceneHelpers.Get( args, "name", "" );
		int vertexIndex = OzmiumSceneHelpers.Get( args, "vertexIndex", -1 );
		float r = OzmiumSceneHelpers.Get( args, "r", 0f );
		float g = OzmiumSceneHelpers.Get( args, "g", 0f );
		float b = OzmiumSceneHelpers.Get( args, "b", 0f );
		float a = OzmiumSceneHelpers.Get( args, "blend", 0f );

		var gameObject = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( gameObject == null )
			return OzmiumSceneHelpers.Txt( $"Error: GameObject not found (id: {id}, name: {name})." );

		try
		{
			var meshComponent = gameObject.Components.Get<MeshComponent>();
			if ( meshComponent == null )
				return OzmiumSceneHelpers.Txt( $"Error: GameObject '{gameObject.Name}' has no MeshComponent." );

			var mesh = meshComponent.Mesh;
			var hVertex = mesh.VertexHandleFromIndex( vertexIndex );
			if ( !hVertex.IsValid )
				return OzmiumSceneHelpers.Txt( $"Error: Invalid vertex index {vertexIndex}." );

			var hHalfEdge = mesh.HalfEdgeHandleFromIndex( vertexIndex );
			if ( !hHalfEdge.IsValid )
				return OzmiumSceneHelpers.Txt( $"Error: Invalid half-edge for vertex {vertexIndex}." );

			var blend = new Color( r, g, b, a );
			mesh.SetVertexBlend( hHalfEdge, blend );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Set vertex {vertexIndex} blend to ({r}, {g}, {b}, {a})",
				gameObjectId = gameObject.Id.ToString(),
				gameObjectName = gameObject.Name,
				vertexIndex = vertexIndex,
				blend = new { r, g, b, a }
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex )
		{
			return OzmiumSceneHelpers.Txt( $"Error setting vertex blend: {ex.Message}" );
		}
	}

	// ── get_mesh_info ─────────────────────────────────────────────────────────

	/// <summary>
	/// Queries detailed information about a mesh including counts and per-face data.
	/// </summary>
	internal static object GetMeshInfo( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "gameObjectId", "" );
		string name = OzmiumSceneHelpers.Get( args, "name", "" );

		var gameObject = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( gameObject == null )
			return OzmiumSceneHelpers.Txt( $"Error: GameObject not found (id: {id}, name: {name})." );

		try
		{
			var meshComponent = gameObject.Components.Get<MeshComponent>();
			if ( meshComponent == null )
				return OzmiumSceneHelpers.Txt( $"Error: GameObject '{gameObject.Name}' has no MeshComponent." );

			var mesh = meshComponent.Mesh;
			var faceCount = MaterialHelper.GetFaceCount( mesh );
			var vertexCount = MaterialHelper.GetVertexCount( mesh );
			var edgeCount = MaterialHelper.GetEdgeCount( mesh );

			// Collect per-face material info
			var faceData = new List<object>();
			int idx = 0;
			foreach ( var hFace in mesh.FaceHandles )
			{
				var material = mesh.GetFaceMaterial( hFace );
				faceData.Add( new
				{
					index = idx++,
					material = material?.ResourcePath ?? "default",
					materialName = material?.Name ?? "default"
				} );
			}

			// Calculate bounds
			var bounds = mesh.CalculateBounds();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Mesh info for '{gameObject.Name}'",
				gameObjectId = gameObject.Id.ToString(),
				gameObjectName = gameObject.Name,
				faceCount = faceCount,
				vertexCount = vertexCount,
				edgeCount = edgeCount,
				bounds = new
				{
					mins = OzmiumSceneHelpers.V3( bounds.Mins ),
					maxs = OzmiumSceneHelpers.V3( bounds.Maxs )
				},
				faces = faceData
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex )
		{
			return OzmiumSceneHelpers.Txt( $"Error getting mesh info: {ex.Message}" );
		}
	}

	// ── Tool schemas (used by ToolDefinitions.All) ───────────────────────────

	internal static Dictionary<string, object> SchemaCreateBlock => OzmiumSceneHelpers.S( "create_block",
		"Creates a primitive block mesh using PolygonMesh. Compatible with S&box mesh editing tools.",
		new Dictionary<string, object>
		{
			["x"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position (default: 0)." },
			["y"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position (default: 0)." },
			["z"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position (default: 0)." },
			["sizeX"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Size along X axis (default: 100)." },
			["sizeY"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Size along Y axis (default: 100)." },
			["sizeZ"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Size along Z axis (default: 100)." },
			["materialPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Path to material asset (default: materials/dev/reflectivity_30.vmat)." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of the block (default: 'Block')." }
		} );

	internal static Dictionary<string, object> SchemaSetFaceMaterial => OzmiumSceneHelpers.S( "set_face_material",
		"Applies a material to a specific face or all faces of a mesh.",
		new Dictionary<string, object>
		{
			["gameObjectId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject GUID (use name instead if not provided)." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject name (used if gameObjectId not provided)." },
			["faceIndex"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Face index to apply material to, or -1 for all faces (default: -1)." },
			["materialPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Path to material asset (required)." }
		}, new[] { "materialPath" } );

	internal static Dictionary<string, object> SchemaSetTextureParameters => OzmiumSceneHelpers.S( "set_texture_parameters",
		"Sets texture mapping parameters (UV axes and scale) for faces.",
		new Dictionary<string, object>
		{
			["gameObjectId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject name." },
			["faceIndex"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Face index, or -1 for all faces (default: -1)." },
			["uAxisX"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "U axis X component (default: 1)." },
			["uAxisY"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "U axis Y component (default: 0)." },
			["uAxisZ"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "U axis Z component (default: 0)." },
			["vAxisX"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "V axis X component (default: 0)." },
			["vAxisY"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "V axis Y component (default: 0)." },
			["vAxisZ"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "V axis Z component (default: 1)." },
			["scaleU"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Texture U scale (default: 1)." },
			["scaleV"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Texture V scale (default: 1)." }
		} );

	internal static Dictionary<string, object> SchemaSetVertexPosition => OzmiumSceneHelpers.S( "set_vertex_position",
		"Sets the position of a vertex by index (for displacement/sculpting).",
		new Dictionary<string, object>
		{
			["gameObjectId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject name." },
			["vertexIndex"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Vertex index (required)." },
			["x"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "New X position." },
			["y"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "New Y position." },
			["z"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "New Z position." }
		}, new[] { "vertexIndex" } );

	internal static Dictionary<string, object> SchemaSetVertexColor => OzmiumSceneHelpers.S( "set_vertex_color",
		"Sets the vertex color for vertex painting.",
		new Dictionary<string, object>
		{
			["gameObjectId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject name." },
			["vertexIndex"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Vertex index (required)." },
			["r"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Red component 0-1 (default: 1)." },
			["g"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Green component 0-1 (default: 1)." },
			["b"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blue component 0-1 (default: 1)." },
			["a"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Alpha component 0-1 (default: 1)." }
		}, new[] { "vertexIndex" } );

	internal static Dictionary<string, object> SchemaSetVertexBlend => OzmiumSceneHelpers.S( "set_vertex_blend",
		"Sets the vertex blend weights for terrain/texturing.",
		new Dictionary<string, object>
		{
			["gameObjectId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject name." },
			["vertexIndex"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Vertex index (required)." },
			["r"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Red blend channel 0-1 (default: 0)." },
			["g"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Green blend channel 0-1 (default: 0)." },
			["b"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blue blend channel 0-1 (default: 0)." },
			["blend"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Alpha blend channel 0-1 (default: 0)." }
		}, new[] { "vertexIndex" } );

	internal static Dictionary<string, object> SchemaGetMeshInfo => OzmiumSceneHelpers.S( "get_mesh_info",
		"Queries detailed information about a mesh including counts, bounds, and per-face materials.",
		new Dictionary<string, object>
		{
			["gameObjectId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject GUID." },
			["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject name." }
		} );

	// ── edit_mesh (Omnibus) ──────────────────────────────────────────────────

	internal static object EditMesh( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"set_face_material"      => SetFaceMaterial( args ),
			"set_texture_parameters" => SetTextureParameters( args ),
			"set_vertex_position"    => SetVertexPosition( args ),
			"set_vertex_color"       => SetVertexColor( args ),
			"set_vertex_blend"       => SetVertexBlend( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: set_face_material, set_texture_parameters, set_vertex_position, set_vertex_color, set_vertex_blend" )
		};
	}

	internal static Dictionary<string, object> SchemaEditMesh => OzmiumSceneHelpers.S( "edit_mesh",
		"Edit a mesh: set face materials, texture parameters, vertex positions, vertex colors, or vertex blend weights.",
		new Dictionary<string, object>
		{
			["operation"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "set_face_material", "set_texture_parameters", "set_vertex_position", "set_vertex_color", "set_vertex_blend" } },
			["gameObjectId"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject GUID." },
			["name"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GameObject name." },
			["faceIndex"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Face index, or -1 for all faces (default: -1)." },
			["vertexIndex"]  = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Vertex index (for vertex operations)." },
			["materialPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Material path (set_face_material)." },
			["x"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "X position or U axis X." },
			["y"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Y position or U axis Y." },
			["z"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Z position or U axis Z." },
			["r"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Red channel 0-1 (vertex color/blend)." },
			["g"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Green channel 0-1 (vertex color/blend)." },
			["b"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blue channel 0-1 (vertex color/blend)." },
			["a"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Alpha 0-1 (vertex color)." },
			["blend"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Alpha blend channel 0-1 (vertex blend)." },
			["uAxisX"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "U axis X component (default: 1)." },
			["uAxisY"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "U axis Y component (default: 0)." },
			["uAxisZ"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "U axis Z component (default: 0)." },
			["vAxisX"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "V axis X component (default: 0)." },
			["vAxisY"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "V axis Y component (default: 0)." },
			["vAxisZ"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "V axis Z component (default: 1)." },
			["scaleU"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Texture U scale (default: 1)." },
			["scaleV"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Texture V scale (default: 1)." }
		},
		new[] { "operation" } );
}
