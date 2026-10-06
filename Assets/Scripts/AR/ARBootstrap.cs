using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

//
// ARBootstrap.cs — runtime scene bootstrapper for ARScanner.
//
// The ARScanner scene ships nearly empty on purpose: this component builds the
// whole AR + UI stack at runtime so there are no prefab/scene reference risks:
//   AR Session, AR Session Origin (+ AR Camera, ARCameraManager,
//   ARCameraBackground), ARTrackedImageManager, EventSystem, screen-space
//   Canvas, status banner, SIM buttons, PassportPanel, MaintenanceHistoryUI,
//   IssueReporterUI.
//
// Failure handling (all surfaced on screen, never silent):
//   * camera permission denied  -> blocking notice (SIM mode still works)
//   * backend unreachable       -> banner turns red, provider falls back to
//                                 offline simulation when ApiClient is absent;
//                                 live errors surface per-request in the panel
//   * invalid/unknown marker    -> "NOT LINKED" panel state
//   * slow network              -> loading spinner + 10s timeout
//   * missing photo/model       -> placeholder text (see PassportPanelController)
//   * AR unsupported / no image
//     library on device         -> banner notice, SIM buttons stay usable
//

namespace ArPassport.AR
{
    public class ARBootstrap : MonoBehaviour
    {
        public const string SceneName = "ARScanner";

        [Header("Options")]
        [Tooltip("Show on-screen SIM 1/2/3 buttons on device builds (always shown in Editor).")]
        public bool showSimButtons = true;
        [Tooltip("Physical print width of markers in meters (must match print!).")]
        public float markerPrintWidthMeters = 0.15f;
        [Tooltip("Name of an XRReferenceImageLibrary in a Resources folder (optional).")]
        public string referenceLibraryResourceName = "ARMarkers";

        public ARMarkerTracker Tracker { get; private set; }
        public PassportPanelController Panel { get; private set; }

        private Canvas _canvas;
        private Text _bannerText;
        private Image _bannerBg;
        private GameObject _simRow;
        private GameObject _blockingPanel;
        private Text _blockingText;

        // ------------------------------------------------------------ auto-boot

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterAutoBoot()
        {
            SceneManager.sceneLoaded += OnSceneLoadedAutoBoot;
        }

        private static void OnSceneLoadedAutoBoot(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != SceneName) return;
            if (FindObjectOfType<ARBootstrap>() != null) return;
            var go = new GameObject("ARBootstrap (auto)");
            go.AddComponent<ARBootstrap>();
            Debug.Log("[AR] Auto-booted ARBootstrap in scene '" + SceneName + "'.");
        }

        // ------------------------------------------------------------ build

        private void Awake()
        {
            // If the scene already contains a manually placed bootstrap, the
            // auto-created duplicate removes itself.
            ARBootstrap[] all = FindObjectsOfType<ARBootstrap>();
            if (all.Length > 1 && all[0] != this)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            EnsureEventSystem();
            EnsureARSession();
            EnsureCanvas();
            BuildBanner();
            BuildBlockingPanel();
            BuildSimRow();
            BuildARUI();
            StartCoroutine(StartupRoutine());
        }

        private IEnumerator StartupRoutine()
        {
            yield return StartCoroutine(CheckCameraPermission());
            yield return StartCoroutine(CheckBackendReachability());
            TryConfigureImageLibrary();
            UpdateBanner();
        }

        // ------------------------------------------------------------ AR stack

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(transform, false);
        }

        private void EnsureARSession()
        {
            ARSession session = FindObjectOfType<ARSession>();
            if (session == null)
            {
                var go = new GameObject("AR Session", typeof(ARSession));
                go.transform.SetParent(transform, false);
                session = go.GetComponent<ARSession>();
            }
            session.gameObject.SetActive(true);

            ARSessionOrigin origin = FindObjectOfType<ARSessionOrigin>();
            GameObject originGo;
            if (origin == null)
            {
                originGo = new GameObject("AR Session Origin", typeof(ARSessionOrigin));
                originGo.transform.SetParent(transform, false);
                origin = originGo.GetComponent<ARSessionOrigin>();
            }
            else originGo = origin.gameObject;

            Camera cam = origin.camera;
            if (cam == null)
            {
                cam = originGo.GetComponentInChildren<Camera>();
                if (cam == null)
                {
                    var camGo = new GameObject("AR Camera",
                        typeof(Camera), typeof(ARCameraManager), typeof(ARCameraBackground));
                    camGo.transform.SetParent(originGo.transform, false);
                    camGo.tag = "MainCamera";
                    cam = camGo.GetComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = Color.black;
                    cam.nearClipPlane = 0.1f;
                    cam.farClipPlane = 100f;
                }
                origin.camera = cam;
            }
            if (cam.GetComponent<ARCameraManager>() == null)
                cam.gameObject.AddComponent<ARCameraManager>();
            if (cam.GetComponent<ARCameraBackground>() == null)
                cam.gameObject.AddComponent<ARCameraBackground>();

            ARTrackedImageManager mgr = originGo.GetComponent<ARTrackedImageManager>();
            if (mgr == null) mgr = originGo.AddComponent<ARTrackedImageManager>();
            mgr.requestedMaxNumberOfMovingImages = 3;

            Tracker = originGo.GetComponent<ARMarkerTracker>();
            if (Tracker == null) Tracker = originGo.AddComponent<ARMarkerTracker>();

            // Surface AR-Session-level problems on the banner.
            ARSession.stateChanged += OnSessionStateChanged;
        }

        private void OnDestroy()
        {
            ARSession.stateChanged -= OnSessionStateChanged;
        }

        private void OnSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            Debug.Log("[AR] Session state: " + args.state);
            if (args.state == ARSessionState.Unsupported)
                SetBanner("AR not supported on this device — use SIM buttons.", ARUiKit.Danger);
            else if (args.state == ARSessionState.NeedsInstall || args.state == ARSessionState.Installing)
                SetBanner("Installing AR services…", ARUiKit.Warning);
            else UpdateBanner();
        }

        // ------------------------------------------------------------ canvas + UI

        private void EnsureCanvas()
        {
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                var go = new GameObject("AR Canvas",
                    typeof(RectTransform), typeof(Canvas),
                    typeof(CanvasScaler), typeof(GraphicRaycaster));
                go.transform.SetParent(transform, false);
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 10;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080, 1920); // portrait-first
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                scaler.referencePixelsPerUnit = 100f;
            }
            _canvas = canvas;
        }

        private Transform CanvasT() { return _canvas.transform; }

        private void BuildBanner()
        {
            var bar = ARUiKit.Panel(CanvasT().gameObject, "StatusBanner", new Color(0.1f, 0.12f, 0.15f, 0.9f));
            var rt = bar.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(0, -110); rt.offsetMax = new Vector2(0, 0);
            _bannerBg = bar.GetComponent<Image>();
            _bannerText = ARUiKit.Label(bar, "BannerText", "Starting AR…", 26,
                ARUiKit.TextLight, FontStyle.Normal, TextAnchor.MiddleCenter);
            var trt = _bannerText.rectTransform;
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = new Vector2(16, 0); trt.offsetMax = new Vector2(-16, 0);
        }

        private void BuildBlockingPanel()
        {
            _blockingPanel = new GameObject("BlockingNotice", typeof(RectTransform));
            _blockingPanel.transform.SetParent(CanvasT(), false);
            ARUiKit.FillParent(_blockingPanel.GetComponent<RectTransform>(), Vector2.zero, Vector2.zero);
            ARUiKit.Panel(_blockingPanel, "Dim", new Color(0, 0, 0, 0.75f));
            var box = ARUiKit.Panel(_blockingPanel, "Box", ARUiKit.BgDark);
            var boxRt = box.GetComponent<RectTransform>();
            boxRt.anchorMin = new Vector2(0.1f, 0.35f); boxRt.anchorMax = new Vector2(0.9f, 0.65f);
            boxRt.offsetMin = Vector2.zero; boxRt.offsetMax = Vector2.zero;
            _blockingText = ARUiKit.Label(box, "Msg", string.Empty, 28, ARUiKit.TextLight,
                FontStyle.Normal, TextAnchor.MiddleCenter);
            var mrt = _blockingText.rectTransform;
            mrt.anchorMin = new Vector2(0, 0.3f); mrt.anchorMax = new Vector2(1, 1);
            mrt.offsetMin = new Vector2(24, 0); mrt.offsetMax = new Vector2(-24, 0);
            var ok = ARUiKit.Button(box, "BtnDismiss", "Continue in SIM mode", () =>
                { _blockingPanel.SetActive(false); }, ARUiKit.Accent);
            var okRt = ok.GetComponent<RectTransform>();
            okRt.anchorMin = new Vector2(0.15f, 0.06f); okRt.anchorMax = new Vector2(0.85f, 0.26f);
            okRt.offsetMin = Vector2.zero; okRt.offsetMax = Vector2.zero;
            _blockingPanel.SetActive(false);
        }

        private void BuildSimRow()
        {
            _simRow = new GameObject("SimRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            _simRow.transform.SetParent(CanvasT(), false);
            var rt = _simRow.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1);
            rt.offsetMin = new Vector2(16, -220); rt.offsetMax = new Vector2(-16, -120);
            var hg = _simRow.GetComponent<HorizontalLayoutGroup>();
            hg.spacing = 16;
            hg.childControlWidth = true; hg.childForceExpandWidth = true;

            ARUiKit.Button(_simRow, "Sim1", "SIM 1",
                () => { if (Tracker != null) Tracker.SimulateMarkerByIndex(0); }, ARUiKit.Card);
            ARUiKit.Button(_simRow, "Sim2", "SIM 2",
                () => { if (Tracker != null) Tracker.SimulateMarkerByIndex(1); }, ARUiKit.Card);
            ARUiKit.Button(_simRow, "Sim3", "SIM 3",
                () => { if (Tracker != null) Tracker.SimulateMarkerByIndex(2); }, ARUiKit.Card);

#if !UNITY_EDITOR
            if (!showSimButtons) _simRow.SetActive(false);
#endif
        }

        private void BuildARUI()
        {
            var historyGo = new GameObject("HistoryUI", typeof(RectTransform), typeof(MaintenanceHistoryUI));
            historyGo.transform.SetParent(CanvasT(), false);
            var historyUI = historyGo.GetComponent<MaintenanceHistoryUI>();
            historyUI.BuildUI();

            var issueGo = new GameObject("IssueUI", typeof(RectTransform), typeof(IssueReporterUI));
            issueGo.transform.SetParent(CanvasT(), false);
            var issueUI = issueGo.GetComponent<IssueReporterUI>();
            issueUI.BuildUI();

            var panelGo = new GameObject("PassportUI", typeof(RectTransform), typeof(PassportPanelController));
            panelGo.transform.SetParent(CanvasT(), false);
            Panel = panelGo.GetComponent<PassportPanelController>();
            Panel.tracker = Tracker;
            Panel.historyUI = historyUI;
            Panel.reporterUI = issueUI;
            Panel.SetProvider(Tracker != null ? Tracker.Provider : null);
            Panel.BuildUI();
        }

        // ------------------------------------------------------------ checks

        private IEnumerator CheckCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                SetBanner("Requesting camera permission…", ARUiKit.Warning);
                yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            }
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                ShowBlocking("Camera permission denied.\n\nLive image tracking is off, but you can still test the full flow with the SIM 1/2/3 buttons.");
                SetBanner("Camera denied — SIM mode only.", ARUiKit.Danger);
            }
#else
            yield return null;
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                // Editor: no real camera needed; simulation covers testing.
                Debug.Log("[AR] (Editor) webcam authorization not granted — simulation mode covers testing.");
            }
#endif
        }

        private bool _backendReachable = true;

        private IEnumerator CheckBackendReachability()
        {
            IAssetProvider p = Tracker != null ? Tracker.Provider : null;
            if (p is SimulationProvider)
            {
                _backendReachable = false; // offline by design
                yield break;
            }
            // GET /health -> {"ok":true,...} (see docs/API.md). Any HTTP response
            // proves the server is up; only a transport failure = unreachable.
            string url = BackendConfigReader.BaseUrl + "/health";
            UnityWebRequest req = null;
            try { req = UnityWebRequest.Get(url); }
            catch { _backendReachable = false; yield break; }
            req.timeout = 5;
            yield return req.SendWebRequest();
            _backendReachable = req.responseCode != 0
                || req.result == UnityWebRequest.Result.Success;
            string body = null;
            try
            {
                var dh = req.downloadHandler;
                if (dh != null) body = dh.text;
            }
            catch { /* ignore */ }
            if (!string.IsNullOrEmpty(body) && !body.Contains("\"ok\""))
                _backendReachable = false;
            Debug.Log("[AR] Backend ping " + url + " -> " + req.result + " HTTP " + req.responseCode);
            req.Dispose();
        }

        private void UpdateBanner()
        {
            if (_bannerText == null) return;
            IAssetProvider p = Tracker != null ? Tracker.Provider : null;
            string label = p != null ? p.BackendLabel : BackendConfigReader.BaseUrl;
            if (p is SimulationProvider)
                SetBanner("OFFLINE SIMULATION — point of demo without backend.", ARUiKit.Warning);
            else if (!_backendReachable)
                SetBanner("Backend unreachable (" + label + ") — showing server errors per request.", ARUiKit.Danger);
            else
                SetBanner("Connected: " + label + "  •  Point at a marker (or tap SIM 1/2/3).", ARUiKit.Success);
        }

        private void SetBanner(string text, Color color)
        {
            if (_bannerText != null) _bannerText.text = text;
            if (_bannerBg != null) _bannerBg.color = color;
        }

        private void ShowBlocking(string text)
        {
            if (_blockingText != null) _blockingText.text = text;
            if (_blockingPanel != null) _blockingPanel.SetActive(true);
        }

        // ------------------------------------------------------------ image library

        /// <summary>
        /// Assigns a serialized XRReferenceImageLibrary from Resources when the
        /// team has baked one (see docs/AR_DEVELOPER.md). Otherwise tries to add
        /// the StreamingAssets/markers PNGs to a runtime mutable library.
        /// Everything is best-effort: failure only affects on-device tracking,
        /// never the simulation flow.
        /// </summary>
        private void TryConfigureImageLibrary()
        {
            ARTrackedImageManager mgr = Tracker != null
                ? Tracker.GetComponent<ARTrackedImageManager>() : null;
            if (mgr == null) return;

            // 1) Serialized library (preferred, baked by a team member in Editor).
            try
            {
                var lib = Resources.Load<XRReferenceImageLibrary>(referenceLibraryResourceName);
                if (lib != null && lib.count > 0)
                {
                    mgr.referenceLibrary = lib;
                    mgr.enabled = true;
                    Debug.Log("[AR] Using serialized reference library '" +
                        referenceLibraryResourceName + "' (" + lib.count + " images).");
                    return;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AR] Serialized library load failed: " + e.Message);
            }

            // 2) Runtime mutable library from StreamingAssets PNGs.
#if !UNITY_EDITOR
            StartCoroutine(BuildRuntimeLibrary(mgr));
#else
            Debug.Log("[AR] (Editor) no serialized library — on-device tracking will need '"
                + referenceLibraryResourceName + "' or StreamingAssets markers (see AR_DEVELOPER.md). SIM mode active.");
#endif
        }

#if !UNITY_EDITOR
        private IEnumerator BuildRuntimeLibrary(ARTrackedImageManager mgr)
        {
            if (mgr == null) yield break;
            bool mutableSupported = false;
            try
            {
                mutableSupported = mgr.subsystem != null
                    && mgr.subsystem.SubsystemDescriptor != null
                    && mgr.subsystem.SubsystemDescriptor.supportsMutableLibrary;
            }
            catch { mutableSupported = false; }
            if (!mutableSupported)
            {
                Debug.LogWarning("[AR] Image-tracking subsystem has no mutable library support; bake a serialized library instead.");
                yield break;
            }

            MutableRuntimeReferenceImageLibrary runtimeLib = null;
            try { runtimeLib = mgr.CreateRuntimeLibrary(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AR] CreateRuntimeLibrary failed: " + e.Message);
                yield break;
            }
            if (runtimeLib == null)
            {
                Debug.LogWarning("[AR] Runtime library unsupported on this device.");
                yield break;
            }

            string[] names = { "MARK-001", "MARK-002", "MARK-003" };
            var handles = new List<Unity.Jobs.JobHandle>();
            foreach (string n in names)
            {
                Texture2D tex = null;
                yield return StartCoroutine(LoadStreamingTexture("markers/" + n + ".png",
                    t => { tex = t; }));
                if (tex == null)
                {
                    Debug.LogWarning("[AR] Marker PNG missing: markers/" + n + ".png");
                    continue;
                }
                try
                {
                    var state = runtimeLib.ScheduleAddImageWithValidationJob(
                        tex, n, markerPrintWidthMeters);
                    handles.Add(state.jobHandle);
                }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[AR] AddImage failed for " + n + ": " + e.Message);
                }
            }
            try
            {
                Unity.Jobs.JobHandle combined = Unity.Jobs.JobHandle.CombineDependencies(handles.ToArray());
                combined.Complete();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[AR] Runtime image jobs failed: " + e.Message);
                yield break;
            }
            int added = 0;
            try { added = runtimeLib.count; } catch { }
            if (added > 0)
            {
                mgr.referenceLibrary = runtimeLib;
                mgr.enabled = true;
                Debug.Log("[AR] Runtime library ready with " + added + " marker(s).");
            }
            else SetBanner("No marker images bundled — use SIM buttons.", ARUiKit.Warning);
        }

        private IEnumerator LoadStreamingTexture(string relative, System.Action<Texture2D> done)
        {
            string url = Path.Combine(Application.streamingAssetsPath, relative);
            UnityWebRequest req = UnityWebRequestTexture.GetTexture(url);
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result == UnityWebRequest.Result.Success)
                done(DownloadHandlerTexture.GetContent(req));
            else
            {
                Debug.LogWarning("[AR] StreamingAssets load failed: " + url + " (" + req.error + ")");
                done(null);
            }
            req.Dispose();
        }
#endif
    }
}
