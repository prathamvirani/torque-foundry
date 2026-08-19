using UnityEngine;
using VehicleEngineeringSandbox.Core.ICE;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    /// <summary>
    /// Procedural, mechanically constrained inline-four teaching visualizer.
    /// One Unity unit equals one metre. Animation RPM is deliberately independent
    /// from simulated engine RPM so the mechanism can be inspected in slow motion.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngineLabController))]
    public sealed class InlineFourVisualizer : MonoBehaviour
    {
        private static readonly float[] CrankPhaseDeg = { 0f, 180f, 180f, 0f };

        [SerializeField] private EngineLabController controller;

        [Header("Teaching Animation")]
        [SerializeField] private bool animateInPlayMode = true;
        [SerializeField, Min(0f)] private float teachingAnimationRpm = 60f;
        [SerializeField, Range(0f, 360f)] private float previewCrankAngleDeg;

        [Header("Presentation Geometry")]
        [SerializeField, Min(1.01f)] private float cylinderSpacingBoreMultiplier = 1.15f;

        [Header("Inspection Visibility")]
        [SerializeField] private bool showRotatingAssembly = true;
        [SerializeField] private bool showPistonsAndRods = true;
        [SerializeField] private bool showBoreGuides = true;

        private Transform generatedRoot;
        private Transform rotatingAssemblyGroup;
        private Transform pistonsAndRodsGroup;
        private Transform boreGuidesGroup;
        private Transform[] pistons;
        private Transform[] wristPins;
        private Transform[] connectingRods;
        private Transform[] crankPins;
        private Transform[,] crankWebs;
        private Transform[,] counterweights;
        private float[] cylinderX;

        private float lastBoreMm = -1f;
        private float lastStrokeMm = -1f;
        private float lastRodLengthMm = -1f;
        private int lastCylinderCount = -1;
        private float lastSpacingMultiplier = -1f;
        private bool rebuildRequested = true;
        private float animatedCrankAngleDeg;

        private Material crankMaterial;
        private Material rodMaterial;
        private Material pistonMaterial;
        private Material guideMaterial;

        public float CylinderSpacingBoreMultiplier => cylinderSpacingBoreMultiplier;
        public bool IsTeachingAnimationPlaying => animateInPlayMode;
        public float TeachingAnimationRpm => teachingAnimationRpm;
        public float CurrentCrankAngleDeg => Application.isPlaying && animateInPlayMode
            ? animatedCrankAngleDeg
            : previewCrankAngleDeg;
        public bool IsRotatingAssemblyVisible => showRotatingAssembly;
        public bool ArePistonsAndRodsVisible => showPistonsAndRods;
        public bool AreBoreGuidesVisible => showBoreGuides;

        public void SetTeachingAnimationPlaying(bool isPlaying)
        {
            if (animateInPlayMode && !isPlaying && Application.isPlaying)
                previewCrankAngleDeg = Mathf.Repeat(animatedCrankAngleDeg, 360f);
            else if (!animateInPlayMode && isPlaying)
                animatedCrankAngleDeg = previewCrankAngleDeg;

            animateInPlayMode = isPlaying;
        }

        public void SetTeachingAnimationRpm(float rpm)
        {
            teachingAnimationRpm = Mathf.Clamp(rpm, 0f, 300f);
        }

        public void SetCrankAngleDeg(float angleDeg)
        {
            previewCrankAngleDeg = Mathf.Repeat(angleDeg, 360f);
            animatedCrankAngleDeg = previewCrankAngleDeg;
            if (generatedRoot != null && pistons != null) UpdateMechanism(previewCrankAngleDeg);
        }

        public void SetRotatingAssemblyVisible(bool isVisible)
        {
            showRotatingAssembly = isVisible;
            ApplyInspectionVisibility();
        }

        public void SetPistonsAndRodsVisible(bool isVisible)
        {
            showPistonsAndRods = isVisible;
            ApplyInspectionVisibility();
        }

        public void SetBoreGuidesVisible(bool isVisible)
        {
            showBoreGuides = isVisible;
            ApplyInspectionVisibility();
        }

        private void Reset()
        {
            controller = GetComponent<EngineLabController>();
            rebuildRequested = true;
        }

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<EngineLabController>();
            rebuildRequested = true;
        }

        private void OnValidate()
        {
            if (controller == null) controller = GetComponent<EngineLabController>();
            teachingAnimationRpm = Mathf.Max(0f, teachingAnimationRpm);
            cylinderSpacingBoreMultiplier = Mathf.Max(1.01f, cylinderSpacingBoreMultiplier);
            rebuildRequested = true;
        }

        private void Update()
        {
            if (controller == null) return;

            if (GeometryChanged()) rebuildRequested = true;
            if (rebuildRequested) RebuildInternal();
            if (generatedRoot == null || pistons == null) return;

            float angleDeg;
            if (Application.isPlaying && animateInPlayMode)
            {
                animatedCrankAngleDeg = Mathf.Repeat(
                    animatedCrankAngleDeg + teachingAnimationRpm * 6f * Time.deltaTime,
                    360f);
                angleDeg = animatedCrankAngleDeg;
            }
            else
            {
                angleDeg = previewCrankAngleDeg;
                animatedCrankAngleDeg = previewCrankAngleDeg;
            }

            UpdateMechanism(angleDeg);
        }

        [ContextMenu("Rebuild I4 Preview")]
        public void RebuildPreview()
        {
            rebuildRequested = true;
            if (!Application.isPlaying) RebuildInternal();
        }

        private bool GeometryChanged()
        {
            return !Mathf.Approximately(lastBoreMm, controller.BoreMm)
                   || !Mathf.Approximately(lastStrokeMm, controller.StrokeMm)
                   || !Mathf.Approximately(lastRodLengthMm, controller.ConnectingRodLengthMm)
                   || lastCylinderCount != controller.CylinderCount
                   || !Mathf.Approximately(lastSpacingMultiplier, cylinderSpacingBoreMultiplier);
        }

        private void RebuildInternal()
        {
            rebuildRequested = false;
            CleanupGenerated();

            lastBoreMm = controller.BoreMm;
            lastStrokeMm = controller.StrokeMm;
            lastRodLengthMm = controller.ConnectingRodLengthMm;
            lastCylinderCount = controller.CylinderCount;
            lastSpacingMultiplier = cylinderSpacingBoreMultiplier;

            if (controller.CylinderCount != 4)
            {
                Debug.LogWarning("InlineFourVisualizer v0.1 only supports a four-cylinder inline crank layout. The core engine model still accepts other cylinder counts.", this);
                return;
            }

            float boreM = Mathf.Max(0.001f, controller.BoreMm / 1000f);
            float strokeM = Mathf.Max(0.001f, controller.StrokeMm / 1000f);
            float crankRadiusM = strokeM * 0.5f;
            float rodLengthM = Mathf.Max(controller.ConnectingRodLengthMm / 1000f, crankRadiusM + 0.000001f);
            float spacingM = boreM * cylinderSpacingBoreMultiplier;

            generatedRoot = new GameObject("Generated I4 Mechanism").transform;
            generatedRoot.SetParent(transform, false);
            generatedRoot.gameObject.hideFlags = HideFlags.DontSaveInEditor;
            rotatingAssemblyGroup = CreateGroup("Rotating Assembly");
            pistonsAndRodsGroup = CreateGroup("Pistons and Rods");
            boreGuidesGroup = CreateGroup("Bore Guides");

            CreateMaterials();

            pistons = new Transform[4];
            wristPins = new Transform[4];
            connectingRods = new Transform[4];
            crankPins = new Transform[4];
            crankWebs = new Transform[4, 2];
            counterweights = new Transform[4, 2];
            cylinderX = new float[4];

            float pistonDiameterM = boreM * 0.90f;
            float pistonHeightM = Mathf.Clamp(strokeM * 0.28f, 0.012f, 0.045f);
            float wristPinRadiusM = Mathf.Max(0.003f, boreM * 0.055f);
            float crankPinRadiusM = Mathf.Max(0.004f, strokeM * 0.075f);
            float rodRadiusM = Mathf.Max(0.0035f, boreM * 0.045f);
            float webRadiusM = Mathf.Max(0.004f, strokeM * 0.06f);
            float pinLengthM = boreM * 0.64f;
            float webAxialOffsetM = pinLengthM * 0.5f + Mathf.Max(0.002f, boreM * 0.035f);

            CreateMainJournals(spacingM, boreM, strokeM);

            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * spacingM;
                cylinderX[i] = x;

                pistons[i] = CreatePiston($"Piston {i + 1}", pistonDiameterM, pistonHeightM);
                wristPins[i] = CreateCylinderBetween(
                    $"Wrist Pin {i + 1}",
                    pistonsAndRodsGroup,
                    new Vector3(x - pinLengthM * 0.5f, 0f, 0f),
                    new Vector3(x + pinLengthM * 0.5f, 0f, 0f),
                    wristPinRadiusM,
                    crankMaterial);
                connectingRods[i] = CreateCylinderBetween(
                    $"Connecting Rod {i + 1}",
                    pistonsAndRodsGroup,
                    Vector3.zero,
                    Vector3.up * rodLengthM,
                    rodRadiusM,
                    rodMaterial);
                crankPins[i] = CreateCylinderBetween(
                    $"Crank Pin {i + 1}",
                    rotatingAssemblyGroup,
                    new Vector3(x - pinLengthM * 0.5f, 0f, 0f),
                    new Vector3(x + pinLengthM * 0.5f, 0f, 0f),
                    crankPinRadiusM,
                    crankMaterial);

                for (int side = 0; side < 2; side++)
                {
                    string sideName = side == 0 ? "Front" : "Rear";
                    float webX = x + (side == 0 ? -webAxialOffsetM : webAxialOffsetM);
                    Vector3 crankCentre = new Vector3(webX, 0f, 0f);

                    crankWebs[i, side] = CreateCylinderBetween(
                        $"Crank Web {i + 1} {sideName}",
                        rotatingAssemblyGroup,
                        crankCentre,
                        crankCentre + Vector3.up * crankRadiusM,
                        webRadiusM,
                        crankMaterial);
                    counterweights[i, side] = CreateCylinderBetween(
                        $"Counterweight {i + 1} {sideName}",
                        rotatingAssemblyGroup,
                        crankCentre,
                        crankCentre + Vector3.down * crankRadiusM * 0.78f,
                        webRadiusM * 1.35f,
                        crankMaterial);
                }

                CreateBoreGuides(i + 1, x, boreM, strokeM, rodLengthM, pistonHeightM);
            }

            UpdateMechanism(previewCrankAngleDeg);
            ApplyInspectionVisibility();
        }

        private void UpdateMechanism(float baseCrankAngleDeg)
        {
            float strokeM = Mathf.Max(0.001f, controller.StrokeMm / 1000f);
            float crankRadiusM = strokeM * 0.5f;
            float rodLengthM = Mathf.Max(controller.ConnectingRodLengthMm / 1000f, crankRadiusM + 0.000001f);
            float boreM = Mathf.Max(0.001f, controller.BoreMm / 1000f);
            float pinLengthM = boreM * 0.64f;
            float webAxialOffsetM = pinLengthM * 0.5f + Mathf.Max(0.002f, boreM * 0.035f);

            for (int i = 0; i < 4; i++)
            {
                float angleDeg = baseCrankAngleDeg + CrankPhaseDeg[i];
                double angleRad = angleDeg * Mathf.Deg2Rad;

                float crankY = (float)SliderCrankKinematics.CrankPinYM(angleRad, crankRadiusM);
                float crankZ = (float)SliderCrankKinematics.CrankPinZM(angleRad, crankRadiusM);
                float pistonPinY = (float)SliderCrankKinematics.PistonPinHeightM(angleRad, crankRadiusM, rodLengthM);

                float x = cylinderX[i];
                Vector3 crankPin = new Vector3(x, crankY, crankZ);
                Vector3 wristPin = new Vector3(x, pistonPinY, 0f);
                Vector3 crankCentre = new Vector3(x, 0f, 0f);

                pistons[i].localPosition = wristPin;
                SetCylinderBetween(wristPins[i],
                    new Vector3(x - pinLengthM * 0.5f, pistonPinY, 0f),
                    new Vector3(x + pinLengthM * 0.5f, pistonPinY, 0f));
                SetCylinderBetween(connectingRods[i], crankPin, wristPin);
                SetCylinderBetween(crankPins[i],
                    crankPin + Vector3.left * pinLengthM * 0.5f,
                    crankPin + Vector3.right * pinLengthM * 0.5f);

                Vector3 crankOffset = crankPin - crankCentre;
                for (int side = 0; side < 2; side++)
                {
                    float webX = x + (side == 0 ? -webAxialOffsetM : webAxialOffsetM);
                    Vector3 webCrankCentre = new Vector3(webX, 0f, 0f);
                    Vector3 webCrankPin = webCrankCentre + crankOffset;
                    Vector3 counterweightTip = webCrankCentre - crankOffset * 0.78f;

                    SetCylinderBetween(crankWebs[i, side], webCrankCentre, webCrankPin);
                    SetCylinderBetween(counterweights[i, side], webCrankCentre, counterweightTip);
                }
            }
        }

        private void CreateMainJournals(float spacingM, float boreM, float strokeM)
        {
            float journalRadiusM = Mathf.Max(0.006f, strokeM * 0.09f);
            float journalLengthM = Mathf.Clamp(boreM * 0.24f, 0.012f, spacingM * 0.35f);

            // An inline-four conventionally has five main bearings bracketing four crank throws.
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2f) * spacingM;
                CreateCylinderBetween(
                    $"Main Journal {i + 1}",
                    rotatingAssemblyGroup,
                    new Vector3(x - journalLengthM * 0.5f, 0f, 0f),
                    new Vector3(x + journalLengthM * 0.5f, 0f, 0f),
                    journalRadiusM,
                    crankMaterial);
            }
        }

        private Transform CreatePiston(string objectName, float diameterM, float heightM)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = objectName;
            go.transform.SetParent(pistonsAndRodsGroup, false);
            go.transform.localScale = new Vector3(diameterM, heightM * 0.5f, diameterM);
            RemoveCollider(go);
            ApplyMaterial(go, pistonMaterial);
            return go.transform;
        }

        private Transform CreateCylinderBetween(
            string objectName,
            Transform parent,
            Vector3 a,
            Vector3 b,
            float radiusM,
            Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            RemoveCollider(go);
            ApplyMaterial(go, material);

            go.transform.localScale = new Vector3(radiusM * 2f, 0.5f, radiusM * 2f);
            SetCylinderBetween(go.transform, a, b);
            return go.transform;
        }

        private static void SetCylinderBetween(Transform cylinder, Vector3 a, Vector3 b)
        {
            Vector3 direction = b - a;
            float length = direction.magnitude;
            if (length < 0.000001f) length = 0.000001f;

            Vector3 scale = cylinder.localScale;
            scale.y = length * 0.5f;
            cylinder.localScale = scale;
            cylinder.localPosition = (a + b) * 0.5f;
            cylinder.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        }

        private void CreateBoreGuides(int cylinderNumber, float x, float boreM, float strokeM, float rodLengthM, float pistonHeightM)
        {
            float crankRadiusM = strokeM * 0.5f;
            float tdcPinY = rodLengthM + crankRadiusM;
            float bdcPinY = rodLengthM - crankRadiusM;
            float heightM = (tdcPinY - bdcPinY) + pistonHeightM * 1.7f;
            float centreY = (tdcPinY + bdcPinY) * 0.5f;
            float guideRadiusM = Mathf.Max(0.0012f, boreM * 0.012f);
            float radius = boreM * 0.5f;

            Vector3[] offsets =
            {
                new Vector3(radius, 0f, 0f),
                new Vector3(-radius, 0f, 0f),
                new Vector3(0f, 0f, radius),
                new Vector3(0f, 0f, -radius)
            };

            for (int j = 0; j < offsets.Length; j++)
            {
                Vector3 centre = new Vector3(x, centreY, 0f) + offsets[j];
                CreateCylinderBetween(
                    $"Cylinder {cylinderNumber} Bore Guide {j + 1}",
                    boreGuidesGroup,
                    centre + Vector3.down * heightM * 0.5f,
                    centre + Vector3.up * heightM * 0.5f,
                    guideRadiusM,
                    guideMaterial);
            }
        }

        private void CreateMaterials()
        {
            crankMaterial = CreateMaterial(new Color(0.16f, 0.18f, 0.20f, 1f));
            rodMaterial = CreateMaterial(new Color(0.52f, 0.55f, 0.58f, 1f));
            pistonMaterial = CreateMaterial(new Color(0.72f, 0.74f, 0.76f, 1f));
            guideMaterial = CreateMaterial(new Color(0.20f, 0.55f, 0.90f, 1f));
        }

        private Transform CreateGroup(string groupName)
        {
            Transform group = new GameObject(groupName).transform;
            group.SetParent(generatedRoot, false);
            return group;
        }

        private void ApplyInspectionVisibility()
        {
            if (rotatingAssemblyGroup != null) rotatingAssemblyGroup.gameObject.SetActive(showRotatingAssembly);
            if (pistonsAndRodsGroup != null) pistonsAndRodsGroup.gameObject.SetActive(showPistonsAndRods);
            if (boreGuidesGroup != null) boreGuidesGroup.gameObject.SetActive(showBoreGuides);
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            var material = new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.DontSave
            };
            return material;
        }

        private static void ApplyMaterial(GameObject go, Material material)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider == null) return;

            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
        }

        private void CleanupGenerated()
        {
            if (generatedRoot != null)
            {
                if (Application.isPlaying) Destroy(generatedRoot.gameObject);
                else DestroyImmediate(generatedRoot.gameObject);
                generatedRoot = null;
            }

            rotatingAssemblyGroup = null;
            pistonsAndRodsGroup = null;
            boreGuidesGroup = null;

            DestroyMaterial(ref crankMaterial);
            DestroyMaterial(ref rodMaterial);
            DestroyMaterial(ref pistonMaterial);
            DestroyMaterial(ref guideMaterial);
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
            material = null;
        }
    }
}
