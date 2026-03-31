using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Scene building automation MCP tools: scatter prefabs, bulk replace prefab
/// instances, align objects to ground, and randomize transforms.
/// </summary>
internal static class BuildAutomationToolHandlers
{

	private const int MaxScatterCount = 500;

	// ── scatter_objects ────────────────────────────────────────────────────

	private static object ScatterObjects( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string prefabPath = OzmiumSceneHelpers.Get( args, "prefabPath", (string)null );
		int count = OzmiumSceneHelpers.Get( args, "count", 10 );
		float minX = OzmiumSceneHelpers.Get( args, "boundsMin", new Vector3( 0, 0, 0 ) ).x;
		float minY = OzmiumSceneHelpers.Get( args, "boundsMin", new Vector3( 0, 0, 0 ) ).y;
		float minZ = OzmiumSceneHelpers.Get( args, "boundsMin", new Vector3( 0, 0, 0 ) ).z;
		float maxX = OzmiumSceneHelpers.Get( args, "boundsMax", new Vector3( 1000, 0, 1000 ) ).x;
		float maxY = OzmiumSceneHelpers.Get( args, "boundsMax", new Vector3( 1000, 0, 1000 ) ).y;
		float maxZ = OzmiumSceneHelpers.Get( args, "boundsMax", new Vector3( 1000, 0, 1000 ) ).z;

		bool randomRotation = OzmiumSceneHelpers.Get( args, "randomRotation", false );
		bool randomScale = OzmiumSceneHelpers.Get( args, "randomScale", false );
		float scaleMin = OzmiumSceneHelpers.Get( args, "scaleMin", 0.8f );
		float scaleMax = OzmiumSceneHelpers.Get( args, "scaleMax", 1.2f );
		bool alignToGround = OzmiumSceneHelpers.Get( args, "alignToGround", false );
		float alignMaxDistance = OzmiumSceneHelpers.Get( args, "alignMaxDistance", 10000f );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );
		string namePrefix = OzmiumSceneHelpers.Get( args, "namePrefix", (string)null );
		int seed = OzmiumSceneHelpers.Get( args, "seed", 0 );

		if ( string.IsNullOrEmpty( prefabPath ) )
			return OzmiumSceneHelpers.Txt( "Provide 'prefabPath'." );
		if ( count <= 0 ) return OzmiumSceneHelpers.Txt( "'count' must be positive." );
		if ( count > MaxScatterCount ) count = MaxScatterCount;

		// Load prefab once
		var asset = AssetSystem.FindByPath( prefabPath );
		if ( asset == null )
			return OzmiumSceneHelpers.Txt( $"Asset not found: '{prefabPath}'. Use browse_assets with type='prefab' to find valid paths." );
		var prefabFile = ResourceLibrary.Get<PrefabFile>( prefabPath );
		if ( prefabFile == null )
			return OzmiumSceneHelpers.Txt( $"Could not load prefab: '{prefabPath}'." );
		var prefabScene = SceneUtility.GetPrefabScene( prefabFile );

		// Resolve optional parent
		GameObject parent = null;
		if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var parentGuid ) )
			parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == parentGuid );

		var rng = seed != 0 ? new Random( seed ) : new Random();
		var created = new List<Dictionary<string, object>>();

		try
		{
			for ( int i = 0; i < count; i++ )
			{
				var go = prefabScene.Clone();

				// Random position within bounds
				float px = minX + (float)rng.NextDouble() * (maxX - minX);
				float py = minY + (float)rng.NextDouble() * (maxY - minY);
				float pz = minZ + (float)rng.NextDouble() * (maxZ - minZ);
				go.WorldPosition = new Vector3( px, py, pz );

				// Optional ground alignment
				if ( alignToGround )
				{
					var hit = scene.Trace.Ray( go.WorldPosition, go.WorldPosition + Vector3.Down * alignMaxDistance ).Run();
					if ( hit.Hit )
						go.WorldPosition = hit.HitPosition + Vector3.Up;
				}

				// Optional random rotation
				if ( randomRotation )
					go.WorldRotation = Rotation.From( (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f );

				// Optional random scale
				if ( randomScale )
				{
					float s = scaleMin + (float)rng.NextDouble() * (scaleMax - scaleMin);
					go.WorldScale = new Vector3( s, s, s );
				}

				// Optional parent
				if ( parent != null ) go.SetParent( parent );

				// Optional name prefix
				if ( !string.IsNullOrEmpty( namePrefix ) )
				{
					go.Name = $"{namePrefix}_{go.Name}";
					go.MakeNameUnique();
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
				message = $"Scattered {created.Count} instances of '{prefabPath}'.",
				count = created.Count,
				bounds = new
				{
					min = new { x = minX, y = minY, z = minZ },
					max = new { x = maxX, y = maxY, z = maxZ }
				},
				alignToGround,
				created
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── replace_prefab_instances ───────────────────────────────────────────

	private static object ReplacePrefabInstances( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string sourcePrefabPath = OzmiumSceneHelpers.Get( args, "sourcePrefabPath", (string)null );
		string targetPrefabPath = OzmiumSceneHelpers.Get( args, "targetPrefabPath", (string)null );
		bool preserveTransform = OzmiumSceneHelpers.Get( args, "preserveTransform", true );

		if ( string.IsNullOrEmpty( sourcePrefabPath ) ) return OzmiumSceneHelpers.Txt( "Provide 'sourcePrefabPath'." );
		if ( string.IsNullOrEmpty( targetPrefabPath ) ) return OzmiumSceneHelpers.Txt( "Provide 'targetPrefabPath'." );

		// Load target prefab once
		var targetAsset = AssetSystem.FindByPath( targetPrefabPath );
		if ( targetAsset == null )
			return OzmiumSceneHelpers.Txt( $"Target asset not found: '{targetPrefabPath}'." );
		var targetPrefabFile = ResourceLibrary.Get<PrefabFile>( targetPrefabPath );
		if ( targetPrefabFile == null )
			return OzmiumSceneHelpers.Txt( $"Could not load target prefab: '{targetPrefabPath}'." );
		var targetPrefabScene = SceneUtility.GetPrefabScene( targetPrefabFile );

		// Collect all source instances first (avoid modifying collection during iteration)
		var allObjects = OzmiumSceneHelpers.WalkAll( scene, true ).ToList();
		var toReplace = allObjects
			.Where( g => g.IsPrefabInstance
				&& g.PrefabInstanceSource != null
				&& g.PrefabInstanceSource.IndexOf( sourcePrefabPath, StringComparison.OrdinalIgnoreCase ) >= 0 )
			.Select( g => new
			{
				go = g,
				pos = g.WorldPosition,
				rot = g.WorldRotation,
				scl = g.WorldScale,
				parent = g.Parent,
				enabled = g.Enabled
			} )
			.ToList();

		if ( toReplace.Count == 0 )
			return OzmiumSceneHelpers.Txt( $"No instances of '{sourcePrefabPath}' found in scene." );

		try
		{
			var replaced = new List<Dictionary<string, object>>();

			foreach ( var entry in toReplace )
			{
				// Destroy source
				try { entry.go.Destroy(); } catch { }

				// Instantiate target
				var newGo = targetPrefabScene.Clone();

				if ( preserveTransform )
				{
					newGo.WorldPosition = entry.pos;
					newGo.WorldRotation = entry.rot;
					newGo.WorldScale = entry.scl;
				}

				if ( entry.parent != null )
					newGo.SetParent( entry.parent );

				newGo.Name = newGo.Name;
				newGo.MakeNameUnique();
				newGo.Enabled = entry.enabled;

				replaced.Add( new Dictionary<string, object>
				{
					["id"] = newGo.Id.ToString(),
					["name"] = newGo.Name,
					["position"] = OzmiumSceneHelpers.V3( newGo.WorldPosition )
				} );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Replaced {replaced.Count} instance(s) of '{sourcePrefabPath}' with '{targetPrefabPath}'.",
				sourcePrefabPath,
				targetPrefabPath,
				preserveTransform,
				replaced
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── align_to_ground ────────────────────────────────────────────────────

	private static object AlignToGround( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float offsetY = OzmiumSceneHelpers.Get( args, "offsetY", 0f );
		float maxDistance = OzmiumSceneHelpers.Get( args, "maxDistance", 10000f );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		int found = 0, aligned = 0, missed = 0;
		var updated = new List<Dictionary<string, object>>();

		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;
			found++;

			var hit = scene.Trace.Ray( go.WorldPosition, go.WorldPosition + Vector3.Down * maxDistance ).Run();
			if ( hit.Hit )
			{
				go.WorldPosition = hit.HitPosition + Vector3.Up * offsetY;
				aligned++;
				updated.Add( new Dictionary<string, object>
				{
					["id"] = go.Id.ToString(),
					["name"] = go.Name,
					["position"] = OzmiumSceneHelpers.V3( go.WorldPosition )
				} );
			}
			else
			{
				missed++;
			}
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Aligned {aligned} object(s) to ground ({missed} missed, found {found} of {ids.Count}).",
			aligned,
			missed,
			offsetY,
			updated
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── randomize_transforms ───────────────────────────────────────────────

	private static object RandomizeTransforms( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		bool randomRotationY = OzmiumSceneHelpers.Get( args, "randomRotationY", false );
		bool randomRotationX = OzmiumSceneHelpers.Get( args, "randomRotationX", false );
		bool randomRotationZ = OzmiumSceneHelpers.Get( args, "randomRotationZ", false );
		bool randomScale = OzmiumSceneHelpers.Get( args, "randomScale", false );
		float scaleMin = OzmiumSceneHelpers.Get( args, "scaleMin", 0.8f );
		float scaleMax = OzmiumSceneHelpers.Get( args, "scaleMax", 1.2f );
		int seed = OzmiumSceneHelpers.Get( args, "seed", 0 );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		if ( !randomRotationY && !randomRotationX && !randomRotationZ && !randomScale )
			return OzmiumSceneHelpers.Txt( "Enable at least one randomization axis (randomRotationY/X/Z, randomScale)." );

		var rng = seed != 0 ? new Random( seed ) : new Random();
		int found = 0, modified = 0;
		var updated = new List<Dictionary<string, object>>();

		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;
			found++;

			var rot = go.WorldRotation;
			var pitch = rot.Pitch();
			var yaw = rot.Yaw();
			var roll = rot.Roll();

			if ( randomRotationX ) pitch = (float)rng.NextDouble() * 360f;
			if ( randomRotationY ) yaw = (float)rng.NextDouble() * 360f;
			if ( randomRotationZ ) roll = (float)rng.NextDouble() * 360f;

			go.WorldRotation = Rotation.From( pitch, yaw, roll );

			if ( randomScale )
			{
				float s = scaleMin + (float)rng.NextDouble() * (scaleMax - scaleMin);
				go.WorldScale = new Vector3( s, s, s );
			}

			modified++;
			updated.Add( new Dictionary<string, object>
			{
				["id"] = go.Id.ToString(),
				["name"] = go.Name,
				["position"] = OzmiumSceneHelpers.V3( go.WorldPosition ),
				["rotation"] = OzmiumSceneHelpers.Rot( go.WorldRotation ),
				["scale"] = OzmiumSceneHelpers.V3( go.WorldScale )
			} );
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Randomized transforms on {modified} object(s) (found {found} of {ids.Count}).",
			modified,
			updated
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── snap_to_grid ───────────────────────────────────────────────────────

	private static object SnapToGrid( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float gridSize = OzmiumSceneHelpers.Get( args, "gridSize", 100f );
		bool snapX = OzmiumSceneHelpers.Get( args, "snapX", true );
		bool snapY = OzmiumSceneHelpers.Get( args, "snapY", false );
		bool snapZ = OzmiumSceneHelpers.Get( args, "snapZ", true );
		var origin = OzmiumSceneHelpers.Get( args, "origin", new Vector3( 0, 0, 0 ) );

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		int found = 0, snapped = 0;
		var updated = new List<Dictionary<string, object>>();

		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go == null ) continue;
			found++;

			var pos = go.WorldPosition;
			if ( snapX ) pos.x = MathF.Round( (pos.x - origin.x) / gridSize ) * gridSize + origin.x;
			if ( snapY ) pos.y = MathF.Round( (pos.y - origin.y) / gridSize ) * gridSize + origin.y;
			if ( snapZ ) pos.z = MathF.Round( (pos.z - origin.z) / gridSize ) * gridSize + origin.z;

			go.WorldPosition = pos;
			snapped++;
			updated.Add( new Dictionary<string, object>
			{
				["id"] = go.Id.ToString(),
				["name"] = go.Name,
				["position"] = OzmiumSceneHelpers.V3( go.WorldPosition )
			} );
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Snapped {snapped} object(s) to {gridSize}-unit grid (found {found} of {ids.Count}).",
			snapped,
			gridSize,
			updated
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── distribute_along_line ─────────────────────────────────────────────

	private static object DistributeAlongLine( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string prefabPath = OzmiumSceneHelpers.Get( args, "prefabPath", (string)null );
		int count = OzmiumSceneHelpers.Get( args, "count", 10 );
		var startPoint = OzmiumSceneHelpers.Get( args, "startPoint", new Vector3( 0, 0, 0 ) );
		var endPoint = OzmiumSceneHelpers.Get( args, "endPoint", new Vector3( 1000, 0, 0 ) );
		bool alignToGround = OzmiumSceneHelpers.Get( args, "alignToGround", false );
		float alignMaxDistance = OzmiumSceneHelpers.Get( args, "alignMaxDistance", 10000f );
		bool randomRotation = OzmiumSceneHelpers.Get( args, "randomRotation", false );
		bool lookAlongPath = OzmiumSceneHelpers.Get( args, "lookAlongPath", true );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );
		string namePrefix = OzmiumSceneHelpers.Get( args, "namePrefix", (string)null );

		if ( string.IsNullOrEmpty( prefabPath ) )
			return OzmiumSceneHelpers.Txt( "Provide 'prefabPath'." );
		if ( count <= 0 ) return OzmiumSceneHelpers.Txt( "'count' must be positive." );
		if ( count > MaxScatterCount ) count = MaxScatterCount;

		// Load prefab once
		var asset = AssetSystem.FindByPath( prefabPath );
		if ( asset == null )
			return OzmiumSceneHelpers.Txt( $"Asset not found: '{prefabPath}'. Use browse_assets with type='prefab' to find valid paths." );
		var prefabFile = ResourceLibrary.Get<PrefabFile>( prefabPath );
		if ( prefabFile == null )
			return OzmiumSceneHelpers.Txt( $"Could not load prefab: '{prefabPath}'." );
		var prefabScene = SceneUtility.GetPrefabScene( prefabFile );

		// Resolve optional parent
		GameObject parent = null;
		if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var parentGuid ) )
			parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == parentGuid );

		// Direction for look-along-path
		var pathDir = (endPoint - startPoint).Normal;

		var created = new List<Dictionary<string, object>>();

		try
		{
			for ( int i = 0; i < count; i++ )
			{
				float t = count > 1 ? (float)i / (count - 1) : 0f;
				var pos = Vector3.Lerp( startPoint, endPoint, t );

				var go = prefabScene.Clone();
				go.WorldPosition = pos;

				// Optional ground alignment
				if ( alignToGround )
				{
					var hit = scene.Trace.Ray( go.WorldPosition, go.WorldPosition + Vector3.Down * alignMaxDistance ).Run();
					if ( hit.Hit )
						go.WorldPosition = hit.HitPosition + Vector3.Up;
				}

				// Look along path direction
				if ( lookAlongPath && !randomRotation )
				{
					go.WorldRotation = Rotation.LookAt( pathDir );
				}
				else if ( randomRotation )
				{
					var rng = new Random();
					go.WorldRotation = Rotation.From( 0, (float)rng.NextDouble() * 360f, 0 );
				}

				if ( parent != null ) go.SetParent( parent );

				if ( !string.IsNullOrEmpty( namePrefix ) )
				{
					go.Name = $"{namePrefix}_{go.Name}";
					go.MakeNameUnique();
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
				message = $"Distributed {created.Count} instances of '{prefabPath}' along line.",
				count = created.Count,
				startPoint = new { x = startPoint.x, y = startPoint.y, z = startPoint.z },
				endPoint = new { x = endPoint.x, y = endPoint.y, z = endPoint.z },
				alignToGround,
				lookAlongPath,
				created
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── match_height ──────────────────────────────────────────────────────

	private static object MatchHeight( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		bool useAverage = OzmiumSceneHelpers.Get( args, "useAverage", false );
		float? explicitHeight = args.TryGetProperty( "height", out var hEl ) ? (float?)hEl.GetSingle() : null;

		if ( !args.TryGetProperty( "ids", out var idsEl ) || idsEl.ValueKind != JsonValueKind.Array || idsEl.GetArrayLength() == 0 )
			return OzmiumSceneHelpers.Txt( "Provide 'ids' as non-empty array of GUIDs." );

		var ids = new List<string>();
		foreach ( var el in idsEl.EnumerateArray() ) ids.Add( el.GetString() );

		// Resolve all objects first
		var objects = new List<GameObject>();
		foreach ( var idStr in ids )
		{
			if ( !Guid.TryParse( idStr, out var guid ) ) continue;
			var go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( go != null ) objects.Add( go );
		}

		if ( objects.Count == 0 )
			return OzmiumSceneHelpers.Txt( $"No objects found from {ids.Count} provided GUIDs." );

		// Determine target height
		float targetHeight;
		if ( explicitHeight.HasValue )
		{
			targetHeight = explicitHeight.Value;
		}
		else if ( useAverage )
		{
			targetHeight = objects.Sum( g => g.WorldPosition.y ) / objects.Count;
		}
		else
		{
			return OzmiumSceneHelpers.Txt( "Provide 'height' (explicit Y value) or set 'useAverage' to true." );
		}

		int modified = 0;
		var updated = new List<Dictionary<string, object>>();

		foreach ( var go in objects )
		{
			var pos = go.WorldPosition;
			pos.y = targetHeight;
			go.WorldPosition = pos;
			modified++;

			updated.Add( new Dictionary<string, object>
			{
				["id"] = go.Id.ToString(),
				["name"] = go.Name,
				["position"] = OzmiumSceneHelpers.V3( go.WorldPosition )
			} );
		}

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			message = $"Set {modified} object(s) to Y={targetHeight:F1}.",
			modified,
			height = targetHeight,
			useAverage = !explicitHeight.HasValue && useAverage,
			updated
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── build_automation (Omnibus) ─────────────────────────────────────────

	internal static object BuildAutomation( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"scatter_objects"        => ScatterObjects( args ),
			"replace_prefab_instances" => ReplacePrefabInstances( args ),
			"align_to_ground"        => AlignToGround( args ),
			"randomize_transforms"   => RandomizeTransforms( args ),
			"snap_to_grid"           => SnapToGrid( args ),
			"distribute_along_line"  => DistributeAlongLine( args ),
			"match_height"           => MatchHeight( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: scatter_objects, replace_prefab_instances, align_to_ground, randomize_transforms, snap_to_grid, distribute_along_line, match_height" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaBuildAutomation
	{
		get
		{
			var stringArrayItem = new Dictionary<string, object> { ["type"] = "string" };
			var props = new Dictionary<string, object>();

			props["operation"] = new Dictionary<string, object>
			{
				["type"] = "string",
				["description"] = "Operation to perform.",
				["enum"] = new[] { "scatter_objects", "replace_prefab_instances", "align_to_ground", "randomize_transforms", "snap_to_grid", "distribute_along_line", "match_height" }
			};

			// scatter_objects params
			props["prefabPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Prefab asset path for scatter_objects." };
			props["count"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Number of instances to scatter (max 500, default 10)." };
			props["boundsMin"] = new Dictionary<string, object> { ["description"] = "Minimum corner of scatter bounding volume {x,y,z} (default 0,0,0)." };
			props["boundsMax"] = new Dictionary<string, object> { ["description"] = "Maximum corner of scatter bounding volume {x,y,z} (default 1000,0,1000)." };
			props["randomRotation"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Randomize rotation for each scattered instance (default false)." };
			props["randomScale"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Randomize scale for each scattered instance (default false)." };
			props["scaleMin"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Minimum random scale (default 0.8)." };
			props["scaleMax"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Maximum random scale (default 1.2)." };
			props["alignToGround"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Raycast down to align each instance to ground (default false)." };
			props["alignMaxDistance"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max raycast distance for ground alignment (default 10000)." };
			props["namePrefix"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Optional prefix for scattered instance names." };
			props["seed"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Random seed for reproducible results (default 0 = non-deterministic)." };

			// replace_prefab_instances params
			props["sourcePrefabPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Prefab path to find instances of (case-insensitive substring match)." };
			props["targetPrefabPath"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Prefab path to replace instances with." };
			props["preserveTransform"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Keep original position/rotation/scale on replaced instances (default true)." };

			// align_to_ground params
			props["ids"] = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of GUIDs to operate on.", ["items"] = stringArrayItem };
			props["offsetY"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Vertical offset above the ground hit point (default 0)." };
			props["maxDistance"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max raycast distance downward (default 10000)." };

			// randomize_transforms params
			props["randomRotationY"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Randomize Y rotation (yaw) (default false)." };
			props["randomRotationX"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Randomize X rotation (pitch) (default false)." };
			props["randomRotationZ"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Randomize Z rotation (roll) (default false)." };

			// snap_to_grid params
			props["gridSize"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Grid spacing in world units (default 100)." };
			props["snapX"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Snap on X axis (default true)." };
			props["snapY"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Snap on Y axis (default false)." };
			props["snapZ"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Snap on Z axis (default true)." };
			props["origin"] = new Dictionary<string, object> { ["description"] = "Grid origin offset {x,y,z} (default 0,0,0)." };

			// distribute_along_line params
			props["startPoint"] = new Dictionary<string, object> { ["description"] = "Start position of the line {x,y,z} (default 0,0,0)." };
			props["endPoint"] = new Dictionary<string, object> { ["description"] = "End position of the line {x,y,z} (default 1000,0,0)." };
			props["lookAlongPath"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Orient objects to face along the line direction (default true)." };

			// match_height params
			props["height"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Explicit Y height to set all objects to." };
			props["useAverage"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Compute average Y from all selected objects instead of using explicit height (default false)." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object>
			{
				["name"] = "build_automation",
				["description"] = "Scene building automation: scatter prefabs randomly in a volume, bulk replace prefab instances, align objects to ground via raycast, randomize transforms, snap objects to a grid, distribute prefabs along a line, or match object heights. Use for DarkRP map setup workflows like placing doors, scattering furniture, organizing props on terrain, swapping placeholder prefabs, aligning city blocks to grid, lining up fence posts, and flattening surfaces.",
				["inputSchema"] = schema
			};
		}
	}
}
