using System.Collections.Generic;

namespace SboxMcpServer;

/// <summary>
/// Static MCP tool schema definitions returned by tools/list.
/// Add a new entry here whenever a new tool is implemented in ToolHandlers.
/// </summary>
internal static class ToolDefinitions
{
	internal static object[] All => new object[]
	{
		GetSceneHierarchy,
		FindGameObjects,
		GetGameObjectDetails,
		GetSceneSummary,
		RunConsoleCommand
	};

	// ── Individual tool schemas ────────────────────────────────────────────

	private static Dictionary<string, object> GetSceneHierarchy => new()
	{
		["name"] = "get_scene_hierarchy",
		["description"] =
			"Lists GameObjects in the active scene as an indented tree. " +
			"Use this when the user explicitly wants to see the scene structure or parent/child nesting. " +
			"Pass rootOnly=true first to get a short top-level overview before expanding. " +
			"Each entry shows name, ID, enabled state, tags, and component types. " +
			"AVOID calling this on large scenes without rootOnly=true — it can return thousands of lines. " +
			"For questions like 'are there any X objects?' use find_game_objects instead. " +
			"For a quick scene overview use get_scene_summary instead.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["rootOnly"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If true, only list root-level GameObjects (no children). Default false."
				},
				["includeDisabled"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If false, exclude disabled GameObjects. Default true (include all)."
				}
			}
		}
	};

	private static Dictionary<string, object> FindGameObjects => new()
	{
		["name"] = "find_game_objects",
		["description"] =
			"Search and filter GameObjects in the active scene by name, tag, or component type. " +
			"Use this whenever the user asks whether something exists in the scene, " +
			"e.g. 'are there any crystals?', 'how many NPCs?', 'find all doors', 'is there a SpawnPoint?'. " +
			"Also use this to locate an object before calling get_game_object_details. " +
			"Filters are ANDed together — combine nameContains, hasTag, and hasComponent freely. " +
			"Returns each match's ID, scene path, tags, component types, world position, and child count. " +
			"Never guess whether an object exists — always call this tool to check.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["nameContains"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Case-insensitive substring to match against GameObject names."
				},
				["hasTag"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Only return objects that have this tag."
				},
				["hasComponent"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Only return objects that have a component whose type name contains this string (case-insensitive). E.g. 'Rigidbody', 'NpcPlayer', 'ModelRenderer'."
				},
				["enabledOnly"] = new Dictionary<string, object>
				{
					["type"]        = "boolean",
					["description"] = "If true, only return enabled GameObjects. Default false (return all)."
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum number of results to return. Default 50, max 500."
				}
			}
		}
	};

	private static Dictionary<string, object> GetGameObjectDetails => new()
	{
		["name"] = "get_game_object_details",
		["description"] =
			"Get full details for one specific GameObject by its GUID or exact name. " +
			"Use this when the user asks about a specific object's position, rotation, scale, " +
			"what components it has, whether it is enabled, who its parent is, or what its children are. " +
			"Prefer using the 'id' (GUID) from a prior find_game_objects call rather than guessing by name. " +
			"Returns world and local transform, all components with enabled state, tags, parent ref, children list, " +
			"network mode, and prefab source.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["id"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The GUID of the GameObject (preferred)."
				},
				["name"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Exact name of the GameObject. If multiple objects share the name, the first match is returned."
				}
			}
		}
	};

	private static Dictionary<string, object> GetSceneSummary => new()
	{
		["name"] = "get_scene_summary",
		["description"] =
			"Returns a high-level overview of the active scene without listing every object. " +
			"Includes: total/root/enabled object counts, all unique tags in use, " +
			"a component-type frequency table (how many objects use each component), and a root object list. " +
			"Call this FIRST before any other scene tool to orient yourself — it tells you what kinds of " +
			"objects and tags exist so you can make smarter follow-up queries. " +
			"Also use this when the user asks 'what tags are in use?', 'what types of objects are in the scene?', " +
			"or 'give me an overview of the scene'.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"]       = "object",
			["properties"] = new Dictionary<string, object>()
		}
	};

	private static Dictionary<string, object> RunConsoleCommand => new()
	{
		["name"]        = "run_console_command",
		["description"] = "Runs a console command in the S&box editor.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["command"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "The console command to run."
				}
			},
			["required"] = new[] { "command" }
		}
	};
}
