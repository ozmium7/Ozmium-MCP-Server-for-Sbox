using System.Collections.Generic;

namespace SboxMcpServer;

/// <summary>
/// MCP tool schema definitions for asset-browsing and editor-context tools.
/// </summary>
internal static class AssetToolDefinitions
{
	internal static Dictionary<string, object> BrowseAssets => new()
	{
		["name"] = "browse_assets",
		["description"] =
			"Search project assets by type and/or name. " +
			"Use this to find model paths (vmdl), prefab paths, materials (vmat), sounds (vsnd), scenes, etc. " +
			"Examples: type='vmdl' nameContains='mp5' finds the MP5 view model path; " +
			"type='prefab' nameContains='player' finds the player prefab. " +
			"Results include the full asset path you can paste into component properties.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"] = "object",
			["properties"] = new Dictionary<string, object>
			{
				["type"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Filter by asset type. E.g. 'vmdl' (model), 'prefab', 'scene', 'vmat' (material), 'vsnd' (sound). Matched case-insensitively against extension or friendly type name."
				},
				["nameContains"] = new Dictionary<string, object>
				{
					["type"]        = "string",
					["description"] = "Case-insensitive substring to match against asset name."
				},
				["maxResults"] = new Dictionary<string, object>
				{
					["type"]        = "integer",
					["description"] = "Maximum number of results to return. Default 100, max 500."
				}
			}
		}
	};

	internal static Dictionary<string, object> GetEditorContext => new()
	{
		["name"] = "get_editor_context",
		["description"] =
			"Returns what the S&box editor currently has open: active scene name, " +
			"all open editor sessions (scene or prefab), current selection, and whether the game is playing. " +
			"Call this first to determine whether to target Game.ActiveScene or an editor prefab session. " +
			"Useful for diagnosing 'No active scene' issues.",
		["inputSchema"] = new Dictionary<string, object>
		{
			["type"]       = "object",
			["properties"] = new Dictionary<string, object>()
		}
	};
}
