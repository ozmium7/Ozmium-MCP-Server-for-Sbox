using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Editor;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Handlers for all scene-write MCP tools:
/// create_game_object, add_component, remove_component, set_component_property,
/// destroy_game_object, reparent_game_object, set_game_object_tags,
/// instantiate_prefab, save_scene, undo, redo.
/// </summary>
internal static class OzmiumWriteHandlers
{

	// ── create_game_object ──────────────────────────────────────────────────

	internal static object CreateGameObject( JsonElement args )
	{
		var scene    = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string name     = OzmiumSceneHelpers.Get( args, "name",     "New GameObject" );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;

			if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var guid ) )
			{
				var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( parent != null ) go.SetParent( parent );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created '{go.Name}'.",
				id      = go.Id.ToString(),
				path    = OzmiumSceneHelpers.GetObjectPath( go )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── add_component ───────────────────────────────────────────────────────

	internal static object AddComponent( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",            (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name",          (string)null );
		string type = OzmiumSceneHelpers.Get( args, "componentType", (string)null );

		if ( string.IsNullOrEmpty( type ) ) return OzmiumSceneHelpers.Txt( "Provide 'componentType'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			// Use TypeLibrary (indexed, fast) to find the component type
			var td = FindComponentTypeDescription( type );
			if ( td == null ) return OzmiumSceneHelpers.Txt( $"Component type '{type}' not found. Use the exact class name." );
			go.Components.Create( td );
			return OzmiumSceneHelpers.Txt( $"Added '{td.Name}' to '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── remove_component ────────────────────────────────────────────────────

	internal static object RemoveComponent( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",            (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name",          (string)null );
		string type = OzmiumSceneHelpers.Get( args, "componentType", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var comp = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( type ?? "", StringComparison.OrdinalIgnoreCase ) >= 0 );
		if ( comp == null ) return OzmiumSceneHelpers.Txt( $"No component '{type}' found on '{go.Name}'." );

		try
		{
			comp.Destroy();
			return OzmiumSceneHelpers.Txt( $"Removed '{comp.GetType().Name}' from '{go.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_component_property ──────────────────────────────────────────────

	internal static object SetComponentProperty( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id           = OzmiumSceneHelpers.Get( args, "id",            (string)null );
		string objName      = OzmiumSceneHelpers.Get( args, "name",          (string)null );
		string compType     = OzmiumSceneHelpers.Get( args, "componentType", (string)null );
		string propName     = OzmiumSceneHelpers.Get( args, "propertyName",  (string)null );

		if ( string.IsNullOrEmpty( propName ) ) return OzmiumSceneHelpers.Txt( "Provide 'propertyName'." );
		if ( !args.TryGetProperty( "value", out var valEl ) ) return OzmiumSceneHelpers.Txt( "Provide 'value'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, objName );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var comp = go.Components.GetAll().FirstOrDefault( c =>
			string.IsNullOrEmpty( compType ) ||
			c.GetType().Name.IndexOf( compType, StringComparison.OrdinalIgnoreCase ) >= 0 );
		if ( comp == null ) return OzmiumSceneHelpers.Txt( $"Component '{compType}' not found." );

		var prop = comp.GetType().GetProperty( propName,
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
		if ( prop == null ) return OzmiumSceneHelpers.Txt( $"Property '{propName}' not found on '{comp.GetType().Name}'." );
		if ( !prop.CanWrite ) return OzmiumSceneHelpers.Txt( $"Property '{propName}' is read-only." );

		try
		{
			object converted = ConvertJsonValue( valEl, prop.PropertyType );
			prop.SetValue( comp, converted );
			var readback = prop.GetValue( comp );
			return OzmiumSceneHelpers.Txt( $"Set '{comp.GetType().Name}.{propName}' = {readback}" );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error setting property: {ex.Message}" ); }
	}

	// ── destroy_game_object ─────────────────────────────────────────────────

	internal static object DestroyGameObject( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var displayName = go.Name;
		try
		{
			go.Destroy();
			return OzmiumSceneHelpers.Txt( $"Destroyed '{displayName}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── reparent_game_object ────────────────────────────────────────────────

	internal static object ReparentGameObject( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id       = OzmiumSceneHelpers.Get( args, "id",       (string)null );
		string name     = OzmiumSceneHelpers.Get( args, "name",     (string)null );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			if ( string.IsNullOrEmpty( parentId ) || parentId == "null" )
			{
				go.SetParent( null );
				return OzmiumSceneHelpers.Txt( $"Moved '{go.Name}' to scene root." );
			}

			if ( !Guid.TryParse( parentId, out var guid ) ) return OzmiumSceneHelpers.Txt( "Invalid parentId GUID." );
			var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( parent == null ) return OzmiumSceneHelpers.Txt( $"Parent '{parentId}' not found." );
			go.SetParent( parent );
			return OzmiumSceneHelpers.Txt( $"Moved '{go.Name}' under '{parent.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_game_object_tags ────────────────────────────────────────────────

	internal static object SetGameObjectTags( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			// set: replace all tags
			if ( args.TryGetProperty( "set", out var setEl ) && setEl.ValueKind == JsonValueKind.Array )
			{
				go.Tags.RemoveAll();
				foreach ( var t in setEl.EnumerateArray() ) go.Tags.Add( t.GetString() );
				return OzmiumSceneHelpers.Txt( $"Tags on '{go.Name}': {string.Join( ", ", go.Tags.TryGetAll() )}" );
			}
			// add/remove individual tags
			if ( args.TryGetProperty( "add", out var addEl ) && addEl.ValueKind == JsonValueKind.Array )
				foreach ( var t in addEl.EnumerateArray() ) go.Tags.Add( t.GetString() );
			if ( args.TryGetProperty( "remove", out var remEl ) && remEl.ValueKind == JsonValueKind.Array )
				foreach ( var t in remEl.EnumerateArray() ) go.Tags.Remove( t.GetString() );

			return OzmiumSceneHelpers.Txt( $"Tags on '{go.Name}': {string.Join( ", ", go.Tags.TryGetAll() )}" );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── instantiate_prefab ──────────────────────────────────────────────────

	internal static object InstantiatePrefab( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string path     = OzmiumSceneHelpers.NormalizePath( OzmiumSceneHelpers.Get( args, "path", (string)null ) );
		float  x        = OzmiumSceneHelpers.Get( args, "x",        0f );
		float  y        = OzmiumSceneHelpers.Get( args, "y",        0f );
		float  z        = OzmiumSceneHelpers.Get( args, "z",        0f );
		string parentId = OzmiumSceneHelpers.Get( args, "parentId", (string)null );

		if ( string.IsNullOrEmpty( path ) ) return OzmiumSceneHelpers.Txt( "Provide 'path' (prefab asset path)." );

		try
		{
			// Verify the asset exists
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null )
				return OzmiumSceneHelpers.Txt( $"Asset not found: '{path}'. Use browse_assets with type='prefab' to find valid prefab paths." );

			var prefabFile = ResourceLibrary.Get<PrefabFile>( path );
			if ( prefabFile == null )
				return OzmiumSceneHelpers.Txt( $"Could not load prefab: '{path}'." );

			var prefabScene = SceneUtility.GetPrefabScene( prefabFile );
			var go = prefabScene.Clone();
			go.WorldPosition = new Vector3( x, y, z );

			if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var guid ) )
			{
				var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( parent != null ) go.SetParent( parent );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message  = $"Instantiated '{path}'.",
				id       = go.Id.ToString(),
				name     = go.Name,
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── save_scene ──────────────────────────────────────────────────────────

	internal static object SaveScene()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session active." );
			session.Save( false );
			return OzmiumSceneHelpers.Txt( $"Saved '{session.Scene?.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error saving: {ex.Message}" ); }
	}

	// ── undo / redo ─────────────────────────────────────────────────────────

	internal static object Undo()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session active." );
			var us = session.UndoSystem;
			if ( us == null ) return OzmiumSceneHelpers.Txt( "UndoSystem not available." );
			us.Undo();
			return OzmiumSceneHelpers.Txt( "Undo performed." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	internal static object Redo()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return OzmiumSceneHelpers.Txt( "No editor session active." );
			var us = session.UndoSystem;
			if ( us == null ) return OzmiumSceneHelpers.Txt( "UndoSystem not available." );
			us.Redo();
			return OzmiumSceneHelpers.Txt( "Redo performed." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_game_object_transform ──────────────────────────────────────────

	internal static object SetGameObjectTransform( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			if ( args.TryGetProperty( "position", out var posEl ) && posEl.ValueKind == JsonValueKind.Object )
			{
				float x = 0, y = 0, z = 0;
				if ( posEl.TryGetProperty( "x", out var xp ) ) x = xp.GetSingle();
				if ( posEl.TryGetProperty( "y", out var yp ) ) y = yp.GetSingle();
				if ( posEl.TryGetProperty( "z", out var zp ) ) z = zp.GetSingle();
				go.WorldPosition = new Vector3( x, y, z );
			}

			if ( args.TryGetProperty( "rotation", out var rotEl ) && rotEl.ValueKind == JsonValueKind.Object )
			{
				float p = 0, yaw = 0, r = 0;
				if ( rotEl.TryGetProperty( "pitch", out var pp ) ) p = pp.GetSingle();
				if ( rotEl.TryGetProperty( "yaw", out var yp ) ) yaw = yp.GetSingle();
				if ( rotEl.TryGetProperty( "roll", out var rp ) ) r = rp.GetSingle();
				go.WorldRotation = Rotation.From( p, yaw, r );
			}

			if ( args.TryGetProperty( "scale", out var scEl ) && scEl.ValueKind == JsonValueKind.Object )
			{
				float x = 1, y = 1, z = 1;
				if ( scEl.TryGetProperty( "x", out var xp ) ) x = xp.GetSingle();
				if ( scEl.TryGetProperty( "y", out var yp ) ) y = yp.GetSingle();
				if ( scEl.TryGetProperty( "z", out var zp ) ) z = zp.GetSingle();
				go.WorldScale = new Vector3( x, y, z );
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Updated transform for '{go.Name}'.",
				id      = go.Id.ToString(),
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				rotation = OzmiumSceneHelpers.Rot( go.WorldRotation ),
				scale    = OzmiumSceneHelpers.V3( go.WorldScale )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── duplicate_game_object ──────────────────────────────────────────────

	internal static object DuplicateGameObject( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		string newName = OzmiumSceneHelpers.Get( args, "newName", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			// Clone() creates objects in Game.ActiveScene which may differ from the
			// editor session scene, causing cross-scene parenting issues. Instead,
			// serialize + deserialize into the correct scene to get a full deep copy.
			var json = go.Serialize();
			var clone = scene.CreateObject( false );
			clone.Deserialize( json );

			if ( !string.IsNullOrEmpty( newName ) )
				clone.Name = newName;
			else
			{
				clone.Name = go.Name;
				clone.MakeNameUnique();
			}

			if ( args.TryGetProperty( "position", out var posEl ) && posEl.ValueKind == JsonValueKind.Object )
			{
				float x = 0, y = 0, z = 0;
				if ( posEl.TryGetProperty( "x", out var xp ) ) x = xp.GetSingle();
				if ( posEl.TryGetProperty( "y", out var yp ) ) y = yp.GetSingle();
				if ( posEl.TryGetProperty( "z", out var zp ) ) z = zp.GetSingle();
				clone.WorldPosition = new Vector3( x, y, z );
			}

			clone.Enabled = true;

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message  = $"Duplicated '{go.Name}' as '{clone.Name}'.",
				id       = clone.Id.ToString(),
				name     = clone.Name,
				position = OzmiumSceneHelpers.V3( clone.WorldPosition )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_game_object_enabled ────────────────────────────────────────────

	internal static object SetGameObjectEnabled( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id   = OzmiumSceneHelpers.Get( args, "id",   (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		bool?  enabled = args.ValueKind != JsonValueKind.Undefined && args.TryGetProperty( "enabled", out var eEl )
			? eEl.GetBoolean() : (bool?)null;

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			if ( enabled.HasValue )
				go.Enabled = enabled.Value;
			else
				go.Enabled = !go.Enabled;

			return OzmiumSceneHelpers.Txt( $"'{go.Name}' is now {(go.Enabled ? "enabled" : "disabled")}." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_game_object_name ───────────────────────────────────────────────

	internal static object SetGameObjectName( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id      = OzmiumSceneHelpers.Get( args, "id",      (string)null );
		string name    = OzmiumSceneHelpers.Get( args, "name",    (string)null );
		string newName = OzmiumSceneHelpers.Get( args, "newName", (string)null );

		if ( string.IsNullOrEmpty( newName ) ) return OzmiumSceneHelpers.Txt( "Provide 'newName'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		try
		{
			var oldName = go.Name;
			go.Name = newName;
			return OzmiumSceneHelpers.Txt( $"Renamed '{oldName}' to '{go.Name}' (ID: {go.Id})." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_component_enabled ──────────────────────────────────────────────

	internal static object SetComponentEnabled( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id       = OzmiumSceneHelpers.Get( args, "id",            (string)null );
		string name     = OzmiumSceneHelpers.Get( args, "name",          (string)null );
		string compType = OzmiumSceneHelpers.Get( args, "componentType", (string)null );
		bool   enabled  = OzmiumSceneHelpers.Get( args, "enabled",       true );

		if ( string.IsNullOrEmpty( compType ) ) return OzmiumSceneHelpers.Txt( "Provide 'componentType'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		var comp = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( compType, StringComparison.OrdinalIgnoreCase ) >= 0 );
		if ( comp == null ) return OzmiumSceneHelpers.Txt( $"Component '{compType}' not found on '{go.Name}'." );

		try
		{
			comp.Enabled = enabled;
			return OzmiumSceneHelpers.Txt( $"Set '{comp.GetType().Name}' on '{go.Name}' to {(enabled ? "enabled" : "disabled")}." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── Private helpers ─────────────────────────────────────────────────────

	/// <summary>
	/// Fast component type lookup using TypeLibrary (indexed).
	/// Prefers game-assembly types over Sandbox built-ins when names collide.
	/// </summary>
	internal static TypeDescription FindComponentTypeDescription( string typeName )
	{
		// Search all component types in TypeLibrary for a name match.
		// TypeLibrary.GetTypes<Component>() is indexed and fast.
		// We search here FIRST so we can prefer game-assembly types over engine built-ins.
		TypeDescription fallback = null;
		foreach ( var candidate in TypeLibrary.GetTypes<Component>() )
		{
			if ( !candidate.Name.Equals( typeName, StringComparison.OrdinalIgnoreCase ) )
				continue;

			// Prefer game types (not in Sandbox namespace) over engine built-ins
			var ns = candidate.TargetType.Namespace ?? "";
			if ( !ns.StartsWith( "Sandbox", StringComparison.OrdinalIgnoreCase ) )
				return candidate; // Game type found — use it immediately

			fallback ??= candidate; // Remember the first engine match as fallback
		}

		if ( fallback != null ) return fallback;

		// Last resort: try exact match by full type name (for edge cases)
		var td = TypeLibrary.GetType( typeName );
		if ( td != null && td.TargetType.IsClass && !td.TargetType.IsAbstract
			&& typeof( Component ).IsAssignableFrom( td.TargetType ) )
			return td;

		return null;
	}

	internal static object ConvertJsonValue( JsonElement el, Type targetType )
	{
		if ( targetType == typeof( string ) )
			return el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();

		if ( targetType == typeof( bool ) )
		{
			if ( el.ValueKind == JsonValueKind.True )  return true;
			if ( el.ValueKind == JsonValueKind.False ) return false;
			if ( el.ValueKind == JsonValueKind.String ) return bool.Parse( el.GetString() );
			return el.GetBoolean();
		}

		if ( targetType == typeof( int ) )
		{
			if ( el.ValueKind == JsonValueKind.String ) return int.Parse( el.GetString() );
			return el.GetInt32();
		}

		if ( targetType == typeof( float ) )
		{
			if ( el.ValueKind == JsonValueKind.String ) return float.Parse( el.GetString(), System.Globalization.CultureInfo.InvariantCulture );
			return el.GetSingle();
		}

		if ( targetType == typeof( double ) )
		{
			if ( el.ValueKind == JsonValueKind.String ) return double.Parse( el.GetString(), System.Globalization.CultureInfo.InvariantCulture );
			return el.GetDouble();
		}

		if ( targetType == typeof( Color ) )
		{
			var str = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
			if ( Color.TryParse( str, out var color ) )
				return color;
			return null;
		}

		if ( targetType == typeof( Vector3 ) && el.ValueKind == JsonValueKind.Object )
		{
			float vx = 0, vy = 0, vz = 0;
			if ( el.TryGetProperty( "x", out var xp ) ) vx = xp.GetSingle();
			if ( el.TryGetProperty( "y", out var yp ) ) vy = yp.GetSingle();
			if ( el.TryGetProperty( "z", out var zp ) ) vz = zp.GetSingle();
			return new Vector3( vx, vy, vz );
		}

		if ( targetType.IsEnum )
		{
			var str = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
			return Enum.Parse( targetType, str, ignoreCase: true );
		}

		// Handle Sandbox.Model (e.g. for SkinnedModelRenderer.Model, ModelRenderer.Model)
		if ( targetType == typeof( Model ) )
		{
			var path = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
			if ( string.IsNullOrEmpty( path ) || path == "null" ) return null;
			return Model.Load( path );
		}

		if ( typeof( Component ).IsAssignableFrom( targetType ) )
		{
			string guidStr = null;
			if ( el.ValueKind == JsonValueKind.String )
				guidStr = el.GetString();
			else if ( el.ValueKind == JsonValueKind.Object )
			{
				if ( el.TryGetProperty( "Id", out var idProp ) ) guidStr = idProp.GetString();
				else if ( el.TryGetProperty( "id", out var idProp2 ) ) guidStr = idProp2.GetString();
			}

			if ( !string.IsNullOrEmpty( guidStr ) && Guid.TryParse( guidStr, out var compGuid ) )
			{
				var scene = OzmiumSceneHelpers.ResolveScene();
				if ( scene != null )
				{
					foreach ( var go in OzmiumSceneHelpers.WalkAll( scene, true ) )
					{
						var match = go.Components.GetAll().FirstOrDefault( c => c.Id == compGuid );
						if ( match != null && targetType.IsAssignableFrom( match.GetType() ) )
							return match;
					}
				}
			}
			return null;
		}

		if ( typeof( GameObject ).IsAssignableFrom( targetType ) )
		{
			string guidStr = null;
			if ( el.ValueKind == JsonValueKind.String )
				guidStr = el.GetString();
			else if ( el.ValueKind == JsonValueKind.Object )
			{
				if ( el.TryGetProperty( "Id", out var idProp ) ) guidStr = idProp.GetString();
				else if ( el.TryGetProperty( "id", out var idProp2 ) ) guidStr = idProp2.GetString();
			}

			if ( !string.IsNullOrEmpty( guidStr ) && Guid.TryParse( guidStr, out var goGuid ) )
			{
				var scene = OzmiumSceneHelpers.ResolveScene();
				if ( scene != null )
				{
					return OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == goGuid );
				}
			}
			return null;
		}

		// Fallback: try parsing from string
		var raw = el.ValueKind == JsonValueKind.String ? el.GetString() : el.GetRawText();
		return Convert.ChangeType( raw, targetType );
	}

	// ── Tool schemas (used by ToolDefinitions.All) ───────────────────────────

	internal static Dictionary<string, object> SchemaCreateGameObject => OzmiumSceneHelpers.S( "create_game_object",
		"Create a new empty GameObject in the current scene.",
		OzmiumSceneHelpers.Ps( ("name", "string", "Name (default 'New GameObject')."), ("parentId", "string", "Parent GUID.") ) );

	internal static Dictionary<string, object> SchemaAddComponent => OzmiumSceneHelpers.S( "add_component",
		"Add a component to a GameObject. Use exact C# class name (e.g. 'PointLight', 'ModelRenderer').",
		OzmiumSceneHelpers.Ps( ("id","string","GUID."), ("name","string","Exact name."), ("componentType","string","C# class name.") ),
		new[] { "componentType" } );

	internal static Dictionary<string, object> SchemaRemoveComponent => OzmiumSceneHelpers.S( "remove_component",
		"Remove a component from a GameObject.",
		OzmiumSceneHelpers.Ps( ("id","string","GUID."), ("name","string","Exact name."), ("componentType","string","Type substring.") ),
		new[] { "componentType" } );

	internal static Dictionary<string, object> SchemaSetComponentProperty => OzmiumSceneHelpers.S( "set_component_property",
		"Set a property on a component. Supports string, bool, int, float, Vector3 {x,y,z}, enum.",
		new Dictionary<string, object>
		{
			["id"]            = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "GUID." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Exact name." },
			["componentType"] = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Type substring." },
			["propertyName"]  = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Exact C# property name." },
			["value"]         = new Dictionary<string, object> { ["description"] = "The value to set. Can be string, number, boolean, object {x,y,z} for Vector3, or Component/GameObject GUID." }
		},
		new[] { "propertyName" } );

	internal static Dictionary<string, object> SchemaDestroyGameObject => OzmiumSceneHelpers.S( "destroy_game_object",
		"Delete a GameObject.",
		OzmiumSceneHelpers.Ps( ("id","string","GUID."), ("name","string","Exact name.") ) );

	internal static Dictionary<string, object> SchemaReparentGameObject => OzmiumSceneHelpers.S( "reparent_game_object",
		"Move a GameObject under a new parent. Pass parentId='null' for root.",
		OzmiumSceneHelpers.Ps( ("id","string","GUID."), ("name","string","Exact name."), ("parentId","string","New parent GUID or 'null'.") ) );

	internal static Dictionary<string, object> SchemaSetGameObjectTags => OzmiumSceneHelpers.S( "set_game_object_tags",
		"Set/add/remove tags. Use 'set' array to replace all, 'add'/'remove' for incremental.",
		new Dictionary<string, object>
		{
			["id"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]   = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["set"]    = new Dictionary<string, object> { ["type"] = "array",  ["description"] = "Replace all tags with this list.", ["items"] = new Dictionary<string, object> { ["type"] = "string" } },
			["add"]    = new Dictionary<string, object> { ["type"] = "array",  ["description"] = "Tags to add.", ["items"] = new Dictionary<string, object> { ["type"] = "string" } },
			["remove"] = new Dictionary<string, object> { ["type"] = "array",  ["description"] = "Tags to remove.", ["items"] = new Dictionary<string, object> { ["type"] = "string" } },
		} );

	internal static Dictionary<string, object> SchemaInstantiatePrefab => OzmiumSceneHelpers.S( "instantiate_prefab",
		"Spawn a prefab at a world position. Use browse_assets to find the path first.",
		OzmiumSceneHelpers.Ps( ("path","string","Prefab asset path."), ("x","number","World X."), ("y","number","World Y."), ("z","number","World Z."), ("parentId","string","Optional parent GUID.") ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaSaveScene => OzmiumSceneHelpers.S( "save_scene",
		"Save the currently open scene or prefab to disk.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaUndo => OzmiumSceneHelpers.S( "undo",
		"Undo the last editor operation.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaRedo => OzmiumSceneHelpers.S( "redo",
		"Redo the last undone editor operation.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaSetGameObjectTransform => OzmiumSceneHelpers.S( "set_game_object_transform",
		"Set position, rotation, and scale of a GameObject in one call.",
		new Dictionary<string, object>
		{
			["id"]       = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "GUID." },
			["name"]     = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Exact name." },
			["position"] = new Dictionary<string, object> { ["type"] = "object",  ["description"] = "World position {x,y,z}." },
			["rotation"] = new Dictionary<string, object> { ["type"] = "object",  ["description"] = "World rotation {pitch,yaw,roll} in degrees." },
			["scale"]    = new Dictionary<string, object> { ["type"] = "object",  ["description"] = "World scale {x,y,z}." }
		} );

	internal static Dictionary<string, object> SchemaDuplicateGameObject => OzmiumSceneHelpers.S( "duplicate_game_object",
		"Clone a GameObject with optional new position/name.",
		new Dictionary<string, object>
		{
			["id"]       = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." },
			["name"]     = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." },
			["position"] = new Dictionary<string, object> { ["type"] = "object", ["description"] = "Optional world position {x,y,z} for the clone." },
			["newName"]  = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Optional new name for the clone." }
		} );

	internal static Dictionary<string, object> SchemaSetGameObjectEnabled => OzmiumSceneHelpers.S( "set_game_object_enabled",
		"Toggle a GameObject's enabled state.",
		new Dictionary<string, object>
		{
			["id"]      = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "GUID." },
			["name"]    = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Exact name." },
			["enabled"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Set enabled state. Omit to toggle." }
		} );

	internal static Dictionary<string, object> SchemaSetGameObjectName => OzmiumSceneHelpers.S( "set_game_object_name",
		"Rename a GameObject.",
		OzmiumSceneHelpers.Ps( ("id","string","GUID."), ("name","string","Exact name."), ("newName","string","New name for the object.") ),
		new[] { "newName" } );

	internal static Dictionary<string, object> SchemaSetComponentEnabled => OzmiumSceneHelpers.S( "set_component_enabled",
		"Toggle a component's enabled state.",
		new Dictionary<string, object>
		{
			["id"]            = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "GUID." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Exact name." },
			["componentType"] = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Type substring." },
			["enabled"]       = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Enabled state." }
		},
		new[] { "componentType", "enabled" } );
}
