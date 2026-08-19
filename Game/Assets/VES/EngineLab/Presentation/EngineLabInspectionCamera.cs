using UnityEngine;
using UnityEngine.InputSystem;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    /// <summary>
    /// Presentation-only orbit camera for inspecting the generated engine model.
    /// It owns no engineering state and cannot alter the simulated operating point.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public sealed class EngineLabInspectionCamera : MonoBehaviour
    {
        [SerializeField] private Transform focusTarget;
        [SerializeField] private EngineLabInspectionPanel inputBlocker;

        [Header("Default View")]
        [SerializeField] private Vector3 focusOffset = new Vector3(0f, 0.15f, 0f);
        [SerializeField] private float defaultYawDeg;
        [SerializeField] private float defaultPitchDeg;
        [SerializeField, Min(0.01f)] private float defaultDistanceM = 0.7f;

        [Header("View Limits")]
        [SerializeField, Min(0.01f)] private float minimumDistanceM = 0.22f;
        [SerializeField, Min(0.01f)] private float maximumDistanceM = 2.2f;
        [SerializeField] private float minimumPitchDeg = -35f;
        [SerializeField] private float maximumPitchDeg = 80f;
        [SerializeField, Min(0f)] private float maximumFocusOffsetM = 0.65f;

        [Header("Input Response")]
        [SerializeField, Min(0f)] private float orbitDegreesPerPixel = 0.2f;
        [SerializeField, Min(0f)] private float panScale = 0.0012f;
        [SerializeField, Min(0f)] private float zoomScale = 0.16f;

        private Vector3 defaultPivot;
        private Vector3 pivot;
        private float yawDeg;
        private float pitchDeg;
        private float distanceM;
        private bool initialized;

        public Vector3 Pivot => pivot;
        public float YawDeg => yawDeg;
        public float PitchDeg => pitchDeg;
        public float DistanceM => distanceM;
        public float MinimumDistanceM => minimumDistanceM;
        public float MaximumDistanceM => maximumDistanceM;

        private void Reset()
        {
            ResolveReferences();
            ValidateSettings();
        }

        private void OnValidate()
        {
            ResolveReferences();
            ValidateSettings();
            if (Application.isPlaying && initialized) ApplyCameraPose();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            ResetEngineView();
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying) return;
            if (!initialized) ResetEngineView();

            HandleKeyboardCommands();
            HandleMouseInput();
            ApplyCameraPose();
        }

        public void FocusEngine()
        {
            RefreshDefaultPivot();
            pivot = defaultPivot;
            ApplyCameraPose();
        }

        public void ResetEngineView()
        {
            ValidateSettings();
            RefreshDefaultPivot();
            pivot = defaultPivot;
            yawDeg = NormalizeYaw(defaultYawDeg);
            pitchDeg = Mathf.Clamp(defaultPitchDeg, minimumPitchDeg, maximumPitchDeg);
            distanceM = Mathf.Clamp(defaultDistanceM, minimumDistanceM, maximumDistanceM);
            initialized = true;
            ApplyCameraPose();
        }

        public void SetOrbit(float yaw, float pitch)
        {
            yawDeg = NormalizeYaw(yaw);
            pitchDeg = Mathf.Clamp(pitch, minimumPitchDeg, maximumPitchDeg);
            initialized = true;
            ApplyCameraPose();
        }

        public void SetDistance(float distance)
        {
            distanceM = Mathf.Clamp(distance, minimumDistanceM, maximumDistanceM);
            initialized = true;
            ApplyCameraPose();
        }

        public void SetPivot(Vector3 requestedPivot)
        {
            RefreshDefaultPivot();
            pivot = defaultPivot + Vector3.ClampMagnitude(requestedPivot - defaultPivot, maximumFocusOffsetM);
            initialized = true;
            ApplyCameraPose();
        }

        private void HandleKeyboardCommands()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.fKey.wasPressedThisFrame) FocusEngine();
            if (keyboard.homeKey.wasPressedThisFrame) ResetEngineView();
        }

        private void HandleMouseInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            Vector2 pointerPosition = mouse.position.ReadValue();
            if (inputBlocker != null && inputBlocker.IsPointerOverPanel(pointerPosition)) return;

            Vector2 delta = mouse.delta.ReadValue();
            if (mouse.leftButton.isPressed)
            {
                yawDeg = NormalizeYaw(yawDeg + delta.x * orbitDegreesPerPixel);
                pitchDeg = Mathf.Clamp(pitchDeg - delta.y * orbitDegreesPerPixel, minimumPitchDeg, maximumPitchDeg);
            }

            if (mouse.middleButton.isPressed)
            {
                Vector3 pan = (-transform.right * delta.x - transform.up * delta.y) * (panScale * distanceM);
                SetPivot(pivot + pan);
            }

            float scrollSteps = mouse.scroll.ReadValue().y / 120f;
            if (Mathf.Abs(scrollSteps) > 0.001f)
            {
                SetDistance(distanceM * Mathf.Exp(-scrollSteps * zoomScale));
            }
        }

        private void ApplyCameraPose()
        {
            Quaternion orbitRotation = Quaternion.Euler(pitchDeg, yawDeg, 0f);
            transform.SetPositionAndRotation(
                pivot + orbitRotation * (Vector3.back * distanceM),
                orbitRotation);
        }

        private void ResolveReferences()
        {
            if (focusTarget == null)
            {
                GameObject engineLab = GameObject.Find("Engine Lab");
                if (engineLab != null) focusTarget = engineLab.transform;
            }

            if (inputBlocker == null) inputBlocker = Object.FindAnyObjectByType<EngineLabInspectionPanel>();
        }

        private void RefreshDefaultPivot()
        {
            if (focusTarget != null)
            {
                defaultPivot = focusTarget.TransformPoint(focusOffset);
            }
            else if (!initialized)
            {
                defaultPivot = transform.position + transform.forward * defaultDistanceM;
            }
        }

        private void ValidateSettings()
        {
            minimumDistanceM = Mathf.Max(0.01f, minimumDistanceM);
            maximumDistanceM = Mathf.Max(minimumDistanceM, maximumDistanceM);
            defaultDistanceM = Mathf.Clamp(defaultDistanceM, minimumDistanceM, maximumDistanceM);
            maximumPitchDeg = Mathf.Max(minimumPitchDeg, maximumPitchDeg);
            maximumFocusOffsetM = Mathf.Max(0f, maximumFocusOffsetM);
            orbitDegreesPerPixel = Mathf.Max(0f, orbitDegreesPerPixel);
            panScale = Mathf.Max(0f, panScale);
            zoomScale = Mathf.Max(0f, zoomScale);
        }

        private static float NormalizeYaw(float yaw)
        {
            return Mathf.Repeat(yaw + 180f, 360f) - 180f;
        }
    }
}
