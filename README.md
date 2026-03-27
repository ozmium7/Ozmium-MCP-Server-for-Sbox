# Ozmium MCP Server for S&box

Connect AI coding assistants to the S&box editor using the [Model Context Protocol](https://modelcontextprotocol.io/). While you're building your game, your AI assistant can see inside the editor in real time — querying your scene, inspecting GameObjects, reading and writing component property values, spawning prefabs, controlling play mode, and running console commands — without any copy-pasting.

---

## Features

- SSE-based MCP server running on `localhost:8098`
- **70 tools** across twenty-five categories: scene read, scene write, asset queries, editor control, console access, mesh editing, terrain, lighting, physics, audio, camera, effects & environment, utilities, navigation, rendering, game entities, scene queries, procedural mesh, material editing, batch operations, build automation, zone management, visibility, navmesh, game entity config, and scene data
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

#### `set_game_object_transform`
Set position, rotation, and scale of a GameObject in one call. Accepts `position`, `rotation`, and `scale` objects. Requires `id` or `name`.

#### `duplicate_game_object`
Clone a GameObject with optional new position and name. Accepts `id`/`name`, `position`, and `newName`.

#### `set_game_object_enabled`
Toggle a GameObject's enabled state. Pass `id`/`name` and `enabled`; omit `enabled` to toggle.

#### `set_game_object_name`
Rename a GameObject. Requires `newName` and `id` or `name`.

#### `set_component_enabled`
Toggle a component's enabled state. Requires `componentType`, `enabled`, and `id` or `name`.

#### `instantiate_prefab`
Spawn a prefab at a world position. Accepts `path` (prefab asset path), `x/y/z`, and optional `parentId`. Use `browse_assets` with `type="prefab"` to find valid paths first.

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

#### `get_component_types`
List all available component types via `TypeLibrary`, so AI knows what components can be added. Supports a `filter` parameter.

#### `search_assets`
Search assets by content (file extension filter + substring matching on name and path). Supports `query`, `type`, and `maxResults`.

#### `get_scene_statistics`
Enhanced scene summary with component type frequency, prefab breakdown, network mode distribution, and tags.

---

### Editor Control

#### `manage_selection`
Omnibus tool for editor selection. Operations:
- `"select"` — Select a GameObject by `id` or `name`
- `"select_many"` — Select multiple objects via `ids` array
- `"clear"` — Clear the editor selection
- `"get_selected"` — Return the currently selected objects
- `"select_by_tag"` — Select all objects matching a tag
- `"select_children"` — Select all children (recursive) of a parent object

#### `manage_editor_state`
Omnibus tool for editor state control. Operations:
- `"start_play"` — Press the Play button
- `"stop_play"` — Press the Stop button
- `"get_play_state"` — Returns `"Playing"` or `"Stopped"`
- `"save_scene"` — Save the currently open scene or prefab to disk
- `"get_scene_info"` — Return the current scene file path, name, and whether it's a prefab session

#### `open_asset`
Open an asset in its default editor (scene, prefab, material, etc.). Requires `path`.

#### `get_editor_log`
Return recent log lines captured from the editor output. Accepts `lines` (default 50).

#### `frame_selection`
Focus the editor camera on the current selection or specified objects. Accepts optional `ids` array.

#### `save_scene_as`
Save the current scene to a new file path. Accepts `path`.

#### `get_scene_unsaved`
Check if the current scene has unsaved changes.

#### `break_from_prefab`
Break a prefab instance's connection to its source prefab. Requires `id` or `name`.

#### `update_from_prefab`
Update a prefab instance to match its source prefab. Requires `id` or `name`.

---

### Console

#### `list_console_commands`
List all `[ConVar]`-attributed console variables registered in the game, with their current values, help text, flags, and declaring type. Use this **before** `run_console_command` to discover valid command names. Supports a `filter` parameter to narrow results.

#### `run_console_command`
Get or set a console variable. Pass just the name to read its current value; pass `name value` to set it. Errors are returned as text rather than thrown as exceptions.

---

### Mesh Editing

#### `create_block`
Creates a primitive block mesh using `PolygonMesh`. Compatible with S&box mesh editing tools. Accepts `x/y/z` (position), `sizeX/Y/Z`, `name`, and `materialPath`.

#### `edit_mesh`
Omnibus tool for mesh editing. Operations:
- `"set_face_material"` — Apply a material to a face or all faces
- `"set_texture_parameters"` — Set UV axes and scale for faces
- `"set_vertex_position"` — Displace a vertex by index
- `"set_vertex_color"` — Set vertex color for painting
- `"set_vertex_blend"` — Set vertex blend weights for terrain

#### `get_mesh_info`
Queries detailed information about a mesh including vertex/face counts, bounds, and per-face materials. Requires `gameObjectId` or `name`.

---

### Terrain

#### `manage_terrain`
Omnibus tool for terrain creation, editing, painting, and analysis. Operations:
- `"create"` — Create a new terrain with resolution, size, and height. Tags the GO with `"terrain"`.
- `"get_info"` — Return terrain metadata (resolution, size, height, materials list).
- `"get_height"` — Sample world height at an XZ position.
- `"set_height"` — Set height in a brush radius with smoothstep falloff.
- `"flatten"` — Flatten terrain to a target height in a brush radius.
- `"paint_material"` — Paint splatmap materials (base/overlay texture IDs, blend factor) in a brush radius.
- `"get_material_at"` — Read the splatmap material at an XZ position.
- `"get_normal"` — Surface normal, slope angle (degrees), compass direction of downhill face, and `suitableForBuilding` flag (slope vs `maxSlope` threshold).
- `"sample_heights"` — Batch height sampling. Accepts an array of `{x, z}` points, returns an array of `{x, z, height}`.
- `"get_height_profile"` — Sample heights along a line from start to end at N steps. Returns min/max height range.
- `"find_flat_areas"` — BFS flood-fill to find connected flat regions within a search radius. Filters by `maxSlope` and `minSize` (texel count). Returns center position, average height, size, and approximate diameter per area.
- `"get_terrain_statistics"` — Overall terrain metrics: min/max/average height, height range, average/max slope, texel count and size.
- `"smooth"` — Smooth terrain in a brush area using 3x3 neighborhood averaging from a snapshot buffer. Strength controls blend intensity.
- `"apply_noise"` — Apply multi-octave FBM Perlin noise (`Noise.Fbm`) to generate natural-looking terrain. Supports frequency, amplitude, octaves, and seed. Set `radius=0` (default) to apply to the entire terrain, or specify a brush center/size.
- `"raise"` — Raise terrain by a relative amount (positive = up) with smoothstep falloff. Unlike `set_height` which sets an absolute target.
- `"terrace"` — Create stepped terraces by quantizing heights to discrete steps. `blendWidth` controls edge smoothing between steps (0 = hard cliffs, 0.5 = no terracing).
- `"erode"` — Simple thermal erosion that simulates material sliding down slopes above a talus angle threshold. Uses double-buffered iterations to prevent directional bias, with smoothstep falloff blending at brush edges.

---

### Lighting

#### `manage_lighting`
Omnibus tool for lighting. Operations:
- `"create_light"` — Create a GO with a light component (PointLight/SpotLight/DirectionalLight)
- `"configure_light"` — Set properties on an existing Light component
- `"create_sky_box"` — Create a GO with a SkyBox2D component
- `"set_sky_box"` — Configure an existing SkyBox2D component
- `"create_ambient_light"` — Create an AmbientLight for global ambient illumination
- `"create_indirect_light_volume"` — Create an IndirectLightVolume (DDGI) for dynamic GI

---

### Physics

#### `manage_physics`
Omnibus tool for physics. Operations:
- `"add_collider"` — Add a collider (Box/Sphere/Capsule/ModelCollider) with configured properties
- `"configure_collider"` — Modify properties on an existing Collider component
- `"add_rigidbody"` — Add a Rigidbody for physics simulation
- `"create_character_controller"` — Create a CharacterController for collision-based movement
- `"add_plane_collider"` — Add a PlaneCollider for flat surfaces
- `"add_hull_collider"` — Add a HullCollider (Box/Cone/Cylinder primitives)
- `"create_model_physics"` — Create a ModelPhysics for ragdolls and per-bone physics

---

### Audio

#### `manage_audio`
Omnibus tool for audio. Operations:
- `"create_sound_point"` — Create a spatial audio SoundPointComponent
- `"configure_sound"` — Configure an existing BaseSoundComponent
- `"create_soundscape_trigger"` — Create a SoundscapeTrigger for ambient audio zones
- `"create_sound_box"` — Create a SoundBoxComponent for area ambient sounds
- `"create_dsp_volume"` — Create a DspVolume for audio effect zones
- `"create_audio_listener"` — Create an AudioListener for custom audio origins

---

### Camera

#### `manage_camera`
Omnibus tool for cameras. Operations:
- `"create_camera"` — Create a GO with a CameraComponent
- `"configure_camera"` — Configure an existing CameraComponent
- `"list_cameras"` — List all CameraComponents in the scene with their properties

---

### Effects & Environment

#### `manage_effects`
Omnibus tool for effects and environment. Operations:
- `"create_particle_effect"` — Create a ParticleEffect component
- `"configure_particle_effect"` — Configure an existing ParticleEffect
- `"create_fog_volume"` — Create a fog volume (GradientFog or VolumetricFogVolume)
- `"configure_post_processing"` — Create a PostProcessVolume for post-processing effects
- `"create_environment_light"` — Create a complete environment (sun + ambient + sky)
- `"create_beam_effect"` — Create a BeamEffect for laser/energy effects
- `"create_verlet_rope"` — Create a VerletRope for rope physics
- `"create_joint"` — Create a physics joint (Fixed/Ball/Hinge/Slider/Spring/Wheel)
- `"create_clutter"` — Create a ClutterComponent for vegetation/object scattering
- `"create_radius_damage"` — Create a RadiusDamage for explosion/area damage

---

### Rendering

#### `create_render_entity`
Omnibus tool for rendering entities. Operations:
- `"TextRenderer"` — Create a GO with a TextRenderer component (text, font size, color, alignment)
- `"LineRenderer"` — Create a GO with a LineRenderer (array of Vector3 points)
- `"SpriteRenderer"` — Create a GO with a SpriteRenderer
- `"TrailRenderer"` — Create a GO with a TrailRenderer (max points, point distance, lifetime, emitting)
- `"ModelRenderer"` — Create a GO with a ModelRenderer (model path, shadows, body groups)
- `"SkinnedModelRenderer"` — Create a GO with a SkinnedModelRenderer (model path, animation graph, bone objects)
- `"ScreenPanel"` — Create a GO with a ScreenPanel (opacity, z-order, auto-screen-scale)

---

### Game Entities

#### `create_game_entity`
Omnibus tool for creating specific game entities with domain-specific property groups. Operations:
- `"SpawnPoint"` — Create a player spawn point with optional team tag and color tint
- `"TriggerHurt"` — Create a damage trigger with configurable damage, rate, damage tags, and start state
- `"EnvmapProbe"` — Create an environment map probe for reflections
- `"Prop"` — Create a physics prop with health, static flag, color tint, and body groups
- `"Decal"` — Create a decal with material, size, projection depth, and lifetime
- `"WorldPanel"` — Create a world-space HTML panel for shop signs and displays
- `"FireDamage"` — Create a fire damage volume
- `"ManualHitbox"` — Create a manual hitbox (sphere or box shape, hitbox tags)
- `"BaseChair"` — Create a sittable chair with sit pose, height, and tooltip
- `"Dresser"` — Create an NPC appearance controller (clothing source, height, tint, age)
- `"Gib"` — Create a gib prop with optional lifetime and fade

---

### Scene Queries

#### `scene_trace`
Omnibus tool for spatial queries. Operations:
- `"ray"` — Cast a ray from start to end, return hit info (position, normal, distance, GameObject). Use to align objects to surfaces.
- `"sphere_trace"` — Sweep a sphere along a ray, return first hit.
- `"box_trace"` — Sweep a box along a ray, return first hit.
- `"sphere_overlap"` — Find all objects within a sphere volume. Returns up to 50 results.
- `"box_overlap"` — Find all objects within a box volume. Returns up to 50 results.
- `"terrain_height"` — Sample terrain height at an XZ position via raycast.

---

### Procedural Mesh

#### `build_procedural_mesh`
Omnibus tool for creating and manipulating procedural geometry. Operations:
- `"create_mesh"` — Create a custom mesh from vertex positions and face indices
- `"add_face"` — Add a face (triangle/quad) to an existing mesh
- `"merge"` — Merge two meshes into one
- `"scale"` — Scale an existing mesh uniformly or per-axis
- `"extrude"` — Extrude a face outward by an offset
- `"create_ramp"` — Create a ramp/inclined plane with configurable width, height, depth, and angle
- `"create_cylinder"` — Create a cylinder with configurable radius, height, and side count
- `"create_arch"` — Create an arch with configurable radius, height, width, and side count

---

### Material Editing

#### `manage_material`
Omnibus tool for editing material shader parameters and model material overrides. Operations:
- `"set_param"` — Set a shader parameter on a material (float, color, vector3, int, bool, texture)
- `"set_texture"` — Swap a texture on a material
- `"get_params"` — Read current shader parameter values from a material
- `"set_model_override"` — Override a material on a ModelRenderer by target name (e.g. `"skin"`)
- `"clear_model_overrides"` — Remove all material overrides from a ModelRenderer

---

### Batch Operations

#### `batch_operations`
Omnibus tool for bulk operations on multiple objects. Operations:
- `"batch_enable"` — Enable or disable multiple objects at once via GUIDs array
- `"batch_delete"` — Delete multiple objects at once
- `"batch_set_tags"` — Replace all tags on multiple objects
- `"batch_set_material"` — Apply a material to multiple objects by face index
- `"duplicate_array"` — Duplicate a source object in a grid pattern (countX/Y/Z, spacing)
- `"batch_set_property"` — Set a property on a specific component type across multiple objects
- `"batch_reparent"` — Move multiple objects under a new parent

---

### Utilities

#### `get_asset_dependencies`
Returns all assets referenced by a given asset (materials, textures, etc.). Requires `assetPath`.

#### `batch_transform`
Applies a position offset `{x,y,z}` to multiple objects at once. Requires `ids` (array of GUIDs) and `position`.

#### `copy_component`
Copies a component from one GameObject to another. Requires `sourceId`, `targetId`, and `componentType`.

#### `get_object_bounds`
Returns the world-space bounding box of a GameObject. Requires `id` or `name`.

---

### Build Automation

#### `build_automation`
Omnibus tool for scene building automation. Operations:
- `"scatter_objects"` — Place N prefab instances randomly in a 3D bounding volume. Supports random rotation/scale, ground alignment via raycast, optional parent, name prefix, and reproducible seed. Count capped at 500.
- `"replace_prefab_instances"` — Bulk replace all instances of one prefab with another. Collects source instances first, then destroys and replaces each. Preserves position/rotation/scale by default.
- `"align_to_ground"` — Drop objects to ground/terrain via downward raycast. Accepts an array of GUIDs, vertical offset, and max raycast distance.
- `"randomize_transforms"` — Bulk transform randomization. Randomize rotation on X/Y/Z axes independently and/or scale uniformly. Supports reproducible seed.
- `"snap_to_grid"` — Snap object positions to a configurable grid. Supports per-axis snapping (X/Y/Z), grid size, and origin offset. Useful for aligning buildings or city blocks.
- `"distribute_along_line"` — Place N prefab instances evenly between two points via linear interpolation. Supports ground alignment, look-along-path orientation, random Y rotation, optional parent, and name prefix.
- `"match_height"` — Set all specified objects to the same Y height. Either provide an explicit `height` value or set `useAverage=true` to compute the average Y across all selected objects.

---

### Zone Management

#### `manage_zones`
Omnibus tool for gameplay zone/area management. Operations:
- `"create_zone_marker"` — Create an invisible GO tagged as a gameplay zone (buy_zone, safe_zone, jail_area, no_pvp, spawn_zone, no_build). Tags integrate with existing tag-based querying.
- `"create_trigger_volume"` — Create a GO with a BoxCollider configured as a trigger volume.
- `"configure_trigger_volume"` — Modify an existing trigger volume's size, trigger state, and enabled state.
- `"tag_objects_in_volume"` — Find objects within a box or sphere around a target and apply a tag. Uses physics overlap queries.
- `"list_zones"` — List all objects with `zone:*` tags, optionally filtered by zone type.
- `"remove_zone_tag"` — Remove a zone tag from an object.
- `"get_objects_in_zone"` — Find all objects physically inside zone markers using box overlap queries.

---

### Visibility & Culling

#### `manage_visibility`
Omnibus tool for object visibility and render culling. Operations:
- `"create_culling_box"` — Create a `SceneCullingBox` for performance culling (Inside mode hides objects inside the box, Outside mode hides objects outside all boxes).
- `"delete_culling_box"` — Delete a tracked culling box and its parent GO.
- `"list_culling_boxes"` — List all tracked culling boxes with positions, sizes, and modes.
- `"set_editor_only"` — Add/remove the `editor_only` tag on an object (hidden during gameplay).
- `"list_editor_only"` — List all objects with the `editor_only` tag.
- `"hide_in_game"` — Bulk-add `editor_only` tag to multiple objects via GUIDs array.

---

### Navigation

#### `create_nav_mesh_agent`
Create a GO with a NavMeshAgent component for AI navigation. Accepts position, agent height, radius, max speed, acceleration, and auto-traverse-links toggle.

#### `create_nav_mesh_link`
Create a NavMeshLink for connecting navigation mesh polygons (ladders, jumps, teleports). Accepts start/end positions, bidirectional toggle, and connection radius.

#### `create_nav_mesh_area`
Create a NavMeshArea volume that blocks or modifies navmesh generation in a region. Defaults to blocker mode.

---

### NavMesh Management

#### `manage_navmesh`
Omnibus tool for NavMesh system management. Operations:
- `"get_navmesh_status"` — Returns enabled, isDirty, isGenerating, and agent parameter values.
- `"configure_navmesh"` — Set agent parameters (height, radius, step size, max slope) and enabled state.
- `"mark_dirty"` — Mark the navmesh as dirty so it rebuilds over the next few frames (deferred, safe in sync handler).
- `"regenerate_area"` — Trigger bounded tile regeneration around a point with a given radius (async, fire-and-forget).
- `"toggle_navmesh"` — Enable or disable the navmesh system.
- `"get_navmesh_config"` — Return full navmesh configuration as JSON.

---

### Game Entity Configuration

#### `configure_game_entities`
Omnibus tool for high-level game entity configuration with domain-specific property groups. Unlike `set_component_property`, this provides preset configurations. Does NOT create entities (use `create_game_entity` for that). Operations:
- `"configure_door"` — Set DarkRP door properties (isPurchasable, isOwnable, isLocked, buyAmount, doorGroups, blacklistedGroups) via reflection.
- `"configure_spawn_point"` — Set team tags and color tint on spawn points.
- `"configure_trigger"` — Configure TriggerHurt properties (damage, rate, damageTags, startEnabled).
- `"configure_prop"` — Configure Prop properties (health, isStatic, tint, bodyGroups).
- `"configure_world_panel"` — Configure WorldPanel display for shop signs (panelSize, renderScale, lookAtCamera, interactionRange).
- `"configure_chair"` — Configure BaseChair interaction (sitPose, sitHeight, tooltipTitle).
- `"configure_dresser"` — Configure NPC appearance via Dresser component (source, manualHeight, manualTint, manualAge, applyHeightScale).

---

### Scene Data

#### `manage_scene_data`
Omnibus tool for scene serialization, cloning, comparison, and network config. Enables scene templates and data-driven workflows. Operations:
- `"serialize_objects"` — Serialize one or more GameObjects to JSON. Accepts `ids` array or uses editor selection if omitted.
- `"deserialize_objects"` — Deserialize objects from a JSON string back into the scene. Supports single object or array. Optional position override and parent.
- `"clone_with_properties"` — Deep-clone an object and apply property overrides. Uses `"componentType.propertyName": value` format in the `overrides` object.
- `"compare_objects"` — Compare two GameObjects: name, enabled, tags, children count, component types, transform, network mode. Set `deep=true` for serialized JSON comparison.
- `"batch_set_network_mode"` — Set `NetworkMode` (Never, Object, Snapshot) on multiple objects at once.
- `"get_serialized"` — Return the full serialized JSON of a single GameObject.

---

## Git Submodule Setup

If you want to track this library as a git submodule in your S&box project (rather than installing via the Library Manager), add it under your project's `Libraries/` directory:

```bash
git submodule add https://github.com/ozmium7/Ozmium-MCP-Server-for-Sbox.git Libraries/ozmium.oz_mcp
```

This will create an entry in your `.gitmodules` like:

```
[submodule "Libraries/ozmium.oz_mcp"]
    path = Libraries/ozmium.oz_mcp
    url = https://github.com/ozmium7/Ozmium-MCP-Server-for-Sbox.git
```

When cloning a project that already has this submodule registered, initialize and pull it with:

```bash
git submodule update --init --recursive
```

To update the submodule to the latest commit on its remote:

```bash
git submodule update --remote Libraries/ozmium.oz_mcp
```

---

## Setup

1. **Install the plugin** — add it via the S&box Library Manager (or as a git submodule — see above) and let it compile.
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

5. **Done.** Your AI assistant can now call all 70 tools directly.

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
| `OzmiumEditorHandlers.cs` | Tool logic for editor-control tools (selection, editor state, open asset, editor log) — also owns editor tool schemas |
| `MeshEditHandlers.cs` | Mesh editing tools (create block, edit_mesh omnibus, mesh info) |
| `TerrainToolHandlers.cs` | Terrain creation, editing, painting, and analysis (manage_terrain omnibus — 17 operations) |
| `LightingToolHandlers.cs` | Lighting omnibus tool (manage_lighting) |
| `PhysicsToolHandlers.cs` | Physics omnibus tool (manage_physics) |
| `AudioToolHandlers.cs` | Audio omnibus tool (manage_audio) |
| `CameraToolHandlers.cs` | Camera omnibus tool (manage_camera) |
| `EffectToolHandlers.cs` | Effects & environment omnibus tool (manage_effects) |
| `UtilityToolHandlers.cs` | Utility tools (asset dependencies, batch transform, copy component, object bounds) |
| `NavigationToolHandlers.cs` | Navigation tools (nav mesh agent, link, area) |
| `RenderingToolHandlers.cs` | Rendering omnibus tool (create_render_entity) |
| `GameToolHandlers.cs` | Game omnibus tool (create_game_entity) |
| `SceneQueryToolHandlers.cs` | Scene spatial queries omnibus (ray cast, sweep, overlap, terrain height) |
| `ProceduralMeshToolHandlers.cs` | Procedural mesh omnibus (custom mesh, ramp, cylinder, arch, merge, extrude) |
| `MaterialToolHandlers.cs` | Material editing omnibus (shader params, texture swap, model overrides) |
| `BatchToolHandlers.cs` | Batch operations omnibus (enable/disable, delete, tag, material, grid duplicate, property, reparent) |
| `BuildAutomationToolHandlers.cs` | Build automation (scatter prefabs, replace prefab instances, align to ground, randomize transforms) |
| `ZoneToolHandlers.cs` | Zone management (zone markers, trigger volumes, tag objects in zones, list/query zones) |
| `VisibilityToolHandlers.cs` | Visibility & culling (culling boxes, editor-only tags, bulk hide) |
| `NavMeshToolHandlers.cs` | NavMesh management (status, config, dirty, regenerate area, toggle) |
| `GameEntityConfigToolHandlers.cs` | Game entity config (doors, spawn points, triggers, props, panels, chairs, dressers) |
| `SceneDataToolHandlers.cs` | Scene data (serialize, deserialize, clone with overrides, compare, network mode) |
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

---

## Acknowledgments
A special thank you to **Oldschoola** for their outstanding contributions and ideas that helped shape and improve this project!
