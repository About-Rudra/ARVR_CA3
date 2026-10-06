using System;
using System.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

//
// ARMarkerTracker.cs — wraps ARTrackedImageManager and resolves the detected
// reference-image name (markerId, e.g. "MARK-001") to an asset via IAssetProvider.
//
// Editor Simulation Mode: pressing 1 / 2 / 3 (or calling SimulateMarker from
// on-screen buttons built by ARBootstrap) fakes a detection of MARK-001/002/003
// so the full lookup -> passport panel flow works with no device, no camera,
// no backend and no printed markers.
//

namespace ArPassport.AR
{
    [RequireComponent(typeof(ARTrackedImageManager))]
    public class ARMarkerTracker : MonoBehaviour
    {
        [Header("Provider")]
        [Tooltip("If set, this provider is used. Otherwise resolved at runtime: live ApiClient when present, offline simulation fallback.")]
        public MonoBehaviour providerBehaviour;

        [Header("Behaviour")]
        [Tooltip("Seconds to wait for the backend before reporting a timeout.")]
        public float requestTimeout = 10f;
        [Tooltip("Ignore repeat detections of the same marker within this cooldown.")]
        public float sameMarkerCooldown = 1.5f;
        [Tooltip("Allow keys 1/2/3 + SimulateMarker() outside the Editor (on-device testing without printed markers).")]
        public bool allowSimulationOnDevice = true;
        public bool verboseLogging = false;

        // ---- Events (subscribed by ARBootstrap / PassportPanelController) ----
        /// <summary>Raised when markerId resolves to an asset.</summary>
        public event Action<AssetDto> OnAssetFound;
        /// <summary>Raised when the backend reports no asset for a markerId.
        /// Argument is the markerId.</summary>
        public event Action<string> OnAssetNotFound;
        /// <summary>Raised on network/timeout/lookup errors. Argument is a
        /// human-readable message safe to show on screen.</summary>
        public event Action<string> OnError;
        /// <summary>Raised when a lookup starts (markerId). UI shows a spinner.</summary>
        public event Action<string> OnLookupStarted;
        /// <summary>Raised when the currently shown marker is lost/removed.</summary>
        public event Action OnAssetLost;

        public string CurrentMarkerId { get; private set; }
        public AssetDto CurrentAsset { get; private set; }
        public bool IsBusy { get; private set; }

        private ARTrackedImageManager _manager;
        private IAssetProvider _provider;
        private float _lastLookupTime = -100f;
        private string _lastLookupMarker;
        private Coroutine _lookupRoutine;

        // Simulation marker ids in fixed order for keys 1/2/3.
        private static readonly string[] SimMarkers = { "MARK-001", "MARK-002", "MARK-003" };

        // ------------------------------------------------------------ lifecycle

        private void Awake()
        {
            _manager = GetComponent<ARTrackedImageManager>();
            if (_manager == null)
            {
                Debug.LogError("[AR] ARMarkerTracker requires ARTrackedImageManager on the same GameObject.");
            }
        }

        private void OnEnable()
        {
            ResolveProvider();
            if (_manager != null)
                _manager.trackedImagesChanged += OnTrackedImagesChanged;
        }

        private void OnDisable()
        {
            if (_manager != null)
                _manager.trackedImagesChanged -= OnTrackedImagesChanged;
        }

        private void Update()
        {
#if UNITY_EDITOR
            PollSimulationKeys();
#else
            if (allowSimulationOnDevice) PollSimulationKeys();
#endif
        }

        private void PollSimulationKeys()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SimulateMarkerByIndex(0);
            else if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SimulateMarkerByIndex(1);
            else if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SimulateMarkerByIndex(2);
        }

        private void ResolveProvider()
        {
            if (providerBehaviour != null && providerBehaviour is IAssetProvider)
            {
                _provider = (IAssetProvider)providerBehaviour;
                return;
            }
            // Attach the best available provider to this GameObject.
            _provider = ProviderFactory.Resolve(this);
            providerBehaviour = _provider as MonoBehaviour;
        }

        /// <summary>Live provider in use (adapter or simulation). For banners.</summary>
        public IAssetProvider Provider
        {
            get
            {
                if (_provider == null) ResolveProvider();
                return _provider;
            }
        }

        // ------------------------------------------------------- AR callbacks

        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs args)
        {
            // New or re-detected images start a lookup.
            foreach (ARTrackedImage img in args.added) HandleImage(img);
            foreach (ARTrackedImage img in args.updated) HandleImage(img);
            // Removed images clear the panel if they were the current one.
            foreach (ARTrackedImage img in args.removed)
            {
                if (img != null && img.referenceImage.name == CurrentMarkerId)
                    ClearCurrent();
            }
        }

        private void HandleImage(ARTrackedImage img)
        {
            if (img == null) return;
            if (img.trackingState == TrackingState.None) return;
            string markerId = img.referenceImage.name;
            if (string.IsNullOrEmpty(markerId)) return;
            if (verboseLogging)
                Debug.Log("[AR] Tracked image: " + markerId + " state=" + img.trackingState);
            Lookup(markerId);
        }

        // ------------------------------------------------------------ lookup

        /// <summary>Resolve a markerId to an asset (debounced + serialized).</summary>
        public void Lookup(string markerId)
        {
            if (string.IsNullOrEmpty(markerId)) return;
            if (IsBusy) { Log("Lookup busy — ignoring " + markerId); return; }
            if (string.Equals(markerId, _lastLookupMarker, StringComparison.OrdinalIgnoreCase)
                && Time.unscaledTime - _lastLookupTime < sameMarkerCooldown)
            { Log("Lookup cooldown — ignoring " + markerId); return; }

            _lastLookupMarker = markerId;
            _lastLookupTime = Time.unscaledTime;
            if (_lookupRoutine != null) StopCoroutine(_lookupRoutine);
            _lookupRoutine = StartCoroutine(LookupRoutine(markerId));
        }

        private IEnumerator LookupRoutine(string markerId)
        {
            IsBusy = true;
            if (OnLookupStarted != null) OnLookupStarted(markerId);

            bool finished = false;
            AssetDto asset = null;
            string failure = null;
            bool notFound = false;

            IEnumerator inner = Provider.GetAssetByMarker(
                markerId,
                ok =>
                {
                    asset = ok;
                    finished = true;
                },
                error =>
                {
                    failure = error ?? "Unknown lookup error.";
                    // The simulation/backend reports unknown markers as "no asset";
                    // surface those through OnAssetNotFound, real errors via OnError.
                    notFound = IsNotFoundMessage(failure);
                    finished = true;
                });

            Coroutine c = StartCoroutine(inner);
            float deadline = Time.unscaledTime + Mathf.Max(1f, requestTimeout);
            while (!finished)
            {
                if (Time.unscaledTime > deadline)
                {
                    try { StopCoroutine(c); } catch { /* already finished */ }
                    failure = "Request timed out after " + requestTimeout + "s. Check connectivity and retry.";
                    notFound = false;
                    break;
                }
                yield return null;
            }

            IsBusy = false;
            _lookupRoutine = null;

            if (asset != null)
            {
                CurrentMarkerId = markerId;
                CurrentAsset = asset;
                Log("Asset found for " + markerId + ": " + asset.name);
                if (OnAssetFound != null) OnAssetFound(asset);
            }
            else if (notFound)
            {
                Log("No asset for " + markerId);
                if (OnAssetNotFound != null) OnAssetNotFound(markerId);
            }
            else
            {
                Log("Lookup error: " + failure);
                if (OnError != null) OnError(failure ?? "Lookup failed.");
            }
        }

        private static bool IsNotFoundMessage(string msg)
        {
            if (string.IsNullOrEmpty(msg)) return false;
            // Backend error codes: MARKER_NOT_FOUND, ASSET_NOT_FOUND (+ messages
            // like 'No asset linked to marker "MARK-999".'). Normalise
            // underscores so both codes and prose match.
            string m = msg.ToLowerInvariant().Replace('_', ' ');
            return m.Contains("no asset") || m.Contains("not found") || m.Contains("404");
        }

        /// <summary>Clear the current asset (marker lost or user closed panel).</summary>
        public void ClearCurrent()
        {
            CurrentMarkerId = null;
            CurrentAsset = null;
            _lastLookupMarker = null;
            if (OnAssetLost != null) OnAssetLost();
        }

        // --------------------------------------------------------- simulation

        /// <summary>Fake a marker detection (Editor keys 1/2/3, on-screen SIM buttons).</summary>
        public void SimulateMarker(string markerId)
        {
            if (string.IsNullOrEmpty(markerId)) return;
            Log("SIMULATE marker: " + markerId);
            Lookup(markerId);
        }

        public void SimulateMarkerByIndex(int index)
        {
            if (index < 0 || index >= SimMarkers.Length) return;
            SimulateMarker(SimMarkers[index]);
        }

        public string[] SimulationMarkerIds() { return (string[])SimMarkers.Clone(); }

        private void Log(string msg)
        {
            if (verboseLogging) Debug.Log("[AR] Tracker: " + msg);
        }
    }
}
