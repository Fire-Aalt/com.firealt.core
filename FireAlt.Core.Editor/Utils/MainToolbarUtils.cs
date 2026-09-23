using System;
using System.Linq;
using UnityEditor;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor
{
    [NoAutoStaticsCleanup]
    public static class MainToolbarUtils
    {
        public static readonly Color EnabledColor = new(0, 1, 0, 0.15f);
        public static readonly Color DisabledColor = new(1, 0, 0, 0.15f);
        public static readonly Color PlaymodeEnabledColor = new Color(68f / 255f, 93f / 255f, 120f / 255f);
        
        public static bool Exists(VisualElement element)
        {
            return element != null && element.panel != null;
        }
        
        public static Texture2D GetEditorIcon(string name)
        {
            var icon = EditorGUIUtility.IconContent(name).image as Texture2D;
            return icon;
        }
        
        public static void StyleElement<T>(string name, T cached, Action<T> styleAction, bool immediate = false) where T : VisualElement
        {
            if (immediate && Exists(cached))
            {
                ApplyStyle(name, cached, styleAction);
                return;
            }
            
            EditorApplication.delayCall += () => {
                if (!immediate && Exists(cached))
                {
                    ApplyStyle(name, cached, styleAction);
                    return;
                }

                var element = FindElementByName(name);
                if (element == null) return;

                T targetElement;
                if (element is T typedElement) {
                    targetElement = typedElement;
                } else {
                    targetElement = element.Query<T>().First();
                }

                if (targetElement != null) {
                    ApplyStyle(name, targetElement, styleAction);
                }
            };
        }

        public static void StyleElement<T>(string name, T cached, Func<VisualElement, T> queryAction,
            Action<T> styleAction) where T : VisualElement
        {
            EditorApplication.delayCall += () => {
                if (Exists(cached))
                {
                    ApplyStyle(name, cached, styleAction);
                    return;
                }
                
                var element = FindElementByName(name);
                if (element == null) return;
                
                if (queryAction == null)
                {
                    if (element is T visualElement)
                    {
                        ApplyStyle(name, visualElement, styleAction);
                    }
                }
                else
                {
                    var queriedElement = queryAction(element);
                    ApplyStyle(name, queriedElement, styleAction);
                }
            };
        }

        private static void ApplyStyle<T>(string name, T element, Action<T> styleAction) 
            where T : VisualElement
        {
            styleAction(element);
        }
        
        private static VisualElement FindElementByName(string name) 
        {
            var window = (EditorWindow)Resources.FindObjectsOfTypeAll(GetMainToolbarWindowType()).FirstOrDefault();
            if (window == null) throw new Exception("Unable to find MainToolbarWindow");
            var root = window.rootVisualElement;
            
            VisualElement element;
            return (element = root.FindElementByName(name)) != null 
                ? element 
                : null;
        }
        
        private static Type GetMainToolbarWindowType()
        {
            var editorAsm = typeof(EditorWindow).Assembly;
            var t = editorAsm.GetType("UnityEditor.MainToolbarWindow", throwOnError: true, ignoreCase: false);
            return t;
        }
        
        private static VisualElement FindElement(this VisualElement element, Func<VisualElement, bool> predicate) 
        {
            if (predicate(element)) {
                return element;
            }
            return element.Query<VisualElement>().Where(predicate).First();
        }
        
        private static VisualElement FindElementByName(this VisualElement element, string name) 
        {
            return element.FindElement(e => e.name == name);
        }
    }
}
