using BovineLabs.Core;
using UnityEditor;

namespace FireAlt.Core.BovineLabs.Editor
{
    // Artifice's non-fallback Object editor otherwise overrides Core's component type picker.
    [CustomEditor(typeof(ComponentAsset), true)]
    public sealed class ComponentAssetEditor : global::BovineLabs.Core.Editor.Component.ComponentAssetEditor
    {
    }
}
