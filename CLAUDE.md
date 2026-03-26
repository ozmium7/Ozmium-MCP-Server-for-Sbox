# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Ozmium MCP Server is an S&box editor library that exposes a [Model Context Protocol](https://modelcontextprotocol.io/) (MCP) server, allowing AI coding assistants (Claude Desktop, Cursor, etc.) to query and manipulate the S&box editor scene in real time over SSE on `localhost:8098`. It provides 95 tools across twelve categories: scene read, scene write, asset queries, editor control, console access, mesh editing, lighting, physics, audio, camera, effects & environment, and utilities.

## Build

This is an S&box library plugin — it compiles automatically when the S&box editor loads the project. There is no standalone `dotnet build` step for the in-editor code.

- **Project definition:** `sbox_mcp.sbproj` (S&box library type)
- **Root namespace:** `Sandbox` (defined in `.sbproj` Compiler metadata)
- **Code namespace:** `SboxMcpServer` (used in all `Editor/*.cs` files)
- **Define constant:** `SANDBOX` (automatically set by the S&box compiler)

A separate standalone ASP.NET Core server exists in `Program.cs` + `Services/` for Docker deployment (uses `ModelContextProtocol` NuGet package), but the primary in-editor implementation lives entirely in `Editor/`.

## Architecture

### Request Flow

```
AI Client → HTTP POST /sse → SboxMcpServer (HTTP listener)
  → McpSession (SSE state) → GameTask.RunInThreadAsync
    → RpcDispatcher.ProcessRpcRequest (JSON-RPC routing)
      → await GameTask.MainThread() (scene API must run on main thread)
        → Handler method returns result object
      → JSON-RPC response sent back over SSE
```

### Key Files in `Editor/`

| File | Role |
|---|---|
| `SboxMcpServer.cs` | HTTP listener, SSE transport, session management |
| `McpSession.cs` | Per-connection SSE state |
| `RpcDispatcher.cs` | JSON-RPC method routing — maps tool names to handler calls |
| `OzmiumSceneHelpers.cs` | Scene resolution (`ResolveScene`), tree walking (`WalkAll`/`WalkSubtree`), object builders |
| `OzmiumReadHandlers.cs` | Scene read tool implementations |
| `OzmiumWriteHandlers.cs` | Scene write tool implementations + their schemas |
| `OzmiumAssetHandlers.cs` | Asset query tool implementations + their schemas |
| `OzmiumEditorHandlers.cs` | Editor control tool implementations + their schemas |
| `SceneToolHandlers.cs` | Original scene read handlers (still active, used by RpcDispatcher) |
| `SceneToolDefinitions.cs` | Schemas for original scene read tools |
| `ConsoleToolHandlers.cs` | Console command tool handlers |
| `ToolDefinitions.cs` | Aggregates all schemas into `All` array for `tools/list` |
| `ToolHandlerBase.cs` | Shared utilities (`TextResult`, `AppendHierarchyLine`) |
| `McpServerWindow.cs` | Editor UI panel with live status and colorized activity log |

### Adding a New Tool

1. Define the schema (anonymous object) — either inline in the handler file or in a `*ToolDefinitions.cs` file
2. Implement the handler as a static method
3. Register the schema in `ToolDefinitions.All` array
4. Add a case to the `toolName switch` in `RpcDispatcher.ProcessRpcRequest`

### Two Handler Generations

There are two generations of handler files. The original handlers (`SceneToolHandlers`, `AssetToolHandlers`, `ConsoleToolHandlers`) are still actively used and registered in `RpcDispatcher`. The newer "Ozmium" handlers (`OzmiumReadHandlers`, `OzmiumWriteHandlers`, `OzmiumAssetHandlers`, `OzmiumEditorHandlers`) own their schemas inline. Both are currently in use — check `RpcDispatcher` to see which handler is called for each tool name.

## Critical Design Patterns

- **Threading:** All scene/tool dispatch goes through `GameTask.RunInThreadAsync` (in `SboxMcpServer.HandleMessage`), then `await GameTask.MainThread()` before calling scene APIs. Never use raw `Task.Run()` — it breaks S&box's main thread scheduling and causes permanent hangs.
- **Scene resolution:** `ResolveScene()` in `OzmiumSceneHelpers` prioritizes `SceneEditorSession.Active` over `Game.ActiveScene`. The latter always returns a minimal runtime scene even when the editor has a real scene open.
- **Tree walking:** `WalkAll`/`WalkSubtree` replace `scene.GetAllObjects(true)` everywhere because the S&box API does not traverse disabled parent subtrees. Subtrees with >25 children are auto-skipped (parent returned, children not walked). Objects named `(MCP IGNORE)` or tagged `mcp_ignore` are skipped entirely.
- **Component property access:** Both `get_component_properties` and `set_component_property` use .NET reflection. `ConvertJsonValue` in `OzmiumWriteHandlers` handles type coercion including `Vector3`, enums, and `Component`/`GameObject` reference resolution via GUID.
- **Console commands:** `run_console_command` is dispatched separately from the main async path via `RunConsoleCommandSafe()` so engine exceptions are reliably catchable.

## `sbox-public/` Subdirectory

Contains a copy of the S&box engine public source code (reference only, not part of this library's build). Used for API lookup. Do not modify files here.

## Standalone Server (Docker)

`Program.cs` + `Services/` + `Dockerfile` provide a separate ASP.NET Core MCP server using the `ModelContextProtocol` NuGet SDK. This is independent from the in-editor server and is used for containerized deployments. Namespace: `SandboxModelContextProtocol.Server`.
