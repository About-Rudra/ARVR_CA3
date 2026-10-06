using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

namespace ArPassport.Utils
{
    /// <summary>
    /// DEV4-owned startup router. Place once in the Boot scene (first in Build Settings).
    /// Responsibilities ONLY: route to Dashboard/ARScanner, check API reachability,
    /// raise an offline banner, and warn when a release build points at localhost.
    /// Does NOT touch AR (DEV1), API models (DEV2) or dashboard UI (DEV3).
    ///
    /// URL convention (must match DEV2 ApiConfig/ApiClient): ApiBaseUrl is the
    /// backend ORIGIN only, e.g. http://localhost:3000 (no "/api" suffix -
    /// ApiClient appends "/api/..."). Health is probed at GET {origin}/health.
    /// The canonical override key is "AR_API_BASE_URL" (same as ApiConfig).
    /// </summary>
    public class AppBootstrapper : MonoBehaviour
    {
        public static class Scenes
        {
            public const string Boot = "Boot";
            public const string Dashboard = "Dashboard";
            public const string ARScanner = "ARScanner";
        }

        public static class DeepLinks
        {
            // arpassport://asset/MARK-001  -> opens ARScanner with a marker preselected.
            public const string Scheme = "arpassport";
            public const string AssetHost = "asset";
        }

        /// <summary>Editor default backend origin. Matches Backend (PORT=3000) and api_config.json.</summary>
        public const string DefaultApiBaseUrl = "http://localhost:3000";

        /// <summary>Canonical override key. MUST equal ArPassport.Core.ApiConfig.PlayerPrefsKey.</summary>
        public const string ApiUrlPlayerPrefsKey = "AR_API_BASE_URL";

        /// <summary>Legacy DEV4 key, still honoured on read (do not write new values here).</summary>
        public const string LegacyApiUrlPlayerPrefsKey = "ar_api_base_url";

        public const string PendingMarkerPlayerPrefsKey = "ar_pending_marker";

        [Header("Startup routing")]
        [Tooltip("Scene to load after the boot checks. Usually Dashboard.")]
        public string startScene = Scenes.Dashboard;

        [Tooltip("Preselected marker id (e.g. MARK-001) forwarded to ARScanner via PlayerPrefs.")]
        public string startupMarkerId = "";

        [Header("API")]
        [Tooltip("Backend ORIGIN (no /api suffix). Editor default; override per-build here, via --api-url CLI arg, env AR_API_BASE_URL, or PlayerPrefs.")]
        public string apiBaseUrl = DefaultApiBaseUrl;

        [Header("Offline UI (optional)")]
        [Tooltip("Banner GameObject toggled when the API is unreachable. May be left empty.")]
        public GameObject offlineBanner;

        /// <summary>Current backend origin after CLI/env/PlayerPrefs/inspector resolution.</summary>
        public static string ApiBaseUrl { get; private set; } = DefaultApiBaseUrl;

        /// <summary>Fired with `true` when the API is reachable, `false` otherwise.</summary>
        public static event Action<bool> OnConnectivityChanged;

        public static bool IsOnline { get; private set; }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            ApiBaseUrl = ResolveApiBaseUrl(apiBaseUrl);
            BroadcastExplicitOverride(apiBaseUrl);
            if (!string.IsNullOrEmpty(startupMarkerId))
                PlayerPrefs.SetString(PendingMarkerPlayerPrefsKey, startupMarkerId);
            GuardProdUrl(ApiBaseUrl);
        }

        private void Start()
        {
            StartCoroutine(BootRoutine());
        }

        private IEnumerator BootRoutine()
        {
            yield return CheckReachability(ApiBaseUrl);
            SetOfflineBanner(!IsOnline);
            if (SceneManager.GetActiveScene().name == Scenes.Boot)
                SceneNavigator.LoadScene(startScene);
        }

        // ---- API ---------------------------------------------------------

        /// <summary>GET {origin}/health with a short timeout. Sets <see cref="IsOnline"/>.</summary>
        public static IEnumerator CheckReachability(string baseUrl, float timeoutSeconds = 5f)
        {
            bool ok = false;
            string url = (baseUrl ?? "").TrimEnd('/') + "/health";
            using (var req = UnityWebRequest.Get(url))
            {
                req.timeout = Mathf.CeilToInt(timeoutSeconds);
                yield return req.SendWebRequest();
                ok = req.result == UnityWebRequest.Result.Success
                     && req.responseCode >= 200 && req.responseCode < 300;
            }
            IsOnline = ok;
            OnConnectivityChanged?.Invoke(ok);
        }

        public static string ResolveApiBaseUrl(string inspectorFallback)
        {
            // 1. CLI:  MyApp.exe --api-url https://ar-api.example.com
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == "--api-url" && !string.IsNullOrWhiteSpace(args[i + 1]))
                    return Normalise(args[i + 1]);
            }
            // 2. Environment variable (CI / containers).
            try
            {
                string env = Environment.GetEnvironmentVariable(ApiUrlPlayerPrefsKey);
                if (!string.IsNullOrWhiteSpace(env)) return Normalise(env);
            }
            catch { /* sandboxed platforms may throw */ }
            // 3. PlayerPrefs overrides: canonical DEV2 key first, legacy DEV4 key second.
            string saved = PlayerPrefs.GetString(ApiUrlPlayerPrefsKey, "");
            if (!string.IsNullOrWhiteSpace(saved)) return Normalise(saved);
            string legacy = PlayerPrefs.GetString(LegacyApiUrlPlayerPrefsKey, "");
            if (!string.IsNullOrWhiteSpace(legacy)) return Normalise(legacy);
            // 4. Inspector default. (DEV2 ApiConfig additionally consults
            // Resources/api_config.json and its ScriptableObject; see BroadcastExplicitOverride.)
            string fromInspector = (inspectorFallback ?? "").Trim();
            if (!string.IsNullOrEmpty(fromInspector)) return Normalise(fromInspector);
            return DefaultApiBaseUrl;
        }

        /// <summary>
        /// When the URL was set EXPLICITLY (CLI / env / non-default inspector),
        /// publish it to the canonical PlayerPrefs key so DEV2 ApiClient follows
        /// the same backend. Otherwise leave PlayerPrefs alone so ApiConfig's own
        /// chain (api_config.json, ScriptableObject) keeps working.
        /// </summary>
        public static void BroadcastExplicitOverride(string inspectorValue)
        {
            bool explicitCliOrEnv = WasSetViaCliOrEnv();
            bool explicitInspector = !string.IsNullOrWhiteSpace(inspectorValue)
                && Normalise(inspectorValue) != DefaultApiBaseUrl;
            if (explicitCliOrEnv || explicitInspector)
            {
                PlayerPrefs.SetString(ApiUrlPlayerPrefsKey, ApiBaseUrl);
                PlayerPrefs.Save();
            }
        }

        private static bool WasSetViaCliOrEnv()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "--api-url" && !string.IsNullOrWhiteSpace(args[i + 1])) return true;
            try
            {
                if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ApiUrlPlayerPrefsKey)))
                    return true;
            }
            catch { /* ignore */ }
            return false;
        }

        private static string Normalise(string url)
        {
            return (url ?? "").Trim().TrimEnd('/');
        }

        /// <summary>
        /// Release builds must not talk to localhost (a phone has no server on loopback).
        /// Also flags a trailing "/api": ApiClient appends "/api/..." itself, so a
        /// suffixed base would produce "/api/api/..." and every call would 404.
        /// Warns loudly instead of silently failing offline.
        /// </summary>
        public static void GuardProdUrl(string url)
        {
            string u = (url ?? "").TrimEnd('/');
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            if (u.Contains("localhost") || u.Contains("127.0.0.1"))
            {
                Debug.LogWarning("[AppBootstrapper] Release build is using a localhost API URL ("
                    + url + "). Point apiBaseUrl / --api-url at the deployed backend origin. " +
                    "See docs/DEPLOYMENT.md. Falling back to offline seed data.");
            }
#endif
            if (u.EndsWith("/api", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[AppBootstrapper] apiBaseUrl should be the backend origin WITHOUT "
                    + "a trailing /api (ApiClient appends it). Got: " + url);
            }
        }

        public void SetOfflineBanner(bool showOffline)
        {
            if (offlineBanner != null) offlineBanner.SetActive(showOffline);
        }

        /// <summary>Deep link entry: arpassport://asset/{markerId}.</summary>
        public static void OpenAsset(string markerId)
        {
            PlayerPrefs.SetString(PendingMarkerPlayerPrefsKey, markerId ?? "");
            SceneNavigator.LoadARScanner(markerId);
        }

        /// <summary>Marker id waiting for ARScanner ("" when none). Consumed by DEV1 scanner.</summary>
        public static string ConsumePendingMarker()
        {
            string m = PlayerPrefs.GetString(PendingMarkerPlayerPrefsKey, "");
            PlayerPrefs.DeleteKey(PendingMarkerPlayerPrefsKey);
            return m;
        }
    }
}
