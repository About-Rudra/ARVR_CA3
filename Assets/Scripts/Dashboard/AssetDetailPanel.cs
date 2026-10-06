using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Overlay panel: full asset info (incl. marker ID with copy), maintenance
    /// history and this asset's issues, plus Edit / Delete / Close actions.
    /// </summary>
    public class AssetDetailPanel : MonoBehaviour
    {
        private DashboardController _controller;
        private GameObject _overlay;
        private Text _titleText;
        private GameObject _statusBadge;
        private RectTransform _content;
        private Text _loadingText;
        private DashboardAsset _asset;
        private bool _built;

        public void Initialize(DashboardController controller)
        {
            _controller = controller;
            Build();
            Hide();
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            _overlay = new GameObject("AssetDetailOverlay", typeof(RectTransform));
            _overlay.transform.SetParent(transform, false);
            DashboardStyle.Stretch(_overlay.GetComponent<RectTransform>());
            Image dim = _overlay.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.45f);

            GameObject card = new GameObject("DetailCard", typeof(RectTransform));
            card.transform.SetParent(_overlay.transform, false);
            Image cardImg = card.AddComponent<Image>();
            cardImg.color = DashboardStyle.CardBg;
            RectTransform crt = card.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.08f, 0.05f);
            crt.anchorMax = new Vector2(0.92f, 0.95f);
            crt.offsetMin = Vector2.zero;
            crt.offsetMax = Vector2.zero;
            VerticalLayoutGroup layout = card.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 8;
            layout.padding = new RectOffset(16, 16, 16, 16);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            GameObject header = DashboardStyle.Row(card.transform, "DetailHeader", 10);
            header.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            _titleText = DashboardStyle.MakeText(header.transform, "", DashboardStyle.TitleSize,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "DetailTitle");
            _titleText.GetComponent<RectTransform>().SetAsFirstSibling();
            LayoutElement tle = _titleText.gameObject.AddComponent<LayoutElement>();
            tle.flexibleWidth = 2f;
            _statusBadge = DashboardStyle.MakeBadge(header.transform, "", DashboardStyle.Success);
            Button close = DashboardStyle.MakeButton(header.transform, "X", DashboardStyle.Neutral,
                () => Hide(), 16, 44f, "CloseButton");
            close.GetComponent<LayoutElement>().preferredWidth = 52;

            _loadingText = DashboardStyle.MakeText(card.transform, "Loading details...",
                DashboardStyle.BodySize, DashboardStyle.Muted, false, TextAnchor.MiddleLeft, "DetailLoading");

            RectTransform content;
            DashboardUIHelper.CreateScrollView(card.transform, "DetailScroll", out content);
            _content = content;

            GameObject footer = DashboardStyle.Row(card.transform, "DetailFooter", 12);
            footer.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            DashboardStyle.Spacer(footer.transform, 1);
            DashboardStyle.MakeButton(footer.transform, "Edit", DashboardStyle.Primary,
                () =>
                {
                    DashboardAsset copy = _asset != null ? _asset.Clone() : null;
                    Hide();
                    if (_controller != null) _controller.ShowEditForm(copy);
                }, 15, 44f, "EditButton").GetComponent<LayoutElement>().preferredWidth = 120;
            DashboardStyle.MakeButton(footer.transform, "Delete", DashboardStyle.Danger,
                () =>
                {
                    if (_controller != null && _asset != null) _controller.RequestDelete(_asset);
                }, 15, 44f, "DeleteButton").GetComponent<LayoutElement>().preferredWidth = 120;
            Button closeBtn = DashboardStyle.MakeButton(footer.transform, "Close", DashboardStyle.Neutral,
                () => Hide(), 15, 44f, "CloseFooterButton");
            closeBtn.GetComponent<LayoutElement>().preferredWidth = 120;
        }

        public void Show(DashboardAsset asset, List<DashboardIssue> allIssues)
        {
            Build();
            if (asset == null) return;
            _asset = asset;
            _overlay.SetActive(true);
            _titleText.text = string.IsNullOrEmpty(asset.name) ? asset.id : asset.name + " (" + asset.id + ")";
            DashboardStyle.SetBadge(_statusBadge,
                DashboardAsset.DisplayStatus(asset.status),
                DashboardStyle.AssetStatusColor(asset.status));

            DashboardUIHelper.ClearChildren(_content);
            AddField("Asset ID", asset.id);
            AddField("Category", asset.category);
            AddField("Location", asset.location);
            AddField("Status", DashboardAsset.DisplayStatus(asset.status));
            AddMarkerField(asset.markerId);
            AddField("Description", string.IsNullOrEmpty(asset.description) ? "-" : asset.description);
            AddField("Last Maintenance", string.IsNullOrEmpty(asset.lastMaintenance) ? "-" : asset.lastMaintenance);
            AddField("Next Maintenance", string.IsNullOrEmpty(asset.nextMaintenance) ? "-" : asset.nextMaintenance);

            // Issues for this asset (already available locally).
            AddSection("Issues for this asset");
            int issueCount = 0;
            if (allIssues != null)
            {
                foreach (var issue in allIssues)
                {
                    if (issue == null) continue;
                    if (!string.Equals(issue.assetId, asset.id, StringComparison.OrdinalIgnoreCase)) continue;
                    issueCount++;
                    AddIssueRow(issue);
                }
            }
            if (issueCount == 0)
            {
                AddMuted("No issues reported for this asset.");
            }

            // Maintenance history loads async (backend or seed).
            AddSection("Maintenance history");
            _loadingText.text = "Loading maintenance history...";
            _loadingText.gameObject.SetActive(true);
            DashboardUIHelper.Rebuild(_content);

            DashboardApiAdapter.FetchMaintenance(asset.id,
                records =>
                {
                    if (_overlay == null || !_overlay.activeSelf) return;
                    _loadingText.gameObject.SetActive(false);
                    if (records == null || records.Count == 0)
                    {
                        AddMuted("No maintenance records found.");
                    }
                    else
                    {
                        records.Sort((x, y) => string.Compare(y.date, x.date, StringComparison.Ordinal));
                        foreach (var r in records)
                        {
                            AddMaintenanceRow(r);
                        }
                    }
                    DashboardUIHelper.Rebuild(_content);
                },
                msg =>
                {
                    if (_overlay == null || !_overlay.activeSelf) return;
                    _loadingText.text = "Maintenance history is unavailable right now.";
                });
        }

        public void Hide()
        {
            if (_overlay != null) _overlay.SetActive(false);
        }

        public bool IsVisible()
        {
            return _overlay != null && _overlay.activeSelf;
        }

        private void AddSection(string title)
        {
            DashboardStyle.Divider(_content.transform);
            DashboardStyle.MakeText(_content.transform, title, DashboardStyle.SectionSize,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "Section");
        }

        private void AddField(string label, string value)
        {
            GameObject row = DashboardStyle.Row(_content.transform, "Field", 10);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            Text l = DashboardStyle.MakeText(row.transform, label, DashboardStyle.SmallSize,
                DashboardStyle.Muted, true, TextAnchor.UpperLeft, "FieldLabel");
            l.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            DashboardStyle.MakeText(row.transform, string.IsNullOrEmpty(value) ? "-" : value,
                DashboardStyle.BodySize, DashboardStyle.Text, false, TextAnchor.UpperLeft, "FieldValue");
        }

        private void AddMarkerField(string markerId)
        {
            GameObject row = DashboardStyle.Row(_content.transform, "MarkerField", 10);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            Text l = DashboardStyle.MakeText(row.transform, "Marker ID", DashboardStyle.SmallSize,
                DashboardStyle.Muted, true, TextAnchor.MiddleLeft, "FieldLabel");
            l.gameObject.AddComponent<LayoutElement>().preferredWidth = 150;
            Text v = DashboardStyle.MakeText(row.transform, string.IsNullOrEmpty(markerId) ? "-" : markerId,
                DashboardStyle.BodySize, DashboardStyle.Text, true, TextAnchor.MiddleLeft, "FieldValue");
            v.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
            if (!string.IsNullOrEmpty(markerId))
            {
                Button copy = DashboardStyle.MakeButton(row.transform, "Copy", DashboardStyle.Info,
                    () =>
                    {
                        try
                        {
                            GUIUtility.systemCopyBuffer = markerId;
                            if (_controller != null) _controller.Toast("Marker ID copied.");
                        }
                        catch (Exception ex)
                        {
                            Debug.LogWarning("[Dashboard] Copy failed: " + ex.Message);
                            if (_controller != null) _controller.Toast("Copy is not available on this device.");
                        }
                    }, 13, 36f, "CopyButton");
                copy.GetComponent<LayoutElement>().preferredWidth = 90;
            }
        }

        private void AddMuted(string text)
        {
            DashboardStyle.MakeText(_content.transform, text, DashboardStyle.BodySize,
                DashboardStyle.Muted, false, TextAnchor.MiddleLeft, "Muted");
        }

        private void AddMaintenanceRow(DashboardMaintenanceRecord r)
        {
            GameObject row = DashboardStyle.Row(_content.transform, "MaintRow", 10);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            Text t = DashboardStyle.MakeText(row.transform,
                (string.IsNullOrEmpty(r.date) ? "Undated" : r.date) + "  -  " +
                (string.IsNullOrEmpty(r.type) ? "Service" : r.type),
                DashboardStyle.BodySize, DashboardStyle.Text, true, TextAnchor.UpperLeft, "MaintTitle");
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 2f;
            string tech = string.IsNullOrEmpty(r.technician) ? "" : " (" + r.technician + ")";
            string notes = string.IsNullOrEmpty(r.notes) ? "" : ": " + r.notes;
            DashboardStyle.MakeText(row.transform, tech + notes, DashboardStyle.SmallSize,
                DashboardStyle.Muted, false, TextAnchor.UpperLeft, "MaintNotes");
        }

        private void AddIssueRow(DashboardIssue issue)
        {
            GameObject row = DashboardStyle.Row(_content.transform, "IssueRow", 10);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            Text t = DashboardStyle.MakeText(row.transform,
                DashboardUIHelper.Truncate(issue.title, 60),
                DashboardStyle.BodySize, DashboardStyle.Text, false, TextAnchor.MiddleLeft, "IssueTitle");
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 2f;
            DashboardStyle.SetBadge(
                DashboardStyle.MakeBadge(row.transform, "", DashboardStyle.Neutral),
                DashboardIssue.DisplayStatus(issue.status),
                DashboardStyle.IssueStatusColor(issue.status));
        }
    }
}
