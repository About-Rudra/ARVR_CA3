#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArPassport.Dashboard
{
    /// <summary>
    /// Editor helper: builds the Dashboard scene contents in the currently open
    /// scene (Canvas + EventSystem + Dashboard root). Save the result as
    /// Assets/Scenes/Dashboard.unity. Runtime code also self-builds, so this is
    /// a convenience, not a requirement.
    /// Menu: ARPassport / Build Dashboard Scene.
    /// </summary>
    public static class DashboardSceneBuilder
    {
        [MenuItem("ARPassport/Build Dashboard Scene")]
        public static void BuildDashboardScene()
        {
            Canvas canvas = Object.FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("DashboardCanvas");
                Undo.RegisterCreatedObjectUndo(canvasGO, "Create Dashboard Canvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1280, 800);
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = 0.5f;
                canvasGO.AddComponent<GraphicRaycaster>();
            }

            EventSystem es = Object.FindObjectOfType<EventSystem>();
            if (es == null)
            {
                GameObject esGO = new GameObject("EventSystem");
                Undo.RegisterCreatedObjectUndo(esGO, "Create EventSystem");
                esGO.AddComponent<EventSystem>();
                esGO.AddComponent<StandaloneInputModule>();
            }

            GameObject root = GameObject.Find("Dashboard");
            if (root == null)
            {
                root = new GameObject("Dashboard");
                Undo.RegisterCreatedObjectUndo(root, "Create Dashboard Root");
            }
            DashboardController controller = root.GetComponent<DashboardController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<DashboardController>(root);
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Dashboard] Scene objects ready. Save as Assets/Scenes/Dashboard.unity and add it to Build Settings. " +
                      "UI is built at runtime by DashboardController, so no manual wiring is needed. " +
                      "AR scanner scene '" + DashboardController.ArScannerSceneName + "' is loaded by name when present.");
        }
    }
}
#endif
