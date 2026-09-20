using BovineLabs.Core;
using NUnit.Framework;
using Unity.Entities;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Tests
{
    public class ComponentAssetInspectorTests
    {
        [Test]
        public void DefaultInspectorShowsSelectedComponentPicker()
        {
            var asset = ScriptableObject.CreateInstance<ComponentAsset>();
            UnityEditor.Editor editor = null;
            try
            {
                using (var serialized = new SerializedObject(asset))
                {
                    serialized.FindProperty("typeName").stringValue = typeof(LocalTransform).AssemblyQualifiedName;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                editor = UnityEditor.Editor.CreateEditor(asset);
                var root = editor.CreateInspectorGUI();
                var buttons = root.Query<Button>().ToList();
                Assert.That(buttons.Exists(button => button.text == nameof(LocalTransform)), Is.True,
                    "The default editor must retain Core's searchable component picker even when Artifice is installed.");
                Assert.That(asset.GetStableTypeHash(), Is.EqualTo(TypeManager.GetTypeInfo<LocalTransform>().StableTypeHash));
            }
            finally
            {
                Object.DestroyImmediate(editor);
                Object.DestroyImmediate(asset);
            }
        }
    }
}
