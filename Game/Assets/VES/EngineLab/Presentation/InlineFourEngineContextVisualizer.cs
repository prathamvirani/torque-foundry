using System.Collections.Generic;
using UnityEngine;
using VehicleEngineeringSandbox.Core.ICE;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    /// <summary>
    /// Disposable, presentation-only block and head context for the I4 mechanism.
    /// All dimensions are derived from authoritative engine geometry plus explicitly
    /// visual proportions; this component never feeds values back into simulation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngineLabController), typeof(InlineFourVisualizer))]
    public sealed class InlineFourEngineContextVisualizer : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated I4 Engine Context";

        [SerializeField] private EngineLabController controller;
        [SerializeField] private InlineFourVisualizer mechanismVisualizer;

        [Header("Context Visibility")]
        [SerializeField] private bool showBlockEnvelope = true;
        [SerializeField] private bool showCylinderLiners = true;
        [SerializeField] private bool showDeckPlane = true;
        [SerializeField] private bool showHeadEnvelope = true;

        [Header("Presentation Proportions")]
        [SerializeField, Min(1.1f)] private float blockDepthBoreMultiplier = 1.35f;
        [SerializeField, Min(0.25f)] private float headHeightBoreMultiplier = 0.65f;
        [SerializeField, Range(45f, 180f)] private float linerCutawayAngleDeg = 100f;

        private Transform generatedRoot;
        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private Material blockMaterial;
        private Material linerMaterial;
        private Material deckMaterial;
        private Material headMaterial;

        private float lastBoreMm = -1f;
        private float lastStrokeMm = -1f;
        private float lastRodLengthMm = -1f;
        private int lastCylinderCount = -1;
        private float lastSpacingMultiplier = -1f;
        private float lastBlockDepthMultiplier = -1f;
        private float lastHeadHeightMultiplier = -1f;
        private float lastLinerCutawayAngleDeg = -1f;
        private bool rebuildRequested = true;

        public string GeneratedHierarchyName => GeneratedRootName;
        public bool IsBlockEnvelopeVisible => showBlockEnvelope;
        public bool AreCylinderLinersVisible => showCylinderLiners;
        public bool IsDeckPlaneVisible => showDeckPlane;
        public bool IsHeadEnvelopeVisible => showHeadEnvelope;

        public void SetBlockEnvelopeVisible(bool isVisible)
        {
            showBlockEnvelope = isVisible;
            ApplyInspectionVisibility();
        }

        public void SetCylinderLinersVisible(bool isVisible)
        {
            showCylinderLiners = isVisible;
            ApplyInspectionVisibility();
        }

        public void SetDeckPlaneVisible(bool isVisible)
        {
            showDeckPlane = isVisible;
            ApplyInspectionVisibility();
        }

        public void SetHeadEnvelopeVisible(bool isVisible)
        {
            showHeadEnvelope = isVisible;
            ApplyInspectionVisibility();
        }

        private void Reset()
        {
            ResolveDependencies();
            rebuildRequested = true;
        }

        private void OnEnable()
        {
            ResolveDependencies();
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            CleanupGenerated();
        }

        private void OnValidate()
        {
            ResolveDependencies();
            blockDepthBoreMultiplier = Mathf.Max(1.1f, blockDepthBoreMultiplier);
            headHeightBoreMultiplier = Mathf.Max(0.25f, headHeightBoreMultiplier);
            linerCutawayAngleDeg = Mathf.Clamp(linerCutawayAngleDeg, 45f, 180f);
            rebuildRequested = true;
        }

        private void Update()
        {
            if (controller == null || mechanismVisualizer == null) return;
            if (GeometryChanged()) rebuildRequested = true;
            if (rebuildRequested) RebuildInternal();
        }

        [ContextMenu("Rebuild Engine Context")]
        public void RebuildPreview()
        {
            rebuildRequested = true;
            if (!Application.isPlaying) RebuildInternal();
        }

        private void ResolveDependencies()
        {
            if (controller == null) controller = GetComponent<EngineLabController>();
            if (mechanismVisualizer == null) mechanismVisualizer = GetComponent<InlineFourVisualizer>();
        }

        private bool GeometryChanged()
        {
            return !Mathf.Approximately(lastBoreMm, controller.BoreMm)
                   || !Mathf.Approximately(lastStrokeMm, controller.StrokeMm)
                   || !Mathf.Approximately(lastRodLengthMm, controller.ConnectingRodLengthMm)
                   || lastCylinderCount != controller.CylinderCount
                   || !Mathf.Approximately(lastSpacingMultiplier, mechanismVisualizer.CylinderSpacingBoreMultiplier)
                   || !Mathf.Approximately(lastBlockDepthMultiplier, blockDepthBoreMultiplier)
                   || !Mathf.Approximately(lastHeadHeightMultiplier, headHeightBoreMultiplier)
                   || !Mathf.Approximately(lastLinerCutawayAngleDeg, linerCutawayAngleDeg);
        }

        private void RebuildInternal()
        {
            rebuildRequested = false;
            CleanupGenerated();
            CaptureCurrentConfiguration();

            if (controller.CylinderCount != 4)
            {
                Debug.LogWarning("InlineFourEngineContextVisualizer only supports the current four-cylinder inline presentation.", this);
                return;
            }

            float boreM = Mathf.Max(0.001f, controller.BoreMm / 1000f);
            float strokeM = Mathf.Max(0.001f, controller.StrokeMm / 1000f);
            float crankRadiusM = strokeM * 0.5f;
            float rodLengthM = Mathf.Max(controller.ConnectingRodLengthMm / 1000f, crankRadiusM + 0.000001f);
            float spacingM = boreM * mechanismVisualizer.CylinderSpacingBoreMultiplier;
            float pistonHeightM = Mathf.Clamp(strokeM * 0.28f, 0.012f, 0.045f);
            float tdcPinYM = (float)SliderCrankKinematics.PistonPinHeightM(0.0, crankRadiusM, rodLengthM);
            float bdcPinYM = (float)SliderCrankKinematics.PistonPinHeightM(Mathf.PI, crankRadiusM, rodLengthM);

            float engineLengthM = spacingM * 3f + boreM * 1.35f;
            float blockDepthM = boreM * blockDepthBoreMultiplier;
            float deckYM = tdcPinYM + pistonHeightM * 0.5f + boreM * 0.03f;
            float blockBottomYM = -crankRadiusM - strokeM * 0.38f;
            float blockHeightM = deckYM - blockBottomYM;
            float linerBottomYM = bdcPinYM - pistonHeightM * 0.65f;
            float linerHeightM = deckYM - linerBottomYM;
            float headHeightM = boreM * headHeightBoreMultiplier;

            generatedRoot = new GameObject(GeneratedRootName).transform;
            generatedRoot.SetParent(transform, false);
            generatedRoot.gameObject.hideFlags = HideFlags.DontSaveInEditor;

            CreateMaterials();

            CreateBlockEnvelope(engineLengthM, blockDepthM, blockBottomYM, blockHeightM, boreM);
            CreateCylinderLiners(spacingM, boreM, linerBottomYM, linerHeightM);
            CreateDeckPlane(engineLengthM, blockDepthM, deckYM, spacingM, boreM);
            CreateHeadEnvelope(engineLengthM, blockDepthM, deckYM, headHeightM, boreM);
            ApplyInspectionVisibility();
        }

        private void CreateBlockEnvelope(float lengthM, float depthM, float bottomYM, float heightM, float boreM)
        {
            Transform group = CreateGroup("Block Envelope");
            float wallM = Mathf.Max(0.004f, boreM * 0.06f);
            float centreYM = bottomYM + heightM * 0.5f;

            // The camera-facing wall is intentionally omitted to create a stable cutaway.
            CreateCube("Block Rear Wall", group,
                new Vector3(0f, centreYM, depthM * 0.5f),
                new Vector3(lengthM, heightM, wallM), blockMaterial);
            CreateCube("Block Left End", group,
                new Vector3(-lengthM * 0.5f, centreYM, 0f),
                new Vector3(wallM, heightM, depthM), blockMaterial);
            CreateCube("Block Right End", group,
                new Vector3(lengthM * 0.5f, centreYM, 0f),
                new Vector3(wallM, heightM, depthM), blockMaterial);
            CreateCube("Block Lower Rail", group,
                new Vector3(0f, bottomYM, 0f),
                new Vector3(lengthM, wallM * 1.4f, depthM), blockMaterial);
        }

        private void CreateCylinderLiners(float spacingM, float boreM, float bottomYM, float heightM)
        {
            Transform group = CreateGroup("Cylinder Liners");
            float innerRadiusM = boreM * 0.5f;
            float wallThicknessM = Mathf.Max(0.0015f, boreM * 0.025f);

            for (int i = 0; i < 4; i++)
            {
                float x = (i - 1.5f) * spacingM;
                CreateCutawayLiner(
                    $"Cylinder Liner {i + 1}",
                    group,
                    new Vector3(x, bottomYM + heightM * 0.5f, 0f),
                    innerRadiusM,
                    innerRadiusM + wallThicknessM,
                    heightM,
                    linerCutawayAngleDeg,
                    linerMaterial);
            }
        }

        private void CreateDeckPlane(float lengthM, float depthM, float deckYM, float spacingM, float boreM)
        {
            Transform group = CreateGroup("Deck Plane");
            float railM = Mathf.Max(0.004f, boreM * 0.055f);
            float edgeZM = depthM * 0.5f - railM * 0.5f;

            CreateCube("Deck Front Rail", group, new Vector3(0f, deckYM, -edgeZM),
                new Vector3(lengthM, railM, railM), deckMaterial);
            CreateCube("Deck Rear Rail", group, new Vector3(0f, deckYM, edgeZM),
                new Vector3(lengthM, railM, railM), deckMaterial);
            CreateCube("Deck Left Rail", group, new Vector3(-lengthM * 0.5f, deckYM, 0f),
                new Vector3(railM, railM, depthM), deckMaterial);
            CreateCube("Deck Right Rail", group, new Vector3(lengthM * 0.5f, deckYM, 0f),
                new Vector3(railM, railM, depthM), deckMaterial);

            for (int i = 0; i < 3; i++)
            {
                float bridgeX = (i - 1f) * spacingM;
                CreateCube($"Deck Bore Bridge {i + 1}", group, new Vector3(bridgeX, deckYM, 0f),
                    new Vector3(railM, railM, depthM), deckMaterial);
            }
        }

        private void CreateHeadEnvelope(float lengthM, float depthM, float deckYM, float heightM, float boreM)
        {
            Transform group = CreateGroup("Cylinder Head Envelope");
            float wallM = Mathf.Max(0.004f, boreM * 0.06f);
            float centreYM = deckYM + heightM * 0.5f;
            float topYM = deckYM + heightM;

            // A rear wall, end caps and top rail establish volume while leaving the mechanism visible.
            CreateCube("Head Rear Wall", group, new Vector3(0f, centreYM, depthM * 0.5f),
                new Vector3(lengthM, heightM, wallM), headMaterial);
            CreateCube("Head Left End", group, new Vector3(-lengthM * 0.5f, centreYM, 0f),
                new Vector3(wallM, heightM, depthM), headMaterial);
            CreateCube("Head Right End", group, new Vector3(lengthM * 0.5f, centreYM, 0f),
                new Vector3(wallM, heightM, depthM), headMaterial);
            CreateCube("Head Top Rail", group, new Vector3(0f, topYM, 0f),
                new Vector3(lengthM, wallM * 1.4f, depthM), headMaterial);
        }

        private Transform CreateGroup(string groupName)
        {
            Transform group = new GameObject(groupName).transform;
            group.SetParent(generatedRoot, false);
            return group;
        }

        private void ApplyInspectionVisibility()
        {
            SetGroupActive("Block Envelope", showBlockEnvelope);
            SetGroupActive("Cylinder Liners", showCylinderLiners);
            SetGroupActive("Deck Plane", showDeckPlane);
            SetGroupActive("Cylinder Head Envelope", showHeadEnvelope);
        }

        private void SetGroupActive(string groupName, bool isVisible)
        {
            if (generatedRoot == null) return;
            Transform group = generatedRoot.Find(groupName);
            if (group != null) group.gameObject.SetActive(isVisible);
        }

        private static void CreateCube(string objectName, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = objectName;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            RemoveCollider(go);
            ApplyMaterial(go, material);
        }

        private void CreateCutawayLiner(
            string objectName,
            Transform parent,
            Vector3 position,
            float innerRadiusM,
            float outerRadiusM,
            float heightM,
            float cutawayAngleDeg,
            Material material)
        {
            const int segmentCount = 28;
            float visibleArcDeg = 360f - cutawayAngleDeg;
            float startAngleDeg = -90f + cutawayAngleDeg * 0.5f;
            float halfHeightM = heightM * 0.5f;
            var vertices = new Vector3[(segmentCount + 1) * 4];
            var triangles = new List<int>(segmentCount * 24 + 24);

            for (int i = 0; i <= segmentCount; i++)
            {
                float angleRad = (startAngleDeg + visibleArcDeg * i / segmentCount) * Mathf.Deg2Rad;
                float cos = Mathf.Cos(angleRad);
                float sin = Mathf.Sin(angleRad);
                int vertex = i * 4;

                vertices[vertex] = new Vector3(cos * outerRadiusM, -halfHeightM, sin * outerRadiusM);
                vertices[vertex + 1] = new Vector3(cos * outerRadiusM, halfHeightM, sin * outerRadiusM);
                vertices[vertex + 2] = new Vector3(cos * innerRadiusM, -halfHeightM, sin * innerRadiusM);
                vertices[vertex + 3] = new Vector3(cos * innerRadiusM, halfHeightM, sin * innerRadiusM);

                if (i == segmentCount) continue;
                int next = vertex + 4;
                AddQuad(triangles, vertex, next, vertex + 1, next + 1);
                AddQuad(triangles, next + 2, vertex + 2, next + 3, vertex + 3);
                AddQuad(triangles, vertex + 1, next + 1, vertex + 3, next + 3);
                AddQuad(triangles, next + 2, vertex + 2, next, vertex);
            }

            AddQuad(triangles, 2, 0, 3, 1);
            int last = segmentCount * 4;
            AddQuad(triangles, last, last + 2, last + 1, last + 3);

            var mesh = new Mesh
            {
                name = objectName + " Mesh",
                hideFlags = HideFlags.DontSave
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            generatedMeshes.Add(mesh);

            var go = new GameObject(objectName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        private static void AddQuad(List<int> triangles, int bottomLeft, int bottomRight, int topLeft, int topRight)
        {
            triangles.Add(bottomLeft);
            triangles.Add(topLeft);
            triangles.Add(bottomRight);
            triangles.Add(bottomRight);
            triangles.Add(topLeft);
            triangles.Add(topRight);
        }

        private void CreateMaterials()
        {
            blockMaterial = CreateMaterial(new Color(0.16f, 0.22f, 0.27f, 1f));
            linerMaterial = CreateMaterial(new Color(0.62f, 0.68f, 0.72f, 1f));
            deckMaterial = CreateMaterial(new Color(0.78f, 0.48f, 0.16f, 1f));
            headMaterial = CreateMaterial(new Color(0.22f, 0.31f, 0.36f, 1f));
        }

        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");

            return new Material(shader)
            {
                color = color,
                hideFlags = HideFlags.DontSave
            };
        }

        private void CaptureCurrentConfiguration()
        {
            lastBoreMm = controller.BoreMm;
            lastStrokeMm = controller.StrokeMm;
            lastRodLengthMm = controller.ConnectingRodLengthMm;
            lastCylinderCount = controller.CylinderCount;
            lastSpacingMultiplier = mechanismVisualizer.CylinderSpacingBoreMultiplier;
            lastBlockDepthMultiplier = blockDepthBoreMultiplier;
            lastHeadHeightMultiplier = headHeightBoreMultiplier;
            lastLinerCutawayAngleDeg = linerCutawayAngleDeg;
        }

        private void CleanupGenerated()
        {
            if (generatedRoot != null)
            {
                if (Application.isPlaying) Destroy(generatedRoot.gameObject);
                else DestroyImmediate(generatedRoot.gameObject);
                generatedRoot = null;
            }

            foreach (Mesh mesh in generatedMeshes)
            {
                if (mesh == null) continue;
                if (Application.isPlaying) Destroy(mesh);
                else DestroyImmediate(mesh);
            }
            generatedMeshes.Clear();

            DestroyMaterial(ref blockMaterial);
            DestroyMaterial(ref linerMaterial);
            DestroyMaterial(ref deckMaterial);
            DestroyMaterial(ref headMaterial);
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
            material = null;
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
    }
}
