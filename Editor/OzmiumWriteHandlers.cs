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
	private static readonly JsonSerializerOptions _json = new()
	{
		PropertyNamingPolicy   = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
	};

	// ── create_game_object ──────────────────────────────────────────────────

	internal static object CreateGameObject( JsonElement args )
	{
		var scene    = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string name     = Get( args, "name",     "New GameObject" );
		string parentId = Get( args, "parentId", (string)null );

		try
		{
			var go = scene.CreateObject();
			go.Name = name;

			if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var guid ) )
			{
				var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( parent != null ) go.SetParent( parent );
			}

			return Txt( JsonSerializer.Serialize( new
			{
				message = $"Created '{go.Name}'.",
				id      = go.Id.ToString(),
				path    = OzmiumSceneHelpers.GetObjectPath( go )
			}, _json ) );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── add_component ───────────────────────────────────────────────────────

	internal static object AddComponent( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string id   = Get( args, "id",            (string)null );
		string name = Get( args, "name",          (string)null );
		string type = Get( args, "componentType", (string)null );

		if ( string.IsNullOrEmpty( type ) ) return Txt( "Provide 'componentType'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return Txt( "Object not found." );

		try
		{
			var compType = FindComponentType( type );
			if ( compType == null ) return Txt( $"Component type '{type}' not found. Use the exact class name." );

			// Use TypeLibrary to get the TypeDescription required by Components.Create
			var td = TypeLibrary.GetType( compType );
			if ( td == null ) return Txt( $"Could not get TypeDescription for '{compType.Name}'." );
			go.Components.Create( td );
			return Txt( $"Added '{compType.Name}' to '{go.Name}'." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── remove_component ────────────────────────────────────────────────────

	internal static object RemoveComponent( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string id   = Get( args, "id",            (string)null );
		string name = Get( args, "name",          (string)null );
		string type = Get( args, "componentType", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return Txt( "Object not found." );

		var comp = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( type ?? "", StringComparison.OrdinalIgnoreCase ) >= 0 );
		if ( comp == null ) return Txt( $"No component '{type}' found on '{go.Name}'." );

		try
		{
			comp.Destroy();
			return Txt( $"Removed '{comp.GetType().Name}' from '{go.Name}'." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_component_property ──────────────────────────────────────────────

	internal static object SetComponentProperty( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string id           = Get( args, "id",            (string)null );
		string objName      = Get( args, "name",          (string)null );
		string compType     = Get( args, "componentType", (string)null );
		string propName     = Get( args, "propertyName",  (string)null );

		if ( string.IsNullOrEmpty( propName ) ) return Txt( "Provide 'propertyName'." );
		if ( !args.TryGetProperty( "value", out var valEl ) ) return Txt( "Provide 'value'." );

		var go = OzmiumSceneHelpers.FindGo( scene, id, objName );
		if ( go == null ) return Txt( "Object not found." );

		var comp = go.Components.GetAll().FirstOrDefault( c =>
			string.IsNullOrEmpty( compType ) ||
			c.GetType().Name.IndexOf( compType, StringComparison.OrdinalIgnoreCase ) >= 0 );
		if ( comp == null ) return Txt( $"Component '{compType}' not found." );

		var prop = comp.GetType().GetProperty( propName,
			System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance );
		if ( prop == null ) return Txt( $"Property '{propName}' not found on '{comp.GetType().Name}'." );
		if ( !prop.CanWrite ) return Txt( $"Property '{propName}' is read-only." );

		try
		{
			object converted = ConvertJsonValue( valEl, prop.PropertyType );
			prop.SetValue( comp, converted );
			var readback = prop.GetValue( comp );
			return Txt( $"Set '{comp.GetType().Name}.{propName}' = {readback}" );
		}
		catch ( Exception ex ) { return Txt( $"Error setting property: {ex.Message}" ); }
	}

	// ── destroy_game_object ─────────────────────────────────────────────────

	internal static object DestroyGameObject( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string id   = Get( args, "id",   (string)null );
		string name = Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return Txt( "Object not found." );

		var displayName = go.Name;
		try
		{
			go.Destroy();
			return Txt( $"Destroyed '{displayName}'." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── reparent_game_object ────────────────────────────────────────────────

	internal static object ReparentGameObject( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string id       = Get( args, "id",       (string)null );
		string name     = Get( args, "name",     (string)null );
		string parentId = Get( args, "parentId", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return Txt( "Object not found." );

		try
		{
			if ( string.IsNullOrEmpty( parentId ) || parentId == "null" )
			{
				go.SetParent( null );
				return Txt( $"Moved '{go.Name}' to scene root." );
			}

			if ( !Guid.TryParse( parentId, out var guid ) ) return Txt( "Invalid parentId GUID." );
			var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
			if ( parent == null ) return Txt( $"Parent '{parentId}' not found." );
			go.SetParent( parent );
			return Txt( $"Moved '{go.Name}' under '{parent.Name}'." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── set_game_object_tags ────────────────────────────────────────────────

	internal static object SetGameObjectTags( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string id   = Get( args, "id",   (string)null );
		string name = Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return Txt( "Object not found." );

		try
		{
			// set: replace all tags
			if ( args.TryGetProperty( "set", out var setEl ) && setEl.ValueKind == JsonValueKind.Array )
			{
				go.Tags.RemoveAll();
				foreach ( var t in setEl.EnumerateArray() ) go.Tags.Add( t.GetString() );
				return Txt( $"Tags on '{go.Name}': {string.Join( ", ", go.Tags.TryGetAll() )}" );
			}
			// add/remove individual tags
			if ( args.TryGetProperty( "add", out var addEl ) && addEl.ValueKind == JsonValueKind.Array )
				foreach ( var t in addEl.EnumerateArray() ) go.Tags.Add( t.GetString() );
			if ( args.TryGetProperty( "remove", out var remEl ) && remEl.ValueKind == JsonValueKind.Array )
				foreach ( var t in remEl.EnumerateArray() ) go.Tags.Remove( t.GetString() );

			return Txt( $"Tags on '{go.Name}': {string.Join( ", ", go.Tags.TryGetAll() )}" );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── instantiate_prefab ──────────────────────────────────────────────────

	internal static object InstantiatePrefab( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return Txt( "No active scene." );

		string path     = Get( args, "path",     (string)null );
		float  x        = Get( args, "x",        0f );
		float  y        = Get( args, "y",        0f );
		float  z        = Get( args, "z",        0f );
		string parentId = Get( args, "parentId", (string)null );

		if ( string.IsNullOrEmpty( path ) ) return Txt( "Provide 'path' (prefab asset path)." );

		try
		{
			// Verify the asset exists
			var asset = AssetSystem.FindByPath( path );
			if ( asset == null )
				return Txt( $"Asset not found: '{path}'. Use browse_assets with type='prefab' to find valid prefab paths." );

			var prefabFile = ResourceLibrary.Get<PrefabFile>( path );
			if ( prefabFile == null )
				return Txt( $"Could not load prefab: '{path}'." );

			var prefabScene = SceneUtility.GetPrefabScene( prefabFile );
			var go = prefabScene.Clone();
			go.WorldPosition = new Vector3( x, y, z );

			if ( !string.IsNullOrEmpty( parentId ) && Guid.TryParse( parentId, out var guid ) )
			{
				var parent = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == guid );
				if ( parent != null ) go.SetParent( parent );
			}

			return Txt( JsonSerializer.Serialize( new
			{
				message  = $"Instantiated '{path}'.",
				id       = go.Id.ToString(),
				name     = go.Name,
				position = OzmiumSceneHelpers.V3( go.WorldPosition )
			}, _json ) );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── save_scene ──────────────────────────────────────────────────────────

	internal static object SaveScene()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return Txt( "No editor session active." );
			session.Save( false );
			return Txt( $"Saved '{session.Scene?.Name}'." );
		}
		catch ( Exception ex ) { return Txt( $"Error saving: {ex.Message}" ); }
	}

	// ── undo / redo ─────────────────────────────────────────────────────────

	internal static object Undo()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return Txt( "No editor session active." );
			var us = session.UndoSystem;
			if ( us == null ) return Txt( "UndoSystem not available." );
			us.Undo();
			return Txt( "Undo performed." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	internal static object Redo()
	{
		try
		{
			var session = SceneEditorSession.Active;
			if ( session == null ) return Txt( "No editor session active." );
			var us = session.UndoSystem;
			if ( us == null ) return Txt( "UndoSystem not available." );
			us.Redo();
			return Txt( "Redo performed." );
		}
		catch ( Exception ex ) { return Txt( $"Error: {ex.Message}" ); }
	}

	// ── Private helpers ─────────────────────────────────────────────────────

	private static Type FindComponentType( string typeName )
	{
		foreach ( var asm in AppDomain.CurrentDomain.GetAssemblies() )
		{
			try
			{
				foreach ( var t in asm.GetTypes() )
					if ( t.IsClass && !t.IsAbstract && typeof( Component ).IsAssignableFrom( t ) )
						if ( t.Name.Equals( typeName, StringComparison.OrdinalIgnoreCase ) ) return t;
			}
			catch { }
		}
		return null;
	}

	private static object ConvertJsonValue( JsonElement el, Type targetType )
	{
		if ( targetType == typeof( string ) )  return el.GetString();
		if ( targetType == typeof( bool ) )    return el.GetBoolean();
		if ( targetType == typeof( int ) )     return el.GetInt32();
		if ( targetType == typeof( float ) )   return el.GetSingle();
		if ( targetType == typeof( double ) )  return el.GetDouble();
		if ( targetType == typeof( Vector3 ) && el.ValueKind == JsonValueKind.Object )
		{
			float vx = 0, vy = 0, vz = 0;
			if ( el.TryGetProperty( "x", out var xp ) ) vx = xp.GetSingle();
			if ( el.TryGetProperty( "y", out var yp ) ) vy = yp.GetSingle();
			if ( el.TryGetProperty( "z", out var zp ) ) vz = zp.GetSingle();
			return new Vector3( vx, vy, vz );
		}
		if ( targetType.IsEnum ) return Enum.Parse( targetType, el.GetString(), ignoreCase: true );
		// Fallback: try parsing from string
		return Convert.ChangeType( el.GetString() ?? el.GetRawText(), targetType );
	}

	private static object Txt( string text ) => new { content = new object[] { new { type = "text", text } } };

	private static T Get<T>( JsonElement el, string key, T def )
	{
		if ( el.ValueKind == JsonValueKind.Undefined ) return def;
		if ( !el.TryGetProperty( key, out var p ) ) return def;
		try
		{
			var t = typeof( T );
			if ( t == typeof( string ) ) return (T)(object)( p.ValueKind == JsonValueKind.Null ? null : p.GetString() );
			if ( t == typeof( bool ) )   return (T)(object)p.GetBoolean();
			if ( t == typeof( int ) )    return (T)(object)p.GetInt32();
			if ( t == typeof( float ) )  return (T)(object)p.GetSingle();
			return def;
		}
		catch { return def; }
	}

	// ── Tool schemas (used by ToolDefinitions.All) ───────────────────────────

	private static Dictionary<string, object> S( string name, string desc, Dictionary<string, object> props, string[] req = null )
	{
		var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
		if ( req != null ) schema["required"] = req;
		return new Dictionary<string, object> { ["name"] = name, ["description"] = desc, ["inputSchema"] = schema };
	}
	private static Dictionary<string, object> Ps( params (string k, string type, string d)[] fields )
	{
		var d = new Dictionary<string, object>();
		foreach ( var (k, tp, desc) in fields )
			d[k] = new Dictionary<string, object> { ["type"] = tp, ["description"] = desc };
		return d;
	}

	internal static Dictionary<string, object> SchemaCreateGameObject => S( "create_game_object",
		"Create a new empty GameObject in the current scene.",
		Ps( ("name", "string", "Name (default 'New GameObject')."), ("parentId", "string", "Parent GUID.") ) );

	internal static Dictionary<string, object> SchemaAddComponent => S( "add_component",
		"Add a component to a GameObject. Use exact C# class name (e.g. 'PointLight', 'ModelRenderer').",
		Ps( ("id","string","GUID."), ("name","string","Exact name."), ("componentType","string","C# class name.") ),
		new[] { "componentType" } );

	internal static Dictionary<string, object> SchemaRemoveComponent => S( "remove_component",
		"Remove a component from a GameObject.",
		Ps( ("id","string","GUID."), ("name","string","Exact name."), ("componentType","string","Type substring.") ),
		new[] { "componentType" } );

	internal static Dictionary<string, object> SchemaSetComponentProperty => S( "set_component_property",
		"Set a property on a component. Supports string, bool, int, float, Vector3 {x,y,z}, enum.",
		new Dictionary<string, object>
		{
			["id"]            = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "GUID." },
			["name"]          = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Exact name." },
			["componentType"] = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Type substring." },
			["propertyName"]  = new Dictionary<string, object> { ["type"] = "string",  ["description"] = "Exact C# property name." },
			["value"]         = new Dictionary<string, object> { ["description"] = "The value to set. Can be string, number, boolean, or object {x,y,z} for Vector3." }
		},
		new[] { "propertyName" } );

	internal static Dictionary<string, object> SchemaDestroyGameObject => S( "destroy_game_object",
		"Delete a GameObject.",
		Ps( ("id","string","GUID."), ("name","string","Exact name.") ) );

	internal static Dictionary<string, object> SchemaReparentGameObject => S( "reparent_game_object",
		"Move a GameObject under a new parent. Pass parentId='null' for root.",
		Ps( ("id","string","GUID."), ("name","string","Exact name."), ("parentId","string","New parent GUID or 'null'.") ) );

	internal static Dictionary<string, object> SchemaSetGameObjectTags => S( "set_game_object_tags",
		"Set/add/remove tags. Use 'set' array to replace all, 'add'/'remove' for incremental.",
		Ps( ("id","string","GUID."), ("name","string","Exact name.") ) );

	internal static Dictionary<string, object> SchemaInstantiatePrefab => S( "instantiate_prefab",
		"Spawn a prefab at a world position. Use browse_assets to find the path first.",
		Ps( ("path","string","Prefab asset path."), ("x","number","World X."), ("y","number","World Y."), ("z","number","World Z."), ("parentId","string","Optional parent GUID.") ),
		new[] { "path" } );

	internal static Dictionary<string, object> SchemaSaveScene => S( "save_scene",
		"Save the currently open scene or prefab to disk.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaUndo => S( "undo",
		"Undo the last editor operation.",
		new Dictionary<string, object>() );

	internal static Dictionary<string, object> SchemaRedo => S( "redo",
		"Redo the last undone editor operation.",
		new Dictionary<string, object>() );
}
