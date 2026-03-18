# Ozmium MCP Server for S&box

Connect AI coding assistants to the S&box editor using the [Model Context Protocol](https://modelcontextprotocol.io/). While you're building your game, your AI assistant can see inside the editor in real time — querying your scene, inspecting GameObjects, and running console commands — without any copy-pasting.

---

## Features

- SSE-based MCP server running on `localhost:8098`
- **5 tools** for intelligent scene querying (see below)
- Built-in Editor panel with live server status, session count, and an activity log
- Localhost-only — nothing leaves your machine

---

## Tools

### `get_scene_summary`
Returns a high-level overview of the active scene: total/root/enabled object counts, all unique tags in use, a component-type frequency breakdown, and a root object list. **Start here** to orient yourself before drilling into specifics.

### `find_game_objects`
Search and filter GameObjects by any combination of:
- `nameContains` — case-insensitive name substring
- `hasTag` — objects that carry a specific tag
- `hasComponent` — objects with a component whose type name contains the given string (e.g. `"NpcPlayer"`, `"ModelRenderer"`)
- `enabledOnly` — skip disabled objects
- `maxResults` — cap results (default 50, max 500)

Returns a flat list with ID, scene path, tags, component types, world position, and child count.

### `get_game_object_details`
Get full details for a single GameObject by `id` (GUID, preferred) or `name`. Returns:
- World **and** local transform (position, rotation, scale)
- All components with their enabled state
- Tags, parent reference, children summary
- Network mode, prefab source

### `get_scene_hierarchy`
Lists the scene as an indented tree. Supports `rootOnly=true` (top-level only, much shorter output) and `includeDisabled=false`. Each line includes tags and component types. For large scenes, prefer `find_game_objects` or `get_scene_summary`.

### `run_console_command`
Executes any S&box console command from the AI assistant.

---

## Setup

1. **Install the plugin** — add it via the S&box Library Manager and let it compile.
2. **Open the MCP panel** — in the S&box editor go to **Editor → MCP → Open MCP Panel**.
3. **Start the server** — click **Start MCP Server**. The status indicator turns green.
4. **Connect your AI assistant** — add this to your MCP config (e.g. `mcp_config.json` for Claude Desktop):

```json
{
  "mcpServers": {
    "sbox": {
      "url": "http://localhost:8098/sse",
      "type": "sse"
    }
  }
}
```

5. **Done.** Your AI assistant can now call all five tools directly.

---

## Requirements

- S&box Editor (latest)
- An MCP-compatible AI client (Claude Desktop, Cursor, etc.)

---

## Code Structure

| File | Responsibility |
|---|---|
| `SboxMcpServer.cs` | HTTP/SSE transport, JSON-RPC dispatch |
| `McpSession.cs` | Session state (SSE connection + lifecycle) |
| `ToolDefinitions.cs` | MCP tool schemas returned by `tools/list` |
| `ToolHandlers.cs` | Tool call logic (one method per tool) |
| `SceneQueryHelpers.cs` | Pure scene-data helpers (path, tags, components, object builders) |
| `McpServerWindow.cs` | Editor UI panel |

To add a new tool: add its schema to `ToolDefinitions.cs`, implement its handler in `ToolHandlers.cs`, and add a case to the switch in `SboxMcpServer.cs`.
