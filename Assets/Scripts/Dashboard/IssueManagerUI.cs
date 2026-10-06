using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Issues tab: filterable list on the left, detail with status changer on
    /// the right. Status transitions (open / in_progress / resolved / rejected)
    /// go through DashboardApiAdapter.UpdateIssueStatus.
    /// </summary>
    public class IssueManagerUI : MonoBehaviour
    {
        private const string AllIssues = "All Issues";

        private DashboardController _controller;
        private List<DashboardIssue> _all = new List<DashboardIssue>();
        private List<DashboardAsset> _assets = new List<DashboardAsset>();
        private DashboardIssue _selected;
        private bool _built;

        private Dropdown _filter;
        private RectTransform _listContent;
        private Text _emptyText;
        private Text _detailTitle;
        private GameObject _detailBadge;
        private Text _detailMeta;
        private Text _detailDesc;
        private Dropdown _statusDropdown;
        private GameObject _detailRoot;

        private readonly List<string> _filterValues = new List<string> { AllIssues };
        private readonly List<string> _statusValues = new List<string>
        {
            DashboardIssue.StatusOpen,
            DashboardIssue.StatusInProgress,
            DashboardIssue.StatusResolved,
            DashboardIssue.StatusRejected
        };

        public void Initialize(DashboardController controller)
        {
            _controller = controller;
            Build();
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            GameObject col = DashboardStyle.Column(transform, "IssueRoot", 10);
            DashboardStyle.Stretch(col.GetComponent<RectTransform>());

            GameObject toolbar = DashboardStyle.Row(col.transform, "IssueToolbar", 10);
            DashboardStyle.MakeText(toolbar.transform, "Filter:", DashboardStyle.BodySize,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "FilterLabel");
            List<string> filterLabels = new List<string> { AllIssues };
            foreach (string s in _statusValues) filterLabels.Add(DashboardIssue.DisplayStatus(s));
            _filter = DashboardUIHelper.CreateDropdown(toolbar.transform, "IssueFilter", filterLabels,
                idx => RenderList());
            DashboardStyle.Spacer(toolbar.transform, 1);

            GameObject split = DashboardStyle.Row(col.transform, "IssueSplit", 12);
            split.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            split.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            LayoutElement splitLe = split.AddComponent<LayoutElement>();
            splitLe.flexibleHeight = 1f;

            GameObject listPanel = new GameObject("IssueListPanel", typeof(RectTransform));
            listPanel.transform.SetParent(split.transform, false);
            LayoutElement listLe = listPanel.AddComponent<LayoutElement>();
            listLe.flexibleWidth = 1.1f;
            listLe.flexibleHeight = 1f;
            VerticalLayoutGroup listLayout = listPanel.AddComponent<VerticalLayoutGroup>();
            listLayout.spacing = 6;
            listLayout.childControlWidth = true;
            listLayout.childControlHeight = true;
            listLayout.childForceExpandWidth = true;
            listLayout.childForceExpandHeight = false;
            DashboardStyle.MakeText(listPanel.transform, "Reports", DashboardStyle.SectionSize,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "ListHeader");
            RectTransform content;
            DashboardUIHelper.CreateScrollView(listPanel.transform, "IssueScroll", out content);
            _listContent = content;
            _emptyText = DashboardStyle.MakeText(listPanel.transform, "No issues match this filter.",
                DashboardStyle.BodySize, DashboardStyle.Muted, false, TextAnchor.MiddleCenter, "IssueEmpty");
            _emptyText.gameObject.SetActive(false);

            _detailRoot = DashboardStyle.MakeCard(split.transform, "IssueDetail");
            LayoutElement detailLe = _detailRoot.AddComponent<LayoutElement>();
            detailLe.flexibleWidth = 1f;
            detailLe.flexibleHeight = 1f;
            GameObject dHeader = DashboardStyle.Row(_detailRoot.transform, "DetailHeader", 10);
            dHeader.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            _detailTitle = DashboardStyle.MakeText(dHeader.transform, "Select an issue",
                DashboardStyle.SectionSize, DashboardStyle.Text, true, TextAnchor.UpperLeft, "DetailTitle");
            _detailTitle.gameObject.AddComponent<LayoutElement>().flexibleWidth = 2f;
            _detailBadge = DashboardStyle.MakeBadge(dHeader.transform, "", DashboardStyle.Neutral);
            _detailMeta = DashboardStyle.MakeText(_detailRoot.transform, "",
                DashboardStyle.SmallSize, DashboardStyle.Muted, false, TextAnchor.UpperLeft, "DetailMeta");
            _detailDesc = DashboardStyle.MakeText(_detailRoot.transform, "",
                DashboardStyle.BodySize, DashboardStyle.Text, false, TextAnchor.UpperLeft, "DetailDesc");
            DashboardStyle.Divider(_detailRoot.transform);
            DashboardStyle.MakeText(_detailRoot.transform, "Change status", DashboardStyle.BodySize,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "StatusLabel");
            List<string> statusLabels = new List<string>();
            foreach (string s in _statusValues) statusLabels.Add(DashboardIssue.DisplayStatus(s));
            GameObject statusRow = DashboardStyle.Row(_detailRoot.transform, "StatusRow", 10);
            statusRow.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            _statusDropdown = DashboardUIHelper.CreateDropdown(statusRow.transform, "StatusDropdown", statusLabels, null);
            Button apply = DashboardStyle.MakeButton(statusRow.transform, "Apply", DashboardStyle.Primary,
                () => ApplyStatus(), 15, 44f, "ApplyButton");
            apply.GetComponent<LayoutElement>().preferredWidth = 120;
            DashboardStyle.Spacer(_detailRoot.transform, 1);
        }

        public void SetData(List<DashboardIssue> issues, List<DashboardAsset> assets)
        {
            Build();
            _all = issues != null ? new List<DashboardIssue>(issues) : new List<DashboardIssue>();
            _assets = assets != null ? assets : new List<DashboardAsset>();
            if (_selected != null)
            {
                DashboardIssue still = _all.Find(i => i != null && i.id == _selected.id);
                _selected = still;
            }
            RenderList();
            RenderDetail();
        }

        private string FilterValue()
        {
            try
            {
                if (_filter != null && _filter.value > 0 && _filter.value <= _statusValues.Count)
                {
                    return _statusValues[_filter.value - 1];
                }
            }
            catch
            {
                // Best effort.
            }
            return null;
        }

        private void RenderList()
        {
            if (!_built) return;
            string want = FilterValue();
            DashboardUIHelper.ClearChildren(_listContent);
            int shown = 0;
            foreach (var issue in _all)
            {
                if (issue == null) continue;
                if (want != null && DashboardIssue.NormalizeIssueStatus(issue.status) != want) continue;
                shown++;
                AddRow(issue);
            }
            _emptyText.gameObject.SetActive(shown == 0);
            DashboardUIHelper.Rebuild(_listContent);
        }

        private void AddRow(DashboardIssue issue)
        {
            GameObject go = new GameObject("IssueRow", typeof(RectTransform));
            go.transform.SetParent(_listContent, false);
            Image bg = go.AddComponent<Image>();
            bool isSel = _selected != null && _selected.id == issue.id;
            bg.color = isSel ? DashboardStyle.Border : DashboardStyle.CardBg;
            Button btn = go.AddComponent<Button>();
            btn.targetGraphic = bg;
            HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 10;
            h.padding = new RectOffset(12, 12, 8, 8);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            LayoutElement root = go.AddComponent<LayoutElement>();
            root.minHeight = 52f;
            root.flexibleWidth = 1f;

            DashboardIssue captured = issue;
            btn.onClick.AddListener(() =>
            {
                _selected = captured;
                RenderList();
                RenderDetail();
            });

            Text t = DashboardStyle.MakeText(go.transform,
                DashboardUIHelper.Truncate(issue.title, 50),
                DashboardStyle.BodySize, DashboardStyle.Text, true, TextAnchor.MiddleLeft, "RowTitle");
            t.gameObject.AddComponent<LayoutElement>().flexibleWidth = 2f;
            Text a = DashboardStyle.MakeText(go.transform, issue.assetId,
                DashboardStyle.SmallSize, DashboardStyle.Muted, false, TextAnchor.MiddleLeft, "RowAsset");
            a.gameObject.AddComponent<LayoutElement>().preferredWidth = 90;
            DashboardStyle.SetBadge(
                DashboardStyle.MakeBadge(go.transform, "", DashboardStyle.Neutral),
                DashboardIssue.DisplayStatus(issue.status),
                DashboardStyle.IssueStatusColor(issue.status));
        }

        private string AssetName(string assetId)
        {
            foreach (var a in _assets)
            {
                if (a != null && string.Equals(a.id, assetId, StringComparison.OrdinalIgnoreCase))
                {
                    return string.IsNullOrEmpty(a.name) ? a.id : a.name;
                }
            }
            return assetId;
        }

        private void RenderDetail()
        {
            if (!_built) return;
            if (_selected == null)
            {
                _detailTitle.text = "Select an issue";
                DashboardStyle.SetBadge(_detailBadge, "-", DashboardStyle.Neutral);
                _detailMeta.text = "";
                _detailDesc.text = "Choose a report on the left to see details and change its status.";
                _statusDropdown.interactable = false;
                return;
            }
            _statusDropdown.interactable = true;
            _detailTitle.text = _selected.title;
            DashboardStyle.SetBadge(_detailBadge,
                DashboardIssue.DisplayStatus(_selected.status),
                DashboardStyle.IssueStatusColor(_selected.status));
            string sev = string.IsNullOrEmpty(_selected.severity) ? "unspecified" : _selected.severity;
            _detailMeta.text = "Asset: " + AssetName(_selected.assetId) + " (" + _selected.assetId + ")"
                + "\nSeverity: " + sev
                + (string.IsNullOrEmpty(_selected.createdAt) ? "" : "\nReported: " + _selected.createdAt);
            _detailDesc.text = string.IsNullOrEmpty(_selected.description) ? "No description provided." : _selected.description;
            int idx = _statusValues.IndexOf(DashboardIssue.NormalizeIssueStatus(_selected.status));
            _statusDropdown.value = Mathf.Clamp(idx, 0, _statusValues.Count - 1);
            _statusDropdown.RefreshShownValue();
        }

        private void ApplyStatus()
        {
            if (_selected == null || _controller == null) return;
            string target = _statusValues[Mathf.Clamp(_statusDropdown.value, 0, _statusValues.Count - 1)];
            if (DashboardIssue.NormalizeIssueStatus(_selected.status) == target)
            {
                _controller.Toast("Issue is already " + DashboardIssue.DisplayStatus(target) + ".");
                return;
            }
            string capturedId = _selected.id;
            DashboardApiAdapter.UpdateIssueStatus(capturedId, target,
                () =>
                {
                    if (_controller != null)
                    {
                        _controller.Toast("Issue " + capturedId + " -> " + DashboardIssue.DisplayStatus(target) + ".");
                        _controller.RefreshAll();
                    }
                },
                msg =>
                {
                    if (_controller != null) _controller.ShowError(msg);
                });
        }
    }
}
