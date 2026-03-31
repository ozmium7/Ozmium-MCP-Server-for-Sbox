using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Scene spatial query MCP tools: ray casts, shape sweeps, volume overlaps,
/// and terrain height sampling. Uses Scene.Trace and Scene.FindInPhysics.
/// </summary>
internal static class SceneQueryToolHandlers
{

	// ── ray ────────────────────────────────────────────────────────────────

	private static object TraceRay( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float sx = OzmiumSceneHelpers.Get( args, "startX", 0f );
		float sy = OzmiumSceneHelpers.Get( args, "startY", 0f );
		float sz = OzmiumSceneHelpers.Get( args, "startZ", 0f );
		float ex = OzmiumSceneHelpers.Get( args, "endX", 0f );
		float ey = OzmiumSceneHelpers.Get( args, "endY", -1000f );
		float ez = OzmiumSceneHelpers.Get( args, "endZ", 0f );

		var start = new Vector3( sx, sy, sz );
		var end   = new Vector3( ex, ey, ez );

		try
		{
			var trace = scene.Trace.Ray( start, end ).UseHitboxes();

			bool hitTriggers = OzmiumSceneHelpers.Get( args, "hitTriggers", false );
			if ( hitTriggers ) trace = trace.HitTriggers();

			string withTag = OzmiumSceneHelpers.Get( args, "withTag", (string)null );
			if ( !string.IsNullOrEmpty( withTag ) ) trace = trace.WithTag( withTag );

			string ignoreId = OzmiumSceneHelpers.Get( args, "ignoreId", (string)null );
			if ( !string.IsNullOrEmpty( ignoreId ) && Guid.TryParse( ignoreId, out var guid ) )
			{
				var ignoreGo = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( ignoreGo != null ) trace = trace.IgnoreGameObjectHierarchy( ignoreGo );
			}

			var result = trace.Run();

			if ( !result.Hit )
				return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
				{
					hit = false,
					start = OzmiumSceneHelpers.V3( start ),
					end   = OzmiumSceneHelpers.V3( end ),
					distance = Vector3.DistanceBetween( start, end )
				}, OzmiumSceneHelpers.JsonSettings ) );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				hit           = true,
				startPosition = OzmiumSceneHelpers.V3( result.StartPosition ),
				endPosition   = OzmiumSceneHelpers.V3( result.EndPosition ),
				hitPosition   = OzmiumSceneHelpers.V3( result.HitPosition ),
				normal        = OzmiumSceneHelpers.V3( result.Normal ),
				distance      = MathF.Round( result.Distance, 2 ),
				fraction      = MathF.Round( result.Fraction, 4 ),
				gameObject    = result.GameObject != null
					? new { id = result.GameObject.Id.ToString(), name = result.GameObject.Name }
					: null,
				component     = result.Component?.GetType().Name,
				surface       = result.Surface?.ResourceName,
				startedSolid  = result.StartedSolid,
				tags          = result.Tags,
				direction     = OzmiumSceneHelpers.V3( result.Direction )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── sphere_trace ───────────────────────────────────────────────────────

	private static object TraceSphere( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float sx = OzmiumSceneHelpers.Get( args, "startX", 0f );
		float sy = OzmiumSceneHelpers.Get( args, "startY", 0f );
		float sz = OzmiumSceneHelpers.Get( args, "startZ", 0f );
		float ex = OzmiumSceneHelpers.Get( args, "endX", 0f );
		float ey = OzmiumSceneHelpers.Get( args, "endY", -1000f );
		float ez = OzmiumSceneHelpers.Get( args, "endZ", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 10f );

		var start = new Vector3( sx, sy, sz );
		var end   = new Vector3( ex, ey, ez );

		try
		{
			var trace = scene.Trace.Sphere( radius, start, end ).UseHitboxes();

			bool hitTriggers = OzmiumSceneHelpers.Get( args, "hitTriggers", false );
			if ( hitTriggers ) trace = trace.HitTriggers();

			string ignoreId = OzmiumSceneHelpers.Get( args, "ignoreId", (string)null );
			if ( !string.IsNullOrEmpty( ignoreId ) && Guid.TryParse( ignoreId, out var guid ) )
			{
				var ignoreGo = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( ignoreGo != null ) trace = trace.IgnoreGameObjectHierarchy( ignoreGo );
			}

			var result = trace.Run();

			if ( !result.Hit )
				return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
				{
					hit    = false,
					radius = radius,
					distance = Vector3.DistanceBetween( start, end )
				}, OzmiumSceneHelpers.JsonSettings ) );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				hit           = true,
				hitPosition   = OzmiumSceneHelpers.V3( result.HitPosition ),
				normal        = OzmiumSceneHelpers.V3( result.Normal ),
				distance      = MathF.Round( result.Distance, 2 ),
				fraction      = MathF.Round( result.Fraction, 4 ),
				gameObject    = result.GameObject != null
					? new { id = result.GameObject.Id.ToString(), name = result.GameObject.Name }
					: null,
				surface       = result.Surface?.ResourceName,
				startedSolid  = result.StartedSolid
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── box_trace ──────────────────────────────────────────────────────────

	private static object TraceBox( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float sx = OzmiumSceneHelpers.Get( args, "startX", 0f );
		float sy = OzmiumSceneHelpers.Get( args, "startY", 0f );
		float sz = OzmiumSceneHelpers.Get( args, "startZ", 0f );
		float ex = OzmiumSceneHelpers.Get( args, "endX", 0f );
		float ey = OzmiumSceneHelpers.Get( args, "endY", -1000f );
		float ez = OzmiumSceneHelpers.Get( args, "endZ", 0f );
		float bsx = OzmiumSceneHelpers.Get( args, "sizeX", 10f );
		float bsy = OzmiumSceneHelpers.Get( args, "sizeY", 10f );
		float bsz = OzmiumSceneHelpers.Get( args, "sizeZ", 10f );

		var start = new Vector3( sx, sy, sz );
		var end   = new Vector3( ex, ey, ez );

		try
		{
			var trace = scene.Trace.Box( new Vector3( bsx, bsy, bsz ), start, end ).UseHitboxes();

			bool hitTriggers = OzmiumSceneHelpers.Get( args, "hitTriggers", false );
			if ( hitTriggers ) trace = trace.HitTriggers();

			string ignoreId = OzmiumSceneHelpers.Get( args, "ignoreId", (string)null );
			if ( !string.IsNullOrEmpty( ignoreId ) && Guid.TryParse( ignoreId, out var guid ) )
			{
				var ignoreGo = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( ignoreGo != null ) trace = trace.IgnoreGameObjectHierarchy( ignoreGo );
			}

			var result = trace.Run();

			if ( !result.Hit )
				return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
				{
					hit    = false,
					size   = new { x = bsx, y = bsy, z = bsz },
					distance = Vector3.DistanceBetween( start, end )
				}, OzmiumSceneHelpers.JsonSettings ) );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				hit           = true,
				hitPosition   = OzmiumSceneHelpers.V3( result.HitPosition ),
				normal        = OzmiumSceneHelpers.V3( result.Normal ),
				distance      = MathF.Round( result.Distance, 2 ),
				fraction      = MathF.Round( result.Fraction, 4 ),
				gameObject    = result.GameObject != null
					? new { id = result.GameObject.Id.ToString(), name = result.GameObject.Name }
					: null,
				surface       = result.Surface?.ResourceName,
				startedSolid  = result.StartedSolid
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── sphere_overlap ─────────────────────────────────────────────────────

	private static object SphereOverlap( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float cx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float cy = OzmiumSceneHelpers.Get( args, "y", 0f );
		float cz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float radius = OzmiumSceneHelpers.Get( args, "radius", 100f );
		int max = OzmiumSceneHelpers.Get( args, "maxResults", 50 );

		var center = new Vector3( cx, cy, cz );

		try
		{
			var sphere = new Sphere( center, radius );
			var results = scene.FindInPhysics( sphere )
				.Select( go => OzmiumSceneHelpers.BuildSummary( go ) )
				.Take( max )
				.ToList();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				summary = $"Found {results.Count} object(s) within sphere (center={cx},{cy},{cz} radius={radius})",
				center = OzmiumSceneHelpers.V3( center ),
				radius,
				results
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── box_overlap ────────────────────────────────────────────────────────

	private static object BoxOverlap( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float cx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float cy = OzmiumSceneHelpers.Get( args, "y", 0f );
		float cz = OzmiumSceneHelpers.Get( args, "z", 0f );
		float hsx = OzmiumSceneHelpers.Get( args, "sizeX", 100f ) / 2f;
		float hsy = OzmiumSceneHelpers.Get( args, "sizeY", 100f ) / 2f;
		float hsz = OzmiumSceneHelpers.Get( args, "sizeZ", 100f ) / 2f;
		int max = OzmiumSceneHelpers.Get( args, "maxResults", 50 );

		var center = new Vector3( cx, cy, cz );
		var bbox = new BBox( center - new Vector3( hsx, hsy, hsz ), center + new Vector3( hsx, hsy, hsz ) );

		try
		{
			var results = scene.FindInPhysics( bbox )
				.Select( go => OzmiumSceneHelpers.BuildSummary( go ) )
				.Take( max )
				.ToList();

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				summary = $"Found {results.Count} object(s) within box (center={cx},{cy},{cz} size={hsx*2},{hsy*2},{hsz*2})",
				center = OzmiumSceneHelpers.V3( center ),
				size = new { x = hsx * 2, y = hsy * 2, z = hsz * 2 },
				results
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception e ) { return OzmiumSceneHelpers.Txt( $"Error: {e.Message}" ); }
	}

	// ── terrain_height ─────────────────────────────────────────────────────

	private static object TerrainHeight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float wx = OzmiumSceneHelpers.Get( args, "x", 0f );
		float wz = OzmiumSceneHelpers.Get( args, "z", 0f );

		try
		{
			var terrain = OzmiumSceneHelpers.WalkAll( scene, true )
				.Select( go => go.Components.Get<Terrain>() )
				.FirstOrDefault( t => t != null );

			if ( terrain == null )
				return OzmiumSceneHelpers.Txt( "No Terrain component found in scene." );

			if ( terrain.Storage == null )
				return OzmiumSceneHelpers.Txt( "Terrain has no storage." );

			var s = terrain.Storage;
			var localPos = terrain.WorldTransform.PointToLocal( new Vector3( wx, 0, wz ) );
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

			var heightWorldPos = terrain.WorldTransform.PointToWorld( new Vector3( localPos.x, worldHeight, localPos.y ) );

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				terrainPosition = OzmiumSceneHelpers.V3( terrain.WorldPosition ),
				queryXZ = new { x = wx, z = wz },
				height = MathF.Round( heightWorldPos.y, 2 ),
				worldPosition = new { x = MathF.Round( heightWorldPos.x, 2 ), y = MathF.Round( heightWorldPos.y, 2 ), z = MathF.Round( heightWorldPos.z, 2 ) }
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception exT ) { return OzmiumSceneHelpers.Txt( $"Error: {exT.Message}" ); }
	}

	// ── scene_trace (Omnibus) ──────────────────────────────────────────────

	internal static object SceneTrace( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"ray"            => TraceRay( args ),
			"sphere_trace"   => TraceSphere( args ),
			"box_trace"      => TraceBox( args ),
			"sphere_overlap" => SphereOverlap( args ),
			"box_overlap"    => BoxOverlap( args ),
			"terrain_height" => TerrainHeight( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: ray, sphere_trace, box_trace, sphere_overlap, box_overlap, terrain_height" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaSceneTrace
	{
		get
		{
			var props = new Dictionary<string, object>();
			props["operation"]    = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Operation to perform.", ["enum"] = new[] { "ray", "sphere_trace", "box_trace", "sphere_overlap", "box_overlap", "terrain_height" } };
			props["startX"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Start X (ray/sweep start, or center for overlaps)." };
			props["startY"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Start Y (ray/sweep start, or center for overlaps)." };
			props["startZ"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Start Z (ray/sweep start, or center for overlaps)." };
			props["endX"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "End X for ray/sweep traces." };
			props["endY"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "End Y for ray/sweep traces." };
			props["endZ"]         = new Dictionary<string, object> { ["type"] = "number", ["description"] = "End Z for ray/sweep traces." };
			props["x"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Center X for overlap/terrain queries." };
			props["y"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Center Y for overlap queries." };
			props["z"]            = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Center Z for overlap/terrain queries." };
			props["radius"]       = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Sphere radius for sphere_trace or sphere_overlap." };
			props["sizeX"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box X size for box_trace or box_overlap." };
			props["sizeY"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box Y size for box_trace or box_overlap." };
			props["sizeZ"]        = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box Z size for box_trace or box_overlap." };
			props["hitTriggers"]  = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether trace hits triggers (default false)." };
			props["ignoreId"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID of GameObject to ignore (and its hierarchy)." };
			props["withTag"]      = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Only hit objects with this tag." };
			props["maxResults"]   = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max results for overlap queries (default 50)." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object> { ["name"] = "scene_trace", ["description"] = "Cast rays, sweep shapes, find overlaps, or query terrain height. Use 'ray' to align objects to surfaces, 'sphere_overlap'/'box_overlap' for volume queries, 'terrain_height' to sample terrain.", ["inputSchema"] = schema };
		}
	}
}
