using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Stats tab: counts by asset status / asset category / issue status,
    /// computed locally from the latest fetched lists. Pure layout bars.
    /// </summary>
    public class StatsPanel : MonoBehaviour
    {
        private bool _built;
        private RectTransform _content;
        private Text _totalText;

        public void Initialize()
        {
            Build();
        }

        private void Build()
        {
            if (_built) return;
            _built = true;

            GameObject col = DashboardStyle.Column(transform, "StatsRoot", 10);
            DashboardStyle.Stretch(col.GetComponent<RectTransform>());

            GameObject header = DashboardStyle.MakeCard(col.transform, "StatsHeader");
            _totalText = DashboardStyle.MakeText(header.transform, "No data yet.",
                DashboardStyle.SectionSize, DashboardStyle.Text, true, TextAnchor.MiddleLeft, "TotalText");

            RectTransform content;
            DashboardUIHelper.CreateScrollView(col.transform, "StatsScroll", out content);
            _content = content;
        }

        public void SetData(List<DashboardAsset> assets, List<DashboardIssue> issues)
        {
            Build();
            DashboardUIHelper.ClearChildren(_content);
            List<DashboardAsset> a = assets ?? new List<DashboardAsset>();
            List<DashboardIssue> issuesList = issues ?? new List<DashboardIssue>();

            _totalText.text = a.Count + " assets  |  " + issuesList.Count + " issues reported";

            AddSection("Assets by status");
            Dictionary<string, int> byStatus = CountBy(a, x => DashboardAsset.NormalizeStatus(x.status));
            int maxStatus = MaxOf(byStatus);
            AddBarRow(DashboardAsset.DisplayStatus(DashboardAsset.StatusOperational),
                GetCount(byStatus, DashboardAsset.StatusOperational), maxStatus,
                DashboardStyle.AssetStatusColor(DashboardAsset.StatusOperational));
            AddBarRow(DashboardAsset.DisplayStatus(DashboardAsset.StatusMaintenance),
                GetCount(byStatus, DashboardAsset.StatusMaintenance), maxStatus,
                DashboardStyle.AssetStatusColor(DashboardAsset.StatusMaintenance));
            AddBarRow(DashboardAsset.DisplayStatus(DashboardAsset.StatusOutOfService),
                GetCount(byStatus, DashboardAsset.StatusOutOfService), maxStatus,
                DashboardStyle.AssetStatusColor(DashboardAsset.StatusOutOfService));

            AddSection("Assets by category");
            Dictionary<string, int> byCat = CountBy(a, x => string.IsNullOrEmpty(x.category) ? "Uncategorized" : x.category);
            int maxCat = MaxOf(byCat);
            List<string> cats = new List<string>(byCat.Keys);
            cats.Sort(StringComparer.OrdinalIgnoreCase);
            if (cats.Count == 0)
            {
                AddMuted("No categories to show.");
            }
            foreach (string c in cats)
            {
                AddBarRow(c, byCat[c], maxCat, DashboardStyle.Primary);
            }

            AddSection("Issues by status");
            Dictionary<string, int> byIssue = CountBy(issuesList, x => DashboardIssue.NormalizeIssueStatus(x.status));
            int maxIssue = MaxOf(byIssue);
            if (maxIssue == 0)
            {
                AddMuted("No issues reported.");
            }
            else
            {
                foreach (string s in DashboardIssue.AllStatuses)
                {
                    AddBarRow(DashboardIssue.DisplayStatus(s), GetCount(byIssue, s), maxIssue,
                        DashboardStyle.IssueStatusColor(s));
                }
            }

            DashboardUIHelper.Rebuild(_content);
        }

        private Dictionary<string, int> CountBy(List<DashboardAsset> list, Func<DashboardAsset, string> key)
        {
            Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in list)
            {
                if (x == null) continue;
                string k = key(x) ?? "";
                if (!d.ContainsKey(k)) d[k] = 0;
                d[k]++;
            }
            return d;
        }

        private Dictionary<string, int> CountBy(List<DashboardIssue> list, Func<DashboardIssue, string> key)
        {
            Dictionary<string, int> d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var x in list)
            {
                if (x == null) continue;
                string k = key(x) ?? "";
                if (!d.ContainsKey(k)) d[k] = 0;
                d[k]++;
            }
            return d;
        }

        private int GetCount(Dictionary<string, int> d, string key)
        {
            int v;
            return d.TryGetValue(key, out v) ? v : 0;
        }

        private int MaxOf(Dictionary<string, int> d)
        {
            int m = 0;
            foreach (int v in d.Values)
            {
                if (v > m) m = v;
            }
            return m;
        }

        private void AddSection(string title)
        {
            DashboardStyle.MakeText(_content.transform, title, DashboardStyle.SectionSize,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "StatsSection");
        }

        private void AddMuted(string text)
        {
            DashboardStyle.MakeText(_content.transform, text, DashboardStyle.BodySize,
                DashboardStyle.Muted, false, TextAnchor.MiddleLeft, "StatsMuted");
        }

        private void AddBarRow(string label, int count, int max, Color color)
        {
            GameObject row = DashboardStyle.Row(_content.transform, "BarRow", 10);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            Text l = DashboardStyle.MakeText(row.transform, DashboardUIHelper.Truncate(label, 28),
                DashboardStyle.BodySize, DashboardStyle.Text, false, TextAnchor.MiddleLeft, "BarLabel");
            l.gameObject.AddComponent<LayoutElement>().preferredWidth = 180;

            GameObject barBg = new GameObject("BarBg", typeof(RectTransform));
            barBg.transform.SetParent(row.transform, false);
            Image bgImg = barBg.AddComponent<Image>();
            bgImg.color = DashboardStyle.Border;
            LayoutElement bgLe = barBg.AddComponent<LayoutElement>();
            bgLe.minHeight = 20;
            bgLe.flexibleWidth = 1f;
            HorizontalLayoutGroup barLayout = barBg.AddComponent<HorizontalLayoutGroup>();
            barLayout.spacing = 0;
            barLayout.padding = new RectOffset(0, 0, 0, 0);
            barLayout.childControlWidth = true;
            barLayout.childControlHeight = true;
            barLayout.childForceExpandWidth = false;
            barLayout.childForceExpandHeight = true;

            GameObject fill = new GameObject("BarFill", typeof(RectTransform));
            fill.transform.SetParent(barBg.transform, false);
            Image fillImg = fill.AddComponent<Image>();
            fillImg.color = color;
            LayoutElement fillLe = fill.AddComponent<LayoutElement>();
            fillLe.minHeight = 20;
            fillLe.flexibleWidth = Math.Max(count, 0);
            fillLe.minWidth = count > 0 ? 6 : 0;

            GameObject rest = new GameObject("BarRest", typeof(RectTransform));
            rest.transform.SetParent(barBg.transform, false);
            LayoutElement restLe = rest.AddComponent<LayoutElement>();
            restLe.minHeight = 20;
            restLe.flexibleWidth = Math.Max(max - count, 0);

            Text c = DashboardStyle.MakeText(row.transform, count.ToString(), DashboardStyle.BodySize,
                DashboardStyle.Text, true, TextAnchor.MiddleRight, "BarCount");
            c.gameObject.AddComponent<LayoutElement>().preferredWidth = 50;
        }
    }
}
