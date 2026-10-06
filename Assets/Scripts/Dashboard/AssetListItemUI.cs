using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// One row of the asset table: ID / Name / Category / Location / Status
    /// badge / Next maintenance + View / Edit / Delete actions.
    /// </summary>
    public class AssetListItemUI : MonoBehaviour
    {
        private Text _idText;
        private Text _nameText;
        private Text _categoryText;
        private Text _locationText;
        private GameObject _statusBadge;
        private Text _nextText;
        private DashboardAsset _asset;
        private bool _built;

        public void Build()
        {
            if (_built) return;
            _built = true;

            HorizontalLayoutGroup h = gameObject.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.padding = new RectOffset(12, 12, 8, 8);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            Image bg = gameObject.AddComponent<Image>();
            bg.color = DashboardStyle.CardBg;
            LayoutElement root = gameObject.AddComponent<LayoutElement>();
            root.minHeight = 52f;
            root.flexibleWidth = 1f;

            _idText = AddCell(90, 0, DashboardStyle.SmallSize, true);
            _nameText = AddCell(0, 2, DashboardStyle.BodySize, true);
            _categoryText = AddCell(140, 0, DashboardStyle.SmallSize, false);
            _locationText = AddCell(140, 0, DashboardStyle.SmallSize, false);

            GameObject badgeCell = new GameObject("StatusCell", typeof(RectTransform));
            badgeCell.transform.SetParent(transform, false);
            LayoutElement bl = badgeCell.AddComponent<LayoutElement>();
            bl.minWidth = 140;
            bl.preferredWidth = 140;
            _statusBadge = DashboardStyle.MakeBadge(badgeCell.transform, "", DashboardStyle.Success);

            _nextText = AddCell(120, 0, DashboardStyle.SmallSize, false);

            GameObject actions = new GameObject("Actions", typeof(RectTransform));
            actions.transform.SetParent(transform, false);
            HorizontalLayoutGroup ah = actions.AddComponent<HorizontalLayoutGroup>();
            ah.spacing = 6;
            ah.childAlignment = TextAnchor.MiddleCenter;
            ah.childControlWidth = true;
            ah.childControlHeight = true;
            ah.childForceExpandWidth = false;
            ah.childForceExpandHeight = false;
            LayoutElement al = actions.AddComponent<LayoutElement>();
            al.minWidth = 216;
            al.preferredWidth = 216;
            actions.name = "Actions";

            AddAction(actions.transform, "View", DashboardStyle.Primary, () => Raise(_onView));
            AddAction(actions.transform, "Edit", DashboardStyle.Neutral, () => Raise(_onEdit));
            AddAction(actions.transform, "Delete", DashboardStyle.Danger, () => Raise(_onDelete));
        }

        private Action<DashboardAsset> _onView;
        private Action<DashboardAsset> _onEdit;
        private Action<DashboardAsset> _onDelete;

        private void Raise(Action<DashboardAsset> handler)
        {
            if (handler != null && _asset != null)
            {
                try { handler(_asset); }
                catch (Exception ex) { Debug.LogWarning("[Dashboard] Row action failed: " + ex.Message); }
            }
        }

        private Text AddCell(float width, float flex, int size, bool bold)
        {
            GameObject go = new GameObject("Cell", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            Text t = DashboardStyle.MakeText(go.transform, "", size, DashboardStyle.Text, bold, TextAnchor.MiddleLeft, "CellText");
            DashboardStyle.Stretch(t.rectTransform);
            LayoutElement le = go.AddComponent<LayoutElement>();
            if (flex > 0)
            {
                le.flexibleWidth = flex;
                le.minWidth = 80;
            }
            else
            {
                le.minWidth = width;
                le.preferredWidth = width;
            }
            le.minHeight = 30;
            return t;
        }

        private void AddAction(Transform parent, string label, Color bg, Action onClick)
        {
            Button b = DashboardStyle.MakeButton(parent, label, bg, () => { if (onClick != null) onClick(); }, 13, 36f, label + "Button");
            LayoutElement le = b.GetComponent<LayoutElement>();
            le.preferredWidth = 66;
            le.flexibleWidth = 0;
        }

        public void Bind(DashboardAsset asset, Action<DashboardAsset> onView, Action<DashboardAsset> onEdit, Action<DashboardAsset> onDelete)
        {
            Build();
            _asset = asset;
            _onView = onView;
            _onEdit = onEdit;
            _onDelete = onDelete;
            if (asset == null) return;
            _idText.text = DashboardUIHelper.Safe(asset.id);
            _nameText.text = DashboardUIHelper.Truncate(DashboardUIHelper.Safe(asset.name), 40);
            _categoryText.text = DashboardUIHelper.Truncate(DashboardUIHelper.Safe(asset.category), 22);
            _locationText.text = DashboardUIHelper.Truncate(DashboardUIHelper.Safe(asset.location), 22);
            DashboardStyle.SetBadge(_statusBadge,
                DashboardAsset.DisplayStatus(asset.status),
                DashboardStyle.AssetStatusColor(asset.status));
            _nextText.text = string.IsNullOrEmpty(asset.nextMaintenance) ? "-" : asset.nextMaintenance;
        }
    }
}
