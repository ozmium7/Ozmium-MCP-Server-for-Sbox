Connect AI coding assistants to the S&box editor using the Model Context Protocol (MCP). While you're building your game, your AI assistant can see inside the editor in real time — reading your scene, listing GameObjects, and running console commands — without any copy-pasting.

Features

    SSE-based MCP server running on localhost:8098
    get_scene_hierarchy — lists all GameObjects in your active scene
    run_console_command — executes engine console commands from the AI
    Built-in Editor panel with live server status, session count, and an activity log
    Localhost-only — nothing leaves your machine


Setup

1. Install the plugin Add it via the S&box Library Manager and let it compile.
2. Open the MCP panel In the S&box editor, go to Editor → MCP → Open MCP Panel
3. Start the server Click Start MCP Server in the panel. The status indicator will turn green.
4. Connect your AI assistant Add this to your MCP config (e.g. mcp_config.json for Claude):

json

{  "mcpServers": {    "sbox": {      "url": "http://localhost:8098/sse",      "type": "sse"    }  }}

5. Done! Your AI assistant can now call get_scene_hierarchy and run_console_command directly.

Requirements

    S&box Editor (latest)
    An MCP-compatible AI client (Claude Desktop, Cursor, etc.)
