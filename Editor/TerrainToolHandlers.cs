using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;
using Sandbox.Utility;

namespace SboxMcpServer;

/// <summary>
/// Terrain creation, editing, and analysis MCP tools.
/// Uses Terrain component, TerrainStorage, and CompactTerrainMaterial APIs.
/// </summary>
internal static class TerrainToolHandlers
{

	// 4-connectivity neighbor offsets for BFS / erosion
	private static readonly int[] N4Dx = { -1, 1, 0, 0 };
	private static readonly int[] N4Dy = { 0, 0, -1, 1 };

	// ── create ──────────────────────────────────────────────────────────────

	private static object Create( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		int resolution = OzmiumSceneHelpers.Get( args, "resolution", 512 );
		float terrainSize = OzmiumSceneHelpers.Get( args, "terrainSize", 20000f );
		float terrainHeight = OzmiumSceneHelpers.Get( args, "terrainHeight", 10000f );
		string name = OzmiumSceneHelpers.Get( args, "name", "Terrain" );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );

			var storage = new TerrainStorage();
			storage.SetResolution( resolution );
			storage.TerrainSize = terrainSize;
			storage.TerrainHeight = terrainHeight;

			var terrain = go.Components.Create<Terrain>();
			terrain.Storage = storage;

			go.Tags.Add( "terrain" );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message      = $"Created terrain '{name}' (resolution={resolution}, size={terrainSize}, height={terrainHeight}).",
				id           = go.Id.ToString(),
				name         = go.Name,
				position     = OzmiumSceneHelpers.V3( go.WorldPosition ),
				resolution,
				terrainSize,
				terrainHeight
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── terrain lookup ──────────────────────────────────────────────────────

	private static Terrain FindTerrain( Scene scene, JsonElement args )
	{
		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		GameObject go = null;
		if ( !string.IsNullOrEmpty( id ) && Guid.TryParse( id, out var guid ) )
			go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
		if ( go == null && !string.IsNullOrEmpty( name ) )
			go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g =>
				string.Equals( g.Name, name, StringComparison.OrdinalIgnoreCase ) );

		if ( go == null )
		{
			// Fall back to first terrain in scene
			go = OzmiumSceneHelpers.WalkAll( scene, true )
				.FirstOrDefault( g => g.Components.Get<Terrain>() != null );
		}

		return go?.Components.Get<Terrain>();
	}

	// ── shared helpers ──────────────────────────────────────────────────────

	private static ushort SampleHeightRaw( ushort[] heightMap, int resolution, int hx, int hy )
	{
		hx = Math.Clamp( hx, 0, resolution - 1 );
		hy = Math.Clamp( hy, 0, resolution - 1 );
		return heightMap[hy * resolution + hx];
	}

	private static (int hx, int hy)? WorldToHeightmap( Terrain terrain, float wx, float wz )
	{
		var s = terrain.Storage;
		var localPos = terrain.WorldTransform.PointToLocal( new Vector3( wx, 0, wz ) );
		float u = localPos.x / s.TerrainSize;
		float v = localPos.y / s.TerrainSize;
		if ( u < 0f || u > 1f || v < 0f || v > 1f )
			return null;
		int hx = Math.Clamp( (int)(u * s.Resolution), 0, s.Resolution - 1 );
		int hy = Math.Clamp( (int)(v * s.Resolution), 0, s.Resolution - 1 );
		return (hx, hy);
	}

	private static float RawToWorldHeight( TerrainStorage storage, ushort raw )
	{
		return raw * (storage.TerrainHeight / ushort.MaxValue);
	}

	private static Vector3 ComputeNormal( Terrain terrain, int hx, int hy )
	{
		var s = terrain.Storage;
		int res = s.Resolution;
		float heightScale = s.TerrainHeight / ushort.MaxValue;
		float texelSize = s.TerrainSize / res;

		float hL = SampleHeightRaw( s.HeightMap, res, hx - 1, hy ) * heightScale;
		float hR = SampleHeightRaw( s.HeightMap, res, hx + 1, hy ) * heightScale;
		float hD = SampleHeightRaw( s.HeightMap, res, hx, hy - 1 ) * heightScale;
		float hU = SampleHeightRaw( s.HeightMap, res, hx, hy + 1 ) * heightScale;

		var n = new Vector3( hL - hR, 2f * texelSize, hD - hU );
		n = n.Normal;
		return terrain.WorldTransform.Rotation * n;
	}

	private static string CompassFromDirection( float dx, float dz )
	{
		float angle = MathF.Atan2( dx, dz ) * (180f / MathF.PI);
		if ( angle < 0 ) angle += 360f;
		string[] dirs = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
		return dirs[(int)Math.Round( angle / 45f ) % 8];
	}

	// ── get_info ────────────────────────────────────────────────────────────

	private static object GetInfo( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var s = terrain.Storage;
		var materials = s.Materials.Select( ( m, i ) => new
		{
			index = i,
			name = m.ResourceName ?? $"Material {i}",
			surface = m.Surface?.ResourceName
		} ).ToList();

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message       = $"Terrain info for '{terrain.GameObject.Name}'.",
			id            = terrain.GameObject.Id.ToString(),
			name          = terrain.GameObject.Name,
			position      = OzmiumSceneHelpers.V3( terrain.WorldPosition ),
			resolution    = s.Resolution,
			terrainSize   = s.TerrainSize,
			terrainHeight = s.TerrainHeight,
			materialCount = s.Materials.Count,
			materials
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── get_height ──────────────────────────────────────────────────────────

	private static object GetHeight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var s = terrain.Storage;
		var localPos = terrain.WorldTransform.PointToLocal( new Vector3( wx, 0, wz ) );

		// Convert local position to heightmap UV
		var uv = new Vector2( localPos.x / s.TerrainSize, localPos.y / s.TerrainSize );
		if ( uv.x < 0 || uv.x > 1 || uv.y < 0 || uv.y > 1 )
			return OzmiumSceneHelpers.Txt( $"Position ({wx}, {wz}) is outside terrain bounds." );

		var hx = (int)(uv.x * s.Resolution);
		var hy = (int)(uv.y * s.Resolution);
		hx = Math.Clamp( hx, 0, s.Resolution - 1 );
		hy = Math.Clamp( hy, 0, s.Resolution - 1 );

		var index = hy * s.Resolution + hx;
		var rawHeight = s.HeightMap[index];
		var heightScale = s.TerrainHeight / ushort.MaxValue;
		var worldHeight = rawHeight * heightScale;

		// Convert height to world space
		var heightWorldPos = terrain.WorldTransform.PointToWorld( new Vector3( localPos.x, worldHeight, localPos.y ) );

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message     = $"Height at ({wx}, {wz}).",
			queryXZ     = new { x = wx, z = wz },
			heightmapUV = new { x = hx, y = hy },
			rawHeight,
			worldHeight = MathF.Round( heightWorldPos.y, 2 ),
			worldPosition = new { x = MathF.Round( heightWorldPos.x, 2 ), y = MathF.Round( heightWorldPos.y, 2 ), z = MathF.Round( heightWorldPos.z, 2 ) }
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── set_height / flatten helpers ────────────────────────────────────────

	private static void ModifyHeightRegion( TerrainStorage storage, Vector2 worldCenter, float worldRadius,
		float terrainSizeScale, Func<ushort, float, ushort> modifier )
	{
		var resolution = storage.Resolution;
		var halfRes = terrainSizeScale / 2f;

		// World to heightmap conversion
		int cx = (int)((worldCenter.x / storage.TerrainSize) * resolution);
		int cy = (int)((worldCenter.y / storage.TerrainSize) * resolution);
		int r = Math.Max( 1, (int)((worldRadius / storage.TerrainSize) * resolution) );

		int x0 = Math.Max( 0, cx - r );
		int y0 = Math.Max( 0, cy - r );
		int x1 = Math.Min( resolution - 1, cx + r );
		int y1 = Math.Min( resolution - 1, cy + r );

		for ( int y = y0; y <= y1; y++ )
		{
			for ( int x = x0; x <= x1; x++ )
			{
				float dx = x - cx;
				float dy = y - cy;
				float dist = MathF.Sqrt( dx * dx + dy * dy );
				if ( dist > r ) continue;

				float falloff = 1.0f - (dist / r);
				falloff = falloff * falloff * (3.0f - 2.0f * falloff); // smoothstep

				int idx = y * resolution + x;
				storage.HeightMap[idx] = modifier( storage.HeightMap[idx], falloff );
			}
		}
	}

	private static void SyncTerrain( Terrain terrain )
	{
		terrain.SyncGPUTexture();
		var region = new RectInt( 0, 0, terrain.Storage.Resolution, terrain.Storage.Resolution );
		terrain.SyncCPUTexture( Terrain.SyncFlags.Height, region );
	}

	private static object SetHeight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 100f );
		float height = OzmiumSceneHelpers.Get( args, "height", 500f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			var heightScale = s.TerrainHeight / ushort.MaxValue;
			ushort targetRaw = (ushort)Math.Clamp( height / heightScale, 0, ushort.MaxValue );

			ModifyHeightRegion( s, new Vector2( wx, wz ), radius, s.TerrainSize / s.Resolution,
				( current, falloff ) =>
				{
					float newHeight = current + (targetRaw - current) * falloff;
					return (ushort)Math.Clamp( newHeight, 0, ushort.MaxValue );
				} );

			SyncTerrain( terrain );

			return OzmiumSceneHelpers.Txt( $"Set height to {height} in a {radius}-unit radius around ({wx}, {wz})." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── flatten ─────────────────────────────────────────────────────────────

	private static object Flatten( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 100f );
		float height = OzmiumSceneHelpers.Get( args, "height", 0f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			var heightScale = s.TerrainHeight / ushort.MaxValue;
			ushort targetRaw = (ushort)Math.Clamp( height / heightScale, 0, ushort.MaxValue );

			ModifyHeightRegion( s, new Vector2( wx, wz ), radius, s.TerrainSize / s.Resolution,
				( current, falloff ) =>
				{
					float newHeight = current + (targetRaw - current) * falloff;
					return (ushort)Math.Clamp( newHeight, 0, ushort.MaxValue );
				} );

			SyncTerrain( terrain );

			return OzmiumSceneHelpers.Txt( $"Flattened terrain to height {height} in a {radius}-unit radius around ({wx}, {wz})." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── paint_material ──────────────────────────────────────────────────────

	private static object PaintMaterial( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 100f );
		byte baseTextureId = (byte)OzmiumSceneHelpers.Get( args, "baseTextureId", 0 );
		byte overlayTextureId = (byte)OzmiumSceneHelpers.Get( args, "overlayTextureId", 0 );
		byte blendFactor = (byte)OzmiumSceneHelpers.Get( args, "blendFactor", 255 );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			var resolution = s.Resolution;
			var newMat = new CompactTerrainMaterial( baseTextureId, overlayTextureId, blendFactor );

			int cx = (int)((wx / s.TerrainSize) * resolution);
			int cy = (int)((wz / s.TerrainSize) * resolution);
			int r = Math.Max( 1, (int)((radius / s.TerrainSize) * resolution) );

			int x0 = Math.Max( 0, cx - r );
			int y0 = Math.Max( 0, cy - r );
			int x1 = Math.Min( resolution - 1, cx + r );
			int y1 = Math.Min( resolution - 1, cy + r );

			int painted = 0;
			for ( int y = y0; y <= y1; y++ )
			{
				for ( int x = x0; x <= x1; x++ )
				{
					float dx = x - cx;
					float dy = y - cy;
					float dist = MathF.Sqrt( dx * dx + dy * dy );
					if ( dist > r ) continue;

					float falloff = 1.0f - (dist / r);
					falloff = falloff * falloff * (3.0f - 2.0f * falloff);

					int idx = y * resolution + x;
					var existing = new CompactTerrainMaterial( s.ControlMap[idx] );

					// Blend the blend factor
					byte newBlend = (byte)(existing.BlendFactor + (blendFactor - existing.BlendFactor) * falloff);
					var blended = new CompactTerrainMaterial( baseTextureId, overlayTextureId, newBlend );
					s.ControlMap[idx] = blended.Packed;
					painted++;
				}
			}

			terrain.SyncGPUTexture();
			var region = new RectInt( 0, 0, resolution, resolution );
			terrain.SyncCPUTexture( Terrain.SyncFlags.Control, region );

			return OzmiumSceneHelpers.Txt( $"Painted material (base={baseTextureId}, overlay={overlayTextureId}, blend={blendFactor}) on {painted} texels in {radius}-unit radius around ({wx}, {wz})." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── get_material_at ────────────────────────────────────────────────────

	private static object GetMaterialAt( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );

		var info = terrain.GetMaterialAtWorldPosition( new Vector3( wx, 0, wz ) );
		if ( info == null )
			return OzmiumSceneHelpers.Txt( $"Position ({wx}, {wz}) is outside terrain bounds." );

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message           = $"Material at ({wx}, {wz}).",
			baseTextureId     = info.Value.BaseTextureId,
			overlayTextureId  = info.Value.OverlayTextureId,
			blendFactor       = MathF.Round( info.Value.BlendFactor, 3 ),
			isHole            = info.Value.IsHole,
			baseMaterial      = info.Value.BaseMaterial?.ResourceName,
			overlayMaterial   = info.Value.OverlayMaterial?.ResourceName,
			dominantMaterial  = info.Value.GetDominantMaterial()?.ResourceName
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── get_normal ──────────────────────────────────────────────────────────

	private static object GetNormal( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float maxSlope = OzmiumSceneHelpers.Get( args, "maxSlope", 30f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var coord = WorldToHeightmap( terrain, wx, wz );
		if ( coord == null )
			return OzmiumSceneHelpers.Txt( $"Position ({wx}, {wz}) is outside terrain bounds." );

		var (hx, hy) = coord.Value;
		var normal = ComputeNormal( terrain, hx, hy );

		float slopeDeg = MathF.Acos( Math.Clamp( MathF.Abs( normal.y ), 0f, 1f ) ) * (180f / MathF.PI);
		string compass = CompassFromDirection( normal.x, normal.z );

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Surface normal at ({wx}, {wz}).",
			queryXZ = new { x = wx, z = wz },
			normal = new { x = MathF.Round( normal.x, 4 ), y = MathF.Round( normal.y, 4 ), z = MathF.Round( normal.z, 4 ) },
			slopeAngleDegrees = MathF.Round( slopeDeg, 2 ),
			compassDirection = compass,
			suitableForBuilding = slopeDeg <= maxSlope
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── sample_heights ──────────────────────────────────────────────────────

	private static object SampleHeights( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var s = terrain.Storage;

		if ( !args.TryGetProperty( "points", out var pointsElem ) || pointsElem.ValueKind != JsonValueKind.Array )
			return OzmiumSceneHelpers.Txt( "'points' must be an array of {x, z} objects." );

		var results = new List<object>();
		foreach ( var p in pointsElem.EnumerateArray() )
		{
			float px = OzmiumSceneHelpers.Get( p, "x", 0f );
			float pz = OzmiumSceneHelpers.Get( p, "z", 0f );

			var coord = WorldToHeightmap( terrain, px, pz );
			if ( coord == null )
			{
				results.Add( new { x = px, z = pz, height = (float?)null, error = "outside terrain bounds" } );
				continue;
			}

			var (hx, hy) = coord.Value;
			ushort raw = s.HeightMap[hy * s.Resolution + hx];
			float worldH = RawToWorldHeight( s, raw );

			var localPos = terrain.WorldTransform.PointToLocal( new Vector3( px, 0, pz ) );
			var worldPos = terrain.WorldTransform.PointToWorld( new Vector3( localPos.x, worldH, localPos.y ) );

			results.Add( new { x = px, z = pz, height = MathF.Round( worldPos.y, 2 ) } );
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Sampled {results.Count} points.",
			count = results.Count,
			heights = results
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── get_height_profile ──────────────────────────────────────────────────

	private static object GetHeightProfile( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float startX = OzmiumSceneHelpers.Get( args, "startX", 0f );
		float startZ = OzmiumSceneHelpers.Get( args, "startZ", 0f );
		float endX = OzmiumSceneHelpers.Get( args, "endX", 1000f );
		float endZ = OzmiumSceneHelpers.Get( args, "endZ", 0f );
		int steps = Math.Clamp( OzmiumSceneHelpers.Get( args, "steps", 20 ), 2, 200 );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var s = terrain.Storage;
		var points = new List<object>();
		float minH = float.MaxValue, maxH = float.MinValue;

		for ( int i = 0; i < steps; i++ )
		{
			float t = (float)i / (steps - 1);
			float wx = MathX.Lerp( startX, endX, t );
			float wz = MathX.Lerp( startZ, endZ, t );

			var coord = WorldToHeightmap( terrain, wx, wz );
			if ( coord == null ) continue;

			var (hx, hy) = coord.Value;
			ushort raw = s.HeightMap[hy * s.Resolution + hx];
			float worldH = RawToWorldHeight( s, raw );

			var localPos = terrain.WorldTransform.PointToLocal( new Vector3( wx, 0, wz ) );
			var worldPos = terrain.WorldTransform.PointToWorld( new Vector3( localPos.x, worldH, localPos.y ) );
			float height = worldPos.y;

			if ( height < minH ) minH = height;
			if ( height > maxH ) maxH = height;

			points.Add( new { x = MathF.Round( wx, 2 ), z = MathF.Round( wz, 2 ), height = MathF.Round( height, 2 ) } );
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Height profile from ({startX}, {startZ}) to ({endX}, {endZ}) with {points.Count} samples.",
			startPoint = new { x = startX, z = startZ },
			endPoint = new { x = endX, z = endZ },
			steps = points.Count,
			heightRange = minH < float.MaxValue
				? new { min = MathF.Round( minH, 2 ), max = MathF.Round( maxH, 2 ) }
				: null,
			points
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── find_flat_areas ─────────────────────────────────────────────────────

	private static object FindFlatAreas( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 1000f );
		float maxSlope = OzmiumSceneHelpers.Get( args, "maxSlope", 15f );
		int minSize = Math.Max( 1, OzmiumSceneHelpers.Get( args, "minSize", 5 ) );
		int maxResults = Math.Clamp( OzmiumSceneHelpers.Get( args, "maxResults", 10 ), 1, 50 );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var s = terrain.Storage;
		int res = s.Resolution;
		float heightScale = s.TerrainHeight / ushort.MaxValue;
		float texelSize = s.TerrainSize / res;
		float maxDeltaH = MathF.Tan( maxSlope * (MathF.PI / 180f) ) * texelSize;

		var center = WorldToHeightmap( terrain, wx, wz );
		if ( center == null )
			return OzmiumSceneHelpers.Txt( $"Position ({wx}, {wz}) is outside terrain bounds." );

		int cxH = center.Value.hx;
		int cyH = center.Value.hy;
		int searchR = Math.Max( 1, (int)(radius / texelSize) );

		int x0 = Math.Max( 0, cxH - searchR );
		int y0 = Math.Max( 0, cyH - searchR );
		int x1 = Math.Min( res - 1, cxH + searchR );
		int y1 = Math.Min( res - 1, cyH + searchR );

		int w = x1 - x0 + 1;
		int h = y1 - y0 + 1;

		// Pre-compute flat mask
		bool[] isFlat = new bool[w * h];
		for ( int ly = 0; ly < h; ly++ )
		{
			for ( int lx = 0; lx < w; lx++ )
			{
				int gx = x0 + lx;
				int gy = y0 + ly;
				float hC = s.HeightMap[gy * res + gx] * heightScale;

				bool flat = true;
				if ( gx > 0 && Math.Abs( s.HeightMap[gy * res + (gx - 1)] * heightScale - hC ) > maxDeltaH ) flat = false;
				else if ( gx < res - 1 && Math.Abs( s.HeightMap[gy * res + (gx + 1)] * heightScale - hC ) > maxDeltaH ) flat = false;
				else if ( gy > 0 && Math.Abs( s.HeightMap[(gy - 1) * res + gx] * heightScale - hC ) > maxDeltaH ) flat = false;
				else if ( gy < res - 1 && Math.Abs( s.HeightMap[(gy + 1) * res + gx] * heightScale - hC ) > maxDeltaH ) flat = false;

				isFlat[ly * w + lx] = flat;
			}
		}

		// BFS flood-fill to find connected flat regions
		bool[] visited = new bool[w * h];
		var areas = new List<List<(int x, int y)>>();

		for ( int ly = 0; ly < h; ly++ )
		{
			for ( int lx = 0; lx < w; lx++ )
			{
				int idx = ly * w + lx;
				if ( !isFlat[idx] || visited[idx] ) continue;

				var region = new List<(int x, int y)>();
				var queue = new Queue<(int x, int y)>();
				queue.Enqueue( (lx, ly) );
				visited[idx] = true;

				while ( queue.Count > 0 )
				{
					var (qx, qy) = queue.Dequeue();
					region.Add( (qx, qy) );

					for ( int d = 0; d < 4; d++ )
					{
						int nx = qx + N4Dx[d];
						int ny = qy + N4Dy[d];
						if ( nx < 0 || nx >= w || ny < 0 || ny >= h ) continue;
						int nIdx = ny * w + nx;
						if ( visited[nIdx] || !isFlat[nIdx] ) continue;
						visited[nIdx] = true;
						queue.Enqueue( (nx, ny) );
					}
				}

				if ( region.Count >= minSize )
					areas.Add( region );
			}
		}

		areas.Sort( ( a, b ) => b.Count - a.Count );

		var results = new List<object>();
		foreach ( var area in areas.Take( maxResults ) )
		{
			float sumX = 0, sumZ = 0, sumH = 0;
			foreach ( var (lx, ly) in area )
			{
				int gx = x0 + lx;
				int gy = y0 + ly;
				sumX += gx;
				sumZ += gy;
				sumH += s.HeightMap[gy * res + gx] * heightScale;
			}

			float avgHx = sumX / area.Count;
			float avgHy = sumZ / area.Count;
			float avgWorldH = sumH / area.Count;

			float worldX = (avgHx / res) * s.TerrainSize;
			float worldZ = (avgHy / res) * s.TerrainSize;
			var worldPos = terrain.WorldTransform.PointToWorld( new Vector3( worldX, avgWorldH, worldZ ) );

			results.Add( new
			{
				centerX = MathF.Round( worldPos.x, 2 ),
				centerZ = MathF.Round( worldPos.z, 2 ),
				averageHeight = MathF.Round( worldPos.y, 2 ),
				size = area.Count,
				approximateDiameter = MathF.Round( MathF.Sqrt( area.Count ) * texelSize, 2 )
			} );
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Found {results.Count} flat areas (searched {w}x{h} region, maxSlope={maxSlope}°, minSize={minSize}).",
			searchCenter = new { x = wx, z = wz },
			searchRadius = radius,
			maxSlope,
			minSize,
			areasFound = results.Count,
			areas = results
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── get_terrain_statistics ──────────────────────────────────────────────

	private static object GetTerrainStatistics( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		var s = terrain.Storage;
		int res = s.Resolution;
		float heightScale = s.TerrainHeight / ushort.MaxValue;
		float texelSize = s.TerrainSize / res;

		float minH = float.MaxValue, maxH = float.MinValue;
		double sumH = 0, sumSlope = 0;
		float maxSlope = 0;

		for ( int y = 0; y < res; y++ )
		{
			for ( int x = 0; x < res; x++ )
			{
				float h = s.HeightMap[y * res + x] * heightScale;
				if ( h < minH ) minH = h;
				if ( h > maxH ) maxH = h;
				sumH += h;

				float hL = SampleHeightRaw( s.HeightMap, res, x - 1, y ) * heightScale;
				float hR = SampleHeightRaw( s.HeightMap, res, x + 1, y ) * heightScale;
				float hD = SampleHeightRaw( s.HeightMap, res, x, y - 1 ) * heightScale;
				float hU = SampleHeightRaw( s.HeightMap, res, x, y + 1 ) * heightScale;

				float dhx = hR - hL;
				float dhy = hU - hD;
				float slopeDeg = MathF.Atan( MathF.Sqrt( dhx * dhx + dhy * dhy ) / (2f * texelSize) ) * (180f / MathF.PI);

				sumSlope += slopeDeg;
				if ( slopeDeg > maxSlope ) maxSlope = slopeDeg;
			}
		}

		int total = res * res;

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Terrain statistics for '{terrain.GameObject.Name}'.",
			id = terrain.GameObject.Id.ToString(),
			name = terrain.GameObject.Name,
			resolution = res,
			terrainSize = s.TerrainSize,
			minHeight = MathF.Round( minH, 2 ),
			maxHeight = MathF.Round( maxH, 2 ),
			averageHeight = MathF.Round( (float)(sumH / total), 2 ),
			heightRange = MathF.Round( maxH - minH, 2 ),
			averageSlopeDegrees = MathF.Round( (float)(sumSlope / total), 2 ),
			maxSlopeDegrees = MathF.Round( maxSlope, 2 ),
			texelCount = total,
			texelSize = MathF.Round( texelSize, 2 )
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── smooth ──────────────────────────────────────────────────────────────

	private static object SmoothTerrain( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 200f );
		float strength = Math.Clamp( OzmiumSceneHelpers.Get( args, "strength", 0.5f ), 0f, 1f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			int resolution = s.Resolution;
			float texelSize = s.TerrainSize / resolution;

			int cx = (int)((wx / s.TerrainSize) * resolution);
			int cy = (int)((wz / s.TerrainSize) * resolution);
			int r = Math.Max( 1, (int)(radius / texelSize) );

			int x0 = Math.Max( 0, cx - r );
			int y0 = Math.Max( 0, cy - r );
			int x1 = Math.Min( resolution - 1, cx + r );
			int y1 = Math.Min( resolution - 1, cy + r );

			int w = x1 - x0 + 1;
			int h = y1 - y0 + 1;

			// Snapshot the region
			ushort[] snapshot = new ushort[w * h];
			for ( int y = y0; y <= y1; y++ )
				for ( int x = x0; x <= x1; x++ )
					snapshot[(y - y0) * w + (x - x0)] = s.HeightMap[y * resolution + x];

			int modified = 0;
			for ( int y = y0; y <= y1; y++ )
			{
				for ( int x = x0; x <= x1; x++ )
				{
					float dx = x - cx;
					float dy = y - cy;
					float dist = MathF.Sqrt( dx * dx + dy * dy );
					if ( dist > r ) continue;

					float falloff = 1f - (dist / r);
					falloff = falloff * falloff * (3f - 2f * falloff);

					// 3x3 neighborhood average from snapshot
					float sum = 0;
					int count = 0;
					for ( int ny = -1; ny <= 1; ny++ )
					{
						for ( int nx = -1; nx <= 1; nx++ )
						{
							int sx = x + nx - x0;
							int sy = y + ny - y0;
							if ( sx >= 0 && sx < w && sy >= 0 && sy < h )
							{
								sum += snapshot[sy * w + sx];
								count++;
							}
						}
					}

					float avg = sum / count;
					float current = snapshot[(y - y0) * w + (x - x0)];
					float smoothed = current + (avg - current) * strength * falloff;
					s.HeightMap[y * resolution + x] = (ushort)Math.Clamp( smoothed, 0, ushort.MaxValue );
					modified++;
				}
			}

			SyncTerrain( terrain );

			return OzmiumSceneHelpers.Txt( $"Smoothed {modified} texels in {radius}-unit radius around ({wx}, {wz}) with strength {strength}." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── apply_noise ─────────────────────────────────────────────────────────

	private static object ApplyNoise( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float frequency = OzmiumSceneHelpers.Get( args, "frequency", 0.01f );
		float amplitude = OzmiumSceneHelpers.Get( args, "amplitude", 0f );
		int octaves = Math.Clamp( OzmiumSceneHelpers.Get( args, "octaves", 4 ), 1, 8 );
		float seed = OzmiumSceneHelpers.Get( args, "seed", 0f );
		float brushRadius = OzmiumSceneHelpers.Get( args, "radius", 0f );
		float brushWx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float brushWz = OzmiumSceneHelpers.Get( args, "z", 0f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			int resolution = s.Resolution;
			float heightScale = s.TerrainHeight / ushort.MaxValue;

			if ( amplitude <= 0f )
				amplitude = s.TerrainHeight * 0.3f;

			int bx0 = 0, by0 = 0, bx1 = resolution - 1, by1 = resolution - 1;
			int bcx = 0, bcy = 0, br = 0;

			if ( brushRadius > 0f )
			{
				float texelSize = s.TerrainSize / resolution;
				bcx = (int)((brushWx / s.TerrainSize) * resolution);
				bcy = (int)((brushWz / s.TerrainSize) * resolution);
				br = Math.Max( 1, (int)(brushRadius / texelSize) );
				bx0 = Math.Max( 0, bcx - br );
				by0 = Math.Max( 0, bcy - br );
				bx1 = Math.Min( resolution - 1, bcx + br );
				by1 = Math.Min( resolution - 1, bcy + br );
			}

			int modified = 0;
			for ( int y = by0; y <= by1; y++ )
			{
				for ( int x = bx0; x <= bx1; x++ )
				{
					float falloff = 1f;
					if ( brushRadius > 0f )
					{
						float dx = x - bcx;
						float dy = y - bcy;
						float dist = MathF.Sqrt( dx * dx + dy * dy );
						if ( dist > br ) continue;
						falloff = 1f - (dist / br);
						falloff = falloff * falloff * (3f - 2f * falloff);
					}

					// Noise.Fbm returns [0, 1]; center around 0.5 for bidirectional displacement
					float n = Noise.Fbm( octaves, x * frequency + seed, y * frequency );
					float heightDelta = (n - 0.5f) * amplitude * falloff;
					float rawDelta = heightDelta / heightScale;

					int idx = y * resolution + x;
					s.HeightMap[idx] = (ushort)Math.Clamp( s.HeightMap[idx] + rawDelta, 0, ushort.MaxValue );
					modified++;
				}
			}

			SyncTerrain( terrain );

			string scope = brushRadius > 0f
				? $"in {brushRadius}-unit radius around ({brushWx}, {brushWz})"
				: "on entire terrain";
			return OzmiumSceneHelpers.Txt( $"Applied noise (freq={frequency}, amp={amplitude:F0}, octaves={octaves}) {scope}. Modified {modified} texels." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── raise ───────────────────────────────────────────────────────────────

	private static object Raise( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 200f );
		float amount = OzmiumSceneHelpers.Get( args, "amount", 100f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			float heightScale = s.TerrainHeight / ushort.MaxValue;
			float rawAmount = amount / heightScale;

			ModifyHeightRegion( s, new Vector2( wx, wz ), radius, s.TerrainSize / s.Resolution,
				( current, falloff ) => (ushort)Math.Clamp( current + rawAmount * falloff, 0, ushort.MaxValue ) );

			SyncTerrain( terrain );

			return OzmiumSceneHelpers.Txt( $"Raised terrain by {amount} units in {radius}-unit radius around ({wx}, {wz})." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── terrace ─────────────────────────────────────────────────────────────

	private static object Terrace( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 500f );
		float stepHeight = OzmiumSceneHelpers.Get( args, "stepHeight", 200f );
		float blendWidth = Math.Clamp( OzmiumSceneHelpers.Get( args, "blendWidth", 0.15f ), 0f, 0.5f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			int resolution = s.Resolution;
			float heightScale = s.TerrainHeight / ushort.MaxValue;
			float texelSize = s.TerrainSize / resolution;

			int cx = (int)((wx / s.TerrainSize) * resolution);
			int cy = (int)((wz / s.TerrainSize) * resolution);
			int r = Math.Max( 1, (int)(radius / texelSize) );

			int x0 = Math.Max( 0, cx - r );
			int y0 = Math.Max( 0, cy - r );
			int x1 = Math.Min( resolution - 1, cx + r );
			int y1 = Math.Min( resolution - 1, cy + r );

			float rawStep = stepHeight / heightScale;
			if ( rawStep < 1f )
				return OzmiumSceneHelpers.Txt( $"stepHeight ({stepHeight}) is too small for this terrain's height range." );

			int modified = 0;
			for ( int y = y0; y <= y1; y++ )
			{
				for ( int x = x0; x <= x1; x++ )
				{
					float dx = x - cx;
					float dy = y - cy;
					float dist = MathF.Sqrt( dx * dx + dy * dy );
					if ( dist > r ) continue;

					float falloff = 1f - (dist / r);
					falloff = falloff * falloff * (3f - 2f * falloff);

					int idx = y * resolution + x;
					float current = s.HeightMap[idx];

					// Quantize to nearest step
					float quantized = MathF.Round( current / rawStep ) * rawStep;

					// Blend based on distance from step edge
					float blendFactor;
					if ( blendWidth <= 0f )
					{
						blendFactor = 1f;
					}
					else
					{
						float t = (current % rawStep) / rawStep;
						if ( t < 0f ) t += 1f;
						float edgeDist = Math.Min( t, 1f - t );
						if ( edgeDist >= blendWidth )
							blendFactor = 1f;
						else
						{
							float bf = edgeDist / blendWidth;
							bf = bf * bf * (3f - 2f * bf); // smoothstep
							blendFactor = bf;
						}
					}

					float result = current + (quantized - current) * blendFactor * falloff;
					s.HeightMap[idx] = (ushort)Math.Clamp( result, 0, ushort.MaxValue );
					modified++;
				}
			}

			SyncTerrain( terrain );

			return OzmiumSceneHelpers.Txt( $"Terraced {modified} texels in {radius}-unit radius around ({wx}, {wz}) with stepHeight={stepHeight}, blendWidth={blendWidth}." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── erode ───────────────────────────────────────────────────────────────

	private static object Erode( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 500f );
		int iterations = Math.Clamp( OzmiumSceneHelpers.Get( args, "iterations", 5 ), 1, 50 );
		float talusAngle = Math.Clamp( OzmiumSceneHelpers.Get( args, "talusAngle", 30f ), 1f, 89f );
		float strength = Math.Clamp( OzmiumSceneHelpers.Get( args, "strength", 0.5f ), 0.01f, 1f );

		var terrain = FindTerrain( scene, args );
		if ( terrain == null ) return OzmiumSceneHelpers.Txt( "No Terrain component found." );
		if ( terrain.Storage == null ) return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

		try
		{
			var s = terrain.Storage;
			int resolution = s.Resolution;
			float texelSize = s.TerrainSize / resolution;
			float heightScale = s.TerrainHeight / ushort.MaxValue;

			int cx = (int)((wx / s.TerrainSize) * resolution);
			int cy = (int)((wz / s.TerrainSize) * resolution);
			int r = Math.Max( 1, (int)(radius / texelSize) );

			int x0 = Math.Max( 0, cx - r );
			int y0 = Math.Max( 0, cy - r );
			int x1 = Math.Min( resolution - 1, cx + r );
			int y1 = Math.Min( resolution - 1, cy + r );

			int w = x1 - x0 + 1;
			int h = y1 - y0 + 1;

			// Precompute brush falloff mask
			float[] falloff = new float[w * h];
			for ( int ly = 0; ly < h; ly++ )
			{
				for ( int lx = 0; lx < w; lx++ )
				{
					float dx = (x0 + lx) - cx;
					float dy = (y0 + ly) - cy;
					float dist = MathF.Sqrt( dx * dx + dy * dy );
					if ( dist > r )
						falloff[ly * w + lx] = 0f;
					else
					{
						float f = 1f - dist / r;
						falloff[ly * w + lx] = f * f * (3f - 2f * f);
					}
				}
			}

			// Snapshot original heights
			ushort[] original = new ushort[w * h];
			for ( int y = y0; y <= y1; y++ )
				for ( int x = x0; x <= x1; x++ )
					original[(y - y0) * w + (x - x0)] = s.HeightMap[y * resolution + x];

			// Talus threshold in raw height units
			float talusRaw = MathF.Tan( talusAngle * (MathF.PI / 180f) ) * texelSize / heightScale;

			// Double-buffer for erosion iterations
			float[] read = new float[w * h];
			float[] write = new float[w * h];
			float[] delta = new float[w * h];
			for ( int i = 0; i < w * h; i++ )
				read[i] = original[i];

			for ( int iter = 0; iter < iterations; iter++ )
			{
				Array.Clear( delta, 0, delta.Length );

				for ( int ly = 0; ly < h; ly++ )
				{
					for ( int lx = 0; lx < w; lx++ )
					{
						int idx = ly * w + lx;
						if ( falloff[idx] <= 0f ) continue;

						float current = read[idx];
						float totalSend = 0f;
						int lowerCount = 0;
						int lowerIdx0 = -1, lowerIdx1 = -1, lowerIdx2 = -1, lowerIdx3 = -1;

						for ( int d = 0; d < 4; d++ )
						{
							int nlx = lx + N4Dx[d];
							int nly = ly + N4Dy[d];
							if ( nlx < 0 || nlx >= w || nly < 0 || nly >= h ) continue;
							int nIdx = nly * w + nlx;
							if ( falloff[nIdx] <= 0f ) continue;

							float diff = current - read[nIdx];
							if ( diff > talusRaw )
							{
								totalSend += (diff - talusRaw) * strength;
								switch ( lowerCount )
								{
									case 0: lowerIdx0 = nIdx; break;
									case 1: lowerIdx1 = nIdx; break;
									case 2: lowerIdx2 = nIdx; break;
									case 3: lowerIdx3 = nIdx; break;
								}
								lowerCount++;
							}
						}

						if ( lowerCount > 0 )
						{
							float perNeighbor = totalSend / lowerCount;
							delta[idx] -= totalSend;
							if ( lowerIdx0 >= 0 ) delta[lowerIdx0] += perNeighbor;
							if ( lowerIdx1 >= 0 ) delta[lowerIdx1] += perNeighbor;
							if ( lowerIdx2 >= 0 ) delta[lowerIdx2] += perNeighbor;
							if ( lowerIdx3 >= 0 ) delta[lowerIdx3] += perNeighbor;
						}
					}
				}

				// Apply deltas
				for ( int i = 0; i < w * h; i++ )
					write[i] = read[i] + delta[i];

				// Swap buffers
				var tmp = read;
				read = write;
				write = tmp;
			}

			// Blend between original and eroded using falloff
			int modified = 0;
			for ( int y = y0; y <= y1; y++ )
			{
				for ( int x = x0; x <= x1; x++ )
				{
					int idx = (y - y0) * w + (x - x0);
					float f = falloff[idx];
					if ( f <= 0f ) continue;
					float result = original[idx] * (1f - f) + read[idx] * f;
					s.HeightMap[y * resolution + x] = (ushort)Math.Clamp( result, 0, ushort.MaxValue );
					modified++;
				}
			}

			SyncTerrain( terrain );

			return OzmiumSceneHelpers.Txt( $"Applied {iterations} erosion iterations in {radius}-unit radius around ({wx}, {wz}). Modified {modified} texels." );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── manage_terrain (Omnibus) ───────────────────────────────────────────

	internal static object ManageTerrain( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create"                => Create( args ),
			"get_info"              => GetInfo( args ),
			"get_height"            => GetHeight( args ),
			"set_height"            => SetHeight( args ),
			"flatten"               => Flatten( args ),
			"paint_material"        => PaintMaterial( args ),
			"get_material_at"       => GetMaterialAt( args ),
			"get_normal"            => GetNormal( args ),
			"sample_heights"        => SampleHeights( args ),
			"get_height_profile"    => GetHeightProfile( args ),
			"find_flat_areas"       => FindFlatAreas( args ),
			"get_terrain_statistics" => GetTerrainStatistics( args ),
			"smooth"                => SmoothTerrain( args ),
			"apply_noise"           => ApplyNoise( args ),
			"raise"                 => Raise( args ),
			"terrace"               => Terrace( args ),
			"erode"                 => Erode( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create, get_info, get_height, set_height, flatten, paint_material, get_material_at, get_normal, sample_heights, get_height_profile, find_flat_areas, get_terrain_statistics, smooth, apply_noise, raise, terrace, erode" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageTerrain
	{
		get
		{
			var props = new Dictionary<string, object>();
			props["operation"]        = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "create", "get_info", "get_height", "set_height", "flatten", "paint_material", "get_material_at", "get_normal", "sample_heights", "get_height_profile", "find_flat_areas", "get_terrain_statistics", "smooth", "apply_noise", "raise", "terrace", "erode" } };
			props["id"]               = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of the terrain GameObject (optional, defaults to first terrain in scene)." };
			props["name"]             = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name of the terrain GO." };
			props["x"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position (create) or XZ center for height/paint/noise operations." };
			props["y"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position (create only)." };
			props["z"]                = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position (create) or Z for height/paint/noise operations." };
			props["resolution"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Heightmap resolution (create, default 512)." };
			props["terrainSize"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World width/length of terrain (create, default 20000)." };
			props["terrainHeight"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max world height of terrain (create, default 10000)." };
			props["radius"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Brush radius for set_height/flatten/paint_material/smooth/raise/terrace/erode. For apply_noise, 0 = entire terrain (default)." };
			props["height"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Target height for set_height/flatten." };
			props["baseTextureId"]    = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Base texture ID 0-31 for paint_material." };
			props["overlayTextureId"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Overlay texture ID 0-31 for paint_material." };
			props["blendFactor"]      = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Blend factor 0-255 for paint_material." };
			props["maxSlope"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max slope in degrees for get_normal (suitableForBuilding threshold, default 30) and find_flat_areas (default 15)." };
			props["points"]           = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of {x, z} objects for sample_heights.", ["items"] = new Dictionary<string, object> { ["type"] = "object", ["properties"] = new Dictionary<string, object> { ["x"] = new Dictionary<string, object> { ["type"] = "number" }, ["z"] = new Dictionary<string, object> { ["type"] = "number" } } } };
			props["startX"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Start X for get_height_profile." };
			props["startZ"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Start Z for get_height_profile." };
			props["endX"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "End X for get_height_profile (default 1000)." };
			props["endZ"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "End Z for get_height_profile." };
			props["steps"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Number of samples for get_height_profile (default 20, max 200)." };
			props["minSize"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Minimum texel count for find_flat_areas (default 5)." };
			props["maxResults"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max areas to return for find_flat_areas (default 10, max 50)." };
			props["strength"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Strength for smooth (0-1, default 0.5) and erode (0.01-1, default 0.5)." };
			props["frequency"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Noise frequency for apply_noise (default 0.01)." };
			props["amplitude"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Noise amplitude in world height units for apply_noise (default terrainHeight*0.3)." };
			props["octaves"]          = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Noise octaves for apply_noise FBM (1-8, default 4)." };
			props["seed"]             = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Noise seed offset for apply_noise (default 0)." };
			props["amount"]           = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Height amount to add for raise (positive = up, default 100)." };
			props["stepHeight"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World units between terrace steps (default 200)." };
			props["blendWidth"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Terrace edge smoothing 0-0.5 for terrace (default 0.15)." };
			props["iterations"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Erosion iterations for erode (1-50, default 5)." };
			props["talusAngle"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max stable slope angle in degrees for erode (1-89, default 30)." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object>
			{
				["name"] = "manage_terrain",
				["description"] = "Create, query, sculpt, paint, and analyze terrain. Create terrain with resolution/size/height, sculpt heightmaps, paint splatmap materials, analyze normals/slopes/flat areas, apply noise, smooth, raise, terrace, and erode.",
				["inputSchema"] = schema
			};
		}
	}
}
