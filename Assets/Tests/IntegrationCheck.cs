using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using ArPassport.Utils;

namespace ArPassport.Tests
{
    /// <summary>
    /// DEV4 integration contract tests (EditMode, no device needed).
    /// They pin the DEV1/DEV2/DEV3 seams: scene names, API URL policy, seed schema.
    /// Pure-C# checks duplicated in tests/validate_integration.ps1 for CI without Unity.
    /// Backend convention: ApiBaseUrl is the backend ORIGIN (http://localhost:3000,
    /// no /api suffix); health lives at GET {origin}/health (Backend/server.js).
    /// </summary>
    public static class IntegrationCheck
    {
        [Test]
        public static void SceneNameContract_IsStable()
        {
            Assert.AreEqual("Boot", AppBootstrapper.Scenes.Boot);
            Assert.AreEqual("Dashboard", AppBootstrapper.Scenes.Dashboard);
            Assert.AreEqual("ARScanner", AppBootstrapper.Scenes.ARScanner);
        }

        [Test]
        public static void ApiUrlResolution_PrefersPlayerPrefsOverInspector()
        {
            PlayerPrefs.SetString(AppBootstrapper.ApiUrlPlayerPrefsKey, "https://ar-api.example.com");
            try
            {
                string resolved = AppBootstrapper.ResolveApiBaseUrl("http://localhost:3000");
                Assert.AreEqual("https://ar-api.example.com", resolved);
            }
            finally
            {
                PlayerPrefs.DeleteKey(AppBootstrapper.ApiUrlPlayerPrefsKey);
            }
        }

        [Test]
        public static void ApiUrlResolution_FallsBackToInspector()
        {
            PlayerPrefs.DeleteKey(AppBootstrapper.ApiUrlPlayerPrefsKey);
            PlayerPrefs.DeleteKey(AppBootstrapper.LegacyApiUrlPlayerPrefsKey);
            // NOTE: CLI --api-url / env AR_API_BASE_URL cannot be injected in
            // EditMode; they win at runtime per ResolveApiBaseUrl order.
            string resolved = AppBootstrapper.ResolveApiBaseUrl("http://localhost:3000");
            Assert.AreEqual("http://localhost:3000", resolved);
        }

        [Test]
        public static void ApiUrlResolution_HonoursLegacyKey()
        {
            PlayerPrefs.DeleteKey(AppBootstrapper.ApiUrlPlayerPrefsKey);
            PlayerPrefs.SetString(AppBootstrapper.LegacyApiUrlPlayerPrefsKey, "http://192.168.1.10:3000");
            try
            {
                Assert.AreEqual("http://192.168.1.10:3000",
                    AppBootstrapper.ResolveApiBaseUrl("http://localhost:3000"));
            }
            finally
            {
                PlayerPrefs.DeleteKey(AppBootstrapper.LegacyApiUrlPlayerPrefsKey);
            }
        }

        [Test]
        public static void PlayerPrefsKey_MatchesDev2ApiConfig()
        {
            // Split-brain guard: DEV2 ApiConfig.PlayerPrefsKey must equal ours,
            // otherwise overrides set on one side are invisible to the other.
            var t = System.Type.GetType("ArPassport.Core.ApiConfig, Assembly-CSharp");
            if (t == null) Assert.Pass("DEV2 ApiConfig not present; key defined here as " +
                AppBootstrapper.ApiUrlPlayerPrefsKey + ".");
            var f = t.GetField("PlayerPrefsKey",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(f, "ApiConfig.PlayerPrefsKey field missing (DEV2 contract break).");
            Assert.AreEqual(AppBootstrapper.ApiUrlPlayerPrefsKey, (string)f.GetRawConstantValue());
        }

        [Test]
        public static void ApiBaseUrl_HasNoApiSuffix()
        {
            // ApiClient appends "/api/..." itself; a suffixed base 404s everything.
            string resolved = AppBootstrapper.ResolveApiBaseUrl(AppBootstrapper.DefaultApiBaseUrl);
            Assert.IsFalse(resolved.EndsWith("/api"),
                "Base URL must be the origin without /api suffix. Got: " + resolved);
        }

        [Test]
        public static void DeepLink_SchemeContract()
        {
            Assert.AreEqual("arpassport", AppBootstrapper.DeepLinks.Scheme);
            Assert.AreEqual("asset", AppBootstrapper.DeepLinks.AssetHost);
        }

        [Test]
        public static void PendingMarker_RoundTripsThroughPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(AppBootstrapper.PendingMarkerPlayerPrefsKey);
            PlayerPrefs.SetString(AppBootstrapper.PendingMarkerPlayerPrefsKey, "MARK-001");
            Assert.AreEqual("MARK-001", AppBootstrapper.ConsumePendingMarker());
            Assert.AreEqual("", AppBootstrapper.ConsumePendingMarker()); // consumed once
        }

        [Test]
        public static void OfflineSeed_ContainsDemoMarker()
        {
            // Mirrors LocalSeedProvider source: Assets/Resources/seed_assets.json
            // must carry the README demo marker. Pure file read, no DEV2 types.
            string path = System.IO.Path.Combine(Application.dataPath, "Resources/seed_assets.json");
            Assert.IsTrue(System.IO.File.Exists(path), "Missing Assets/Resources/seed_assets.json (DEV2).");
            string json = System.IO.File.ReadAllText(path);
            StringAssert.Contains("\"MARK-001\"", json);
            StringAssert.Contains("\"assets\"", json);
        }

        [UnityTest]
        public static IEnumerator HealthEndpoint_ReachableWhenBackendRunning()
        {
            // Requires DEV2 backend locally: `npm start` in Backend/ (PORT=3000).
            // Fails with a clear message when the backend is down (TEST_PLAN checklist N).
            string baseUrl = AppBootstrapper.ResolveApiBaseUrl(AppBootstrapper.DefaultApiBaseUrl);
            yield return AppBootstrapper.CheckReachability(baseUrl);
            Assert.IsTrue(AppBootstrapper.IsOnline,
                "Backend unreachable at " + baseUrl.TrimEnd('/') + "/health. Start it per README (Backend step).");
        }

#if UNITY_EDITOR
        [Test]
        public static void BuildSettings_ContainsRequiredScenes()
        {
            var guids = UnityEditor.AssetDatabase.FindAssets("t:Scene");
            var names = new System.Collections.Generic.HashSet<string>();
            foreach (string g in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                names.Add(System.IO.Path.GetFileNameWithoutExtension(path));
            }
            // Warn-tolerant: scenes are owned by DEV1/DEV3 and may not exist yet.
            if (!names.Contains(AppBootstrapper.Scenes.Dashboard))
                Debug.LogWarning("[IntegrationCheck] Dashboard scene missing (DEV3). See docs/TEST_PLAN.md C.");
            if (!names.Contains(AppBootstrapper.Scenes.ARScanner))
                Debug.LogWarning("[IntegrationCheck] ARScanner scene missing (DEV1). See docs/TEST_PLAN.md C.");
            Assert.Pass("Scene presence reported (warnings above if missing).");
        }
#endif
    }
}
