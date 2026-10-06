using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ArPassport.Utils
{
    /// <summary>
    /// DEV4-owned scene router. Thin wrapper over SceneManager with an optional
    /// loading screen. Scene names must match <see cref="AppBootstrapper.Scenes"/>.
    /// </summary>
    public class SceneNavigator : MonoBehaviour
    {
        private static SceneNavigator _instance;

        [Header("Loading UI (optional)")]
        [Tooltip("Root GameObject shown while a scene loads. May be left empty.")]
        public GameObject loadingScreen;

        [Tooltip("Minimum seconds the loading screen stays visible (avoids flicker).")]
        public float minLoadingSeconds = 0.4f;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            SetLoadingVisible(false);
        }

        // ---- Static API (usable without a scene instance) ------------------

        public static void LoadDashboard() => LoadScene(AppBootstrapper.Scenes.Dashboard);

        public static void LoadARScanner(string markerId = null)
        {
            if (!string.IsNullOrEmpty(markerId))
                PlayerPrefs.SetString(AppBootstrapper.PendingMarkerPlayerPrefsKey, markerId);
            LoadScene(AppBootstrapper.Scenes.ARScanner);
        }

        public static void LoadScene(string sceneName)
        {
            if (_instance != null)
                _instance.StartCoroutine(_instance.LoadRoutine(sceneName));
            else
                SceneManager.LoadScene(sceneName); // no loading screen available yet
        }

        // ---- Instance routine ------------------------------------------------

        private IEnumerator LoadRoutine(string sceneName)
        {
            SetLoadingVisible(true);
            float t0 = Time.realtimeSinceStartup;
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null) // scene missing from Build Settings
            {
                Debug.LogError("[SceneNavigator] Scene '" + sceneName +
                    "' failed to load. Add Boot, Dashboard, ARScanner to Build Settings. " +
                    "See docs/TEST_PLAN.md checklist C.");
                SetLoadingVisible(false);
                yield break;
            }
            while (!op.isDone) yield return null;
            float elapsed = Time.realtimeSinceStartup - t0;
            if (elapsed < minLoadingSeconds)
                yield return new WaitForSecondsRealtime(minLoadingSeconds - elapsed);
            SetLoadingVisible(false);
        }

        private void SetLoadingVisible(bool visible)
        {
            if (loadingScreen != null) loadingScreen.SetActive(visible);
        }
    }
}
