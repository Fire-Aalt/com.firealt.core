using System;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Toolbars;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor
{
    [NoAutoStaticsCleanup]
    public static partial class LeakDetectionLevelDropdown
    {
        private const string PATH = "FireAlt/Leak Detection Level";

        private static Image _iconImage;
        private static TextElement _label;
        
        [OnEnteringPlayMode]
        [OnExitingPlayMode]
        private static void RefreshToolbarOnPlayModeChange() => Refresh();
        
        [MainToolbarElement(PATH, defaultDockPosition = MainToolbarDockPosition.Middle)]
        public static MainToolbarElement LeakDetectionLevel()
        {
            var content = new MainToolbarContent(GetText(), GetIcon(), string.Empty);
            var element = new MainToolbarDropdown(content, ShowDropdownMenu) { enabled = !EditorApplication.isPlayingOrWillChangePlaymode };
            return element;
        }

        private static void ShowDropdownMenu(Rect dropDownRect)
        {
            var menu = new GenericMenu();

            var modeNames = Enum.GetNames(typeof(NativeLeakDetectionMode));
            foreach (var modeName in modeNames)
            {
                var mode = Enum.Parse<NativeLeakDetectionMode>(modeName);
                var isOn = NativeLeakDetection.Mode == mode;
                
                menu.AddItem(new GUIContent(modeName), isOn, () =>
                {
                    NativeLeakDetection.Mode = mode;
                    Refresh();
                });
            }
            menu.DropDown(dropDownRect);
        }
        
        private static string GetText()
        {
            string text;
            switch (NativeLeakDetection.Mode)
            {
                case NativeLeakDetectionMode.Disabled:
                case NativeLeakDetectionMode.Enabled:
                    text = NativeLeakDetection.Mode.ToString();
                    break;
                case NativeLeakDetectionMode.EnabledWithStackTrace:
                    text = "StackTrace";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return text;
        }
        
        private static Texture2D GetIcon()
        {
            string iconName;
            switch (NativeLeakDetection.Mode)
            {
                case NativeLeakDetectionMode.Disabled:
                    iconName = "d_DebuggerDisabled";
                    break;
                case NativeLeakDetectionMode.Enabled:
                    iconName = "d_DebuggerEnabled";
                    break;
                case NativeLeakDetectionMode.EnabledWithStackTrace:
                    iconName = "d_DebuggerAttached";
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return MainToolbarUtils.GetEditorIcon(iconName);
        }
        
        private static void Refresh()
        {
            MainToolbar.Refresh(PATH);
        }
    }
}
