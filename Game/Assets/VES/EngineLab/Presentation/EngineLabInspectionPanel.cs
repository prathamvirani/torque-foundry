using UnityEngine;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    /// <summary>
    /// Lightweight teaching and inspection controls for the foundation scene.
    /// This panel changes presentation state only; simulated engine RPM remains
    /// owned by EngineLabController and is shown read-only for clarity.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EngineLabInspectionPanel : MonoBehaviour
    {
        [SerializeField] private EngineLabController controller;
        [SerializeField] private InlineFourVisualFidelityAssembly visualAssembly;
        [SerializeField] private EngineLabInspectionCamera inspectionCamera;
        [SerializeField] private Rect panelRect = new Rect(16f, 16f, 350f, 535f);

        private static readonly string[] InspectionModeLabels =
        {
            "Full Engine",
            "Cutaway",
            "Transparent Block / Head",
            "Rotating Assembly Only",
            "Valvetrain Only"
        };

        public Rect PanelRect => panelRect;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnValidate()
        {
            ResolveReferences();
            panelRect.width = Mathf.Max(280f, panelRect.width);
            panelRect.height = Mathf.Max(420f, panelRect.height);
        }

        public bool IsPointerOverPanel(Vector2 screenPosition)
        {
            Vector2 guiPosition = new Vector2(screenPosition.x, Screen.height - screenPosition.y);
            return panelRect.Contains(guiPosition);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || controller == null || visualAssembly == null)
                return;

            GUILayout.BeginArea(panelRect, GUI.skin.box);
            GUILayout.Label("ENGINE LAB — INSPECTION");
            GUILayout.Space(4f);

            GUILayout.Label("Camera");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Focus Engine")) inspectionCamera?.FocusEngine();
            if (GUILayout.Button("Reset View (Home)")) inspectionCamera?.ResetEngineView();
            GUILayout.EndHorizontal();
            GUILayout.Label("LMB orbit  •  MMB pan  •  wheel zoom  •  F focus");

            GUILayout.Space(8f);
            GUILayout.Label("Teaching Motion");
            string playLabel = visualAssembly.IsTeachingAnimationPlaying ? "Pause" : "Play";
            if (GUILayout.Button(playLabel))
            {
                visualAssembly.SetTeachingAnimationPlaying(!visualAssembly.IsTeachingAnimationPlaying);
            }

            float crankAngleDeg = visualAssembly.CurrentCrankAngleDeg;
            GUILayout.Label($"Crank angle: {crankAngleDeg:0.0}°");
            float requestedAngleDeg = GUILayout.HorizontalSlider(crankAngleDeg, 0f, 360f);
            if (!Mathf.Approximately(requestedAngleDeg, crankAngleDeg))
            {
                visualAssembly.SetTeachingAnimationPlaying(false);
                visualAssembly.SetCrankAngleDeg(requestedAngleDeg);
            }

            float teachingRpm = visualAssembly.TeachingAnimationRpm;
            GUILayout.Label($"Teaching animation: {teachingRpm:0} rpm");
            float requestedTeachingRpm = GUILayout.HorizontalSlider(teachingRpm, 0f, 300f);
            if (!Mathf.Approximately(requestedTeachingRpm, teachingRpm))
                visualAssembly.SetTeachingAnimationRpm(requestedTeachingRpm);
            GUILayout.Label($"Simulated operating point: {controller.EngineSpeedRpm:0} rpm (unchanged)");

            GUILayout.Space(8f);
            GUILayout.Label("Inspection Mode");
            int currentMode = (int)visualAssembly.InspectionMode;
            int requestedMode = GUILayout.SelectionGrid(currentMode, InspectionModeLabels, 1);
            if (requestedMode != currentMode)
            {
                visualAssembly.SetInspectionMode((EngineInspectionMode)requestedMode);
                inspectionCamera?.SetPivot(transform.TransformPoint(visualAssembly.RecommendedFocusPointLocal));
                inspectionCamera?.SetDistance(visualAssembly.RecommendedCameraDistanceM);
            }
            GUILayout.EndArea();
        }

        private void ResolveReferences()
        {
            if (controller == null) controller = GetComponent<EngineLabController>();
            if (visualAssembly == null) visualAssembly = GetComponent<InlineFourVisualFidelityAssembly>();
            if (inspectionCamera == null) inspectionCamera = Object.FindAnyObjectByType<EngineLabInspectionCamera>();
        }
    }
}
