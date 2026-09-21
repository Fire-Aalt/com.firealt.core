# FireAlt.Core
A library of Unity extensions and utils that I use regularly both at Runtime and Editor.

## Overview
`FireAlt.Core` is a Unity package of runtime and editor utilities used across DOTS/ECS, rendering, tooling, and general project helpers.

Main folders:
- `Runtime/`: gameplay/runtime code, ECS systems, data containers, extensions.
- `Editor/`: inspector integrations, toolbar widgets, editor tools, and asset helpers.

## Important Systems

### ArtificeToolkit Integration
Artifice integration is split between runtime attributes and editor drawers:
- `Runtime/ArtificeToolkit/EnableIfMethodAttribute.cs`: show/hide a field based on a method result.
- `Runtime/ArtificeToolkit/InlineNoFoldoutAttribute.cs`: render nested fields inline (no foldout).
- `Editor/ArtificeToolkit/Artifice_CustomAttributeDrawer_*.cs`: UI Toolkit drawers that implement the behavior.
- `Editor/Tools/Artifice_EditorWindow.cs`: base editor window that renders itself via `ArtificeDrawer`.

Note: `ButtonEnableIfMethodAttribute` and `ScriptableObjectCreator` are present but commented out (legacy/inactive).

### ParallelList (PrallelList) and Parallel-to-List Mapping
`Runtime/NZCore/ParallelList/ParallelList.cs` provides a high-performance per-thread list container for jobs, with:
- thread/chunk writers and readers,
- single-threaded and multi-threaded copy jobs to flatten data,
- safety integration for Unity collections checks.

`Runtime/Data/Collections/ParallelToListMapper.cs` wraps `ParallelList<T>` + `NativeList<T>` for common "collect in parallel, consume sequentially" flows.

```csharp
var mapper = new ParallelToListMapper<MyData>(128, Allocator.TempJob);
var writer = mapper.AsThreadWriter();
// write in jobs...
var handle = mapper.CopyParallelToListSingle(dependency);
```

### RuntimeMaterial Pipeline (ECS Graphics)
Material remapping is handled by:
- `RuntimeMaterial` (`IComponentData`) storing a material reference.
- `RuntimeMaterialLookup` (`IEnableableComponent`) storing `(source material, texture)` lookup keys.
- `RuntimeMaterialSystem` cloning/caching materials, registering `BatchMaterialID` when needed, applying to entities, then disabling lookup to avoid reprocessing.

This is useful for per-texture runtime material instancing without rebuilding all render data manually.

## Other Runtime Utilities

### Rendering
- `RendererUtility`: sprite UV atlas/pivot helpers + `RenderParams` creation.
- `CameraDistort`: projection tweak component for stylized camera distortion.
- `GlobalUnscaledShaderTimeSystem`: updates global shader `UnscaledTime`.
- `CameraSpaceUIDocumentScaler`: camera-space UI Toolkit scaling and coordinate conversion.
- `VisualElementMaterial`: creates per-element material instances for UI Toolkit.

### Data & Collections
- `BlittableBool`, `SampledAnimationCurve`, `UniTimer`.
- `NativeDynamicPerfectHashMap<TKey, TValue>` for dynamic updates + rebuilt perfect hashing.
- `Extensions/Collections/*`: many helpers for `NativeList`, hash maps/sets, unsafe containers, and `ParallelList` enumeration.

### Core Utils
- `RandomUtils`: weighted random selection.
- `TrajectoryUtils`: projectile path sampling.
- `PlayerLoopUtils`: register/remove custom loop callbacks.
- `ReflectionUtils`, `MemoryUtils`, `HashUtils`, `StringUtils`, `Database`.

## Editor Utilities
- `Editor/Tools/FindMissingScriptsTool.cs`: scan scenes for missing script components.
- `Editor/Tools/NormalMapperWindow.cs`: batch generate sprite normal maps.
- `Editor/Tools/GameViewVSyncFix.cs`: preserves Game view VSync state through play mode.
- `Editor/MainToolbar/*`: scene-group dropdown, leak detection mode selector, Anchor toolbar visibility toggle.
- `Editor/Utils/*`: `AssetDatabaseUtils`, `ScopedEditorPrefs`, toolbar styling helpers, serialization helpers.

## Conditional Integrations
Several features compile conditionally:
- `BL_CORE`
- `BL_QUILL`
- `BL_ESSENSE`

Check `Runtime/FireAlt.Core.asmdef` and `Editor/FireAlt.Core.Editor.asmdef` for package references and version defines.

