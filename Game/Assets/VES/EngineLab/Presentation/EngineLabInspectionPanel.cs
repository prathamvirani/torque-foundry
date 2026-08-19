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
        [SerializeField] private InlineFourVisualizer mechanismVisualizer;
        [SerializeField] private InlineFourEngineContextVisualizer contextVisualizer;
        [SerializeField] private EngineLabInspectionCamera inspectionCamera;
        [SerializeField] private Rect panelRect = new Rect(16f, 16f, 330f, 510f);

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
            if (!Application.isPlaying || controller == null || mechanismVisualizer == null || contextVisualizer == null)
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
            string playLabel = mechanismVisualizer.IsTeachingAnimationPlaying ? "Pause" : "Play";
            if (GUILayout.Button(playLabel))
            {
                mechanismVisualizer.SetTeachingAnimationPlaying(!mechanismVisualizer.IsTeachingAnimationPlaying);
            }

            float crankAngleDeg = mechanismVisualizer.CurrentCrankAngleDeg;
            GUILayout.Label($"Crank angle: {crankAngleDeg:0.0}°");
            float requestedAngleDeg = GUILayout.HorizontalSlider(crankAngleDeg, 0f, 360f);
            if (!Mathf.Approximately(requestedAngleDeg, crankAngleDeg))
            {
                mechanismVisualizer.SetTeachingAnimationPlaying(false);
                mechanismVisualizer.SetCrankAngleDeg(requestedAngleDeg);
            }

            float teachingRpm = mechanismVisualizer.TeachingAnimationRpm;
            GUILayout.Label($"Teaching animation: {teachingRpm:0} rpm");
            float requestedTeachingRpm = GUILayout.HorizontalSlider(teachingRpm, 0f, 300f);
            if (!Mathf.Approximately(requestedTeachingRpm, teachingRpm))
                mechanismVisualizer.SetTeachingAnimationRpm(requestedTeachingRpm);
            GUILayout.Label($"Simulated operating point: {controller.EngineSpeedRpm:0} rpm (unchanged)");

            GUILayout.Space(8f);
            GUILayout.Label("Mechanism Visibility");
            SetToggle("Rotating assembly", mechanismVisualizer.IsRotatingAssemblyVisible,
                mechanismVisualizer.SetRotatingAssemblyVisible);
            SetToggle("Pistons and rods", mechanismVisualizer.ArePistonsAndRodsVisible,
                mechanismVisualizer.SetPistonsAndRodsVisible);
            SetToggle("Bore guides", mechanismVisualizer.AreBoreGuidesVisible,
                mechanismVisualizer.SetBoreGuidesVisible);

            GUILayout.Space(4f);
            GUILayout.Label("Context Visibility");
            SetToggle("Cylinder liners", contextVisualizer.AreCylinderLinersVisible,
                contextVisualizer.SetCylinderLinersVisible);
            SetToggle("Block envelope", contextVisualizer.IsBlockEnvelopeVisible,
                contextVisualizer.SetBlockEnvelopeVisible);
            SetToggle("Deck plane", contextVisualizer.IsDeckPlaneVisible,
                contextVisualizer.SetDeckPlaneVisible);
            SetToggle("Head envelope", contextVisualizer.IsHeadEnvelopeVisible,
                contextVisualizer.SetHeadEnvelopeVisible);
            GUILayout.EndArea();
        }

        private void ResolveReferences()
        {
            if (controller == null) controller = GetComponent<EngineLabController>();
            if (mechanismVisualizer == null) mechanismVisualizer = GetComponent<InlineFourVisualizer>();
            if (contextVisualizer == null) contextVisualizer = GetComponent<InlineFourEngineContextVisualizer>();
            if (inspectionCamera == null) inspectionCamera = Object.FindAnyObjectByType<EngineLabInspectionCamera>();
        }

        private static void SetToggle(string label, bool currentValue, System.Action<bool> setter)
        {
            bool requestedValue = GUILayout.Toggle(currentValue, label);
            if (requestedValue != currentValue) setter(requestedValue);
        }
    }
}
