using FireAlt.Core.Inspectors;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor.Inspectors
{
    [CustomPropertyDrawer(typeof(InlineScriptableObjectAttribute))]
    public class InlineScriptableObjectPropertyDrawer : PropertyDrawer
    {
        private const string SCRIPT_PROPERTY_NAME = "m_Script";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.TryAddStyleSheet(DrawerStyleResources.CommonStyleSheet);
            container.TryAddStyleSheet(DrawerStyleResources.InlineScriptableObjectStyleSheet);
            container.AddToClassList("kras-inline-so-root");

            if (property.propertyType != SerializedPropertyType.ObjectReference)
            {
                container.Add(new Label("InlineScriptableObjectAttribute can only be used on Object references."));
                container.Add(new PropertyField(property));
                return container;
            }

            var fieldType = fieldInfo?.FieldType;
            if (fieldType == null || !typeof(ScriptableObject).IsAssignableFrom(fieldType))
            {
                container.Add(new Label("InlineScriptableObjectAttribute can only be used on ScriptableObject fields."));
                container.Add(new PropertyField(property));
                return container;
            }

            var objectField = new ObjectField(property.displayName)
            {
                objectType = fieldType,
                value = property.objectReferenceValue,
            };

            objectField.AddToClassList(BaseField<Object>.alignedFieldUssClassName);
            objectField.AddToClassList("kras-inline-so-object-field");
            objectField.labelElement?.AddToClassList("kras-inline-so-object-field-label");

            var headerContainer = new VisualElement();
            headerContainer.AddToClassList("kras-drawer-box-header");
            headerContainer.Add(objectField);

            var bodyContainer = new VisualElement();
            bodyContainer.AddToClassList("kras-drawer-box-body");

            container.Add(headerContainer);
            container.Add(bodyContainer);

            objectField.RegisterValueChangedCallback(change =>
            {
                property.objectReferenceValue = change.newValue;
                property.serializedObject.ApplyModifiedProperties();
                Rebuild(bodyContainer, objectField, property);
            });

            container.TrackPropertyValue(property, _ => Rebuild(bodyContainer, objectField, property));
            Rebuild(bodyContainer, objectField, property);

            return container;
        }

        private static void Rebuild(VisualElement bodyContainer, ObjectField objectField, SerializedProperty property)
        {
            objectField.SetValueWithoutNotify(property.objectReferenceValue);
            bodyContainer.Clear();

            if (property.hasMultipleDifferentValues)
            {
                AddSubtleInfoLabel(bodyContainer, "Multiple different values.");
                return;
            }

            if (property.objectReferenceValue == null)
            {
                return;
            }

            if (property.objectReferenceValue is not ScriptableObject)
            {
                AddSubtleInfoLabel(bodyContainer, "Assigned reference is not a ScriptableObject.");
                return;
            }

            var serializedObject = new SerializedObject(property.objectReferenceValue);
            var iterator = serializedObject.GetIterator();
            var enterChildren = true;
            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (iterator.name == SCRIPT_PROPERTY_NAME)
                {
                    continue;
                }

                var child = iterator.Copy();
                var childField = new PropertyField(child);
                childField.Bind(serializedObject);
                bodyContainer.Add(childField);
            }
        }

        private static void AddSubtleInfoLabel(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.AddToClassList("kras-inline-so-info-label");
            parent.Add(label);
        }
    }
}
