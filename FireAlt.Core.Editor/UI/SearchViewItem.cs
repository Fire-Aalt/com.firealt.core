// <copyright file="SearchViewItem.cs" company="BovineLabs">
//     Copyright (c) BovineLabs. All rights reserved.
// </copyright>

using Unity.Scripting.LifecycleManagement;
using UnityEngine.UIElements;

namespace FireAlt.Core.Editor.UI
{
    [NoAutoStaticsCleanup]
    internal class SearchViewItem : VisualElement
    {
        private static readonly UITemplate SearchItemTemplate = new(SearchWindow.RootUIPath + "SearchItem");

        private readonly VisualElement icon;
        private readonly Label label;
        private readonly VisualElement nextIcon;

        public SearchViewItem()
        {
            this.AddToClassList("SearchItem");

            SearchItemTemplate.Clone(this);

            this.label = this.Q<Label>("Label");
            this.icon = this.Q("Icon");
            this.nextIcon = this.Q("NextIcon");
            this.tabIndex = -1;
        }

        public string Name { get; private set; }

        public TreeNode<SearchView.Item> Item
        {
            get => this.userData as TreeNode<SearchView.Item>;
            set
            {
                this.userData = value;
                this.icon.style.backgroundImage = value.Value.Icon;
                this.Name = value.Value.Name;
                this.label.text = this.Name;

                this.nextIcon.style.visibility = value.ChildCount == 0 ? Visibility.Hidden : Visibility.Visible;
            }
        }
    }
}
