using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using HalfEdgeMesh;

namespace SboxMcpServer;

/// <summary>
/// Procedural geometry MCP tools: custom meshes from vertex/face arrays,
/// ramps, cylinders, arches, merge, scale, and extrude operations.
/// Uses PolygonMesh API from S&box's half-edge mesh system.
/// </summary>
internal static class ProceduralMeshToolHandlers
{

	// ── create_mesh ─────────────────────────────────────────────────────────

	private static object CreateMesh( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name = OzmiumSceneHelpers.Get( args, "name", "Custom Mesh" );
		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", "materials/dev/reflectivity_30.vmat" );

		if ( !args.TryGetProperty( "vertices", out var vertsEl ) || vertsEl.ValueKind != JsonValueKind.Array )
			return OzmiumSceneHelpers.Txt( "Provide 'vertices' as array of {x,y,z} objects." );
		if ( !args.TryGetProperty( "faces", out var facesEl ) || facesEl.ValueKind != JsonValueKind.Array )
			return OzmiumSceneHelpers.Txt( "Provide 'faces' as array of vertex index arrays." );

		try
		{
			// Parse vertices
			var positions = new List<Vector3>();
			foreach ( var v in vertsEl.EnumerateArray() )
			{
				float vx = 0, vy = 0, vz = 0;
				if ( v.TryGetProperty( "x", out var xp ) ) vx = xp.GetSingle();
				if ( v.TryGetProperty( "y", out var yp ) ) vy = yp.GetSingle();
				if ( v.TryGetProperty( "z", out var zp ) ) vz = zp.GetSingle();
				positions.Add( new Vector3( vx, vy, vz ) );
			}

			// Create mesh
			var mesh = new PolygonMesh();
			var hVertices = mesh.AddVertices( positions.ToArray() );

			// Parse faces
			var hFaces = new List<FaceHandle>();
			foreach ( var f in facesEl.EnumerateArray() )
			{
				var indices = new List<int>();
				foreach ( var idx in f.EnumerateArray() )
					indices.Add( idx.GetInt32() );

				var faceVerts = indices.Select( i => hVertices[i] ).ToArray();
				hFaces.Add( mesh.AddFace( faceVerts ) );
			}

			// Apply material
			var material = MaterialHelper.LoadMaterialOrDefault( materialPath );
			foreach ( var hf in hFaces )
				mesh.SetFaceMaterial( hf, material );

			mesh.TextureAlignToGrid( mesh.Transform );
			mesh.SetSmoothingAngle( 40.0f );

			// Create GO
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );
			var mc = go.Components.Create<MeshComponent>();
			mc.Mesh = mesh;

			go.Tags.Add( "mesh" );
			go.Tags.Add( "procedural" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message     = $"Created custom mesh '{name}' ({positions.Count} verts, {hFaces.Count} faces).",
				id          = go.Id.ToString(),
				name        = go.Name,
				position    = OzmiumSceneHelpers.V3( go.WorldPosition ),
				vertexCount = positions.Count,
				faceCount   = hFaces.Count,
				material    = materialPath
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── add_face ────────────────────────────────────────────────────────────

	private static object AddFace( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", "" );

		if ( !args.TryGetProperty( "vertices", out var vertsEl ) || vertsEl.ValueKind != JsonValueKind.Array )
			return OzmiumSceneHelpers.Txt( "Provide 'vertices' as array of {x,y,z} objects." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var mc = go.Components.Get<MeshComponent>();
		if ( mc == null || mc.Mesh == null )
			return OzmiumSceneHelpers.Txt( $"No MeshComponent on '{go.Name}'." );

		try
		{
			var mesh = mc.Mesh;
			var positions = new List<Vector3>();
			foreach ( var v in vertsEl.EnumerateArray() )
			{
				float vx = 0, vy = 0, vz = 0;
				if ( v.TryGetProperty( "x", out var xp ) ) vx = xp.GetSingle();
				if ( v.TryGetProperty( "y", out var yp ) ) vy = yp.GetSingle();
				if ( v.TryGetProperty( "z", out var zp ) ) vz = zp.GetSingle();
				positions.Add( new Vector3( vx, vy, vz ) );
			}

			var hVertices = mesh.AddVertices( positions.ToArray() );
			var hFace = mesh.AddFace( hVertices );

			if ( !string.IsNullOrEmpty( materialPath ) )
			{
				var mat = MaterialHelper.LoadMaterial( materialPath );
				if ( mat != null ) mesh.SetFaceMaterial( hFace, mat );
			}

			mc.Mesh = mesh; // Trigger rebuild

			return OzmiumSceneHelpers.Txt( $"Added face ({positions.Count} vertices) to '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── merge ───────────────────────────────────────────────────────────────

	private static object Merge( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string sourceId   = OzmiumSceneHelpers.Get( args, "sourceId",   (string)null );
		string sourceName = OzmiumSceneHelpers.Get( args, "sourceName", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Target object not found." );

		var sourceGo = OzmiumSceneHelpers.FindGo( scene, sourceId, sourceName );
		if ( sourceGo == null ) return OzmiumSceneHelpers.Txt( "Source object not found." );

		var mcTarget = go.Components.Get<MeshComponent>();
		var mcSource = sourceGo.Components.Get<MeshComponent>();
		if ( mcTarget == null || mcTarget.Mesh == null ) return OzmiumSceneHelpers.Txt( $"Target '{go.Name}' has no MeshComponent." );
		if ( mcSource == null || mcSource.Mesh == null ) return OzmiumSceneHelpers.Txt( $"Source '{sourceGo.Name}' has no MeshComponent." );

		try
		{
			var transform = sourceGo.WorldTransform;
			mcTarget.Mesh.MergeMesh( mcSource.Mesh, transform,
				out _, out _, out _ );

			mcTarget.Mesh = mcTarget.Mesh; // Trigger rebuild
			sourceGo.Destroy();

			return OzmiumSceneHelpers.Txt( $"Merged '{sourceGo.Name}' into '{go.Name}'. Source destroyed." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── scale ───────────────────────────────────────────────────────────────

	private static object Scale( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		float sx = OzmiumSceneHelpers.Get( args, "scaleX", 1f );
		float sy = OzmiumSceneHelpers.Get( args, "scaleY", 1f );
		float sz = OzmiumSceneHelpers.Get( args, "scaleZ", 1f );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var mc = go.Components.Get<MeshComponent>();
		if ( mc == null || mc.Mesh == null )
			return OzmiumSceneHelpers.Txt( $"No MeshComponent on '{go.Name}'." );

		try
		{
			mc.Mesh.Scale( new Vector3( sx, sy, sz ) );
			mc.Mesh = mc.Mesh; // Trigger rebuild

			return OzmiumSceneHelpers.Txt( $"Scaled '{go.Name}' by ({sx}, {sy}, {sz})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── extrude ─────────────────────────────────────────────────────────────

	private static object Extrude( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		int faceIndex = OzmiumSceneHelpers.Get( args, "faceIndex", 0 );
		float dx = OzmiumSceneHelpers.Get( args, "offsetX", 0f );
		float dy = OzmiumSceneHelpers.Get( args, "offsetY", 100f );
		float dz = OzmiumSceneHelpers.Get( args, "offsetZ", 0f );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var mc = go.Components.Get<MeshComponent>();
		if ( mc == null || mc.Mesh == null )
			return OzmiumSceneHelpers.Txt( $"No MeshComponent on '{go.Name}'." );

		try
		{
			var mesh = mc.Mesh;
			var hFace = mesh.FaceHandleFromIndex( faceIndex );
			if ( !hFace.IsValid )
				return OzmiumSceneHelpers.Txt( $"Invalid face index {faceIndex}." );

			mesh.ExtrudeFaces( new[] { hFace }, out var newFaces, out var connectingFaces, new Vector3( dx, dy, dz ) );
			mc.Mesh = mesh; // Trigger rebuild

			return OzmiumSceneHelpers.Txt( $"Extruded face {faceIndex} on '{go.Name}' by ({dx}, {dy}, {dz}). Created {newFaces.Count} new faces, {connectingFaces.Count} connecting faces." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_ramp ─────────────────────────────────────────────────────────

	private static object CreateRamp( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		float width  = OzmiumSceneHelpers.Get( args, "width",  100f );
		float height = OzmiumSceneHelpers.Get( args, "height", 100f );
		float depth  = OzmiumSceneHelpers.Get( args, "depth",  200f );
		float angle  = OzmiumSceneHelpers.Get( args, "angle",  0f );
		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", "materials/dev/reflectivity_30.vmat" );
		string name = OzmiumSceneHelpers.Get( args, "name", "Ramp" );

		try
		{
			// Build ramp as a wedge (triangular prism):
			// Bottom face: 4 vertices
			// Slope face: 3 vertices (hypotenuse)
			// Back face: 3 vertices (vertical)
			// Left side: triangle
			// Right side: triangle
			var hw = width / 2f;
			var vertices = new Vector3[]
			{
				// Bottom face (4 vertices)
				new( -hw, 0, 0 ),
				new(  hw, 0, 0 ),
				new(  hw, 0, depth ),
				new( -hw, 0, depth ),
				// Top edge (2 vertices)
				new( -hw, height, depth ),
				new(  hw, height, depth ),
			};

			var faceDefs = new int[][]
			{
				new[] { 0, 1, 2, 3 },       // Bottom
				new[] { 0, 4, 5, 1 },       // Slope (front)
				new[] { 3, 2, 5, 4 },       // Back
				new[] { 0, 3, 4 },          // Left side
				new[] { 1, 5, 2 },          // Right side
			};

			var mesh = new PolygonMesh();
			var hVerts = mesh.AddVertices( vertices );
			var material = MaterialHelper.LoadMaterialOrDefault( materialPath );

			var hFaces = new List<FaceHandle>();
			foreach ( var face in faceDefs )
			{
				var fv = face.Select( i => hVerts[i] ).ToArray();
				hFaces.Add( mesh.AddFace( fv ) );
			}

			foreach ( var hf in hFaces )
				mesh.SetFaceMaterial( hf, material );

			mesh.TextureAlignToGrid( mesh.Transform );
			mesh.SetSmoothingAngle( 40.0f );

			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );
			if ( angle != 0f ) go.WorldRotation = Rotation.From( 0, angle, 0 );

			var mc = go.Components.Create<MeshComponent>();
			mc.Mesh = mesh;

			go.Tags.Add( "mesh" );
			go.Tags.Add( "ramp" );
			go.Tags.Add( "building" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created ramp '{name}' ({width}x{height}x{depth}, angle={angle}).",
				id       = go.Id.ToString(),
				name     = go.Name,
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				faceCount = hFaces.Count,
				material = materialPath
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_cylinder ─────────────────────────────────────────────────────

	private static object CreateCylinder( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 50f );
		float height = OzmiumSceneHelpers.Get( args, "height", 200f );
		int sides = OzmiumSceneHelpers.Get( args, "sides", 16 );
		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", "materials/dev/reflectivity_30.vmat" );
		string name = OzmiumSceneHelpers.Get( args, "name", "Cylinder" );

		sides = Math.Clamp( sides, 3, 64 );

		try
		{
			var vertices = new List<Vector3>();
			var halfH = height / 2f;

			// Bottom cap center
			vertices.Add( new Vector3( 0, -halfH, 0 ) );
			// Bottom cap ring
			for ( int i = 0; i < sides; i++ )
			{
				float a = (i / (float)sides) * MathF.PI * 2f;
				vertices.Add( new Vector3( MathF.Cos( a ) * radius, -halfH, MathF.Sin( a ) * radius ) );
			}
			// Top cap center
			vertices.Add( new Vector3( 0, halfH, 0 ) );
			// Top cap ring
			for ( int i = 0; i < sides; i++ )
			{
				float a = (i / (float)sides) * MathF.PI * 2f;
				vertices.Add( new Vector3( MathF.Cos( a ) * radius, halfH, MathF.Sin( a ) * radius ) );
			}

			var faceDefs = new List<int[]>();
			int botCenter = 0;
			int topCenter = 1 + sides;

			// Bottom cap (fan)
			for ( int i = 0; i < sides; i++ )
			{
				int next = (i + 1) % sides;
				faceDefs.Add( new[] { botCenter, 1 + i, 1 + next } );
			}

			// Top cap (fan, reversed winding)
			for ( int i = 0; i < sides; i++ )
			{
				int next = (i + 1) % sides;
				faceDefs.Add( new[] { topCenter, topCenter + 1 + next, topCenter + 1 + i } );
			}

			// Side faces
			for ( int i = 0; i < sides; i++ )
			{
				int next = (i + 1) % sides;
				faceDefs.Add( new[] { 1 + i, topCenter + 1 + i, topCenter + 1 + next, 1 + next } );
			}

			var mesh = new PolygonMesh();
			var hVerts = mesh.AddVertices( vertices.ToArray() );
			var material = MaterialHelper.LoadMaterialOrDefault( materialPath );

			var hFaces = new List<FaceHandle>();
			foreach ( var face in faceDefs )
			{
				var fv = face.Select( idx => hVerts[idx] ).ToArray();
				hFaces.Add( mesh.AddFace( fv ) );
			}

			foreach ( var hf in hFaces )
				mesh.SetFaceMaterial( hf, material );

			mesh.TextureAlignToGrid( mesh.Transform );
			mesh.SetSmoothingAngle( 40.0f );

			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var mc = go.Components.Create<MeshComponent>();
			mc.Mesh = mesh;

			go.Tags.Add( "mesh" );
			go.Tags.Add( "cylinder" );
			go.Tags.Add( "building" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message     = $"Created cylinder '{name}' (radius={radius}, height={height}, sides={sides}).",
				id          = go.Id.ToString(),
				name        = go.Name,
				position    = OzmiumSceneHelpers.V3( go.WorldPosition ),
				vertexCount = vertices.Count,
				faceCount   = hFaces.Count,
				material    = materialPath
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── create_arch ─────────────────────────────────────────────────────────

	private static object CreateArch( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		float width  = OzmiumSceneHelpers.Get( args, "width",  100f );
		float height = OzmiumSceneHelpers.Get( args, "height", 200f );
		float depth  = OzmiumSceneHelpers.Get( args, "depth",  50f );
		float archRadius = OzmiumSceneHelpers.Get( args, "radius", 40f );
		int archSides = OzmiumSceneHelpers.Get( args, "sides", 12 );
		string materialPath = OzmiumSceneHelpers.Get( args, "materialPath", "materials/dev/reflectivity_30.vmat" );
		string name = OzmiumSceneHelpers.Get( args, "name", "Arch" );

		archSides = Math.Clamp( archSides, 4, 32 );

		try
		{
			var hw = width / 2f;
			var hd = depth / 2f;
			var archY = height - archRadius; // Y position of arch center
			var innerRadius = archRadius;
			var thickness = depth;

			// Build arch as outer box with arched cutout
			// We build the arch frame as individual quads:
			// - Left pillar (front, back, inner, outer, top)
			// - Right pillar (front, back, inner, outer, top)
			// - Arch top (outer curve, inner curve, top, bottom)

			var vertices = new List<Vector3>();
			var faceDefs = new List<int[]>();

			// Left pillar: box from (-hw,0,0) to (-hw+thickness, archY, depth)
			float lx1 = -hw, lx2 = -hw + thickness;
			float lz1 = 0, lz2 = depth;
			int v = vertices.Count;
			vertices.AddRange( new Vector3[]
			{
				new( lx1, 0, lz1 ), new( lx2, 0, lz1 ), new( lx2, 0, lz2 ), new( lx1, 0, lz2 ),  // bottom
				new( lx1, archY, lz1 ), new( lx2, archY, lz1 ), new( lx2, archY, lz2 ), new( lx1, archY, lz2 ),  // top
			} );
			faceDefs.Add( new[] { v, v+1, v+2, v+3 } ); // bottom
			faceDefs.Add( new[] { v+4, v+5, v+6, v+7 } ); // top
			faceDefs.Add( new[] { v, v+4, v+7, v+3 } ); // front (outer)
			faceDefs.Add( new[] { v+1, v+2, v+6, v+5 } ); // back (inner)
			faceDefs.Add( new[] { v+1, v+5, v+4, v } ); // left side
			faceDefs.Add( new[] { v+2, v+3, v+7, v+6 } ); // right side

			// Right pillar: box from (hw-thickness,0,0) to (hw, archY, depth)
			float rx1 = hw - thickness, rx2 = hw;
			v = vertices.Count;
			vertices.AddRange( new Vector3[]
			{
				new( rx1, 0, lz1 ), new( rx2, 0, lz1 ), new( rx2, 0, lz2 ), new( rx1, 0, lz2 ),
				new( rx1, archY, lz1 ), new( rx2, archY, lz1 ), new( rx2, archY, lz2 ), new( rx1, archY, lz2 ),
			} );
			faceDefs.Add( new[] { v, v+1, v+2, v+3 } );
			faceDefs.Add( new[] { v+4, v+5, v+6, v+7 } );
			faceDefs.Add( new[] { v+1, v+5, v+4, v } );
			faceDefs.Add( new[] { v, v+4, v+7, v+3 } );
			faceDefs.Add( new[] { v+1, v+2, v+6, v+5 } );
			faceDefs.Add( new[] { v+2, v+3, v+7, v+6 } );

			// Arch top beam: box from (-hw, archY, 0) to (hw, height, depth)
			v = vertices.Count;
			vertices.AddRange( new Vector3[]
			{
				new( lx1, archY, lz1 ), new( rx2, archY, lz1 ), new( rx2, archY, lz2 ), new( lx1, archY, lz2 ),
				new( lx1, height, lz1 ), new( rx2, height, lz1 ), new( rx2, height, lz2 ), new( lx1, height, lz2 ),
			} );
			faceDefs.Add( new[] { v, v+1, v+2, v+3 } ); // bottom
			faceDefs.Add( new[] { v+4, v+5, v+6, v+7 } ); // top
			faceDefs.Add( new[] { v, v+4, v+7, v+3 } ); // front
			faceDefs.Add( new[] { v+1, v+2, v+6, v+5 } ); // back
			faceDefs.Add( new[] { v, v+1, v+5, v+4 } ); // bottom face
			faceDefs.Add( new[] { v+2, v+3, v+7, v+6 } ); // top face

			// Inner arch curve (semicircle) from left pillar top to right pillar top
			var archCenterX = 0f;
			var archVertsBottom = new List<int>();
			var archVertsTop = new List<int>();

			for ( int i = 0; i <= archSides; i++ )
			{
				float t = i / (float)archSides;
				float a = MathF.PI * t; // 0 to PI (left to right semicircle)
				float cx = archCenterX + MathF.Cos( a ) * (archRadius + thickness / 2f - hw + thickness / 2f);
				// Actually: center the arch at X=0, sweep from PI (left) to 0 (right)
				float ax = archCenterX - MathF.Cos( a ) * (hw - thickness);
				float ay = archY + MathF.Sin( a ) * archRadius;

				archVertsBottom.Add( vertices.Count );
				vertices.Add( new Vector3( ax, ay, lz1 ) );

				archVertsTop.Add( vertices.Count );
				vertices.Add( new Vector3( ax, ay, lz2 ) );
			}

			// Create arch curve faces
			for ( int i = 0; i < archSides; i++ )
			{
				faceDefs.Add( new[] { archVertsBottom[i], archVertsTop[i], archVertsTop[i + 1], archVertsBottom[i + 1] } );
			}

			var mesh = new PolygonMesh();
			var hVerts = mesh.AddVertices( vertices.ToArray() );
			var material = MaterialHelper.LoadMaterialOrDefault( materialPath );

			var hFaces = new List<FaceHandle>();
			foreach ( var face in faceDefs )
			{
				var fv = face.Select( idx => hVerts[idx] ).ToArray();
				hFaces.Add( mesh.AddFace( fv ) );
			}

			foreach ( var hf in hFaces )
				mesh.SetFaceMaterial( hf, material );

			mesh.TextureAlignToGrid( mesh.Transform );
			mesh.SetSmoothingAngle( 40.0f );

			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var mc = go.Components.Create<MeshComponent>();
			mc.Mesh = mesh;

			go.Tags.Add( "mesh" );
			go.Tags.Add( "arch" );
			go.Tags.Add( "building" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message     = $"Created arch '{name}' ({width}x{height}, arch radius={archRadius}).",
				id          = go.Id.ToString(),
				name        = go.Name,
				position    = OzmiumSceneHelpers.V3( go.WorldPosition ),
				vertexCount = vertices.Count,
				faceCount   = hFaces.Count,
				material    = materialPath
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── build_procedural_mesh (Omnibus) ────────────────────────────────────

	internal static object BuildProceduralMesh( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_mesh"   => CreateMesh( args ),
			"add_face"      => AddFace( args ),
			"merge"         => Merge( args ),
			"scale"         => Scale( args ),
			"extrude"       => Extrude( args ),
			"create_ramp"   => CreateRamp( args ),
			"create_cylinder" => CreateCylinder( args ),
			"create_arch"   => CreateArch( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create_mesh, add_face, merge, scale, extrude, create_ramp, create_cylinder, create_arch" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaBuildProceduralMesh
	{
		get
		{
			var props = new Dictionary<string, object>();
			props["operation"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "create_mesh", "add_face", "merge", "scale", "extrude", "create_ramp", "create_cylinder", "create_arch" } };
			props["id"]           = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of target mesh GO." };
			props["name"]         = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of target GO." };
			props["x"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position (create_ramp/create_cylinder/create_arch)." };
			props["y"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." };
			props["z"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." };
			var itemsObj = new Dictionary<string, object> { ["type"] = "object" };
			props["vertices"]     = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of {x,y,z} vertex positions (create_mesh/add_face).", ["items"] = itemsObj };
			var itemsArr = new Dictionary<string, object> { ["type"] = "array" };
			props["faces"]        = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of vertex index arrays defining faces (create_mesh).", ["items"] = itemsArr };
			props["materialPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Material path (default materials/dev/reflectivity_30.vmat)." };
			props["sourceId"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Source GO GUID for merge." };
			props["sourceName"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Source GO name for merge." };
			props["scaleX"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Scale X (default 1)." };
			props["scaleY"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Scale Y (default 1)." };
			props["scaleZ"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Scale Z (default 1)." };
			props["faceIndex"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Face index to extrude." };
			props["offsetX"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Extrude offset X (default 0)." };
			props["offsetY"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Extrude offset Y (default 100)." };
			props["offsetZ"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Extrude offset Z (default 0)." };
			props["width"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Width for ramp/cylinder/arch." };
			props["height"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Height for ramp/cylinder/arch." };
			props["depth"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Depth for ramp/arch." };
			props["angle"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Rotation angle for ramp (default 0)." };
			props["radius"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Radius for cylinder/arch." };
			props["sides"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Number of sides for cylinder/arch (default 16)." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object> { ["name"] = "build_procedural_mesh", ["description"] = "Create and manipulate procedural geometry: custom meshes from vertices/faces, ramps, cylinders, arches. Also merge, scale, and extrude existing meshes.", ["inputSchema"] = schema };
		}
	}
}
