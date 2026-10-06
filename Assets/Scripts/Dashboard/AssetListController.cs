using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Assets tab: search box, status/category/location dropdown filters, add
    /// button, column header and a scrollable row list with empty state.
    /// All filtering happens locally on the latest fetched dataset.
    /// </summary>
    public class AssetListController : MonoBehaviour
    {
        private const string AllStatuses = "All Statuses";
        private const string AllCategories = "All Categories";
        private const string AllLocations = "All Locations";

        private DashboardController _controller;
        private List<DashboardAsset> _all = new List<DashboardAsset>();
        private bool _built;

        private InputField _search;
        private Dropdown _statusFilter;
        private Dropdown _categoryFilter;
        private Dropdown _locationFilter;
        private RectTransform _listContent;
        private Text _countText;
        private Text _emptyText;

        private readonly List<string> _statusOptions = new List<string>();
        private readonly List<string> _categoryOptions = new List<string>();
        private readonly List<string> _locationOptions = new List<string>();

        public void Initialize(DashboardController controller)
        {
            _controller = controller;
            Build();
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            GameObject col = DashboardStyle.Column(transform, "AssetListRoot", 10);
            DashboardStyle.Stretch(col.GetComponent<RectTransform>());

            // Toolbar: search + add.
            GameObject toolbar = DashboardStyle.Row(col.transform, "Toolbar", 10);
            _search = DashboardStyle.MakeInput(toolbar.transform, "Search id, name, category, location...", false, "SearchInput");
            _search.onValueChanged.AddListener(_ => ApplyFilter());
            Button add = DashboardStyle.MakeButton(toolbar.transform, "+ New Asset", DashboardStyle.Success,
                () => { if (_controller != null) _controller.ShowCreateForm(); }, 15, 44f, "AddButton");
            add.GetComponent<LayoutElement>().preferredWidth = 150;

            // Filter row.
            GameObject filters = DashboardStyle.Row(col.transform, "Filters", 10);
            _statusFilter = DashboardUIHelper.CreateDropdown(filters.transform, "StatusFilter",
                new List<string> { AllStatuses }, idx => ApplyFilter());
            _categoryFilter = DashboardUIHelper.CreateDropdown(filters.transform, "CategoryFilter",
                new List<string> { AllCategories }, idx => ApplyFilter());
            _locationFilter = DashboardUIHelper.CreateDropdown(filters.transform, "LocationFilter",
                new List<string> { AllLocations }, idx => ApplyFilter());

            _countText = DashboardStyle.MakeText(col.transform, "", DashboardStyle.SmallSize,
                DashboardStyle.Muted, false, TextAnchor.MiddleLeft, "CountText");

            // Column header.
            GameObject header = DashboardStyle.Row(col.transform, "Header", 10);
            Image headerBg = header.AddComponent<Image>();
            headerBg.color = DashboardStyle.Border;
            AddHeaderCell(header.transform, "ID", 90, 0);
            AddHeaderCell(header.transform, "Name", 0, 2);
            AddHeaderCell(header.transform, "Category", 140, 0);
            AddHeaderCell(header.transform, "Location", 140, 0);
            AddHeaderCell(header.transform, "Status", 140, 0);
            AddHeaderCell(header.transform, "Next Maint.", 120, 0);
            AddHeaderCell(header.transform, "Actions", 216, 0);

            // Scrollable rows.
            RectTransform content;
            DashboardUIHelper.CreateScrollView(col.transform, "AssetScroll", out content);
            _listContent = content;

            _emptyText = DashboardStyle.MakeText(col.transform, "No assets found. Try a different search or filter.",
                DashboardStyle.BodySize, DashboardStyle.Muted, false, TextAnchor.MiddleCenter, "EmptyText");
            _emptyText.gameObject.SetActive(false);
        }

        private void AddHeaderCell(Transform parent, string text, float width, float flex)
        {
            GameObject go = new GameObject("HeaderCell", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Text t = DashboardStyle.MakeText(go.transform, text, DashboardStyle.SmallSize,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "HeaderText");
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
            le.minHeight = 24;
        }

        public void SetData(List<DashboardAsset> assets)
        {
            Build();
            _all = assets != null ? new List<DashboardAsset>(assets) : new List<DashboardAsset>();
            RebuildFilterOptions();
            ApplyFilter();
        }

        private void RebuildFilterOptions()
        {
            _statusOptions.Clear();
            _statusOptions.Add(AllStatuses);
            _statusOptions.Add(DashboardAsset.DisplayStatus(DashboardAsset.StatusOperational));
            _statusOptions.Add(DashboardAsset.DisplayStatus(DashboardAsset.StatusMaintenance));
            _statusOptions.Add(DashboardAsset.DisplayStatus(DashboardAsset.StatusOutOfService));

            SortedSet<string> cats = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            SortedSet<string> locs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var a in _all)
            {
                if (a == null) continue;
                if (!string.IsNullOrEmpty(a.category)) cats.Add(a.category);
                if (!string.IsNullOrEmpty(a.location)) locs.Add(a.location);
            }
            _categoryOptions.Clear();
            _categoryOptions.Add(AllCategories);
            _categoryOptions.AddRange(cats);
            _locationOptions.Clear();
            _locationOptions.Add(AllLocations);
            _locationOptions.AddRange(locs);

            DashboardUIHelper.SetOptions(_statusFilter, _statusOptions);
            DashboardUIHelper.SetOptions(_categoryFilter, _categoryOptions);
            DashboardUIHelper.SetOptions(_locationFilter, _locationOptions);
        }

        private void ApplyFilter()
        {
            if (!_built) return;
            string query = _search != null ? _search.text.Trim().ToLowerInvariant() : "";
            string statusSel = Selected(_statusFilter, _statusOptions, AllStatuses);
            string catSel = Selected(_categoryFilter, _categoryOptions, AllCategories);
            string locSel = Selected(_locationFilter, _locationOptions, AllLocations);

            List<DashboardAsset> filtered = new List<DashboardAsset>();
            foreach (var a in _all)
            {
                if (a == null) continue;
                if (!string.IsNullOrEmpty(query))
                {
                    string hay = ((a.id ?? "") + " " + (a.name ?? "") + " " + (a.category ?? "") + " " + (a.location ?? "")).ToLowerInvariant();
                    if (!hay.Contains(query)) continue;
                }
                if (statusSel != AllStatuses &&
                    !string.Equals(DashboardAsset.DisplayStatus(a.status), statusSel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (catSel != AllCategories &&
                    !string.Equals(a.category ?? "", catSel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (locSel != AllLocations &&
                    !string.Equals(a.location ?? "", locSel, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                filtered.Add(a);
            }
            filtered.Sort((x, y) => string.Compare(x.id, y.id, StringComparison.OrdinalIgnoreCase));
            RenderRows(filtered);
        }

        private string Selected(Dropdown d, List<string> options, string fallback)
        {
            try
            {
                if (d != null && options != null && d.value >= 0 && d.value < options.Count)
                {
                    return options[d.value];
                }
            }
            catch
            {
                // Best effort.
            }
            return fallback;
        }

        private void RenderRows(List<DashboardAsset> rows)
        {
            DashboardUIHelper.ClearChildren(_listContent);
            bool alt = false;
            foreach (var a in rows)
            {
                GameObject rowGO = new GameObject("AssetRow", typeof(RectTransform));
                rowGO.transform.SetParent(_listContent, false);
                AssetListItemUI row = rowGO.AddComponent<AssetListItemUI>();
                row.Build();
                row.Bind(a, OnView, OnEdit, OnDelete);
                if (alt)
                {
                    Image bg = rowGO.GetComponent<Image>();
                    if (bg != null) bg.color = DashboardStyle.RowAlt;
                }
                alt = !alt;
            }
            _countText.text = rows.Count + " of " + _all.Count + " assets";
            _emptyText.gameObject.SetActive(rows.Count == 0);
            DashboardUIHelper.Rebuild(_listContent);
        }

        private void OnView(DashboardAsset a)
        {
            if (_controller != null) _controller.ShowDetail(a);
        }

        private void OnEdit(DashboardAsset a)
        {
            if (_controller != null) _controller.ShowEditForm(a);
        }

        private void OnDelete(DashboardAsset a)
        {
            if (_controller != null) _controller.RequestDelete(a);
        }
    }
}
