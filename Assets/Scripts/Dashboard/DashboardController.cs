using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Dashboard root: header (title, AR Scanner navigation, refresh), summary
    /// cards, Assets/Issues/Stats tabs, loading overlay, error banner with retry
    /// + offline sample-data option, and a status footer. Builds the whole UGUI
    /// tree in code so the scene has no fragile serialized references.
    /// </summary>
    public class DashboardController : MonoBehaviour
    {
        public const string ArScannerSceneName = "ARScanner";

        private List<DashboardAsset> _assets = new List<DashboardAsset>();
        private List<DashboardIssue> _issues = new List<DashboardIssue>();
        private bool _built;
        private bool _loading;

        private Transform _canvas;
        private GameObject _assetsTab;
        private GameObject _issuesTab;
        private GameObject _statsTab;
        private AssetListController _assetList;
        private IssueManagerUI _issueManager;
        private StatsPanel _statsPanel;
        private AssetDetailPanel _detailPanel;
        private AssetFormPanel _formPanel;

        private Text _totalValue;
        private Text _operationalValue;
        private Text _maintenanceValue;
        private Text _issuesValue;
        private Button _tabAssetsBtn;
        private Button _tabIssuesBtn;
        private Button _tabStatsBtn;
        private GameObject _loadingOverlay;
        private GameObject _errorBanner;
        private Text _errorText;
        private Text _modeText;
        private Text _messageText;

        private void Awake()
        {
            Build();
            RefreshAll();
        }

        // ------------------------------------------------------------------ //
        // Construction                                                        //
        // ------------------------------------------------------------------ //

        private void Build()
        {
            if (_built) return;
            _built = true;

            EnsureCanvasAndEventSystem();

            GameObject root = new GameObject("DashboardRoot", typeof(RectTransform));
            root.transform.SetParent(_canvas, false);
            RectTransform rrt = root.GetComponent<RectTransform>();
            DashboardStyle.Stretch(rrt);
            Image rootBg = root.AddComponent<Image>();
            rootBg.color = DashboardStyle.Bg;
            VerticalLayoutGroup layout = root.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12;
            layout.padding = new RectOffset(16, 16, 16, 12);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            BuildHeader(root.transform);
            BuildSummaryCards(root.transform);
            BuildTabBar(root.transform);
            BuildContent(root.transform);
            BuildErrorBanner(root.transform);
            BuildStatusBar(root.transform);
            BuildLoadingOverlay();
            ShowTab(0);
        }

        private void EnsureCanvasAndEventSystem()
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                canvas = FindObjectOfType<Canvas>();
            }
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("DashboardCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.pixelPerfect = false;
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 800);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            _canvas = canvas.transform;

            EventSystem es = FindObjectOfType<EventSystem>();
            if (es == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }
        }

        private void BuildHeader(Transform parent)
        {
            GameObject header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(parent, false);
            Image bg = header.AddComponent<Image>();
            bg.color = DashboardStyle.HeaderBg;
            HorizontalLayoutGroup h = header.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.padding = new RectOffset(16, 16, 10, 10);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            LayoutElement le = header.AddComponent<LayoutElement>();
            le.minHeight = 64;
            le.flexibleWidth = 1f;

            Text title = DashboardStyle.MakeText(header.transform, "AR Digital Passport  -  Asset Dashboard",
                DashboardStyle.TitleSize, DashboardStyle.HeaderText, true, TextAnchor.MiddleLeft, "Title");
            title.gameObject.AddComponent<LayoutElement>().flexibleWidth = 3f;

            DashboardStyle.MakeButton(header.transform, "Open AR Scanner", DashboardStyle.Primary,
                () => OpenArScanner(), 15, 44f, "ArButton").GetComponent<LayoutElement>().preferredWidth = 190;
            DashboardStyle.MakeButton(header.transform, "Refresh", DashboardStyle.Neutral,
                () => RefreshAll(), 15, 44f, "RefreshButton").GetComponent<LayoutElement>().preferredWidth = 130;
        }

        private void BuildSummaryCards(Transform parent)
        {
            GameObject row = DashboardStyle.Row(parent, "SummaryRow", 12);
            row.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            _totalValue = AddSummaryCard(row.transform, "Total Assets");
            _operationalValue = AddSummaryCard(row.transform, "Operational");
            _maintenanceValue = AddSummaryCard(row.transform, "Under Maintenance");
            _issuesValue = AddSummaryCard(row.transform, "Issues Open");
        }

        private Text AddSummaryCard(Transform parent, string caption)
        {
            GameObject card = DashboardStyle.MakeCard(parent, "SummaryCard");
            LayoutElement le = card.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minWidth = 140;
            le.minHeight = 92;
            Text value = DashboardStyle.MakeText(card.transform, "-", 30,
                DashboardStyle.Text, true, TextAnchor.MiddleLeft, "CardValue");
            DashboardStyle.MakeText(card.transform, caption, DashboardStyle.SmallSize,
                DashboardStyle.Muted, false, TextAnchor.MiddleLeft, "CardCaption");
            return value;
        }

        private void BuildTabBar(Transform parent)
        {
            GameObject bar = DashboardStyle.Row(parent, "TabBar", 8);
            bar.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(0, 0, 0, 0);
            _tabAssetsBtn = AddTab(bar.transform, "Assets", () => ShowTab(0));
            _tabIssuesBtn = AddTab(bar.transform, "Issues", () => ShowTab(1));
            _tabStatsBtn = AddTab(bar.transform, "Stats", () => ShowTab(2));
        }

        private Button AddTab(Transform parent, string label, Action onClick)
        {
            Button b = DashboardStyle.MakeButton(parent, label, DashboardStyle.Border,
                () => { if (onClick != null) onClick(); }, 16, 48f, "Tab_" + label);
            b.GetComponent<LayoutElement>().flexibleWidth = 1f;
            return b;
        }

        private void BuildContent(Transform parent)
        {
            GameObject content = DashboardStyle.MakeCard(parent, "ContentPanel");
            LayoutElement le = content.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.flexibleHeight = 1f;
            le.minHeight = 300;

            _assetsTab = TabRoot(content.transform, "AssetsTab");
            _assetList = _assetsTab.AddComponent<AssetListController>();
            _assetList.Initialize(this);

            _issuesTab = TabRoot(content.transform, "IssuesTab");
            _issueManager = _issuesTab.AddComponent<IssueManagerUI>();
            _issueManager.Initialize(this);

            _statsTab = TabRoot(content.transform, "StatsTab");
            _statsPanel = _statsTab.AddComponent<StatsPanel>();
            _statsPanel.Initialize();

            GameObject detailHost = new GameObject("DetailHost");
            detailHost.transform.SetParent(_canvas, false);
            _detailPanel = detailHost.AddComponent<AssetDetailPanel>();
            _detailPanel.Initialize(this);

            GameObject formHost = new GameObject("FormHost");
            formHost.transform.SetParent(_canvas, false);
            _formPanel = formHost.AddComponent<AssetFormPanel>();
            _formPanel.Initialize(this);
        }

        private GameObject TabRoot(Transform parent, string name)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            DashboardStyle.Stretch(go.GetComponent<RectTransform>());
            return go;
        }

        private void BuildErrorBanner(Transform parent)
        {
            _errorBanner = new GameObject("ErrorBanner", typeof(RectTransform));
            _errorBanner.transform.SetParent(parent, false);
            Image bg = _errorBanner.AddComponent<Image>();
            bg.color = DashboardStyle.ErrorBg;
            HorizontalLayoutGroup h = _errorBanner.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 12;
            h.padding = new RectOffset(12, 12, 8, 8);
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            LayoutElement le = _errorBanner.AddComponent<LayoutElement>();
            le.flexibleWidth = 1f;
            le.minHeight = 56;

            _errorText = DashboardStyle.MakeText(_errorBanner.transform, "",
                DashboardStyle.BodySize, DashboardStyle.ErrorText, true, TextAnchor.MiddleLeft, "ErrorText");
            _errorText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 3f;
            DashboardStyle.MakeButton(_errorBanner.transform, "Retry", DashboardStyle.Danger,
                () => RefreshAll(), 14, 40f, "RetryButton").GetComponent<LayoutElement>().preferredWidth = 110;
            DashboardStyle.MakeButton(_errorBanner.transform, "Use sample data", DashboardStyle.Neutral,
                () => LoadSampleData(), 14, 40f, "SampleButton").GetComponent<LayoutElement>().preferredWidth = 170;
            _errorBanner.SetActive(false);
        }

        private void BuildStatusBar(Transform parent)
        {
            GameObject bar = DashboardStyle.Row(parent, "StatusBar", 12);
            bar.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(4, 4, 0, 0);
            _modeText = DashboardStyle.MakeText(bar.transform, "", DashboardStyle.SmallSize,
                DashboardStyle.Muted, true, TextAnchor.MiddleLeft, "ModeText");
            _messageText = DashboardStyle.MakeText(bar.transform, "", DashboardStyle.SmallSize,
                DashboardStyle.Muted, false, TextAnchor.MiddleLeft, "StatusMessage");
            _messageText.gameObject.AddComponent<LayoutElement>().flexibleWidth = 2f;
        }

        private void BuildLoadingOverlay()
        {
            _loadingOverlay = new GameObject("LoadingOverlay", typeof(RectTransform));
            _loadingOverlay.transform.SetParent(_canvas, false);
            DashboardStyle.Stretch(_loadingOverlay.GetComponent<RectTransform>());
            Image dim = _loadingOverlay.AddComponent<Image>();
            dim.color = new Color(1, 1, 1, 0.65f);
            DashboardStyle.MakeText(_loadingOverlay.transform, "Loading, please wait...",
                DashboardStyle.SectionSize, DashboardStyle.Text, true, TextAnchor.MiddleCenter, "LoadingText")
                .rectTransform.anchorMin = Vector2.zero;
            _loadingOverlay.GetComponentInChildren<Text>().rectTransform.anchorMax = Vector2.one;
            _loadingOverlay.GetComponentInChildren<Text>().rectTransform.offsetMin = Vector2.zero;
            _loadingOverlay.GetComponentInChildren<Text>().rectTransform.offsetMax = Vector2.zero;
            _loadingOverlay.SetActive(false);
        }

        // ------------------------------------------------------------------ //
        // Tabs                                                                //
        // ------------------------------------------------------------------ //

        public void ShowTab(int index)
        {
            if (!_built) return;
            _assetsTab.SetActive(index == 0);
            _issuesTab.SetActive(index == 1);
            _statsTab.SetActive(index == 2);
            TintTab(_tabAssetsBtn, index == 0);
            TintTab(_tabIssuesBtn, index == 1);
            TintTab(_tabStatsBtn, index == 2);
        }

        private void TintTab(Button b, bool active)
        {
            if (b == null) return;
            Image img = b.GetComponent<Image>();
            if (img != null) img.color = active ? DashboardStyle.Primary : DashboardStyle.Border;
            Text t = b.GetComponentInChildren<Text>();
            if (t != null) t.color = active ? Color.white : DashboardStyle.Text;
        }

        // ------------------------------------------------------------------ //
        // Data                                                                //
        // ------------------------------------------------------------------ //

        public void RefreshAll()
        {
            if (!_built || _loading) return;
            _loading = true;
            SetLoading(true);
            HideError();
            SetMessage("Loading assets...");
            DashboardApiAdapter.FetchAssets(
                assets =>
                {
                    _assets = assets ?? new List<DashboardAsset>();
                    DashboardApiAdapter.FetchIssues(
                        issues =>
                        {
                            _loading = false;
                            SetLoading(false);
                            _issues = issues ?? new List<DashboardIssue>();
                            UpdateAll(false);
                            SetMessage("Updated just now.");
                        },
                        msg =>
                        {
                            _loading = false;
                            SetLoading(false);
                            _issues = new List<DashboardIssue>();
                            UpdateAll(false);
                            ShowError(msg + " Asset list was loaded; issues are unavailable.");
                        });
                },
                msg =>
                {
                    _loading = false;
                    SetLoading(false);
                    ShowError(msg);
                });
        }

        private void LoadSampleData()
        {
            if (_loading) return;
            _loading = true;
            SetLoading(true);
            HideError();
            DashboardApiAdapter.FetchSeedBundle(
                (assets, issues, maintenance) =>
                {
                    _loading = false;
                    SetLoading(false);
                    _assets = assets ?? new List<DashboardAsset>();
                    _issues = issues ?? new List<DashboardIssue>();
                    UpdateAll(true);
                    SetMessage("Showing offline sample data.");
                    Toast("Offline sample data loaded.");
                },
                msg =>
                {
                    _loading = false;
                    SetLoading(false);
                    ShowError(msg);
                });
        }

        private void UpdateAll(bool forceOfflineLabel)
        {
            int operational = 0;
            int maintenance = 0;
            foreach (var a in _assets)
            {
                if (a == null) continue;
                string s = DashboardAsset.NormalizeStatus(a.status);
                if (s == DashboardAsset.StatusOperational) operational++;
                else if (s == DashboardAsset.StatusMaintenance) maintenance++;
            }
            int openIssues = 0;
            foreach (var i in _issues)
            {
                if (i == null) continue;
                if (DashboardIssue.NormalizeIssueStatus(i.status) == DashboardIssue.StatusOpen) openIssues++;
            }
            _totalValue.text = _assets.Count.ToString();
            _operationalValue.text = operational.ToString();
            _maintenanceValue.text = maintenance.ToString();
            _issuesValue.text = openIssues.ToString();

            if (_assetList != null) _assetList.SetData(_assets);
            if (_issueManager != null) _issueManager.SetData(_issues, _assets);
            if (_statsPanel != null) _statsPanel.SetData(_assets, _issues);

            string reason;
            bool online = !forceOfflineLabel && !DashboardApiAdapter.LastUsedSeed
                && DashboardApiAdapter.IsBackendAvailable(out reason);
            if (online)
            {
                _modeText.text = "Online";
                _modeText.color = DashboardStyle.Success;
            }
            else
            {
                _modeText.text = "Offline sample data";
                _modeText.color = DashboardStyle.Warning;
            }
        }

        // ------------------------------------------------------------------ //
        // Navigation / panels / feedback                                      //
        // ------------------------------------------------------------------ //

        public void OpenArScanner()
        {
            // Prefer DEV4's SceneNavigator when present (canonical scene names +
            // loading screen); fall back to a direct SceneManager load.
            try
            {
                Type navType = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        navType = asm.GetType("ArPassport.Utils.SceneNavigator", false, false);
                        if (navType != null) break;
                    }
                    catch
                    {
                        // Keep scanning other assemblies.
                    }
                }
                if (navType != null)
                {
                    var m = navType.GetMethod("LoadARScanner",
                        System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (m != null)
                    {
                        m.Invoke(null, new object[] { null });
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] SceneNavigator lookup failed: " + ex.Message);
            }
            try
            {
                if (Application.CanStreamedLevelBeLoaded(ArScannerSceneName))
                {
                    SceneManager.LoadScene(ArScannerSceneName);
                }
                else
                {
                    Toast("AR Scanner scene is not in this build yet.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[Dashboard] Scene navigation failed: " + ex.Message);
                Toast("AR Scanner scene could not be opened.");
            }
        }

        public void ShowDetail(DashboardAsset asset)
        {
            if (asset == null || _detailPanel == null) return;
            if (_formPanel != null) _formPanel.Hide();
            ShowTab(0);
            _detailPanel.Show(asset, _issues);
        }

        public void ShowCreateForm()
        {
            if (_formPanel == null) return;
            if (_detailPanel != null) _detailPanel.Hide();
            _formPanel.ShowForCreate(() => RefreshAll());
        }

        public void ShowEditForm(DashboardAsset asset)
        {
            if (asset == null || _formPanel == null) return;
            if (_detailPanel != null) _detailPanel.Hide();
            _formPanel.ShowForEdit(asset, () => RefreshAll());
        }

        public void RequestDelete(DashboardAsset asset)
        {
            if (asset == null) return;
            DashboardDialog.ShowConfirm(_canvas,
                "Delete " + asset.id + "?",
                "This permanently removes '" + asset.id + "' and cannot be undone.",
                "Delete",
                () =>
                {
                    DashboardApiAdapter.DeleteAsset(asset.id,
                        () =>
                        {
                            if (_detailPanel != null) _detailPanel.Hide();
                            Toast("Asset " + asset.id + " deleted.");
                            RefreshAll();
                        },
                        msg => ShowError(msg));
                });
        }

        public void Toast(string message)
        {
            DashboardUIHelper.ShowToast(this, _canvas, message);
        }

        public void ShowError(string message)
        {
            if (!_built) return;
            _errorText.text = string.IsNullOrEmpty(message)
                ? "Something went wrong. Please try again."
                : message;
            _errorBanner.SetActive(true);
        }

        public void HideError()
        {
            if (_errorBanner != null) _errorBanner.SetActive(false);
        }

        private void SetLoading(bool on)
        {
            if (_loadingOverlay != null) _loadingOverlay.SetActive(on);
        }

        private void SetMessage(string msg)
        {
            if (_messageText != null) _messageText.text = msg ?? "";
        }
    }
}
