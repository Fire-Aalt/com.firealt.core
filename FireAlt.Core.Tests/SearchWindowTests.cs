using System.Collections;
using System.Collections.Generic;
using FireAlt.Core.Editor.UI;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace FireAlt.Core.Tests
{
    public class SearchWindowTests
    {
        [UnityTest]
        public IEnumerator TemplateCreatesSearchFieldAndFiltersSelectableItems()
        {
            var first = new object();
            var second = new object();
            var view = new SearchView
            {
                Items = new List<SearchView.Item>
                {
                    new() { Path = "Effects/Fire", Data = first },
                    new() { Path = "Effects/Smoke", Data = second },
                },
            };
            object selected = null;
            view.OnSelection += item => selected = item.Data;

            var host = ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                host.rootVisualElement.Add(view);
                host.ShowUtility();
                yield return null;
                var field = view.Q<TextField>();
                Assert.That(field, Is.Not.Null);
                field.value = "Smoke";
                yield return null;

                var results = view.Q<ListView>("SearchResults");
                Assert.That(results.itemsSource.Count, Is.EqualTo(1));
                results.SetSelection(0);
                Assert.That(selected, Is.SameAs(second));
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [Test]
        public void WindowCreatesAndFocusesItsOwnSearchField()
        {
            var window = SearchWindow.Create();
            try
            {
                window.Items = new List<SearchView.Item> { new() { Path = "Effect", Data = new object() } };
                Assert.That(window.rootVisualElement.Q<TextField>(), Is.Not.Null);
                typeof(SearchWindow).GetMethod("OnFocus", System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic).Invoke(window, null);
            }
            finally
            {
                Object.DestroyImmediate(window);
            }
        }
    }
}
