using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ArPassport.Core
{
    /// <summary>
    /// Singleton MonoBehaviour REST client for the AR Passport backend.
    /// All calls are UnityWebRequest coroutines with consistent callbacks:
    ///   onSuccess(T result), onError(code, message).
    /// Error bodies follow {error:{code,message}} and are parsed via JsonListHelper.
    /// When the backend is unreachable, callbacks fire with code NETWORK_OFFLINE
    /// (no internet per Application.internetReachability) or NETWORK_ERROR —
    /// callers should then fall back to LocalSeedProvider.
    /// Newtonsoft note: with com.unity.nuget.newtonsoft-json installed you may
    /// replace JsonListHelper.FromJsonArray with JsonConvert.DeserializeObject;
    /// this file intentionally uses only JsonUtility so it compiles stock.
    /// </summary>
    public class ApiClient : MonoBehaviour
    {
        private static ApiClient _instance;
        public static ApiClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<ApiClient>();
                    if (_instance == null)
                    {
                        var go = new GameObject("ApiClient");
                        _instance = go.AddComponent<ApiClient>();
                    }
                }
                return _instance;
            }
        }

        [Tooltip("If set, overrides ApiConfig resolution. Leave empty to use ApiConfig.")]
        public string baseUrlOverride = string.Empty;

        public string BaseUrl
        {
            get
            {
                if (!string.IsNullOrEmpty(baseUrlOverride)) return baseUrlOverride.TrimEnd('/');
                return ApiConfig.GetBaseUrl();
            }
        }

        public float TimeoutSeconds => ApiConfig.GetTimeoutSeconds();

        /// <summary>True when the device reports no network route at all.</summary>
        public bool IsOffline => Application.internetReachability == NetworkReachability.NotReachable;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ---------------- generic plumbing ----------------

        private IEnumerator SendGet(string path, Action<string> onSuccess, Action<string, string> onError)
        {
            if (IsOffline) { onError?.Invoke("NETWORK_OFFLINE", "No internet connection (offline mode)."); yield break; }
            string url = BaseUrl + path;
            using (UnityWebRequest req = UnityWebRequest.Get(url))
            {
                req.timeout = Mathf.RoundToInt(TimeoutSeconds);
                req.SetRequestHeader("Accept", "application/json");
                yield return req.SendWebRequest();
                if (IsRequestError(req))
                {
                    EmitRequestError(req, onError);
                }
                else onSuccess?.Invoke(req.downloadHandler.text ?? string.Empty);
            }
        }

        private IEnumerator SendWithBody(string path, string method, string jsonBody,
            Action<string> onSuccess, Action<string, string> onError)
        {
            if (IsOffline) { onError?.Invoke("NETWORK_OFFLINE", "No internet connection (offline mode)."); yield break; }
            string url = BaseUrl + path;
            byte[] body = string.IsNullOrEmpty(jsonBody) ? null : Encoding.UTF8.GetBytes(jsonBody);
            using (UnityWebRequest req = new UnityWebRequest(url, method))
            {
                if (body != null)
                {
                    req.uploadHandler = new UploadHandlerRaw(body);
                    req.SetRequestHeader("Content-Type", "application/json");
                }
                req.downloadHandler = new DownloadHandlerBuffer();
                req.timeout = Mathf.RoundToInt(TimeoutSeconds);
                req.SetRequestHeader("Accept", "application/json");
                yield return req.SendWebRequest();
                if (IsRequestError(req))
                {
                    // DELETE returns 204 with empty body — treat as success.
                    if (method == "DELETE" && req.responseCode == 204)
                    {
                        onSuccess?.Invoke(string.Empty);
                        yield break;
                    }
                    EmitRequestError(req, onError);
                }
                else onSuccess?.Invoke(req.downloadHandler.text ?? string.Empty);
            }
        }

        private static bool IsRequestError(UnityWebRequest req)
        {
#if UNITY_2020_1_OR_NEWER
            return req.result == UnityWebRequest.Result.ConnectionError
                || req.result == UnityWebRequest.Result.ProtocolError;
#else
            return req.isNetworkError || req.isHttpError;
#endif
        }

        private static void EmitRequestError(UnityWebRequest req, Action<string, string> onError)
        {
            long status = req.responseCode;
            string body = req.downloadHandler != null ? req.downloadHandler.text : string.Empty;
            if (JsonListHelper.TryParseError(body, out string code, out string message))
            {
                onError?.Invoke(code, message);
                return;
            }
#if UNITY_2020_1_OR_NEWER
            bool connection = req.result == UnityWebRequest.Result.ConnectionError;
#else
            bool connection = req.isNetworkError;
#endif
            if (connection || status == 0)
                onError?.Invoke("NETWORK_ERROR", string.IsNullOrEmpty(req.error) ? "Connection failed" : req.error);
            else
                onError?.Invoke("HTTP_" + status, string.IsNullOrEmpty(body) ? req.error : body);
        }

        private static string Escape(string s) => UnityWebRequest.EscapeURL(s ?? string.Empty);

        // ---------------- endpoints ----------------

        public Coroutine GetAssets(Action<List<Asset>> onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendGet("/api/assets",
                json => onSuccess?.Invoke(JsonListHelper.FromJsonArray<Asset>(json)), onError));
        }

        public Coroutine GetAssetById(string id, Action<Asset> onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendGet("/api/assets/" + Escape(id),
                json => onSuccess?.Invoke(Asset.FromJson(json)), onError));
        }

        /// <summary>GET /api/assets/marker/:markerId -&gt; {assetId}. Success returns the asset id string.</summary>
        public Coroutine GetAssetByMarker(string markerId, Action<string> onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendGet("/api/assets/marker/" + Escape(markerId),
                json =>
                {
                    try
                    {
                        var r = MarkerLookupResponse.FromJson(json);
                        onSuccess?.Invoke(r != null ? r.assetId : null);
                    }
                    catch (Exception e) { onError?.Invoke("PARSE_ERROR", e.Message); }
                }, onError));
        }

        public Coroutine GetMaintenance(string assetId, Action<List<MaintenanceRecord>> onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendGet("/api/assets/" + Escape(assetId) + "/maintenance",
                json => onSuccess?.Invoke(JsonListHelper.FromJsonArray<MaintenanceRecord>(json)), onError));
        }

        public Coroutine GetIssues(string assetIdOrNull, string statusOrNull,
            Action<List<Issue>> onSuccess, Action<string, string> onError)
        {
            string q = string.Empty;
            if (!string.IsNullOrEmpty(assetIdOrNull)) q += (q.Length == 0 ? "?" : "&") + "assetId=" + Escape(assetIdOrNull);
            if (!string.IsNullOrEmpty(statusOrNull)) q += (q.Length == 0 ? "?" : "&") + "status=" + Escape(statusOrNull);
            return StartCoroutine(SendGet("/api/issues" + q,
                json => onSuccess?.Invoke(JsonListHelper.FromJsonArray<Issue>(json)), onError));
        }

        public Coroutine GetIssues(Action<List<Issue>> onSuccess, Action<string, string> onError)
        {
            return GetIssues(null, null, onSuccess, onError);
        }

        public Coroutine ReportIssue(IssueDraft draft, Action<Issue> onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendWithBody("/api/issues", "POST", draft != null ? draft.ToJson() : "{}",
                json => onSuccess?.Invoke(Issue.FromJson(json)), onError));
        }

        public Coroutine UpdateIssueStatus(string issueId, string status, Action<Issue> onSuccess, Action<string, string> onError)
        {
            string payload = "{\"status\":\"" + status + "\"}";
            return StartCoroutine(SendWithBody("/api/issues/" + Escape(issueId) + "/status", "PATCH", payload,
                json => onSuccess?.Invoke(Issue.FromJson(json)), onError));
        }

        public Coroutine CreateAsset(Asset asset, Action<Asset> onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendWithBody("/api/assets", "POST", asset != null ? asset.ToJson() : "{}",
                json => onSuccess?.Invoke(Asset.FromJson(json)), onError));
        }

        public Coroutine UpdateAsset(string id, Asset asset, Action<Asset> onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendWithBody("/api/assets/" + Escape(id), "PATCH", asset != null ? asset.ToJson() : "{}",
                json => onSuccess?.Invoke(Asset.FromJson(json)), onError));
        }

        public Coroutine DeleteAsset(string id, Action onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendWithBody("/api/assets/" + Escape(id), "DELETE", null,
                _ => onSuccess?.Invoke(), onError));
        }

        public Coroutine GetLocations(Action<List<Location>> onSuccess, Action<string, string> onError)
        {
            return StartCoroutine(SendGet("/api/locations",
                json => onSuccess?.Invoke(JsonListHelper.FromJsonArray<Location>(json)), onError));
        }
    }
}
