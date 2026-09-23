using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor.Inspectors
{
    [NoAutoStaticsCleanup]
    public static partial class DrawerStyleResources
    {
        public static StyleSheet CommonStyleSheet;
        public static StyleSheet EnumToggleButtonsStyleSheet;
        public static StyleSheet InlineScriptableObjectStyleSheet;

        [OnCodeInitializing]
        private static void LoadStyles()
        {
            CommonStyleSheet = Load<StyleSheet>("Styles/DrawerCommon.uss");
            EnumToggleButtonsStyleSheet = Load<StyleSheet>("Styles/EnumToggleButtonsDrawer.uss");
            InlineScriptableObjectStyleSheet = Load<StyleSheet>("Styles/InlineScriptableObjectDrawer.uss");
        }

        private static T Load<T>(string path) where T : Object
        {
            return AssetDatabaseUtils.LoadEditorResource<T>(path, "com.firealt.core");
        }
    }
}
