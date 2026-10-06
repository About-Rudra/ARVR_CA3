using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

//
// PassportPanelController.cs — bottom-sheet UGUI panel showing the scanned
// asset ("digital passport"). Built 100% programmatically: no prefabs, no
// inspector references, safe to instantiate from ARBootstrap at runtime.
//

namespace ArPassport.AR
{
    public class PassportPanelController : MonoBehaviour
    {
        [Header("Wiring (set by ARBootstrap)")]
        public ARMarkerTracker tracker;
        public MaintenanceHistoryUI historyUI;
        public IssueReporterUI reporterUI;

        // Runtime state
        private IAssetProvider _provider;
        private AssetDto _asset;
        private bool _detailsExpanded;

        // UI refs
        private GameObject _root;
        private GameObject _sheet;
        private Text _nameText, _idText, _categoryText, _locationText;
        private Image _badgeBg;
        private Text _badgeText;
        private Text _maintText;
        private GameObject _loadingRow;
        private RectTransform _spinner;
        private Text _loadingText;
        private Text _errorText;
        private GameObject _detailSection;
        private Text _descText;
        private Image _photoBox;
        private Text _photoPlaceholder;
        private Text _modelText;
        private Button _detailsButton;
        private Coroutine _spinnerRoutine;
        private Coroutine _photoRoutine;

        // ------------------------------------------------------------ setup

        /// <summary>Build the whole panel hierarchy under this GameObject's
        /// parent canvas. Safe to call once; subsequent calls are no-ops.</summary>
        public void BuildUI()
        {
            if (_root != null) return;
            _provider = tracker != null ? (IAssetProvider)tracker.Provider : null;
            BuildHierarchy();
            Subscribe();
            Hide();
        }

        /// <summary> Late-wire the provider (e.g. if tracker resolved later).</summary>
        public void SetProvider(IAssetProvider provider) { _provider = provider; }

        private void OnDestroy() { Unsubscribe(); }

        private void Subscribe()
        {
            if (tracker == null) return;
            tracker.OnAssetFound += Show;
            tracker.OnLookupStarted += ShowLoading;
            tracker.OnAssetNotFound += ShowNotFound;
            tracker.OnError += ShowError;
            tracker.OnAssetLost += Hide;
        }

        private void Unsubscribe()
        {
            if (tracker == null) return;
            tracker.OnAssetFound -= Show;
            tracker.OnLookupStarted -= ShowLoading;
            tracker.OnAssetNotFound -= ShowNotFound;
            tracker.OnError -= ShowError;
            tracker.OnAssetLost -= Hide;
        }

        // ------------------------------------------------------------ hierarchy

        private void BuildHierarchy()
        {
            _root = new GameObject("PassportPanel", typeof(RectTransform));
            _root.transform.SetParent(transform, false);
            var rootRt = _root.GetComponent<RectTransform>();
            ARUiKit.FillParent(rootRt, Vector2.zero, Vector2.zero);

            // Bottom sheet.
            _sheet = ARUiKit.Panel(_root, "Sheet", ARUiKit.BgDark);
            var sheetRt = _sheet.GetComponent<RectTransform>();
            sheetRt.anchorMin = new Vector2(0, 0);
            sheetRt.anchorMax = new Vector2(1, 0.62f);
            sheetRt.offsetMin = new Vector2(0, 0);
            sheetRt.offsetMax = new Vector2(0, 0);

            // Rounded-top look: thin accent bar.
            var bar = ARUiKit.Panel(_sheet, "AccentBar", ARUiKit.Accent);
            var barRt = bar.GetComponent<RectTransform>();
            barRt.anchorMin = new Vector2(0, 1); barRt.anchorMax = new Vector2(1, 1);
            barRt.offsetMin = new Vector2(0, -12); barRt.offsetMax = new Vector2(0, 0);

            // Scrollable content.
            var scrollGo = new GameObject("Scroll", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(ScrollRect));
            scrollGo.transform.SetParent(_sheet.transform, false);
            var scrollRt = scrollGo.GetComponent<RectTransform>();
            scrollRt.anchorMin = new Vector2(0, 0); scrollRt.anchorMax = Vector2.one;
            scrollRt.offsetMin = new Vector2(0, 8); scrollRt.offsetMax = new Vector2(0, -16);
            var scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(scrollGo.transform, false);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0, 1); contentRt.anchorMax = new Vector2(1, 1);
            contentRt.pivot = new Vector2(0.5f, 1);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0, 0);
            ARUiKit.Vertical(content, 28, 14);
            scroll.content = contentRt;

            // Header: name + id.
            _nameText = ARUiKit.Label(content, "Name", "—", 34, ARUiKit.TextLight, FontStyle.Bold);
            _idText = ARUiKit.Label(content, "AssetId", "—", 24, ARUiKit.TextDim);

            // Category / location row.
            _categoryText = ARUiKit.Label(content, "Category", "—", 26, ARUiKit.TextLight);
            _locationText = ARUiKit.Label(content, "Location", "—", 26, ARUiKit.TextLight);

            // Status badge.
            var badge = ARUiKit.Panel(content, "StatusBadge", ARUiKit.Grey);
            var badgeRt = badge.GetComponent<RectTransform>();
            badgeRt.sizeDelta = new Vector2(0, 64);
            var badgeLe = badge.AddComponent<LayoutElement>();
            badgeLe.minHeight = 64; badgeLe.preferredHeight = 64;
            _badgeBg = badge.GetComponent<Image>();
            _badgeText = ARUiKit.Label(badge, "StatusText", "UNKNOWN", 28, Color.white, FontStyle.Bold,
                TextAnchor.MiddleCenter);
            var btRt = _badgeText.rectTransform;
            btRt.anchorMin = Vector2.zero; btRt.anchorMax = Vector2.one;
            btRt.offsetMin = Vector2.zero; btRt.offsetMax = Vector2.zero;

            // Maintenance dates.
            _maintText = ARUiKit.Label(content, "Maintenance", "—", 24, ARUiKit.TextDim);

            // Loading row (spinner + text).
            _loadingRow = new GameObject("LoadingRow", typeof(RectTransform));
            _loadingRow.transform.SetParent(content.transform, false);
            var loadLe = _loadingRow.AddComponent<LayoutElement>();
            loadLe.minHeight = 72; loadLe.preferredHeight = 72;
            var spinBox = new GameObject("Spinner", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            spinBox.transform.SetParent(_loadingRow.transform, false);
            var spinImg = spinBox.GetComponent<Image>();
            spinImg.color = ARUiKit.Accent;
            _spinner = spinBox.GetComponent<RectTransform>();
            _spinner.anchorMin = new Vector2(0, 0.5f); _spinner.anchorMax = new Vector2(0, 0.5f);
            _spinner.pivot = new Vector2(0.5f, 0.5f);
            _spinner.anchoredPosition = new Vector2(48, 0);
            _spinner.sizeDelta = new Vector2(48, 48);
            _loadingText = ARUiKit.Label(_loadingRow, "LoadingText", "Loading…", 26, ARUiKit.TextLight);
            var ltRt = _loadingText.rectTransform;
            ltRt.anchorMin = new Vector2(0, 0); ltRt.anchorMax = new Vector2(1, 1);
            ltRt.offsetMin = new Vector2(120, 0); ltRt.offsetMax = Vector2.zero;

            // Error text.
            _errorText = ARUiKit.Label(content, "Error", string.Empty, 26, ARUiKit.Danger);

            // Detail section (collapsible).
            _detailSection = new GameObject("DetailSection", typeof(RectTransform));
            _detailSection.transform.SetParent(content.transform, false);
            ARUiKit.Vertical(_detailSection, 0, 12);
            _descText = ARUiKit.Label(_detailSection, "Description", string.Empty, 26, ARUiKit.TextLight);

            var photoGo = new GameObject("Photo", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            photoGo.transform.SetParent(_detailSection.transform, false);
            _photoBox = photoGo.GetComponent<Image>();
            _photoBox.color = new Color(1f, 1f, 1f, 0.08f);
            var photoRt = photoGo.GetComponent<RectTransform>();
            photoRt.sizeDelta = new Vector2(0, 320);
            var photoLe = photoGo.AddComponent<LayoutElement>();
            photoLe.minHeight = 320; photoLe.preferredHeight = 320;
            _photoPlaceholder = ARUiKit.Label(photoGo, "PhotoPlaceholder",
                "No photo available", 26, ARUiKit.TextDim, FontStyle.Italic, TextAnchor.MiddleCenter);
            var ppRt = _photoPlaceholder.rectTransform;
            ppRt.anchorMin = Vector2.zero; ppRt.anchorMax = Vector2.one;
            ppRt.offsetMin = Vector2.zero; ppRt.offsetMax = Vector2.zero;

            _modelText = ARUiKit.Label(_detailSection, "Model", string.Empty, 24, ARUiKit.TextDim);

            // Button grid 2x2.
            var gridGo = new GameObject("Buttons", typeof(RectTransform), typeof(GridLayoutGroup));
            gridGo.transform.SetParent(content.transform, false);
            var grid = gridGo.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(440, 110);
            grid.spacing = 20f;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 2;
            grid.childAlignment = TextAnchor.MiddleCenter;
            var gridLe = gridGo.AddComponent<LayoutElement>();
            gridLe.minHeight = 260; gridLe.preferredHeight = 260;

            _detailsButton = ARUiKit.Button(gridGo, "BtnDetails", "View Details",
                ToggleDetails, ARUiKit.Card);
            ARUiKit.Button(gridGo, "BtnHistory", "Maintenance\nHistory",
                OpenHistory, ARUiKit.Card);
            ARUiKit.Button(gridGo, "BtnIssue", "Report Issue",
                OpenReporter, new Color(0.75f, 0.45f, 0.12f, 1f));
            ARUiKit.Button(gridGo, "BtnClose", "Close",
                () => { if (tracker != null) tracker.ClearCurrent(); Hide(); },
                ARUiKit.Grey);
        }

        // ------------------------------------------------------------ states

        public void Show(AssetDto asset)
        {
            if (_root == null) BuildUI();
            StopSpinner();
            _asset = asset;
            _root.SetActive(true);
            _loadingRow.SetActive(false);
            _errorText.text = string.Empty;

            _nameText.text = ARUiKit.Safe(asset.name, "Unnamed asset");
            _idText.text = "ID: " + ARUiKit.Safe(asset.id) + "   •   Marker: " + ARUiKit.Safe(asset.markerId);
            _categoryText.text = "Category:  " + ARUiKit.Safe(asset.category);
            _locationText.text = "Location:  " + ARUiKit.Safe(asset.location);

            _badgeBg.color = ARUiKit.StatusColor(asset.status);
            _badgeText.text = ARUiKit.StatusText(asset.status);

            _maintText.text = "Last maintenance: " + ARUiKit.Safe(asset.lastMaintenance)
                + "\nNext maintenance: " + ARUiKit.Safe(asset.nextMaintenance);

            // Reset detail section (collapsed until View Details).
            _detailsExpanded = false;
            _detailSection.SetActive(false);
            _detailsButton.GetComponentInChildren<Text>().text = "View Details";

            _descText.text = ARUiKit.Safe(asset.description, "No description provided.");
            _modelText.text = string.IsNullOrEmpty(asset.modelUrl)
                ? "3D model: not attached (see Dashboard on desktop)."
                : "3D model: " + asset.modelUrl + "\n(Open the Dashboard on desktop for the 3D view.)";
            StartPhotoLoad(asset.imageUrl);
        }

        public void ShowLoading(string markerId)
        {
            if (_root == null) BuildUI();
            _root.SetActive(true);
            _loadingRow.SetActive(true);
            _loadingText.text = "Loading asset for " + markerId + "…";
            _errorText.text = string.Empty;
            StartSpinner();
        }

        public void ShowNotFound(string markerId)
        {
            StopSpinner();
            if (_root == null) BuildUI();
            _root.SetActive(true);
            _loadingRow.SetActive(false);
            _asset = null;
            _detailSection.SetActive(false);
            _nameText.text = "Unknown marker";
            _idText.text = "Marker: " + ARUiKit.Safe(markerId, "?");
            _categoryText.text = string.Empty;
            _locationText.text = string.Empty;
            _badgeBg.color = ARUiKit.Grey;
            _badgeText.text = "NOT LINKED";
            _maintText.text = string.Empty;
            _errorText.text = "No asset is linked to marker '" + markerId
                + "'. Check the marker ID or register it in the Dashboard.";
        }

        public void ShowError(string message)
        {
            StopSpinner();
            if (_root == null) BuildUI();
            _root.SetActive(true);
            _loadingRow.SetActive(false);
            _errorText.text = message ?? "Something went wrong.";
        }

        public void Hide()
        {
            StopSpinner();
            StopPhoto();
            if (_root != null) _root.SetActive(false);
            _asset = null;
        }

        // ------------------------------------------------------------ actions

        private void ToggleDetails()
        {
            _detailsExpanded = !_detailsExpanded;
            _detailSection.SetActive(_detailsExpanded);
            var label = _detailsButton.GetComponentInChildren<Text>();
            if (label != null) label.text = _detailsExpanded ? "Hide Details" : "View Details";
        }

        private void OpenHistory()
        {
            if (_asset == null) return;
            IAssetProvider p = _provider ?? (tracker != null ? tracker.Provider : null);
            if (historyUI != null) historyUI.OpenForAsset(_asset.id, _asset.name, p);
            else Debug.LogWarning("[AR] MaintenanceHistoryUI not wired.");
        }

        private void OpenReporter()
        {
            if (_asset == null) return;
            IAssetProvider p = _provider ?? (tracker != null ? tracker.Provider : null);
            if (reporterUI != null) reporterUI.OpenForAsset(_asset, p);
            else Debug.LogWarning("[AR] IssueReporterUI not wired.");
        }

        // ------------------------------------------------------------ spinner + photo

        private void StartSpinner()
        {
            StopSpinner();
            _spinnerRoutine = StartCoroutine(SpinForever());
        }

        private void StopSpinner()
        {
            if (_spinnerRoutine != null) { StopCoroutine(_spinnerRoutine); _spinnerRoutine = null; }
        }

        private IEnumerator SpinForever()
        {
            while (true)
            {
                if (_spinner != null) _spinner.Rotate(0, 0, -180f * Time.unscaledDeltaTime);
                yield return null;
            }
        }

        private void StartPhotoLoad(string url)
        {
            StopPhoto();
            _photoBox.color = new Color(1f, 1f, 1f, 0.08f);
            _photoBox.sprite = null;
            _photoPlaceholder.text = string.IsNullOrEmpty(url)
                ? "No photo available" : "Loading photo…";
            _photoPlaceholder.gameObject.SetActive(true);
            if (!string.IsNullOrEmpty(url))
                _photoRoutine = StartCoroutine(LoadPhoto(url));
        }

        private void StopPhoto()
        {
            if (_photoRoutine != null) { StopCoroutine(_photoRoutine); _photoRoutine = null; }
        }

        private IEnumerator LoadPhoto(string url)
        {
            UnityWebRequest req = null;
            try { req = UnityWebRequestTexture.GetTexture(url); }
            catch (Exception e)
            {
                _photoPlaceholder.text = "Photo failed to load.";
                Debug.LogWarning("[AR] Bad photo URL: " + e.Message);
                yield break;
            }
            req.timeout = 10;
            yield return req.SendWebRequest();
            bool ok = req.result == UnityWebRequest.Result.Success;
            if (ok)
            {
                Texture2D tex = DownloadHandlerTexture.GetContent(req);
                if (tex != null)
                {
                    Sprite sprite = Sprite.Create(tex,
                        new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                    _photoBox.sprite = sprite;
                    _photoBox.color = Color.white;
                    _photoPlaceholder.gameObject.SetActive(false);
                }
                else _photoPlaceholder.text = "Photo failed to load.";
            }
            else
            {
                _photoPlaceholder.text = "Photo unavailable (offline or bad link).";
                if (req.responseCode != 0)
                    Debug.LogWarning("[AR] Photo HTTP " + req.responseCode + " for " + url);
            }
            req.Dispose();
            _photoRoutine = null;
        }
    }
}
