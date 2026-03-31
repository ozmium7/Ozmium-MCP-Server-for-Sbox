using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// Visibility and culling management MCP tools: create/delete/list culling boxes,
/// manage editor-only tags, bulk hide objects for gameplay.
/// </summary>
internal static class VisibilityToolHandlers
{

	/// <summary>Tracks culling boxes by their parent GameObject ID.</summary>
	private static readonly Dictionary<Guid, (SceneCullingBox Box, string Name)> _cullingBoxes = new();

	private const string EditorOnlyTag = "editor_only";

	// ── create_culling_box ─────────────────────────────────────────────────

	private static object CreateCullingBox( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		float x = OzmiumSceneHelpers.Get( args, "x", 0f );
		float y = OzmiumSceneHelpers.Get( args, "y", 0f );
		float z = OzmiumSceneHelpers.Get( args, "z", 0f );
		string name = OzmiumSceneHelpers.Get( args, "name", "CullingBox" );
		float sizeX = OzmiumSceneHelpers.Get( args, "sizeX", 500f );
		float sizeY = OzmiumSceneHelpers.Get( args, "sizeY", 500f );
		float sizeZ = OzmiumSceneHelpers.Get( args, "sizeZ", 500f );
		string mode = OzmiumSceneHelpers.Get( args, "mode", "Inside" );
		float pitch = OzmiumSceneHelpers.Get( args, "pitch", 0f );
		float yaw = OzmiumSceneHelpers.Get( args, "yaw", 0f );
		float roll = OzmiumSceneHelpers.Get( args, "roll", 0f );

		try
		{
			var sceneWorld = scene.SceneWorld;
			if ( sceneWorld == null )
				return OzmiumSceneHelpers.Txt( "Scene has no SceneWorld (needed for culling boxes)." );

			var go = scene.CreateObject();
			go.Name = name;
			go.WorldPosition = new Vector3( x, y, z );
			go.WorldRotation = Rotation.From( pitch, yaw, roll );

			var transform = new Transform( go.WorldPosition, go.WorldRotation );
			var size = new Vector3( sizeX, sizeY, sizeZ );

			var cullMode = mode.Equals( "Outside", StringComparison.OrdinalIgnoreCase )
				? SceneCullingBox.CullMode.Outside
				: SceneCullingBox.CullMode.Inside;

			var box = new SceneCullingBox( sceneWorld, transform, size, cullMode );

			_cullingBoxes[go.Id] = (box, go.Name);

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Created culling box '{go.Name}'.",
				id = go.Id.ToString(),
				name = go.Name,
				position = OzmiumSceneHelpers.V3( go.WorldPosition ),
				size = new { x = sizeX, y = sizeY, z = sizeZ },
				mode = cullMode.ToString()
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── delete_culling_box ─────────────────────────────────────────────────

	private static object DeleteCullingBox( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		if ( !_cullingBoxes.TryGetValue( go.Id, out var entry ) )
			return OzmiumSceneHelpers.Txt( $"No culling box tracked for '{go.Name}'." );

		try
		{
			entry.Box.Delete();
			_cullingBoxes.Remove( go.Id );
			go.Destroy();

			return OzmiumSceneHelpers.Txt( $"Deleted culling box '{entry.Name}'." );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── list_culling_boxes ─────────────────────────────────────────────────

	private static object ListCullingBoxes( JsonElement args )
	{
		if ( _cullingBoxes.Count == 0 )
			return OzmiumSceneHelpers.Txt( "No culling boxes tracked." );

		var list = _cullingBoxes.Select( kvp =>
		{
			var scene = OzmiumSceneHelpers.ResolveScene();
			GameObject go = null;
			if ( scene != null )
				go = OzmiumSceneHelpers.WalkAll( scene, true ).FirstOrDefault( g => g.Id == kvp.Key );

			return new Dictionary<string, object>
			{
				["id"] = kvp.Key.ToString(),
				["name"] = kvp.Value.Name,
				["valid"] = kvp.Value.Box.IsValid,
				["position"] = go != null ? OzmiumSceneHelpers.V3( go.WorldPosition ) : null,
				["size"] = OzmiumSceneHelpers.V3( kvp.Value.Box.Size ),
				["mode"] = kvp.Value.Box.Mode.ToString(),
			};
		} ).ToList();

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			summary = $"Tracking {_cullingBoxes.Count} culling box(es).",
			cullingBoxes = list
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── set_editor_only ────────────────────────────────────────────────────

	private static object SetEditorOnly( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );
		bool editorOnly = OzmiumSceneHelpers.Get( args, "editorOnly", true );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( "Object not found." );

		if ( editorOnly )
			go.Tags.Add( EditorOnlyTag );
		else
			go.Tags.Remove( EditorOnlyTag );

		return OzmiumSceneHelpers.Txt( $"'{go.Name}' editor_only = {editorOnly}. Tags: {string.Join( ", ", go.Tags.TryGetAll() )}" );
	}

	// ── list_editor_only ──────────────────────────────────────────────────

	private static object ListEditorOnly( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		var objects = OzmiumSceneHelpers.WalkAll( scene, true )
			.Where( g => g.Tags.Has( EditorOnlyTag ) )
			.Select( g => new Dictionary<string, object>
			{
				["id"] = g.Id.ToString(),
				["name"] = g.Name,
				["path"] = OzmiumSceneHelpers.GetObjectPath( g ),
				["position"] = OzmiumSceneHelpers.V3( g.WorldPosition )
			} )
			.ToList();

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
		{
			summary = $"Found {objects.Count} editor-only object(s).",
			objects
		}, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── hide_in_game ───────────────────────────────────────────────────────

	private static object HideInGame( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

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
			go.Tags.Add( EditorOnlyTag );
			modified++;
		}

		return OzmiumSceneHelpers.Txt( $"Added 'editor_only' tag to {modified} object(s) (found {found} of {ids.Count})." );
	}

	// ── manage_visibility (Omnibus) ───────────────────────────────────────

	internal static object ManageVisibility( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"create_culling_box" => CreateCullingBox( args ),
			"delete_culling_box" => DeleteCullingBox( args ),
			"list_culling_boxes" => ListCullingBoxes( args ),
			"set_editor_only"    => SetEditorOnly( args ),
			"list_editor_only"   => ListEditorOnly( args ),
			"hide_in_game"       => HideInGame( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: create_culling_box, delete_culling_box, list_culling_boxes, set_editor_only, list_editor_only, hide_in_game" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaManageVisibility
	{
		get
		{
			var stringArrayItem = new Dictionary<string, object> { ["type"] = "string" };
			var props = new Dictionary<string, object>();

			props["operation"] = new Dictionary<string, object>
			{
				["type"] = "string",
				["description"] = "Operation to perform.",
				["enum"] = new[] { "create_culling_box", "delete_culling_box", "list_culling_boxes", "set_editor_only", "list_editor_only", "hide_in_game" }
			};

			// create_culling_box params
			props["x"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World X position." };
			props["y"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Y position." };
			props["z"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "World Z position." };
			props["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Name for the GO (default 'CullingBox')." };
			props["sizeX"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box size X (default 500)." };
			props["sizeY"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box size Y (default 500)." };
			props["sizeZ"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Box size Z (default 500)." };
			props["mode"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Culling mode: Inside (hide objects inside box) or Outside (hide objects outside all boxes)." };
			props["pitch"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Pitch rotation in degrees (default 0)." };
			props["yaw"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Yaw rotation in degrees (default 0)." };
			props["roll"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Roll rotation in degrees (default 0)." };

			// set_editor_only params
			props["editorOnly"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Set editor_only tag (true) or remove it (false) for set_editor_only." };

			// hide_in_game params
			props["ids"] = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Array of GUIDs for hide_in_game.", ["items"] = stringArrayItem };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object>
			{
				["name"] = "manage_visibility",
				["description"] = "Control object visibility, render culling, and editor-only helpers. Create/delete/list SceneCullingBox volumes for performance culling, mark objects as editor_only to hide during gameplay, or bulk-hide objects.",
				["inputSchema"] = schema
			};
		}
	}
}
