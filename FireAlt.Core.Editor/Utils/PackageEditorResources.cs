using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor
{
    /// <summary>
    /// Lazily loads editor assets from a package's "Editor Default Resources" folder through its
    /// <c>Packages/&lt;package-name&gt;/...</c> virtual path, which resolves the same way for embedded, local,
    /// git and registry installs.
    /// </summary>
    public sealed class PackageEditorResources
    {
        private const string EDITOR_RESOURCES_FOLDER = "Editor Default Resources";

        private readonly Dictionary<string, Object> _cache = new();

        public PackageEditorResources(string packageName)
        {
            Root = $"Packages/{packageName}/{EDITOR_RESOURCES_FOLDER}/";
        }

        /// <summary>Asset path of the package's resources folder, ending with a slash.</summary>
        public string Root { get; }

        public string GetPath(string relativePath) => Root + relativePath;

        /// <summary>
        /// Loads and caches the asset at <paramref name="relativePath"/>. Returns null if it is not imported (yet);
        /// the next call tries again.
        /// </summary>
        public T Load<T>(string relativePath) where T : Object
        {
            // Unity's null check also catches assets destroyed by a reimport.
            if (_cache.TryGetValue(relativePath, out var cached) && cached != null)
            {
                return cached as T;
            }

            var asset = AssetDatabase.LoadAssetAtPath<T>(GetPath(relativePath));
            if (asset != null)
            {
                _cache[relativePath] = asset;
            }

            return asset;
        }
    }

    public static class StyleSheetExtensions
    {
        /// <summary>Adds <paramref name="styleSheet"/> if it is loaded, so a missing asset does not throw.</summary>
        public static void TryAddStyleSheet(this VisualElement element, StyleSheet styleSheet)
        {
            if (styleSheet != null)
            {
                element.styleSheets.Add(styleSheet);
            }
        }
    }
}
