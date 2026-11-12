using System;
using System.Collections.Generic;
using FireAlt.Core.Inspectors;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor.Inspectors
{
    [CustomPropertyDrawer(typeof(EnumToggleButtonsAttribute))]
    public class EnumToggleButtonsPropertyDrawer : PropertyDrawer
    {
        private struct ButtonState
        {
            public Button Button;
            public int Value;
            public bool ExactMatch;
        }

        private sealed class EnumToggleButtonsField : BaseField<int>
        {
            public EnumToggleButtonsField(string label, bool hideLabel)
                : this(label, hideLabel, new VisualElement())
            {
            }

            private EnumToggleButtonsField(string label, bool hideLabel, VisualElement inputElement)
                : base(label, inputElement)
            {
                InputElement = inputElement;
                AddToClassList("kras-enum-toggle-root");
                AddToClassList(alignedFieldUssClassName);

                InputElement.AddToClassList("unity-property-field__input");
                InputElement.AddToClassList("kras-enum-toggle-input");

                if (hideLabel)
                {
                    AddToClassList(noLabelVariantUssClassName);
                }
                else
                {
                    labelElement.AddToClassList("unity-property-field__label");
                    labelElement.AddToClassList("kras-enum-toggle-label");
                }
            }

            public VisualElement InputElement { get; }
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            if (property.propertyType != SerializedPropertyType.Enum)
            {
                return new PropertyField(property);
            }

            var enumType = fieldInfo?.FieldType;
            if (enumType == null || !enumType.IsEnum)
            {
                return new PropertyField(property);
            }

            var isDarkTheme = EditorGUIUtility.isProSkin;
            var attribute = (EnumToggleButtonsAttribute)this.attribute;
            var useFlags = enumType.IsDefined(typeof(FlagsAttribute), false);
            var enumValues = Enum.GetValues(enumType);
            var allFlags = CalculateAllFlags(enumValues);

            var root = new EnumToggleButtonsField(property.displayName, attribute.HideLabel);
            root.TryAddStyleSheet(DrawerStyleResources.EnumToggleButtonsStyleSheet);
            var buttonsContainer = root.InputElement;
            buttonsContainer.AddToClassList("kras-enum-toggle-container");

            var states = new List<ButtonState>();

            if (useFlags)
            {
                var allButton = BuildButton("All", isDarkTheme);
                allButton.clicked += () =>
                {
                    property.serializedObject.Update();
                    property.enumValueFlag = property.enumValueFlag == allFlags ? 0 : allFlags;
                    property.serializedObject.ApplyModifiedProperties();
                    UpdateButtonVisuals(property, states, useFlags);
                };

                buttonsContainer.Add(allButton);
                states.Add(new ButtonState { Button = allButton, Value = allFlags, ExactMatch = true });
            }

            foreach (var enumValue in enumValues)
            {
                var valueName = enumValue.ToString();
                var valueInt = Convert.ToInt32(enumValue);
                var button = BuildButton(valueName, isDarkTheme);
                button.clicked += () =>
                {
                    property.serializedObject.Update();
                    property.enumValueFlag = useFlags ? property.enumValueFlag ^ valueInt : valueInt;
                    property.serializedObject.ApplyModifiedProperties();
                    UpdateButtonVisuals(property, states, useFlags);
                };

                buttonsContainer.Add(button);
                states.Add(new ButtonState
                {
                    Button = button,
                    Value = valueInt,
                    ExactMatch = useFlags && valueInt == 0,
                });
            }

            void UpdateVisuals()
            {
                UpdateButtonVisuals(property, states, useFlags);
            }

            root.TrackPropertyValue(property, _ => UpdateVisuals());
            Undo.undoRedoPerformed += UpdateVisuals;
            root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= UpdateVisuals);

            UpdateVisuals();
            return root;
        }

        private static int CalculateAllFlags(Array enumValues)
        {
            var allFlags = 0;
            foreach (var enumValue in enumValues)
            {
                allFlags |= Convert.ToInt32(enumValue);
            }

            return allFlags;
        }

        private static Button BuildButton(string label, bool isDarkTheme)
        {
            var button = new Button { text = label };
            button.AddToClassList("kras-enum-toggle-button");
            button.AddToClassList(isDarkTheme ? "kras-enum-toggle-button--dark" : "kras-enum-toggle-button--light");
            return button;
        }

        private static void UpdateButtonVisuals(
            SerializedProperty property,
            IReadOnlyList<ButtonState> states,
            bool useFlags)
        {
            property.serializedObject.Update();
            var currentValue = property.enumValueFlag;

            for (var i = 0; i < states.Count; i++)
            {
                var state = states[i];
                var isPressed = IsPressed(currentValue, state.Value, state.ExactMatch, useFlags);
                ApplyButtonStyle(state.Button, isPressed);
            }
        }

        private static bool IsPressed(int currentValue, int buttonValue, bool exactMatch, bool useFlags)
        {
            if (exactMatch || !useFlags)
            {
                return currentValue == buttonValue;
            }

            return (currentValue & buttonValue) == buttonValue;
        }

        private static void ApplyButtonStyle(Button button, bool isPressed)
        {
            button.EnableInClassList("kras-enum-toggle-button--pressed", isPressed);
        }
    }
}
