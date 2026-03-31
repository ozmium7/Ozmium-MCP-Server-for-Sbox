using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Sandbox;

namespace SboxMcpServer;

/// <summary>
/// High-level game entity configuration MCP tools: configure doors, spawn points,
/// triggers, props, world panels, chairs, and dressers with domain-specific
/// property groups. Does NOT create entities (use create_game_entity for that).
/// </summary>
internal static class GameEntityConfigToolHandlers
{

	private static readonly System.Reflection.BindingFlags PublicSet = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance;

	// ── configure_door ─────────────────────────────────────────────────────

	private static object ConfigureDoor( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'. Use find_game_objects to locate the correct name." );

		var doorComp = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( "door", StringComparison.OrdinalIgnoreCase ) >= 0
			&& c.GetType().Name.IndexOf( "collider", StringComparison.OrdinalIgnoreCase ) < 0 );

		if ( doorComp == null )
			return OzmiumSceneHelpers.Txt( $"No door component found on '{go.Name}'. Ensure the DarkRP addon is loaded and the object has a door component." );

		var modified = new List<string>();
		var errors = new List<string>();

		// DarkRP door properties — set via reflection
		void TrySet( string prop, JsonElement el )
		{
			var p = doorComp.GetType().GetProperty( prop, PublicSet );
			if ( p == null || !p.CanWrite ) { errors.Add( $"Property '{prop}' not found/writable." ); return; }
			try { p.SetValue( doorComp, OzmiumWriteHandlers.ConvertJsonValue( el, p.PropertyType ) ); modified.Add( prop ); }
			catch ( Exception ex ) { errors.Add( $"Error setting '{prop}': {ex.Message}" ); }
		}

		if ( args.TryGetProperty( "isPurchasable", out var el1 ) ) TrySet( "IsPurchasable", el1 );
		if ( args.TryGetProperty( "isOwnable", out var el2 ) ) TrySet( "IsOwnable", el2 );
		if ( args.TryGetProperty( "isLocked", out var el3 ) ) TrySet( "IsLocked", el3 );
		if ( args.TryGetProperty( "buyAmount", out var el4 ) ) TrySet( "BuyAmount", el4 );

		if ( args.TryGetProperty( "doorGroups", out var dgEl ) && dgEl.ValueKind == JsonValueKind.Array )
		{
			var prop = doorComp.GetType().GetProperty( "DoorGroups", PublicSet );
			if ( prop != null && prop.CanWrite )
			{
				try
				{
					var groups = dgEl.EnumerateArray().Select( e => e.GetString() ).ToList();
					prop.SetValue( doorComp, groups );
					modified.Add( "DoorGroups" );
				}
				catch ( Exception ex ) { errors.Add( $"Error setting DoorGroups: {ex.Message}" ); }
			}
		}

		if ( args.TryGetProperty( "blacklistedGroups", out var bgEl ) && bgEl.ValueKind == JsonValueKind.Array )
		{
			var prop = doorComp.GetType().GetProperty( "BlacklistedGroups", PublicSet );
			if ( prop != null && prop.CanWrite )
			{
				try
				{
					var groups = bgEl.EnumerateArray().Select( e => e.GetString() ).ToList();
					prop.SetValue( doorComp, groups );
					modified.Add( "BlacklistedGroups" );
				}
				catch ( Exception ex ) { errors.Add( $"Error setting BlacklistedGroups: {ex.Message}" ); }
			}
		}

		var result = new Dictionary<string, object>
		{
			["message"] = $"Configured door '{go.Name}' ({doorComp.GetType().Name}). Modified: {string.Join( ", ", modified )}"
		};
		if ( errors.Count > 0 ) result["errors"] = errors;

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( result, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── configure_spawn_point ──────────────────────────────────────────────

	private static object ConfigureSpawnPoint( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'. Use find_game_objects to locate the correct name." );

		try
		{
			string teamName = OzmiumSceneHelpers.Get( args, "teamName", (string)null );
			if ( !string.IsNullOrEmpty( teamName ) )
				go.Tags.Add( $"team:{teamName}" );

			if ( args.TryGetProperty( "color", out var colEl ) && colEl.ValueKind == JsonValueKind.String )
			{
				var tintProp = go.Components.GetAll().FirstOrDefault( c => c.GetType().Name == "SpawnPoint" )
					?.GetType().GetProperty( "ColorTint", PublicSet );
				if ( tintProp != null && tintProp.CanWrite )
				{
					var colorStr = colEl.GetString();
					if ( Color.TryParse( colorStr, out var color ) )
						tintProp.SetValue( go.Components.Get<SpawnPoint>(), color );
				}
			}

			return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( new
			{
				message = $"Configured spawn point '{go.Name}'.",
				id = go.Id.ToString(),
				tags = OzmiumSceneHelpers.GetTags( go )
			}, OzmiumSceneHelpers.JsonSettings ) );
		}
		catch ( Exception ex ) { return OzmiumSceneHelpers.Txt( $"Error: {ex.Message}" ); }
	}

	// ── configure_trigger ──────────────────────────────────────────────────

	private static object ConfigureTrigger( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'. Use find_game_objects to locate the correct name." );

		// Find TriggerHurt or similar trigger component
		var trigger = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( "TriggerHurt", StringComparison.OrdinalIgnoreCase ) >= 0
			|| c.GetType().Name.IndexOf( "Trigger", StringComparison.OrdinalIgnoreCase ) >= 0 );

		if ( trigger == null )
			return OzmiumSceneHelpers.Txt( $"No trigger component found on '{go.Name}'." );

		var modified = new List<string>();
		var errors = new List<string>();

		void TrySet( string prop, JsonElement el )
		{
			var p = trigger.GetType().GetProperty( prop, PublicSet );
			if ( p == null || !p.CanWrite ) { errors.Add( $"Property '{prop}' not found/writable." ); return; }
			try { p.SetValue( trigger, OzmiumWriteHandlers.ConvertJsonValue( el, p.PropertyType ) ); modified.Add( prop ); }
			catch ( Exception ex ) { errors.Add( $"Error setting '{prop}': {ex.Message}" ); }
		}

		if ( args.TryGetProperty( "damage", out var d ) ) TrySet( "Damage", d );
		if ( args.TryGetProperty( "rate", out var r ) ) TrySet( "DamageRate", r );
		if ( args.TryGetProperty( "startEnabled", out var se ) ) TrySet( "StartEnabled", se );
		if ( args.TryGetProperty( "damageTags", out var dtEl ) && dtEl.ValueKind == JsonValueKind.String )
		{
			TrySet( "DamageTags", dtEl );
		}

		var result = new Dictionary<string, object>
		{
			["message"] = $"Configured trigger '{go.Name}' ({trigger.GetType().Name}). Modified: {string.Join( ", ", modified )}"
		};
		if ( errors.Count > 0 ) result["errors"] = errors;

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( result, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── configure_prop ─────────────────────────────────────────────────────

	private static object ConfigureProp( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'. Use find_game_objects to locate the correct name." );

		// Find Prop component
		var prop = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( "Prop", StringComparison.OrdinalIgnoreCase ) >= 0 );

		if ( prop == null )
			return OzmiumSceneHelpers.Txt( $"No Prop component found on '{go.Name}'." );

		var modified = new List<string>();
		var errors = new List<string>();

		void TrySetProp( string propName, JsonElement el )
		{
			var p = prop.GetType().GetProperty( propName, PublicSet );
			if ( p == null || !p.CanWrite ) { errors.Add( $"Property '{propName}' not found/writable." ); return; }
			try { p.SetValue( prop, OzmiumWriteHandlers.ConvertJsonValue( el, p.PropertyType ) ); modified.Add( propName ); }
			catch ( Exception ex ) { errors.Add( $"Error setting '{propName}': {ex.Message}" ); }
		}

		if ( args.TryGetProperty( "health", out var h ) ) TrySetProp( "Health", h );
		if ( args.TryGetProperty( "isStatic", out var s ) ) TrySetProp( "IsStatic", s );
		if ( args.TryGetProperty( "tint", out var t ) ) TrySetProp( "Tint", t );
		if ( args.TryGetProperty( "bodyGroups", out var bg ) ) TrySetProp( "BodyGroups", bg );

		var result = new Dictionary<string, object>
		{
			["message"] = $"Configured prop '{go.Name}' ({prop.GetType().Name}). Modified: {string.Join( ", ", modified )}"
		};
		if ( errors.Count > 0 ) result["errors"] = errors;

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( result, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── configure_world_panel ──────────────────────────────────────────────

	private static object ConfigureWorldPanel( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'. Use find_game_objects to locate the correct name." );

		var panel = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( "WorldPanel", StringComparison.OrdinalIgnoreCase ) >= 0
			|| c.GetType().Name.IndexOf( "HtmlPanel", StringComparison.OrdinalIgnoreCase ) >= 0 );

		if ( panel == null )
			return OzmiumSceneHelpers.Txt( $"No WorldPanel/HtmlPanel component found on '{go.Name}'." );

		var modified = new List<string>();
		var errors = new List<string>();

		void TrySetPanel( string propName, JsonElement el )
		{
			var p = panel.GetType().GetProperty( propName, PublicSet );
			if ( p == null || !p.CanWrite ) { errors.Add( $"Property '{propName}' not found/writable." ); return; }
			try { p.SetValue( panel, OzmiumWriteHandlers.ConvertJsonValue( el, p.PropertyType ) ); modified.Add( propName ); }
			catch ( Exception ex ) { errors.Add( $"Error setting '{propName}': {ex.Message}" ); }
		}

		if ( args.TryGetProperty( "panelSize", out var ps ) ) TrySetPanel( "PanelSize", ps );
		if ( args.TryGetProperty( "renderScale", out var rs ) ) TrySetPanel( "RenderScale", rs );
		if ( args.TryGetProperty( "lookAtCamera", out var lac ) ) TrySetPanel( "LookAtCamera", lac );
		if ( args.TryGetProperty( "interactionRange", out var ir ) ) TrySetPanel( "InteractionRange", ir );

		var result = new Dictionary<string, object>
		{
			["message"] = $"Configured panel '{go.Name}' ({panel.GetType().Name}). Modified: {string.Join( ", ", modified )}"
		};
		if ( errors.Count > 0 ) result["errors"] = errors;

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( result, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── configure_chair ────────────────────────────────────────────────────

	private static object ConfigureChair( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'. Use find_game_objects to locate the correct name." );

		var chair = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( "BaseChair", StringComparison.OrdinalIgnoreCase ) >= 0
			|| c.GetType().Name.IndexOf( "Chair", StringComparison.OrdinalIgnoreCase ) >= 0 );

		if ( chair == null )
			return OzmiumSceneHelpers.Txt( $"No Chair component found on '{go.Name}'." );

		var modified = new List<string>();
		var errors = new List<string>();

		void TrySetChair( string propName, JsonElement el )
		{
			var p = chair.GetType().GetProperty( propName, PublicSet );
			if ( p == null || !p.CanWrite ) { errors.Add( $"Property '{propName}' not found/writable." ); return; }
			try { p.SetValue( chair, OzmiumWriteHandlers.ConvertJsonValue( el, p.PropertyType ) ); modified.Add( propName ); }
			catch ( Exception ex ) { errors.Add( $"Error setting '{propName}': {ex.Message}" ); }
		}

		if ( args.TryGetProperty( "sitPose", out var sp ) ) TrySetChair( "SitPose", sp );
		if ( args.TryGetProperty( "sitHeight", out var sh ) ) TrySetChair( "SitHeight", sh );
		if ( args.TryGetProperty( "tooltipTitle", out var tt ) ) TrySetChair( "TooltipTitle", tt );

		var result = new Dictionary<string, object>
		{
			["message"] = $"Configured chair '{go.Name}' ({chair.GetType().Name}). Modified: {string.Join( ", ", modified )}"
		};
		if ( errors.Count > 0 ) result["errors"] = errors;

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( result, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── configure_dresser ──────────────────────────────────────────────────

	private static object ConfigureDresser( JsonElement args )
	{
		var scene = OzmiumSceneHelpers.ResolveScene();
		if ( scene == null ) return OzmiumSceneHelpers.Txt( "No active scene." );

		string id = OzmiumSceneHelpers.Get( args, "id", (string)null );
		string name = OzmiumSceneHelpers.Get( args, "name", (string)null );

		var go = OzmiumSceneHelpers.FindGo( scene, id, name );
		if ( go == null ) return OzmiumSceneHelpers.Txt( $"Object not found: id='{id}' name='{name}'. Use find_game_objects to locate the correct name." );

		var dresser = go.Components.GetAll().FirstOrDefault( c =>
			c.GetType().Name.IndexOf( "Dresser", StringComparison.OrdinalIgnoreCase ) >= 0 );

		if ( dresser == null )
			return OzmiumSceneHelpers.Txt( $"No Dresser component found on '{go.Name}'." );

		var modified = new List<string>();
		var errors = new List<string>();

		void TrySetDresser( string propName, JsonElement el )
		{
			var p = dresser.GetType().GetProperty( propName, PublicSet );
			if ( p == null || !p.CanWrite ) { errors.Add( $"Property '{propName}' not found/writable." ); return; }
			try { p.SetValue( dresser, OzmiumWriteHandlers.ConvertJsonValue( el, p.PropertyType ) ); modified.Add( propName ); }
			catch ( Exception ex ) { errors.Add( $"Error setting '{propName}': {ex.Message}" ); }
		}

		if ( args.TryGetProperty( "source", out var src ) ) TrySetDresser( "Source", src );
		if ( args.TryGetProperty( "manualHeight", out var mh ) ) TrySetDresser( "ManualHeight", mh );
		if ( args.TryGetProperty( "manualTint", out var mt ) ) TrySetDresser( "ManualTint", mt );
		if ( args.TryGetProperty( "manualAge", out var ma ) ) TrySetDresser( "ManualAge", ma );
		if ( args.TryGetProperty( "applyHeightScale", out var ahs ) ) TrySetDresser( "ApplyHeightScale", ahs );

		var result = new Dictionary<string, object>
		{
			["message"] = $"Configured dresser '{go.Name}' ({dresser.GetType().Name}). Modified: {string.Join( ", ", modified )}"
		};
		if ( errors.Count > 0 ) result["errors"] = errors;

		return OzmiumSceneHelpers.Txt( JsonSerializer.Serialize( result, OzmiumSceneHelpers.JsonSettings ) );
	}

	// ── configure_game_entities (Omnibus) ──────────────────────────────────

	internal static object ConfigureGameEntities( JsonElement args )
	{
		string operation = OzmiumSceneHelpers.Get( args, "operation", "" );
		return operation switch
		{
			"configure_door"        => ConfigureDoor( args ),
			"configure_spawn_point" => ConfigureSpawnPoint( args ),
			"configure_trigger"     => ConfigureTrigger( args ),
			"configure_prop"        => ConfigureProp( args ),
			"configure_world_panel" => ConfigureWorldPanel( args ),
			"configure_chair"       => ConfigureChair( args ),
			"configure_dresser"     => ConfigureDresser( args ),
			_ => OzmiumSceneHelpers.Txt( $"Unknown operation: {operation}. Use: configure_door, configure_spawn_point, configure_trigger, configure_prop, configure_world_panel, configure_chair, configure_dresser" )
		};
	}

	// ── Schema ─────────────────────────────────────────────────────────────

	internal static Dictionary<string, object> SchemaConfigureGameEntities
	{
		get
		{
			var stringArrayItem = new Dictionary<string, object> { ["type"] = "string" };
			var props = new Dictionary<string, object>();

			props["operation"] = new Dictionary<string, object>
			{
				["type"] = "string",
				["description"] = "Operation to perform.",
				["enum"] = new[] { "configure_door", "configure_spawn_point", "configure_trigger", "configure_prop", "configure_world_panel", "configure_chair", "configure_dresser" }
			};

			// Common id/name params
			props["id"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "GUID." };
			props["name"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Exact name." };

			// configure_door params
			props["isPurchasable"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether door is purchasable (DarkRP door component)." };
			props["isOwnable"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether door is ownable." };
			props["isLocked"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether door starts locked." };
			props["buyAmount"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Price to purchase door." };
			props["doorGroups"] = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Door groups that can access this door.", ["items"] = stringArrayItem };
			props["blacklistedGroups"] = new Dictionary<string, object> { ["type"] = "array", ["description"] = "Groups that cannot access this door.", ["items"] = stringArrayItem };

			// configure_spawn_point params
			props["teamName"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Team name tag (adds team:{name} tag)." };
			props["color"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex for spawn point." };

			// configure_trigger params
			props["damage"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Damage per tick for TriggerHurt." };
			props["rate"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Seconds between damage ticks." };
			props["damageTags"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Damage tags for trigger." };
			props["startEnabled"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Whether trigger starts enabled." };

			// configure_prop params
			props["health"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Prop health." };
			props["tint"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Color tint hex." };
			props["bodyGroups"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Body group mask (ulong)." };

			// configure_world_panel params
			props["panelSize"] = new Dictionary<string, object> { ["description"] = "Panel size {x,y}." };
			props["renderScale"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Render scale (WorldPanel)." };
			props["lookAtCamera"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Billboard toward camera." };
			props["interactionRange"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Max interaction distance." };

			// configure_chair params
			props["sitPose"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Sit pose animation." };
			props["sitHeight"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Sit height offset." };
			props["tooltipTitle"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Tooltip title." };

			// configure_dresser params
			props["source"] = new Dictionary<string, object> { ["type"] = "string", ["description"] = "Clothing source." };
			props["manualHeight"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual height." };
			props["manualTint"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual skin tint." };
			props["manualAge"] = new Dictionary<string, object> { ["type"] = "number", ["description"] = "Manual age." };
			props["applyHeightScale"] = new Dictionary<string, object> { ["type"] = "boolean", ["description"] = "Apply height scale." };

			var schema = new Dictionary<string, object> { ["type"] = "object", ["properties"] = props };
			schema["required"] = new[] { "operation" };
			return new Dictionary<string, object>
			{
				["name"] = "configure_game_entities",
				["description"] = "Configure existing game entities with domain-specific property groups. Unlike set_component_property, this provides high-level presets for doors, spawn points, triggers, props, world panels, chairs, and dressers. Does NOT create entities (use create_game_entity for that).",
				["inputSchema"] = schema
			};
		}
	}
}
