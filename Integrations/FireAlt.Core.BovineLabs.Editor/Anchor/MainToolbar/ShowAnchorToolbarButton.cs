#if UNITY_EDITOR
using BovineLabs.Anchor;
using BovineLabs.Anchor.Debug.Toolbar;
using BovineLabs.Anchor.MVVM;
using BovineLabs.Core.ConfigVars;
using FireAlt.Core.Utility;
using Unity.Burst;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;
using Button = Unity.AppUI.UI.Button;

namespace FireAlt.Core.Editor
{
    [Configurable]
    public class ShowAnchorToolbarButton
    {
        private const string PATH = "FireAlt/Show Anchor Toolbar";
        private static readonly string Name = StringUtils.RemoveAllWhitespace(PATH);
        
        [ConfigVar("krascore.anchor-toolbar.show-on-start", true, "Should the toolbar be shown on startup", true, true)]
        private static readonly SharedStatic<bool> ShowOnStart = SharedStatic<bool>.GetOrCreate<ShowAnchorToolbarButton, EnabledVar>();

        private static EditorToolbarButton _button;
        private static bool _isVisible;

        [InitializeOnLoadMethod]
        public static void Init()
        {
            EditorApplication.playModeStateChanged += PlayModeChanged;
        }
        
        private static void PlayModeChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                if (AnchorApp.Current == null) return;
                var toolbarView = AnchorApp.Current.Services.GetRequiredService<ToolbarView>();

                // Remove 'close' button
                var button = FindButtonWithTrailingIcon(toolbarView.panel.visualTree, "x");
                button.RemoveFromHierarchy();

                SetToolbarVisibility(toolbarView, ShowOnStart.Data);
                ApplyStyle();
            }
            else if (change == PlayModeStateChange.EnteredEditMode)
            {
                ApplyStyle();
            }
        }

        [MainToolbarElement(PATH, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement ShowAnchorToolbar() {
            var icon = EditorGUIUtility.IconContent("CustomTool").image as Texture2D;
            var content = new MainToolbarContent(icon);
            
            var element = new MainToolbarButton(content, ButtonClicked);
            ApplyStyle();
            return element;
        }

        private static void ButtonClicked()
        {
            if (!Application.isPlaying)
            {
                ShowOnStart.Data = !ShowOnStart.Data;
                ApplyStyle();
                return;
            }
            
            var toolbarView = AnchorApp.Current.Services.GetRequiredService<ToolbarView>();
            SetToolbarVisibility(toolbarView, !_isVisible);
            ApplyStyle();
        }

        private static void SetToolbarVisibility(ToolbarView toolbarView, bool visible)
        {
            _isVisible = visible;
            if (_isVisible)
            {
                toolbarView.RestoreToolbar();
            }
            else
            {
                toolbarView.HideToolbar();
            }
        }
        
        private static void ApplyStyle()
        {
            MainToolbarUtils.StyleElement(Name, _button, element =>
            {
                _button = element;
                
                if (!Application.isPlaying)
                {
                    element.style.backgroundColor = ShowOnStart.Data ? MainToolbarUtils.EnabledColor : MainToolbarUtils.DisabledColor;
                }
                else
                {
                    element.style.backgroundColor = _isVisible ? MainToolbarUtils.PlaymodeEnabledColor : StyleKeyword.None;
                }
            }, immediate: true);
        }
        
        private static Button FindButtonWithTrailingIcon(VisualElement root, string trailingIcon)
        {
            return root.Query<Button>().Where(b => b.trailingIcon == trailingIcon).First();
        }
        
        private struct EnabledVar
        {
        }
    }
}
#endif