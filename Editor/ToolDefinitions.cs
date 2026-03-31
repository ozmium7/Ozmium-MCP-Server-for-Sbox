using System.Linq;

namespace SboxMcpServer;

/// <summary>
/// Aggregates all MCP tool schema definitions returned by tools/list.
/// </summary>
internal static class ToolDefinitions
{
	internal static object[] All => new object[]
	{
		// ── Original 9 read + asset + console tools ────────────────────────
		SceneToolDefinitions.GetSceneSummary,
		SceneToolDefinitions.GetSceneHierarchy,
		SceneToolDefinitions.FindGameObjects,
		SceneToolDefinitions.FindGameObjectsInRadius,
		SceneToolDefinitions.GetGameObjectDetails,
		SceneToolDefinitions.GetComponentProperties,
		SceneToolDefinitions.GetPrefabInstances,
		AssetToolDefinitions.BrowseAssets,
		AssetToolDefinitions.GetEditorContext,
		ConsoleToolDefinitions.ListConsoleCommands,
		ConsoleToolDefinitions.RunConsoleCommand,

		// ── Write tools ────────────────────────────────────────────────────
		OzmiumWriteHandlers.SchemaCreateGameObject,
		OzmiumWriteHandlers.SchemaAddComponent,
		OzmiumWriteHandlers.SchemaRemoveComponent,
		OzmiumWriteHandlers.SchemaSetComponentProperty,
		OzmiumWriteHandlers.SchemaDestroyGameObject,
		OzmiumWriteHandlers.SchemaReparentGameObject,
		OzmiumWriteHandlers.SchemaSetGameObjectTags,
		OzmiumWriteHandlers.SchemaInstantiatePrefab,
		OzmiumWriteHandlers.SchemaUndo,
		OzmiumWriteHandlers.SchemaRedo,

		// ── Batch transform & object management ────────────────────────────
		OzmiumWriteHandlers.SchemaSetGameObjectTransform,
		OzmiumWriteHandlers.SchemaDuplicateGameObject,
		OzmiumWriteHandlers.SchemaSetGameObjectEnabled,
		OzmiumWriteHandlers.SchemaSetGameObjectName,
		OzmiumWriteHandlers.SchemaSetComponentEnabled,

		// ── Asset tools ────────────────────────────────────────────────────
		OzmiumAssetHandlers.SchemaGetModelInfo,
		OzmiumAssetHandlers.SchemaGetMaterialProperties,
		OzmiumAssetHandlers.SchemaGetPrefabStructure,
		OzmiumAssetHandlers.SchemaReloadAsset,
		OzmiumAssetHandlers.SchemaGetComponentTypes,
		OzmiumAssetHandlers.SchemaSearchAssets,
		OzmiumAssetHandlers.SchemaGetSceneStatistics,

		// ── Editor control ─────────────────────────────────────────────────
		OzmiumEditorHandlers.SchemaOpenAsset,
		OzmiumEditorHandlers.SchemaGetEditorLog,
		OzmiumEditorHandlers.SchemaManageSelection,
		OzmiumEditorHandlers.SchemaManageEditorState,
		OzmiumEditorHandlers.SchemaFrameSelection,
		OzmiumEditorHandlers.SchemaSaveSceneAs,
		OzmiumEditorHandlers.SchemaGetSceneUnsaved,
		OzmiumEditorHandlers.SchemaBreakFromPrefab,
		OzmiumEditorHandlers.SchemaUpdateFromPrefab,

		// ── Mesh editing tools ─────────────────────────────────────────────
		MeshEditHandlers.SchemaCreateBlock,
		MeshEditHandlers.SchemaGetMeshInfo,
		MeshEditHandlers.SchemaEditMesh,

		// ── Lighting (omnibus) ────────────────────────────────────────────
		LightingToolHandlers.SchemaManageLighting,

		// ── Physics (omnibus) ──────────────────────────────────────────────
		PhysicsToolHandlers.SchemaManagePhysics,

		// ── Audio (omnibus) ────────────────────────────────────────────────
		AudioToolHandlers.SchemaManageAudio,

		// ── Camera (omnibus) ───────────────────────────────────────────────
		CameraToolHandlers.SchemaManageCamera,

		// ── Effects & environment (omnibus) ───────────────────────────────
		EffectToolHandlers.SchemaManageEffects,

		// ── Utility tools ──────────────────────────────────────────────────
		UtilityToolHandlers.SchemaGetAssetDependencies,
		UtilityToolHandlers.SchemaBatchTransform,
		UtilityToolHandlers.SchemaCopyComponent,
		UtilityToolHandlers.SchemaGetObjectBounds,

		// ── Navigation tools ──────────────────────────────────────────────
		NavigationToolHandlers.SchemaCreateNavMeshAgent,
		NavigationToolHandlers.SchemaCreateNavMeshLink,
		NavigationToolHandlers.SchemaCreateNavMeshArea,

		// ── Rendering (omnibus) ───────────────────────────────────────────
		RenderingToolHandlers.SchemaCreateRenderEntity,

		// ── Game (omnibus) ────────────────────────────────────────────────
		GameToolHandlers.SchemaCreateGameEntity,

		// ── Scene spatial queries (omnibus) ───────────────────────────────
		SceneQueryToolHandlers.SchemaSceneTrace,

		// ── Terrain (omnibus) ────────────────────────────────────────────
		TerrainToolHandlers.SchemaManageTerrain,

		// ── Procedural mesh (omnibus) ────────────────────────────────────
		ProceduralMeshToolHandlers.SchemaBuildProceduralMesh,

		// ── Material editing (omnibus) ──────────────────────────────────
		MaterialToolHandlers.SchemaManageMaterial,

		// ── Batch operations (omnibus) ──────────────────────────────────
		BatchToolHandlers.SchemaBatchOperations,

		// ── Build automation (omnibus) ────────────────────────────────────
		BuildAutomationToolHandlers.SchemaBuildAutomation,

		// ── Zone management (omnibus) ────────────────────────────────────
		ZoneToolHandlers.SchemaManageZones,

		// ── Visibility & culling (omnibus) ───────────────────────────────
		VisibilityToolHandlers.SchemaManageVisibility,

		// ── NavMesh management (omnibus) ─────────────────────────────────
		NavMeshToolHandlers.SchemaManageNavmesh,

		// ── Game entity config (omnibus) ─────────────────────────────────
		GameEntityConfigToolHandlers.SchemaConfigureGameEntities,

		// ── Scene data (omnibus) ─────────────────────────────────────────
		SceneDataToolHandlers.SchemaManageSceneData,

		// ── Prefab management (omnibus) ──────────────────────────────────
		PrefabToolHandlers.SchemaManagePrefabs,

		// ── Compilation management (omnibus) ─────────────────────────────
		CompilationToolHandlers.SchemaManageCompilation,

		// ── Asset management (omnibus) ───────────────────────────────────
		AssetManagementToolHandlers.SchemaManageAssets,
	};
}
