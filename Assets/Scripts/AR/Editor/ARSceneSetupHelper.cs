// Assets/Scripts/AR/Editor/ARSceneSetupHelper.cs
// Editor-only helper: rebuilds the ARScanner bootstrap object in the open scene.
// The scene boots itself at runtime (ARBootstrap auto-boot), so this is purely
// a convenience for inspecting wiring in the Editor.

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ArPassport.AR.Editor
{
    public static class ARSceneSetupHelper
    {
        [MenuItem("AR Passport/Setup ARScanner Scene")]
        public static void SetupScene()
        {
            ARBootstrap existing = Object.FindObjectOfType<ARBootstrap>();
            if (existing == null)
            {
                var go = new GameObject("ARBootstrap");
                go.AddComponent<ARBootstrap>();
                Debug.Log("[AR] Added ARBootstrap to scene '" +
                    EditorSceneManager.GetActiveScene().name + "'. Press Play — SIM 1/2/3 test the full flow.");
            }
            else Debug.Log("[AR] ARBootstrap already present in scene.");

            // Ensure the scene is the canonical ARScanner scene file.
            string path = "Assets/Scenes/ARScanner.unity";
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.path != path)
                Debug.LogWarning("[AR] Active scene is '" + scene.path +
                    "'. Save it as " + path + " when ready (File > Save As).");
        }

        [MenuItem("AR Passport/Simulate MARK-001")]
        public static void Sim1() { Simulate(0); }

        [MenuItem("AR Passport/Simulate MARK-002")]
        public static void Sim2() { Simulate(1); }

        [MenuItem("AR Passport/Simulate MARK-003")]
        public static void Sim3() { Simulate(2); }

        private static void Simulate(int index)
        {
            if (!EditorApplication.isPlaying)
            {
                Debug.LogWarning("[AR] Enter Play mode first, then simulate markers.");
                return;
            }
            ARMarkerTracker t = Object.FindObjectOfType<ARMarkerTracker>();
            if (t == null) Debug.LogError("[AR] No ARMarkerTracker in scene.");
            else t.SimulateMarkerByIndex(index);
        }
    }
}
#endif
