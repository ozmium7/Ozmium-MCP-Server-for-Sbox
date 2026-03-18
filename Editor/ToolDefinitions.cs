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

		// ── New write tools ────────────────────────────────────────────────
		OzmiumWriteHandlers.SchemaCreateGameObject,
		OzmiumWriteHandlers.SchemaAddComponent,
		OzmiumWriteHandlers.SchemaRemoveComponent,
		OzmiumWriteHandlers.SchemaSetComponentProperty,
		OzmiumWriteHandlers.SchemaDestroyGameObject,
		OzmiumWriteHandlers.SchemaReparentGameObject,
		OzmiumWriteHandlers.SchemaSetGameObjectTags,
		OzmiumWriteHandlers.SchemaInstantiatePrefab,
		OzmiumWriteHandlers.SchemaSaveScene,
		OzmiumWriteHandlers.SchemaUndo,
		OzmiumWriteHandlers.SchemaRedo,

		// ── New asset tools ────────────────────────────────────────────────
		OzmiumAssetHandlers.SchemaGetModelInfo,
		OzmiumAssetHandlers.SchemaGetMaterialProperties,
		OzmiumAssetHandlers.SchemaGetPrefabStructure,
		OzmiumAssetHandlers.SchemaReloadAsset,

		// ── New editor control tools ───────────────────────────────────────
		OzmiumEditorHandlers.SchemaSelectGameObject,
		OzmiumEditorHandlers.SchemaOpenAsset,
		OzmiumEditorHandlers.SchemaGetPlayState,
		OzmiumEditorHandlers.SchemaStartPlayMode,
		OzmiumEditorHandlers.SchemaStopPlayMode,
		OzmiumEditorHandlers.SchemaGetEditorLog,
	};
}
