# Ozmium MCP Server for S&box

Connect AI coding assistants to the S&box editor using the [Model Context Protocol](https://modelcontextprotocol.io/). While you're building your game, your AI assistant can see inside the editor in real time — querying your scene, inspecting GameObjects, reading and writing component property values, spawning prefabs, controlling play mode, and running console commands — without any copy-pasting.

---

## Features

- SSE-based MCP server running on `localhost:8098`
- **31 tools** across five categories: scene read, scene write, asset queries, editor control, and console access
- Disabled objects and disabled subtrees are fully visible to all query tools
- Built-in Editor panel with live server status, session count, and an activity log
- Localhost-only — nothing leaves your machine

---

## Tools

### Scene Read

#### `get_scene_summary`
Returns a high-level overview of the active scene: total/root/enabled/disabled object counts, all unique tags in use, a component-type frequency breakdown, a **prefab source breakdown** (which prefabs have how many instances), a **network mode distribution**, and a root object list. **Start here** to orient yourself before drilling into specifics.

#### `find_game_objects`
Search and filter GameObjects by any combination of:
- `nameContains` — case-insensitive name substring
- `hasTag` — objects that carry a specific tag
- `hasComponent` — objects with a component whose type name contains the given string
- `pathContains` — objects whose full scene path contains the string (e.g. `"Units/"`)
- `enabledOnly` — skip disabled objects (default: false — disabled objects are included)
- `isNetworkRoot` — filter to network roots or non-roots
- `isPrefabInstance` — filter to prefab instances or non-instances
- `maxResults` — cap results (default 50, max 500)
- `sortBy` — sort by `"name"`, `"distance"` (requires `sortOriginX/Y/Z`), or `"componentCount"`

Returns a flat list with ID, scene path, tags, component types, world position, child count, isPrefabInstance, prefabSource, isNetworkRoot, and networkMode.

#### `find_game_objects_in_radius`
Find all GameObjects within a world-space radius of a point, sorted by distance. Useful for spatial questions: *"what's near the player?"*, *"which resource nodes are close to my building?"*, *"what units are within attack range?"*. Supports `hasTag`, `hasComponent`, and `enabledOnly` filters. Results include `distanceFromOrigin`.

#### `get_game_object_details`
Get full details for a single GameObject by `id` (GUID, preferred) or `name`. Returns world **and** local transform, all components with enabled state, tags, parent reference, children summary, network mode, prefab source, and isNetworkRoot. Set `includeChildrenRecursive=true` to get the full subtree in one call.

#### `get_component_properties`
Get the **runtime property values** of a specific component on a GameObject. Returns all readable public properties with their current values. Requires `componentType` (case-insensitive substring match) plus either `id` or `name`.

#### `get_scene_hierarchy`
Lists the scene as an indented tree. Supports `rootOnly=true`, `includeDisabled=false`, and `rootId` to walk only a specific subtree by GUID. For large scenes, prefer `find_game_objects` or `get_scene_summary`.

#### `get_prefab_instances`
Find all instances of a specific prefab, or get a full breakdown of all prefabs and their instance counts. `prefabPath` is matched as a case-insensitive substring. Omit it to get the full breakdown.

---

### Scene Write

#### `create_game_object`
Create a new empty GameObject in the current scene. Accepts `name` and optional `parentId` (GUID).

#### `add_component`
Add a component to a GameObject by exact C# class name (e.g. `"PointLight"`, `"ModelRenderer"`). Requires `componentType` plus either `id` or `name`.

#### `remove_component`
Remove a component from a GameObject. Matches `componentType` as a case-insensitive substring.

#### `set_component_property`
Set a property on a component. Supports `string`, `bool`, `int`, `float`, `Vector3` (`{x,y,z}`), and `enum` values. Requires `propertyName` and `value`; optionally scoped by `componentType`.

#### `destroy_game_object`
Delete a GameObject by `id` or `name`.

#### `reparent_game_object`
Move a GameObject under a new parent. Pass `parentId="null"` to move to scene root.

#### `set_game_object_tags`
Set, add, or remove tags on a GameObject. Use `set` (array) to replace all tags, or `add`/`remove` arrays for incremental changes.

#### `instantiate_prefab`
Spawn a prefab at a world position. Accepts `path` (prefab asset path), `x/y/z`, and optional `parentId`. Use `browse_assets` with `type="prefab"` to find valid paths first.

#### `save_scene`
Save the currently open scene or prefab to disk.

#### `undo`
Undo the last editor operation.

#### `redo`
Redo the last undone editor operation.

---

### Asset Queries

#### `browse_assets`
Search project assets by type and/or name. Use this to find model paths (`.vmdl`), prefab paths, materials (`.vmat`), sounds (`.vsnd`), scenes, etc. Supports `type`, `nameContains`, and `maxResults` filters. Results include the full asset path.

#### `get_editor_context`
Returns what the S&box editor currently has open: active scene name, all open editor sessions (scene or prefab), current selection, and whether the game is playing. Call this first to determine whether to target `Game.ActiveScene` or an editor prefab session.

#### `get_model_info`
Return bone count, attachment points, and sequence info for a `.vmdl` model. Requires `path`.

#### `get_material_properties`
Return shader name and surface properties for a `.vmat` material. Requires `path`.

#### `get_prefab_structure`
Return the full object/component hierarchy of a `.prefab` file without opening it in the editor. Reads the raw prefab JSON from disk. Requires `path`.

#### `reload_asset`
Force reimport/recompile of a specific asset — useful after modifying source files on disk. Requires `path`.

---

### Editor Control

#### `select_game_object`
Select a GameObject in the editor hierarchy and viewport by `id` or `name`.

#### `open_asset`
Open an asset in its default editor (scene, prefab, material, etc.). Requires `path`.

#### `get_play_state`
Returns the current play state: `"Playing"` or `"Stopped"`.

#### `start_play_mode`
Press the Play button in the editor.

#### `stop_play_mode`
Press the Stop button in the editor.

#### `get_editor_log`
Return recent log lines captured from the editor output. Accepts `lines` (default 50).

---

### Console

#### `list_console_commands`
List all `[ConVar]`-attributed console variables registered in the game, with their current values, help text, flags, and declaring type. Use this **before** `run_console_command` to discover valid command names. Supports a `filter` parameter to narrow results.

#### `run_console_command`
Get or set a console variable. Pass just the name to read its current value; pass `name value` to set it. Errors are returned as text rather than thrown as exceptions.

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

5. **Done.** Your AI assistant can now call all 31 tools directly.

---

## Requirements

- S&box Editor (latest)
- An MCP-compatible AI client (Claude Desktop, Cursor, etc.)

---

## Code Structure

| File | Responsibility |
|---|---|
| `SboxMcpServer.cs` | HTTP/SSE transport — listener, session management, SSE writes |
| `McpSession.cs` | Session state (SSE connection + lifecycle) |
| `RpcDispatcher.cs` | JSON-RPC method routing — maps tool names to handler calls |
| `OzmiumReadHandlers.cs` | Tool logic for all scene-read tools |
| `OzmiumWriteHandlers.cs` | Tool logic for all scene-write tools (create, add/remove component, set property, destroy, reparent, tags, instantiate, save, undo/redo) — also owns write tool schemas |
| `OzmiumAssetHandlers.cs` | Tool logic for asset-query tools (browse, model info, material, prefab structure, reload) — also owns asset tool schemas |
| `OzmiumEditorHandlers.cs` | Tool logic for editor-control tools (select, open asset, play state, play/stop, editor log, console commands) — also owns editor tool schemas |
| `AssetToolHandlers.cs` | Legacy asset handler (superseded by `OzmiumAssetHandlers`) |
| `ConsoleToolHandlers.cs` | Tool logic for `list_console_commands` and `run_console_command` |
| `ToolHandlerBase.cs` | Shared handler utilities (`TextResult`, `AppendHierarchyLine`) |
| `SceneToolHandlers.cs` | Legacy scene-read handlers (superseded by `OzmiumReadHandlers`) |
| `SceneToolDefinitions.cs` | MCP tool schemas for scene-read tools |
| `AssetToolDefinitions.cs` | MCP tool schemas for `browse_assets` and `get_editor_context` |
| `ConsoleToolDefinitions.cs` | MCP tool schemas for console tools |
| `ToolDefinitions.cs` | Aggregates all schemas for `tools/list` |
| `OzmiumSceneHelpers.cs` | Scene resolution, tree walking (`WalkAll`/`WalkSubtree`), object builders (`BuildSummary`/`BuildDetail`), path/tag/component helpers |
| `SceneQueryHelpers.cs` | Legacy scene helpers (superseded by `OzmiumSceneHelpers`) |
| `McpServerWindow.cs` | Editor UI panel |

To add a new tool: add its schema (either inline in the handler file or in a `*ToolDefinitions.cs` file), implement its handler, register it in `ToolDefinitions.All`, and add a case to the switch in `RpcDispatcher.cs`.

### Key design notes

- **`WalkAll` / `WalkSubtree`** in `OzmiumSceneHelpers` replace `scene.GetAllObjects(true)` everywhere. The s&box API's `GetAllObjects` does not traverse into disabled parent subtrees; the manual walk does.
- **`get_component_properties`** uses standard .NET reflection (`GetProperties`) to read public instance properties at runtime. It handles `Vector3`, `Enum`, primitives, and strings with graceful fallback for unreadable properties.
- **`set_component_property`** also uses reflection to write properties, with a `ConvertJsonValue` helper that coerces JSON strings/numbers/booleans/objects into the correct .NET type (including `Vector3` and enums).
- **`list_console_commands`** enumerates `[ConVar]`-attributed static properties across all loaded assemblies via `AppDomain.CurrentDomain.GetAssemblies()`, since `ConsoleSystem` has no enumeration API.
- **`run_console_command`** uses `ConsoleSystem.GetValue`/`SetValue` and is dispatched outside the normal async path so that engine exceptions are reliably catchable.
- **`get_prefab_structure`** reads the raw prefab JSON from disk via `AssetSystem.FindByPath` + `File.ReadAllText`, since `PrefabFile` does not expose a live scene when not open in the editor.
- **`get_editor_log`** captures log lines into a concurrent ring buffer (`MaxLogLines = 500`) fed by the editor's log callback.
