using UnityEngine;

namespace ArPassport.Core
{
    /// <summary>
    /// API endpoint configuration. Create via Assets &gt; Create &gt; AR Passport &gt; Api Config.
    /// Resolution order for BaseUrl:
    ///   1. Environment variable AR_API_BASE_URL (where supported)
    ///   2. PlayerPrefs key "AR_API_BASE_URL" (device / editor override)
    ///   3. Resources/api_config.json { "BaseUrl": "...", "TimeoutSeconds": 10 }
    ///   4. This ScriptableObject's BaseUrl field (default http://localhost:3000)
    /// Never hardcode a production URL in code; set it via one of the above.
    /// </summary>
    [CreateAssetMenu(fileName = "ApiConfig", menuName = "AR Passport/Api Config")]
    public class ApiConfig : ScriptableObject
    {
        [Tooltip("Base URL of the REST API, e.g. http://localhost:3000")]
        public string BaseUrl = "http://localhost:3000";

        [Tooltip("Per-request timeout in seconds.")]
        [Range(1f, 60f)]
        public float TimeoutSeconds = 10f;

        private static ApiConfig _cached;

        public const string PlayerPrefsKey = "AR_API_BASE_URL";
        public const string EnvVarKey = "AR_API_BASE_URL";

        public static ApiConfig Current
        {
            get
            {
                if (_cached != null) return _cached;
                // Optional project-level asset at Assets/Resources/ApiConfig.asset
                _cached = Resources.Load<ApiConfig>("ApiConfig");
                if (_cached == null)
                {
                    _cached = CreateInstance<ApiConfig>();
                }
                return _cached;
            }
        }

        public static string GetBaseUrl()
        {
            // 1. Environment variable
            try
            {
                string env = System.Environment.GetEnvironmentVariable(EnvVarKey);
                if (!string.IsNullOrEmpty(env)) return Normalise(env);
            }
            catch { /* sandboxed platforms may throw */ }

            // 2. PlayerPrefs override
            try
            {
                if (PlayerPrefs.HasKey(PlayerPrefsKey))
                {
                    string pref = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);
                    if (!string.IsNullOrEmpty(pref)) return Normalise(pref);
                }
            }
            catch { /* ignore */ }

            // 3. Resources/api_config.json -> {"BaseUrl":"...","TimeoutSeconds":10}
            try
            {
                TextAsset json = Resources.Load<TextAsset>("api_config");
                if (json != null && !string.IsNullOrEmpty(json.text))
                {
                    var parsed = JsonUtility.FromJson<ApiConfigJson>(json.text);
                    if (parsed != null && !string.IsNullOrEmpty(parsed.BaseUrl))
                        return Normalise(parsed.BaseUrl);
                }
            }
            catch { /* ignore */ }

            // 4. ScriptableObject fallback
            return Normalise(Current != null ? Current.BaseUrl : "http://localhost:3000");
        }

        public static float GetTimeoutSeconds()
        {
            try
            {
                TextAsset json = Resources.Load<TextAsset>("api_config");
                if (json != null && !string.IsNullOrEmpty(json.text))
                {
                    var parsed = JsonUtility.FromJson<ApiConfigJson>(json.text);
                    if (parsed != null && parsed.TimeoutSeconds > 0) return parsed.TimeoutSeconds;
                }
            }
            catch { /* ignore */ }
            if (_cached != null && _cached.TimeoutSeconds > 0) return _cached.TimeoutSeconds;
            return 10f;
        }

        /// <summary>Set a runtime override (stored in PlayerPrefs). Pass null/empty to clear.</summary>
        public static void SetOverride(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl)) PlayerPrefs.DeleteKey(PlayerPrefsKey);
            else PlayerPrefs.SetString(PlayerPrefsKey, Normalise(baseUrl));
            PlayerPrefs.Save();
        }

        private static string Normalise(string url)
        {
            if (string.IsNullOrEmpty(url)) return "http://localhost:3000";
            return url.TrimEnd('/');
        }

        [System.Serializable]
        private class ApiConfigJson
        {
            public string BaseUrl;
            public float TimeoutSeconds;
        }
    }
}
