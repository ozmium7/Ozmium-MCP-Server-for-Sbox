using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Implements the logic for each MCP tool call.
/// Each public method corresponds to one tool name and returns a result object
/// suitable for wrapping in a JSON-RPC response, or throws on hard errors.
/// </summary>
internal static class ToolHandlers
{
	// ── get_scene_hierarchy ────────────────────────────────────────────────

	internal static object GetSceneHierarchy( JsonElement args )
	{
		bool rootOnly = args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "rootOnly", out var roP ) && roP.GetBoolean();

		bool includeDisabled = true;
		if ( args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "includeDisabled", out var idP ) )
			includeDisabled = idP.GetBoolean();

		string rootId = null;
		if ( args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "rootId", out var ridP ) )
			rootId = ridP.GetString();

		var sb    = new StringBuilder();
		var scene = Game.ActiveScene;

		if ( scene == null )
		{
			sb.Append( "No active scene." );
		}
		else
		{
			sb.AppendLine( $"Scene: {scene.Name}" );

			// If rootId is specified, walk only that subtree
			if ( !string.IsNullOrEmpty( rootId ) && Guid.TryParse( rootId, out var guid ) )
			{
				var subtreeRoot = SceneQueryHelpers.WalkAll( scene )
					.FirstOrDefault( g => g.Id == guid );

				if ( subtreeRoot == null )
				{
					sb.Append( $"No GameObject found with id='{rootId}'." );
				}
				else
				{
					void WalkSub( GameObject go, int depth )
					{
						if ( !includeDisabled && !go.Enabled ) return;
						AppendHierarchyLine( sb, go, depth, showChildCount: rootOnly );
						if ( !rootOnly )
							foreach ( var child in go.Children )
								WalkSub( child, depth + 1 );
					}
					WalkSub( subtreeRoot, 0 );
				}
			}
			else if ( rootOnly )
			{
				foreach ( var go in scene.Children )
				{
					if ( !includeDisabled && !go.Enabled ) continue;
					AppendHierarchyLine( sb, go, 0, showChildCount: true );
				}
			}
			else
			{
				void Walk( GameObject go, int depth )
				{
					if ( !includeDisabled && !go.Enabled ) return;
					AppendHierarchyLine( sb, go, depth, showChildCount: false );
					foreach ( var child in go.Children )
						Walk( child, depth + 1 );
				}
				foreach ( var go in scene.Children )
					Walk( go, 0 );
			}
		}

		return TextResult( sb.ToString() );
	}

	// ── find_game_objects ──────────────────────────────────────────────────

	internal static object FindGameObjects( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string nameContains   = null;
		string hasTag         = null;
		string hasComponent   = null;
		string pathContains   = null;
		bool   enabledOnly    = false;
		bool?  isNetworkRoot  = null;
		bool?  isPrefabInst   = null;
		int    maxResults     = 50;
		string sortBy         = null;
		float? sortOriginX    = null;
		float? sortOriginY    = null;
		float? sortOriginZ    = null;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "nameContains",    out var nc  ) ) nameContains  = nc.GetString();
			if ( args.TryGetProperty( "hasTag",          out var ht  ) ) hasTag        = ht.GetString();
			if ( args.TryGetProperty( "hasComponent",    out var hc  ) ) hasComponent  = hc.GetString();
			if ( args.TryGetProperty( "pathContains",    out var pc  ) ) pathContains  = pc.GetString();
			if ( args.TryGetProperty( "enabledOnly",     out var eo  ) ) enabledOnly   = eo.GetBoolean();
			if ( args.TryGetProperty( "isNetworkRoot",   out var inr ) ) isNetworkRoot = inr.GetBoolean();
			if ( args.TryGetProperty( "isPrefabInstance",out var ipi ) ) isPrefabInst  = ipi.GetBoolean();
			if ( args.TryGetProperty( "maxResults",      out var mr  ) ) maxResults    = Math.Clamp( mr.GetInt32(), 1, 500 );
			if ( args.TryGetProperty( "sortBy",          out var sb2 ) ) sortBy        = sb2.GetString();
			if ( args.TryGetProperty( "sortOriginX",     out var sox ) ) sortOriginX   = sox.GetSingle();
			if ( args.TryGetProperty( "sortOriginY",     out var soy ) ) sortOriginY   = soy.GetSingle();
			if ( args.TryGetProperty( "sortOriginZ",     out var soz ) ) sortOriginZ   = soz.GetSingle();
		}

		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		// Use WalkAll so disabled objects inside disabled parents are reachable
		var allObjects = SceneQueryHelpers.WalkAll( scene, includeDisabled: true );

		var matches = new List<Dictionary<string, object>>();
		int totalSearched = 0;

		foreach ( var go in allObjects )
		{
			totalSearched++;
			if ( enabledOnly && !go.Enabled ) continue;
			if ( !string.IsNullOrEmpty( nameContains ) &&
				go.Name.IndexOf( nameContains, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			if ( !string.IsNullOrEmpty( hasTag ) && !go.Tags.Has( hasTag ) ) continue;
			if ( !string.IsNullOrEmpty( hasComponent ) )
			{
				bool found = go.Components.GetAll().Any( c =>
					c.GetType().Name.IndexOf( hasComponent, StringComparison.OrdinalIgnoreCase ) >= 0 );
				if ( !found ) continue;
			}
			if ( !string.IsNullOrEmpty( pathContains ) )
			{
				var path = SceneQueryHelpers.GetObjectPath( go );
				if ( path.IndexOf( pathContains, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			}
			if ( isNetworkRoot.HasValue && go.IsNetworkRoot != isNetworkRoot.Value ) continue;
			if ( isPrefabInst.HasValue  && go.IsPrefabInstance != isPrefabInst.Value ) continue;

			matches.Add( SceneQueryHelpers.BuildObjectSummary( go ) );
		}

		// Sorting
		if ( !string.IsNullOrEmpty( sortBy ) )
		{
			if ( sortBy.Equals( "name", StringComparison.OrdinalIgnoreCase ) )
			{
				matches = matches.OrderBy( m => m["name"]?.ToString() ).ToList();
			}
			else if ( sortBy.Equals( "distance", StringComparison.OrdinalIgnoreCase ) &&
				sortOriginX.HasValue && sortOriginY.HasValue && sortOriginZ.HasValue )
			{
				var ox = sortOriginX.Value;
				var oy = sortOriginY.Value;
				var oz = sortOriginZ.Value;
				matches = matches.OrderBy( m =>
				{
					var pos = (Dictionary<string, object>)m["position"];
					var dx  = (float)(double)pos["x"] - ox;
					var dy  = (float)(double)pos["y"] - oy;
					var dz  = (float)(double)pos["z"] - oz;
					return MathF.Sqrt( dx * dx + dy * dy + dz * dz );
				} ).ToList();
			}
			else if ( sortBy.Equals( "componentCount", StringComparison.OrdinalIgnoreCase ) )
			{
				matches = matches.OrderByDescending( m => ( (List<string>)m["components"] ).Count ).ToList();
			}
		}

		// Apply maxResults after sorting
		bool truncated = matches.Count > maxResults;
		if ( truncated ) matches = matches.Take( maxResults ).ToList();

		var summary = $"Found {matches.Count} matching object(s) (searched {totalSearched} total).";
		if ( truncated )
			summary += $" Result limit ({maxResults}) reached — refine your filters for more specific results.";

		var json = JsonSerializer.Serialize( new { summary, results = matches }, jsonOptions );
		return TextResult( json );
	}

	// ── get_game_object_details ────────────────────────────────────────────

	internal static object GetGameObjectDetails( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string idStr   = null;
		string nameStr = null;
		bool   recurse = false;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "id",                       out var idP   ) ) idStr   = idP.GetString();
			if ( args.TryGetProperty( "name",                     out var nameP ) ) nameStr = nameP.GetString();
			if ( args.TryGetProperty( "includeChildrenRecursive", out var recP  ) ) recurse = recP.GetBoolean();
		}

		if ( string.IsNullOrEmpty( idStr ) && string.IsNullOrEmpty( nameStr ) )
			throw new ArgumentException( "Provide either 'id' or 'name'." );

		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		GameObject target = null;

		if ( !string.IsNullOrEmpty( idStr ) && Guid.TryParse( idStr, out var guid ) )
		{
			// Check WalkAll first (covers enabled objects), then fall back to scene.Children
			// directly to catch disabled root objects that WalkAll may skip.
			target = SceneQueryHelpers.WalkAll( scene, includeDisabled: true ).FirstOrDefault( g => g.Id == guid );
			if ( target == null )
				target = scene.Children.FirstOrDefault( g => g.Id == guid );
		}

		if ( target == null && !string.IsNullOrEmpty( nameStr ) )
		{
			target = SceneQueryHelpers.WalkAll( scene, includeDisabled: true ).FirstOrDefault( g =>
				string.Equals( g.Name, nameStr, StringComparison.OrdinalIgnoreCase ) );
			if ( target == null )
				target = scene.Children.FirstOrDefault( g =>
					string.Equals( g.Name, nameStr, StringComparison.OrdinalIgnoreCase ) );
		}

		if ( target == null )
			return TextResult( $"No GameObject found matching id='{idStr}' name='{nameStr}'." );

		var json = JsonSerializer.Serialize( SceneQueryHelpers.BuildObjectDetail( target, recurse ), jsonOptions );
		return TextResult( json );
	}

	// ── get_scene_summary ──────────────────────────────────────────────────

	internal static object GetSceneSummary( JsonSerializerOptions jsonOptions )
	{
		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		// Use WalkAll so disabled subtrees are included in counts
		var allObjects  = SceneQueryHelpers.WalkAll( scene, includeDisabled: true ).ToList();
		var rootObjects = scene.Children.ToList();
		int totalCount  = allObjects.Count;
		int rootCount   = rootObjects.Count;
		int enabledCount   = allObjects.Count( g => g.Enabled );
		int disabledCount  = allObjects.Count( g => !g.Enabled );

		// Component type frequency
		var compCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
		{
			foreach ( var comp in go.Components.GetAll() )
			{
				var typeName = comp.GetType().Name;
				compCounts.TryGetValue( typeName, out var existing );
				compCounts[typeName] = existing + 1;
			}
		}
		var topComponents = compCounts
			.OrderByDescending( kv => kv.Value )
			.Select( kv => new Dictionary<string, object> { ["type"] = kv.Key, ["count"] = kv.Value } )
			.ToList();

		// All unique tags
		var allTags = new HashSet<string>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
			foreach ( var tag in go.Tags.TryGetAll() )
				allTags.Add( tag );

		// Prefab source breakdown
		var prefabCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects.Where( g => g.IsPrefabInstance && g.PrefabInstanceSource != null ) )
		{
			var src = go.PrefabInstanceSource;
			prefabCounts.TryGetValue( src, out var existing );
			prefabCounts[src] = existing + 1;
		}
		var prefabBreakdown = prefabCounts
			.OrderByDescending( kv => kv.Value )
			.Select( kv => new Dictionary<string, object> { ["prefab"] = kv.Key, ["instances"] = kv.Value } )
			.ToList();

		// Network mode distribution
		var netModeCounts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
		foreach ( var go in allObjects )
		{
			var mode = go.NetworkMode.ToString();
			netModeCounts.TryGetValue( mode, out var existing );
			netModeCounts[mode] = existing + 1;
		}

		// Root object quick list
		var rootNames = rootObjects.Select( g => new Dictionary<string, object>
		{
			["name"]       = g.Name,
			["id"]         = g.Id.ToString(),
			["enabled"]    = g.Enabled,
			["childCount"] = g.Children.Count,
			["components"] = SceneQueryHelpers.GetComponentNames( g )
		} ).ToList();

		var summary = new Dictionary<string, object>
		{
			["sceneName"]           = scene.Name,
			["totalObjects"]        = totalCount,
			["rootObjects"]         = rootCount,
			["enabledObjects"]      = enabledCount,
			["disabledObjects"]     = disabledCount,
			["uniqueTags"]          = allTags.OrderBy( t => t ).ToList(),
			["componentBreakdown"]  = topComponents,
			["prefabBreakdown"]     = prefabBreakdown,
			["networkModeBreakdown"]= netModeCounts,
			["rootObjectList"]      = rootNames
		};

		var json = JsonSerializer.Serialize( summary, jsonOptions );
		return TextResult( json );
	}

	// ── get_component_properties ──────────────────────────────────────────

	internal static object GetComponentProperties( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string idStr        = null;
		string nameStr      = null;
		string componentType = null;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "id",            out var idP  ) ) idStr         = idP.GetString();
			if ( args.TryGetProperty( "name",          out var nmP  ) ) nameStr       = nmP.GetString();
			if ( args.TryGetProperty( "componentType", out var ctP  ) ) componentType = ctP.GetString();
		}

		if ( string.IsNullOrEmpty( idStr ) && string.IsNullOrEmpty( nameStr ) )
			throw new ArgumentException( "Provide either 'id' or 'name'." );
		if ( string.IsNullOrEmpty( componentType ) )
			throw new ArgumentException( "Provide 'componentType'." );

		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		GameObject target = null;
		if ( !string.IsNullOrEmpty( idStr ) && Guid.TryParse( idStr, out var guid ) )
			target = SceneQueryHelpers.WalkAll( scene ).FirstOrDefault( g => g.Id == guid );
		if ( target == null && !string.IsNullOrEmpty( nameStr ) )
			target = SceneQueryHelpers.WalkAll( scene ).FirstOrDefault( g =>
				string.Equals( g.Name, nameStr, StringComparison.OrdinalIgnoreCase ) );

		if ( target == null )
			return TextResult( $"No GameObject found matching id='{idStr}' name='{nameStr}'." );

		var comp = target.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( componentType, StringComparison.OrdinalIgnoreCase ) >= 0 );

		if ( comp == null )
			return TextResult( $"No component matching '{componentType}' found on '{target.Name}'." );

		// Reflect public properties via TypeLibrary
		var props = new Dictionary<string, object>();
		var type  = comp.GetType();

		foreach ( var prop in type.GetProperties( System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance ) )
		{
			if ( !prop.CanRead ) continue;
			try
			{
				var val = prop.GetValue( comp );
				props[prop.Name] = val switch
				{
					null           => null,
					bool b         => (object)b,
					int i          => (object)i,
					float f        => (object)MathF.Round( f, 4 ),
					double d       => (object)Math.Round( d, 4 ),
					string s       => (object)s,
					Enum e         => (object)e.ToString(),
					Vector3 v      => (object)new { x = MathF.Round( v.x, 2 ), y = MathF.Round( v.y, 2 ), z = MathF.Round( v.z, 2 ) },
					_              => (object)val.ToString()
				};
			}
			catch
			{
				props[prop.Name] = "<error reading value>";
			}
		}

		var result = new Dictionary<string, object>
		{
			["gameObjectId"]   = target.Id.ToString(),
			["gameObjectName"] = target.Name,
			["componentType"]  = comp.GetType().Name,
			["enabled"]        = comp.Enabled,
			["properties"]     = props
		};

		var json = JsonSerializer.Serialize( result, jsonOptions );
		return TextResult( json );
	}

	// ── find_game_objects_in_radius ────────────────────────────────────────

	internal static object FindGameObjectsInRadius( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		float  x            = 0f;
		float  y            = 0f;
		float  z            = 0f;
		float  radius       = 1000f;
		string hasTag       = null;
		string hasComponent = null;
		bool   enabledOnly  = false;
		int    maxResults   = 50;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "x",           out var xP  ) ) x           = xP.GetSingle();
			if ( args.TryGetProperty( "y",           out var yP  ) ) y           = yP.GetSingle();
			if ( args.TryGetProperty( "z",           out var zP  ) ) z           = zP.GetSingle();
			if ( args.TryGetProperty( "radius",      out var rP  ) ) radius      = rP.GetSingle();
			if ( args.TryGetProperty( "hasTag",      out var ht  ) ) hasTag      = ht.GetString();
			if ( args.TryGetProperty( "hasComponent",out var hc  ) ) hasComponent = hc.GetString();
			if ( args.TryGetProperty( "enabledOnly", out var eo  ) ) enabledOnly = eo.GetBoolean();
			if ( args.TryGetProperty( "maxResults",  out var mr  ) ) maxResults  = Math.Clamp( mr.GetInt32(), 1, 500 );
		}

		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		var origin      = new Vector3( x, y, z );
		float radiusSq  = radius * radius;

		var matches = new List<(float dist, Dictionary<string, object> summary)>();

		foreach ( var go in SceneQueryHelpers.WalkAll( scene, includeDisabled: true ) )
		{
			if ( enabledOnly && !go.Enabled ) continue;
			if ( !string.IsNullOrEmpty( hasTag ) && !go.Tags.Has( hasTag ) ) continue;
			if ( !string.IsNullOrEmpty( hasComponent ) )
			{
				bool found = go.Components.GetAll().Any( c =>
					c.GetType().Name.IndexOf( hasComponent, StringComparison.OrdinalIgnoreCase ) >= 0 );
				if ( !found ) continue;
			}

			var pos  = go.WorldPosition;
			var diff = pos - origin;
			var distSq = diff.x * diff.x + diff.y * diff.y + diff.z * diff.z;
			if ( distSq > radiusSq ) continue;

			matches.Add( (MathF.Sqrt( distSq ), SceneQueryHelpers.BuildObjectSummary( go )) );
		}

		matches.Sort( ( a, b ) => a.dist.CompareTo( b.dist ) );

		int totalCandidates = matches.Count;
		bool truncated = matches.Count > maxResults;
		var results    = matches.Take( maxResults )
			.Select( m =>
			{
				m.summary["distanceFromOrigin"] = MathF.Round( m.dist, 2 );
				return m.summary;
			} )
			.ToList();

		var summary = $"Found {results.Count} object(s) within radius {radius} of ({x},{y},{z}) (searched {totalCandidates} candidates).";
		if ( truncated )
			summary += $" Result limit ({maxResults}) reached.";

		var json = JsonSerializer.Serialize( new { summary, results }, jsonOptions );
		return TextResult( json );
	}

	// ── get_prefab_instances ───────────────────────────────────────────────

	internal static object GetPrefabInstances( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string prefabPath  = null;
		bool   enabledOnly = false;
		int    maxResults  = 100;

		if ( args.ValueKind != JsonValueKind.Undefined )
		{
			if ( args.TryGetProperty( "prefabPath",  out var pp ) ) prefabPath  = pp.GetString();
			if ( args.TryGetProperty( "enabledOnly", out var eo ) ) enabledOnly = eo.GetBoolean();
			if ( args.TryGetProperty( "maxResults",  out var mr ) ) maxResults  = Math.Clamp( mr.GetInt32(), 1, 500 );
		}

		var scene = Game.ActiveScene;
		if ( scene == null )
			return TextResult( "No active scene." );

		// If no prefabPath given, return a breakdown of all prefab sources
		if ( string.IsNullOrEmpty( prefabPath ) )
		{
			var counts = new Dictionary<string, int>( StringComparer.OrdinalIgnoreCase );
			foreach ( var go in SceneQueryHelpers.WalkAll( scene, includeDisabled: true ) )
			{
				if ( !go.IsPrefabInstance || go.PrefabInstanceSource == null ) continue;
				if ( enabledOnly && !go.Enabled ) continue;
				counts.TryGetValue( go.PrefabInstanceSource, out var c );
				counts[go.PrefabInstanceSource] = c + 1;
			}
			var breakdown = counts
				.OrderByDescending( kv => kv.Value )
				.Select( kv => new Dictionary<string, object> { ["prefab"] = kv.Key, ["instances"] = kv.Value } )
				.ToList();
			var bJson = JsonSerializer.Serialize( new { summary = $"{counts.Count} unique prefab(s) in scene.", breakdown }, jsonOptions );
			return TextResult( bJson );
		}

		// Return instances of a specific prefab
		var matches = new List<Dictionary<string, object>>();
		foreach ( var go in SceneQueryHelpers.WalkAll( scene, includeDisabled: true ) )
		{
			if ( matches.Count >= maxResults ) break;
			if ( !go.IsPrefabInstance ) continue;
			if ( enabledOnly && !go.Enabled ) continue;
			if ( go.PrefabInstanceSource == null ) continue;
			if ( go.PrefabInstanceSource.IndexOf( prefabPath, StringComparison.OrdinalIgnoreCase ) < 0 ) continue;
			matches.Add( SceneQueryHelpers.BuildObjectSummary( go ) );
		}

		bool truncated = matches.Count >= maxResults;
		var sumStr = $"Found {matches.Count} instance(s) of '{prefabPath}'.";
		if ( truncated ) sumStr += $" Result limit ({maxResults}) reached.";

		var json = JsonSerializer.Serialize( new { summary = sumStr, results = matches }, jsonOptions );
		return TextResult( json );
	}

	// ── list_console_commands ──────────────────────────────────────────────

	internal static object ListConsoleCommands( JsonElement args, JsonSerializerOptions jsonOptions )
	{
		string filter = null;
		if ( args.ValueKind != JsonValueKind.Undefined &&
			args.TryGetProperty( "filter", out var fP ) )
			filter = fP.GetString();

		var entries = new List<Dictionary<string, object>>();

		var skippedAssemblies = new List<string>();

		// Enumerate all [ConVar]-attributed properties across all loaded assemblies
		foreach ( var assembly in AppDomain.CurrentDomain.GetAssemblies() )
		{
			try
			{
				foreach ( var type in assembly.GetTypes() )
				{
					foreach ( var prop in type.GetProperties(
						System.Reflection.BindingFlags.Public |
						System.Reflection.BindingFlags.NonPublic |
						System.Reflection.BindingFlags.Static ) )
					{
						var attr = prop.GetCustomAttributes( typeof( ConVarAttribute ), false )
							.FirstOrDefault() as ConVarAttribute;
						if ( attr == null ) continue;

						var cvarName = !string.IsNullOrEmpty( attr.Name )
							? attr.Name
							: prop.Name.ToLowerInvariant();

						if ( !string.IsNullOrEmpty( filter ) &&
							cvarName.IndexOf( filter, StringComparison.OrdinalIgnoreCase ) < 0 )
							continue;

						// Try to read current value
						string currentValue = null;
						try { currentValue = ConsoleSystem.GetValue( cvarName ); } catch { }

						entries.Add( new Dictionary<string, object>
						{
							["name"]         = cvarName,
							["help"]         = attr.Help ?? "",
							["flags"]        = attr.Flags.ToString(),
							["saved"]        = attr.Saved,
							["currentValue"] = currentValue,
							["declaringType"]= type.Name
						} );
					}
				}
			}
			catch ( Exception ex )
			{
				skippedAssemblies.Add( $"{assembly.GetName().Name}: {ex.Message}" );
			}
		}

		entries = entries
			.GroupBy( e => e["name"]?.ToString() )
			.Select( g => g.First() )
			.OrderBy( e => e["name"]?.ToString() )
			.ToList();

		var summary = $"Found {entries.Count} [ConVar] entries" +
			( string.IsNullOrEmpty( filter ) ? "." : $" matching '{filter}'." );
		if ( skippedAssemblies.Count > 0 )
			summary += $" ({skippedAssemblies.Count} assemblies skipped due to reflection errors.)";

		var json = JsonSerializer.Serialize( new { summary, entries, skippedAssemblies }, jsonOptions );
		return TextResult( json );
	}

	// ── run_console_command ────────────────────────────────────────────────

	internal static object RunConsoleCommand( JsonElement args )
	{
		var cmd     = args.GetProperty( "command" ).GetString();
		var parts   = cmd.Trim().Split( ' ', StringSplitOptions.RemoveEmptyEntries );
		var cmdName = parts[0];

		// Only support convars (readable via GetValue). ConCmd methods and unknown
		// commands are rejected with a friendly message — ConsoleSystem.Run throws
		// uncatchable exceptions in s&box's sandbox for both cases.
		string currentValue = null;
		try { currentValue = ConsoleSystem.GetValue( cmdName ); } catch { }

		if ( currentValue == null )
			return TextResult( $"Unknown convar: '{cmdName}'. Only [ConVar] properties are supported. Use list_console_commands to see available names." );

		// Read-only query (no value argument) — just return the current value.
		if ( parts.Length == 1 )
			return TextResult( $"{cmdName} = {currentValue}" );

		// Write: set the convar value using SetValue (no main-thread restriction).
		var newValue = string.Join( " ", parts, 1, parts.Length - 1 );
		ConsoleSystem.SetValue( cmdName, newValue );

		// Read back to confirm the change.
		string readback = null;
		try { readback = ConsoleSystem.GetValue( cmdName ); } catch { }

		return TextResult( $"Set {cmdName} = {readback ?? newValue}" );
	}

	// ── Shared helpers ─────────────────────────────────────────────────────

	/// <summary>Wraps a plain text string in the MCP content envelope.</summary>
	internal static object TextResult( string text ) => new
	{
		content = new object[] { new { type = "text", text } }
	};

	private static void AppendHierarchyLine( StringBuilder sb, GameObject go, int depth, bool showChildCount )
	{
		var indent  = new string( ' ', depth * 2 );
		var comps   = SceneQueryHelpers.GetComponentNames( go );
		var tags    = SceneQueryHelpers.GetTags( go );
		var compStr = comps.Count > 0 ? $" [{string.Join( ", ", comps )}]" : "";
		var tagStr  = tags.Count  > 0 ? $" #{string.Join( " #", tags )}" : "";
		var disStr  = go.Enabled ? "" : " (disabled)";
		var childStr = showChildCount ? $"  children:{go.Children.Count}" : "";
		sb.AppendLine( $"{indent}- {go.Name} (ID: {go.Id}){disStr}{tagStr}{compStr}{childStr}" );
	}
}
