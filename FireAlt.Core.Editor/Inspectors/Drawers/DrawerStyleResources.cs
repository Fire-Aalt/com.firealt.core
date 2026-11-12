using Unity.Scripting.LifecycleManagement;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor.Inspectors
{
    [NoAutoStaticsCleanup]
    public static partial class DrawerStyleResources
    {
        private static readonly PackageEditorResources PackageResources = new("com.firealt.core");

        public static StyleSheet CommonStyleSheet => PackageResources.Load<StyleSheet>("Styles/DrawerCommon.uss");
        public static StyleSheet EnumToggleButtonsStyleSheet => PackageResources.Load<StyleSheet>("Styles/EnumToggleButtonsDrawer.uss");
        public static StyleSheet InlineScriptableObjectStyleSheet => PackageResources.Load<StyleSheet>("Styles/InlineScriptableObjectDrawer.uss");
    }
}
