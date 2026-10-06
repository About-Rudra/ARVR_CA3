using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

//
// AssetProvider.cs — data contracts + provider abstraction for the AR scene.
//
// WHY THIS FILE EXISTS (read before editing):
//   ApiClient + Core models (Asset, MaintenanceRecord, Issue, IssueDraft in
//   namespace ArPassport.Core) are owned by DEV 2 and may not exist yet when
//   this file compiles. To keep the AR scene compiling AND working offline in
//   the Editor, this file deliberately avoids a compile-time dependency on
//   ArPassport.Core:
//
//   * The shared JSON field names are mirrored in the local DTOs below
//     (AssetDto, MaintenanceRecordDto, IssueDraftDto, IssueDto). DO NOT rename
//     these fields — they are the shared contract:
//       Asset: {id,name,category,location,status,description,
//               imageUrl,modelUrl,lastMaintenance,nextMaintenance,markerId}
//   * ApiClientAdapter talks to the real ApiClient purely via reflection and
//     converts Core models -> DTOs with a JsonUtility round-trip (works because
//     field names match). The scene picks the live client up automatically at
//     runtime with zero code changes here, and keeps working (offline) if Core
//     is ever absent.
//   * SimulationProvider serves built-in MARK-001/002/003 data so the AR scene
//     is fully testable in the Editor with no backend and no AR hardware.
//
//   Assumed ApiClient interface (DEV 2 contract — do not redefine here).
//   NOTE: verified against Assets/Scripts/Core/ApiClient.cs (DEV 2, landed).
//   Real signatures return Coroutine (not IEnumerator) and use a TWO-PART
//   error callback (code, message). Marker lookup returns the ASSET ID
//   string ({assetId}), so this adapter chains marker -> assetId -> Asset.
//     public class ApiClient : MonoBehaviour {
//       public static ApiClient Instance;              // auto-creates itself
//       public Coroutine GetAssetById(string id, Action<Asset> ok, Action<string,string> err);
//       public Coroutine GetAssetByMarker(string markerId, Action<string> ok, Action<string,string> err);
//       public Coroutine GetMaintenance(string assetId, Action<List<MaintenanceRecord>> ok, Action<string,string> err);
//       public Coroutine ReportIssue(IssueDraft draft, Action<Issue> ok, Action<string,string> err);
//     }
//   Assumed config (DEV 2): ArPassport.Core.ApiConfig.GetBaseUrl() /
//   GetTimeoutSeconds() (static methods on a ScriptableObject) + fallback
//   Resources/api_config.json {BaseUrl, TimeoutSeconds}.
//   BackendConfigReader below reads all of these via reflection when present.
//

namespace ArPassport.AR
{
    // ------------------------------------------------------------------ DTOs

    /// <summary>Mirrors the shared Asset JSON contract. Field names are frozen.</summary>
    [Serializable]
    public class AssetDto
    {
        public string id;
        public string name;
        public string category;
        public string location;
        public string status;
        public string description;
        public string imageUrl;
        public string modelUrl;
        public string lastMaintenance;
        public string nextMaintenance;
        public string markerId;
    }

    /// <summary>Mirrors ArPassport.Core.MaintenanceRecord /
    /// GET /api/assets/:id/maintenance. Field names are frozen.</summary>
    [Serializable]
    public class MaintenanceRecordDto
    {
        public string id;
        public string assetId;
        public string date;
        public string type;
        public string technician;
        public string notes;
    }

    /// <summary>POST /api/issues request body.</summary>
    [Serializable]
    public class IssueDraftDto
    {
        public string assetId;
        public string title;
        public string description;
        public string reportedBy;
    }

    /// <summary>POST /api/issues response body. Field names are frozen.</summary>
    [Serializable]
    public class IssueDto
    {
        public string id;
        public string assetId;
        public string title;
        public string description;
        public string status;
        public string reportedAt;
        public string reportedBy;
    }

    // -------------------------------------------------------------- Interface

    /// <summary>
    /// Fallback-safe provider interface. The live ApiClientAdapter and the
    /// offline SimulationProvider both implement this, so ARMarkerTracker and
    /// the UI controllers never touch ApiClient directly.
    /// </summary>
    public interface IAssetProvider
    {
        IEnumerator GetAssetByMarker(string markerId, Action<AssetDto> ok, Action<string> err);
        IEnumerator GetAssetById(string id, Action<AssetDto> ok, Action<string> err);
        IEnumerator GetMaintenance(string assetId, Action<List<MaintenanceRecordDto>> ok, Action<string> err);
        IEnumerator ReportIssue(IssueDraftDto draft, Action<IssueDto> ok, Action<string> err);

        /// <summary>Short human-readable label for the on-screen status banner,
        /// e.g. "Live: http://10.0.2.2:3000" or "Simulation (offline)".</summary>
        string BackendLabel { get; }
    }

    // ------------------------------------------------------------ Config read

    /// <summary>
    /// Reads the backend base URL without a compile-time dependency on DEV 2's
    /// ApiConfig. When ArPassport.Core is present, ApiConfig.GetBaseUrl() is
    /// invoked via reflection (it already implements the full resolution:
    /// AR_API_BASE_URL env -&gt; PlayerPrefs "AR_API_BASE_URL" -&gt;
    /// Resources/api_config.json -&gt; ScriptableObject default). When Core is
    /// absent (offline simulation), the same chain is mirrored manually, ending
    /// at an Android-emulator-safe default (10.0.2.2 == host loopback;
    /// "localhost" on-device would point at the phone itself, so it is never
    /// used as a default here).
    /// </summary>
    public static class BackendConfigReader
    {
        public const string PrefsKey = "AR_API_BASE_URL"; // DEV 2's override key
        public const string LegacyPrefsKey = "arpassport.base_url";
        public const string EnvKey = "AR_API_BASE_URL";
        public const string DefaultBaseUrl = "http://10.0.2.2:3000";
        public const float DefaultTimeoutSeconds = 10f;

        [Serializable]
        private class ConfigJson
        {
            public string BaseUrl;
            public float TimeoutSeconds;
        }

        public static string BaseUrl
        {
            get
            {
                string viaCore = TryCoreGetBaseUrl();
                if (!string.IsNullOrEmpty(viaCore)) return viaCore.TrimEnd('/');
                try
                {
                    string env = Environment.GetEnvironmentVariable(EnvKey);
                    if (!string.IsNullOrEmpty(env)) return env.TrimEnd('/');
                }
                catch { /* sandboxed platforms may throw */ }
                string pref = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (!string.IsNullOrEmpty(pref)) return pref.TrimEnd('/');
                string jsonUrl = TryConfigJsonUrl();
                if (!string.IsNullOrEmpty(jsonUrl)) return jsonUrl;
                string legacy = PlayerPrefs.GetString(LegacyPrefsKey, string.Empty);
                if (!string.IsNullOrEmpty(legacy)) return legacy.TrimEnd('/');
                return DefaultBaseUrl;
            }
        }

        public static float TimeoutSeconds()
        {
            float t = TryCoreTimeout();
            if (t > 0) return t;
            float j = TryConfigJsonTimeout();
            if (j > 0) return j;
            return DefaultTimeoutSeconds;
        }

        public static void SetOverride(string baseUrl)
        {
            if (string.IsNullOrEmpty(baseUrl)) PlayerPrefs.DeleteKey(PrefsKey);
            else PlayerPrefs.SetString(PrefsKey, baseUrl.TrimEnd('/'));
            PlayerPrefs.Save();
        }

        private static string TryCoreGetBaseUrl()
        {
            try
            {
                Type t = Type.GetType("ArPassport.Core.ApiConfig, Assembly-CSharp");
                if (t == null) return null;
                MethodInfo m = t.GetMethod("GetBaseUrl",
                    BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (m == null || m.ReturnType != typeof(string)) return null;
                return m.Invoke(null, null) as string;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AR] BackendConfigReader: ApiConfig.GetBaseUrl reflection failed: " + e.Message);
                return null;
            }
        }

        private static float TryCoreTimeout()
        {
            try
            {
                Type t = Type.GetType("ArPassport.Core.ApiConfig, Assembly-CSharp");
                if (t == null) return 0;
                MethodInfo m = t.GetMethod("GetTimeoutSeconds",
                    BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (m == null) return 0;
                object v = m.Invoke(null, null);
                if (v is float) return (float)v;
                if (v is double) return (float)(double)v;
                if (v is int) return (int)v;
                return 0;
            }
            catch { return 0; }
        }

        private static ConfigJson TryReadConfigJson()
        {
            try
            {
                TextAsset ta = Resources.Load<TextAsset>("api_config");
                if (ta == null || string.IsNullOrEmpty(ta.text)) return null;
                return JsonUtility.FromJson<ConfigJson>(ta.text);
            }
            catch { return null; }
        }

        private static string TryConfigJsonUrl()
        {
            ConfigJson c = TryReadConfigJson();
            if (c == null || string.IsNullOrEmpty(c.BaseUrl)) return null;
            return c.BaseUrl.TrimEnd('/');
        }

        private static float TryConfigJsonTimeout()
        {
            ConfigJson c = TryReadConfigJson();
            if (c == null) return 0;
            return c.TimeoutSeconds;
        }
    }

    // --------------------------------------------------------------- Factory

    /// <summary>Picks the live adapter when ApiClient exists, else simulation.</summary>
    public static class ProviderFactory
    {
        public static IAssetProvider Resolve(MonoBehaviour host)
        {
            if (host == null) throw new ArgumentNullException("host");
            if (ApiClientAdapter.IsAvailable())
            {
                var adapter = host.gameObject.AddComponent<ApiClientAdapter>();
                Debug.Log("[AR] Provider: live backend via ApiClient (" + adapter.BackendLabel + ")");
                return adapter;
            }
            var sim = host.gameObject.AddComponent<SimulationProvider>();
            Debug.Log("[AR] Provider: ApiClient not found — using offline SimulationProvider.");
            return sim;
        }
    }

    // -------------------------------------------------------------- Simulation

    /// <summary>
    /// Offline fallback serving 3 sample assets (MARK-001/002/003) with a small
    /// artificial delay so loading/timeout UI paths stay exercisable.
    /// Sample data mirrors database/seed.sql so SIM mode is representative of
    /// the live backend (same ids, names, statuses, record shapes).
    /// </summary>
    public class SimulationProvider : MonoBehaviour, IAssetProvider
    {
        [Tooltip("Simulated network latency in seconds.")]
        public float simulatedDelay = 0.4f;

        public string BackendLabel { get { return "Simulation (offline)"; } }

        private static readonly List<AssetDto> Assets = new List<AssetDto>
        {
            new AssetDto {
                id = "AST-001", name = "Epson EB-X49 Projector", category = "AV Equipment",
                location = "CL-6", status = "operational",
                description = "3LCD XGA projector, 3600 lumens. Ceiling-mounted in Computer Lab 6, used for lectures and demos.",
                imageUrl = "https://example.com/images/ast-001.jpg", modelUrl = "",
                lastMaintenance = "2026-07-15", nextMaintenance = "2026-10-15",
                markerId = "MARK-001"
            },
            new AssetDto {
                id = "AST-002", name = "Creality Ender-3 3D Printer", category = "Fabrication",
                location = "FAB-LAB", status = "operational",
                description = "FDM 3D printer, 220x220x250mm build volume. Shared fabrication-lab asset.",
                imageUrl = "https://example.com/images/ast-002.jpg", modelUrl = "",
                lastMaintenance = "2026-08-20", nextMaintenance = "2026-11-20",
                markerId = "MARK-002"
            },
            new AssetDto {
                id = "AST-003", name = "Tektronix TBS1052B Oscilloscope", category = "Lab Equipment",
                location = "FAB-LAB", status = "under_maintenance",
                description = "50 MHz 2-channel digital storage oscilloscope on electronics bench. Currently in calibration.",
                imageUrl = "https://example.com/images/ast-003.jpg", modelUrl = "",
                lastMaintenance = "2026-06-01", nextMaintenance = "2026-10-10",
                markerId = "MARK-003"
            },
        };

        private static readonly Dictionary<string, List<MaintenanceRecordDto>> History =
            new Dictionary<string, List<MaintenanceRecordDto>>
        {
            { "AST-001", new List<MaintenanceRecordDto> {
                new MaintenanceRecordDto { id="MNT-001", assetId="AST-001", date="2026-07-15", type="preventive", technician="R. Kumar", notes="Filter cleaned, lamp hours checked (812h). Focus recalibrated." },
                new MaintenanceRecordDto { id="MNT-002", assetId="AST-001", date="2026-04-10", type="corrective", technician="R. Kumar", notes="Replaced HDMI cable, fixed intermittent signal drop." },
            }},
            { "AST-002", new List<MaintenanceRecordDto> {
                new MaintenanceRecordDto { id="MNT-003", assetId="AST-002", date="2026-08-20", type="preventive", technician="S. Iyer", notes="Bed levelled, nozzle replaced, belts tensioned." },
            }},
            { "AST-003", new List<MaintenanceRecordDto> {
                new MaintenanceRecordDto { id="MNT-004", assetId="AST-003", date="2026-06-01", type="calibration", technician="P. Nair", notes="Annual calibration started. Channel 2 probe reads 5% high; sent for probe replacement." },
            }},
        };

        public IEnumerator GetAssetByMarker(string markerId, Action<AssetDto> ok, Action<string> err)
        {
            yield return new WaitForSeconds(simulatedDelay);
            AssetDto found = Assets.Find(a => string.Equals(a.markerId, markerId, StringComparison.OrdinalIgnoreCase));
            if (found != null) { if (ok != null) ok(found); }
            else if (err != null) err("No asset linked to marker '" + markerId + "'.");
        }

        public IEnumerator GetAssetById(string id, Action<AssetDto> ok, Action<string> err)
        {
            yield return new WaitForSeconds(simulatedDelay);
            AssetDto found = Assets.Find(a => string.Equals(a.id, id, StringComparison.OrdinalIgnoreCase));
            if (found != null) { if (ok != null) ok(found); }
            else if (err != null) err("Asset '" + id + "' not found.");
        }

        public IEnumerator GetMaintenance(string assetId, Action<List<MaintenanceRecordDto>> ok, Action<string> err)
        {
            yield return new WaitForSeconds(simulatedDelay);
            List<MaintenanceRecordDto> list;
            if (History.TryGetValue(assetId ?? string.Empty, out list))
            { if (ok != null) ok(new List<MaintenanceRecordDto>(list)); }
            else if (ok != null) ok(new List<MaintenanceRecordDto>());
        }

        public IEnumerator ReportIssue(IssueDraftDto draft, Action<IssueDto> ok, Action<string> err)
        {
            yield return new WaitForSeconds(simulatedDelay);
            if (draft == null || string.IsNullOrEmpty(draft.assetId) || string.IsNullOrEmpty(draft.title))
            { if (err != null) err("Asset and title are required."); yield break; }
            if (ok != null) ok(new IssueDto
            {
                id = "SIM-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss"),
                assetId = draft.assetId, title = draft.title,
                description = draft.description, reportedBy = draft.reportedBy,
                status = "open", reportedAt = DateTime.UtcNow.ToString("o")
            });
        }
    }

    // ------------------------------------------------------- Live API adapter

    /// <summary>
    /// Bridges IAssetProvider to DEV 2's ApiClient via reflection so this file
    /// compiles with or without ArPassport.Core present. Verified against the
    /// real Assets/Scripts/Core/ApiClient.cs:
    ///   * methods return Coroutine and use (code, message) error callbacks;
    ///   * GetAssetByMarker resolves to an assetId STRING ({assetId}), so the
    ///     adapter chains marker -> assetId -> Asset internally;
    ///   * Core models convert to local DTOs through a JsonUtility round-trip
    ///     (field names match the shared contract 1:1).
    /// Callback delegates are built with exact signature matches (generic
    /// forwarder instantiated per payload type) — no variance tricks.
    /// Every request is bounded by ApiConfig.GetTimeoutSeconds() (10s default).
    /// </summary>
    public class ApiClientAdapter : MonoBehaviour, IAssetProvider
    {
        private const string CorePrefix = "ArPassport.Core.";
        private const string AssemblySuffix = ", Assembly-CSharp";

        public string BackendLabel { get { return "Live: " + BackendConfigReader.BaseUrl; } }

        private static float TimeoutSeconds() { return BackendConfigReader.TimeoutSeconds(); }

        /// <summary>True when the Core client + models are present. Never touches
        /// ApiClient.Instance here (it auto-creates a GameObject as a side effect).</summary>
        public static bool IsAvailable()
        {
            try
            {
                Type api = Type.GetType(CorePrefix + "ApiClient" + AssemblySuffix);
                if (api == null) return false;
                if (api.GetMethod("GetAssetByMarker", BindingFlags.Public | BindingFlags.Instance) == null) return false;
                if (api.GetMethod("GetAssetById", BindingFlags.Public | BindingFlags.Instance) == null) return false;
                if (Type.GetType(CorePrefix + "Asset" + AssemblySuffix) == null) return false;
                return true;
            }
            catch { return false; }
        }

        private static object ApiInstance()
        {
            Type t = Type.GetType(CorePrefix + "ApiClient" + AssemblySuffix);
            if (t == null) return null;
            PropertyInfo p = t.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (p != null) return p.GetValue(null, null);
            FieldInfo f = t.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
            if (f != null) return f.GetValue(null);
            return null;
        }

        public IEnumerator GetAssetByMarker(string markerId, Action<AssetDto> ok, Action<string> err)
        {
            float deadline = Time.unscaledTime + TimeoutSeconds();
            // Step 1: marker -> assetId (Core returns the id string, not the asset).
            string assetId = null;
            string errCode = null, errMsg = null;
            yield return StartCoroutine(CallCoreMethod("GetAssetByMarker", markerId,
                v => { assetId = v as string; },
                (c, m) => { errCode = c; errMsg = m; },
                deadline));
            if (!string.IsNullOrEmpty(errCode))
            {
                if (err != null) err(FormatError(errCode, errMsg));
                yield break;
            }
            if (string.IsNullOrEmpty(assetId))
            {
                if (err != null) err("Empty marker response from server.");
                yield break;
            }
            // Step 2: assetId -> Asset (shares the original deadline).
            yield return StartCoroutine(GetAssetById(assetId, ok, err, deadline));
        }

        public IEnumerator GetAssetById(string id, Action<AssetDto> ok, Action<string> err)
        {
            return GetAssetById(id, ok, err, Time.unscaledTime + TimeoutSeconds());
        }

        private IEnumerator GetAssetById(string id, Action<AssetDto> ok, Action<string> err, float deadline)
        {
            AssetDto dto = null;
            string errCode = null, errMsg = null;
            yield return StartCoroutine(CallCoreMethod("GetAssetById", id,
                v => { dto = ConvertJson<AssetDto>(v); },
                (c, m) => { errCode = c; errMsg = m; },
                deadline));
            if (!string.IsNullOrEmpty(errCode))
            {
                if (err != null) err(FormatError(errCode, errMsg));
                yield break;
            }
            if (dto == null)
            {
                if (err != null) err("Response parse failed.");
                yield break;
            }
            if (ok != null) ok(dto);
        }

        public IEnumerator GetMaintenance(string assetId, Action<List<MaintenanceRecordDto>> ok, Action<string> err)
        {
            float deadline = Time.unscaledTime + TimeoutSeconds();
            List<MaintenanceRecordDto> rows = null;
            string errCode = null, errMsg = null;
            yield return StartCoroutine(CallCoreMethod("GetMaintenance", assetId,
                v =>
                {
                    // Core payload is List<MaintenanceRecord>; JsonUtility cannot
                    // serialize a bare List, so convert row by row.
                    var outList = new List<MaintenanceRecordDto>();
                    var src = v as System.Collections.IList;
                    if (src != null)
                    {
                        foreach (var item in src)
                        {
                            if (item == null) continue;
                            try { outList.Add(JsonUtility.FromJson<MaintenanceRecordDto>(JsonUtility.ToJson(item))); }
                            catch { /* skip malformed row */ }
                        }
                    }
                    rows = outList;
                },
                (c, m) => { errCode = c; errMsg = m; },
                deadline));
            if (!string.IsNullOrEmpty(errCode))
            {
                if (err != null) err(FormatError(errCode, errMsg));
                yield break;
            }
            if (ok != null) ok(rows ?? new List<MaintenanceRecordDto>());
        }

        public IEnumerator ReportIssue(IssueDraftDto draft, Action<IssueDto> ok, Action<string> err)
        {
            float deadline = Time.unscaledTime + TimeoutSeconds();
            object coreDraft = null;
            try
            {
                Type draftType = Type.GetType(CorePrefix + "IssueDraft" + AssemblySuffix);
                if (draftType == null) throw new Exception("Core type IssueDraft not found.");
                coreDraft = JsonUtility.FromJson(JsonUtility.ToJson(draft), draftType);
                if (coreDraft == null) throw new Exception("Draft conversion returned null.");
            }
            catch (Exception e)
            {
                if (err != null) err("Issue draft conversion failed: " + e.Message);
                yield break;
            }
            IssueDto dto = null;
            string errCode = null, errMsg = null;
            yield return StartCoroutine(CallCoreMethod("ReportIssue", coreDraft,
                v => { dto = ConvertJson<IssueDto>(v); },
                (c, m) => { errCode = c; errMsg = m; },
                deadline));
            if (!string.IsNullOrEmpty(errCode))
            {
                if (err != null) err(FormatError(errCode, errMsg));
                yield break;
            }
            if (dto == null)
            {
                if (err != null) err("Response parse failed.");
                yield break;
            }
            if (ok != null) ok(dto);
        }

        // ------------------------------------------------------- core driver

        /// <summary>
        /// Invokes one ApiClient method by name. Callback delegate types are
        /// read from the real MethodInfo (no hardcoded generic signatures), the
        /// success delegate is built with an exact-match generic forwarder, and
        /// the wait is bounded by <paramref name="deadline"/> (handles both
        /// Coroutine and IEnumerator returns).
        /// </summary>
        private IEnumerator CallCoreMethod(string methodName, object arg,
            Action<object> onOkValue, Action<string, string> onErr, float deadline)
        {
            Type apiType = Type.GetType(CorePrefix + "ApiClient" + AssemblySuffix);
            object api = ApiInstance();
            if (apiType == null || api == null)
            {
                if (onErr != null) onErr("NO_API", "Backend client unavailable.");
                yield break;
            }
            MethodInfo m = apiType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            if (m == null)
            {
                if (onErr != null) onErr("NO_API", "Backend method missing: " + methodName);
                yield break;
            }
            System.Reflection.ParameterInfo[] ps = m.GetParameters();
            if (ps.Length != 3)
            {
                if (onErr != null) onErr("NO_API", "Unexpected backend signature: " + methodName);
                yield break;
            }

            bool gotResult = false;
            object value = null;

            // Success delegate: exact signature match via a per-payload forwarder.
            Delegate okDel;
            try
            {
                Type okCbType = ps[1].ParameterType;
                Type payload = okCbType.IsGenericType
                    && okCbType.GetGenericTypeDefinition() == typeof(Action<>)
                    ? okCbType.GetGenericArguments()[0] : null;
                if (payload == null)
                {
                    if (onErr != null) onErr("NO_API", "Unexpected success callback: " + okCbType.Name);
                    yield break;
                }
                object fwd = Activator.CreateInstance(typeof(CoreForwarder<>).MakeGenericType(payload));
                ((ICoreForwarder)fwd).Delivered = o => { value = o; gotResult = true; };
                okDel = Delegate.CreateDelegate(okCbType, fwd, "OnOk");
            }
            catch (Exception e)
            {
                if (onErr != null) onErr("NO_API", "Callback bind failed: " + e.Message);
                yield break;
            }

            // Error delegate: the real signature is Action<string,string>.
            Delegate errDel;
            try
            {
                Type errCbType = ps[2].ParameterType;
                Action<string, string> errAct = (c, msg) =>
                {
                    gotResult = true;
                    value = new CoreError { Code = c, Message = msg };
                };
                errDel = errCbType == typeof(Action<string, string>)
                    ? (Delegate)errAct
                    : Delegate.CreateDelegate(errCbType, errAct.Target, errAct.Method);
            }
            catch (Exception e)
            {
                if (onErr != null) onErr("NO_API", "Error-callback bind failed: " + e.Message);
                yield break;
            }

            object ret;
            try
            {
                ret = m.Invoke(api, new object[] { arg, okDel, errDel });
            }
            catch (Exception e)
            {
                if (onErr != null) onErr("NO_API", "Backend call failed: " + Unwrap(e));
                yield break;
            }

            // Bounded wait. ApiClient always fires exactly one callback; the
            // deadline additionally guards against hung requests (slow network).
            bool timedOut = false;
            if (ret is IEnumerator enumer)
            {
                Coroutine c = null;
                try { c = StartCoroutine(enumer); }
                catch (Exception e)
                {
                    if (onErr != null) onErr("NO_API", "Could not start backend routine: " + e.Message);
                    yield break;
                }
                while (!gotResult)
                {
                    if (Time.unscaledTime > deadline)
                    {
                        timedOut = true;
                        try { if (c != null) StopCoroutine(c); } catch { /* already finished */ }
                        break;
                    }
                    yield return null;
                }
            }
            else if (ret is Coroutine running)
            {
                while (!gotResult)
                {
                    if (Time.unscaledTime > deadline)
                    {
                        timedOut = true;
                        try { StopCoroutine(running); } catch { /* already finished */ }
                        break;
                    }
                    yield return null;
                }
            }
            else if (!gotResult)
            {
                if (onErr != null) onErr("NO_API",
                    "Backend returned " + (ret == null ? "null" : ret.GetType().Name) + ".");
                yield break;
            }

            if (timedOut)
            {
                if (onErr != null) onErr("TIMEOUT",
                    "Request timed out after " + Mathf.RoundToInt(TimeoutSeconds()) + "s.");
                yield break;
            }
            CoreError ce = value as CoreError;
            if (ce != null)
            {
                if (onErr != null) onErr(ce.Code ?? "UNKNOWN", ce.Message);
                yield break;
            }
            if (onOkValue != null) onOkValue(value);
        }

        private static TDto ConvertJson<TDto>(object coreObj) where TDto : new()
        {
            if (coreObj == null) return default(TDto);
            try { return JsonUtility.FromJson<TDto>(JsonUtility.ToJson(coreObj)); }
            catch { return default(TDto); }
        }

        private static string FormatError(string code, string message)
        {
            if (string.IsNullOrEmpty(message)) return string.IsNullOrEmpty(code) ? "Unknown error." : code;
            if (string.IsNullOrEmpty(code)) return message;
            return message + " [" + code + "]";
        }

        private static string Unwrap(Exception e)
        {
            if (e is TargetInvocationException && e.InnerException != null)
                return e.InnerException.Message;
            return e.Message;
        }

        /// <summary>Exact-signature success forwarder, instantiated per payload type.</summary>
        private interface ICoreForwarder
        {
            Action<object> Delivered { set; }
        }

        private class CoreForwarder<T> : ICoreForwarder
        {
            public Action<object> Delivered { get; set; }
            public void OnOk(T v)
            {
                if (Delivered != null) Delivered(v);
            }
        }

        private class CoreError
        {
            public string Code;
            public string Message;
        }
    }
}
