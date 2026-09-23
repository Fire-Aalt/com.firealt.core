// <copyright file="SearchElement.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using System;
using System.Collections.Generic;
using UnityEditor;
using Unity.Scripting.LifecycleManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor.UI
{
    [NoAutoStaticsCleanup]
    public class SearchElement : BaseField<int>
    {
        private static readonly Color DARK_BASE = new(0.251f, 0.251f, 0.251f, 1f); // #404040
        private static readonly Color DARK_HOVER = new(0.271f, 0.271f, 0.271f, 1f); // #454545
        private static readonly Color DARK_BORDER = new(0.137f, 0.137f, 0.137f, 1f); // #232323
        private static readonly Color DARK_ACCENT = new(0.702f, 0.420f, 0.129f, 1f); // #b36b21
        private static readonly Color DARK_TEXT = new(0.92f, 0.92f, 0.92f, 1f);

        private static readonly Color LIGHT_BASE = new(0.812f, 0.812f, 0.812f, 1f); // #cfcfcf
        private static readonly Color LIGHT_HOVER = new(0.871f, 0.871f, 0.871f, 1f); // #dedede
        private static readonly Color LIGHT_BORDER = new(0.6f, 0.6f, 0.6f, 1f); // #999999
        private static readonly Color LIGHT_ACCENT = new(0.847f, 0.518f, 0.184f, 1f); // #d8842f
        private static readonly Color LIGHT_TEXT = new(0.14f, 0.14f, 0.14f, 1f);

        private readonly List<SearchView.Item> items;
        private readonly Button componentButton;
        private readonly VisualElement inputElement;
        private readonly VisualElement arrowElement;

        public SearchElement(List<SearchView.Item> items, string defaultText, string displayName = "")
            : this(items, defaultText, displayName, new VisualElement())
        {
        }

        private SearchElement(List<SearchView.Item> items, string defaultText, string displayName, VisualElement element)
            : base(displayName, element)
        {
            this.inputElement = element;
            this.AddToClassList(BaseField<string>.alignedFieldUssClassName);
            this.AddToClassList(TextInputBaseField<string>.ussClassName);

            element.AddToClassList("unity-base-field__input");
            element.AddToClassList("unity-base-popup-field__input");
            element.AddToClassList("unity-popup-field__input");
            element.AddToClassList("unity-property-field__input");

            this.componentButton = new Button();
            element.Add(this.componentButton);
            this.componentButton.AddToClassList("unity-base-popup-field__text");
            this.componentButton.RemoveFromClassList("unity-button");

            var image = new VisualElement();
            element.Add(image);
            image.AddToClassList("unity-base-popup-field__arrow");
            this.arrowElement = image;

            this.items = items ?? new List<SearchView.Item>();
            this.labelElement.style.minWidth = 60;
            this.ApplySearchPaletteStyles();

            this.componentButton.clicked += () =>
            {
                if (!TryGetPopupRect(element, out var popupRect))
                {
                    return;
                }

                var searchWindow = SearchWindow.Create();
                searchWindow.Title = displayName;
                searchWindow.Items = this.items;
                searchWindow.OnSelection += item =>
                {
                    this.OnSelection?.Invoke(item);
                    this.componentButton.text = this.SetText(item);
                };

                searchWindow.position = popupRect;
                searchWindow.ShowPopup();
            };

            this.componentButton.text = defaultText;
        }

        public event Action<SearchView.Item> OnSelection;

        public Func<SearchView.Item, string> SetText { get; set; } = item => item.Name;

        public float Height { get; set; } = 315;

        public string Text
        {
            get => this.componentButton.text;
            set => this.componentButton.text = value;
        }

        public void SetValue(int index)
        {
            var item = this.items[index];

            this.OnSelection?.Invoke(item);
            this.componentButton.text = this.SetText(item);
        }

        private bool TryGetPopupRect(VisualElement element, out Rect popupRect)
        {
            var hostWindow = EditorWindow.focusedWindow ?? EditorWindow.mouseOverWindow;
            if (hostWindow == null)
            {
                var editorWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                if (editorWindows != null && editorWindows.Length > 0)
                {
                    hostWindow = editorWindows[0];
                }
            }

            if (hostWindow == null)
            {
                popupRect = default;
                return false;
            }

            var hostWindowRect = hostWindow.position;
            Rect worldBounds;
            if (this.labelElement.parent == null)
            {
                worldBounds = element.worldBound;
            }
            else
            {
                worldBounds = this.labelElement.worldBound;
                worldBounds.width += element.worldBound.width;
            }

            popupRect = new Rect(
                hostWindowRect.x + worldBounds.x,
                hostWindowRect.y + worldBounds.y + worldBounds.height,
                worldBounds.width,
                this.Height);

            return true;
        }

        private void ApplySearchPaletteStyles()
        {
            var isDark = EditorGUIUtility.isProSkin;

            var baseColor = isDark ? DARK_BASE : LIGHT_BASE;
            var hoverColor = isDark ? DARK_HOVER : LIGHT_HOVER;
            var borderColor = isDark ? DARK_BORDER : LIGHT_BORDER;
            var accentColor = isDark ? DARK_ACCENT : LIGHT_ACCENT;
            var textColor = isDark ? DARK_TEXT : LIGHT_TEXT;

            this.inputElement.style.backgroundColor = baseColor;
            this.inputElement.style.borderTopWidth = 1f;
            this.inputElement.style.borderRightWidth = 1f;
            this.inputElement.style.borderBottomWidth = 1f;
            this.inputElement.style.borderLeftWidth = 3f;
            this.inputElement.style.borderTopColor = borderColor;
            this.inputElement.style.borderRightColor = borderColor;
            this.inputElement.style.borderBottomColor = borderColor;
            this.inputElement.style.borderLeftColor = accentColor;

            this.componentButton.style.backgroundColor = Color.clear;
            this.componentButton.style.color = textColor;
            this.componentButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            this.componentButton.style.unityTextAlign = TextAnchor.MiddleLeft;

            this.arrowElement.style.opacity = isDark ? 0.95f : 0.85f;
            
            void SetHover(bool isHovered)
            {
                this.inputElement.style.backgroundColor = isHovered ? hoverColor : baseColor;
            }

            this.inputElement.RegisterCallback<MouseEnterEvent>(_ => SetHover(true));
            this.inputElement.RegisterCallback<MouseLeaveEvent>(_ => SetHover(false));
        }
    }
}
