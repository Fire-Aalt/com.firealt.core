using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.Toolbars;

namespace FireAlt.Core.Editor
{
    public static class CleanScriptReloadButton
    {
        private const string PATH = "FireAlt/Clean Script Reload";

        [MainToolbarElement(PATH, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement Create()
        {
            var content = new MainToolbarContent(
                "Clean Script Reload",
                MainToolbarUtils.GetEditorIcon("Refresh"),
                "Reload all scripts with a clean build cache to see compilation warnings.");

            return new MainToolbarButton(content, RequestCleanScriptReload);
        }

        private static void RequestCleanScriptReload()
        {
            var confirmed = EditorUtility.DisplayDialog(
                "Clean Script Reload",
                "Reload all scripts with a clean build cache to see the compilation warnings?",
                "Yes",
                "No");

            if (confirmed)
            {
                CompilationPipeline.RequestScriptCompilation(RequestScriptCompilationOptions.CleanBuildCache);
            }
        }
    }
}
