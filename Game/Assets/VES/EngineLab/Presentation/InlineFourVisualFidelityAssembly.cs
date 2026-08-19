using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using VehicleEngineeringSandbox.Core.ICE;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    public enum EngineInspectionMode
    {
        FullEngine,
        Cutaway,
        TransparentBlockAndHead,
        RotatingAssemblyOnly,
        ValvetrainOnly
    }

    /// <summary>
    /// Semi-realistic procedural inline-four assembly for Visual Fidelity Pass v1.
    /// Bore, stroke, rod length, spacing, phasing and slider-crank positions come
    /// from the authoritative controller/Core model. Every other dimension below
    /// is an explicitly named presentation assumption and never feeds simulation.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(EngineLabController))]
    public sealed class InlineFourVisualFidelityAssembly : MonoBehaviour
    {
        private const string GeneratedRootName = "Generated I4 Visual Fidelity Assembly";
        private static readonly float[] CrankPhaseDeg = { 0f, 180f, 180f, 0f };

        [SerializeField] private EngineLabController controller;

        [Header("Teaching Animation (presentation only)")]
        [SerializeField] private bool animateInPlayMode = true;
        [SerializeField, Range(0f, 300f)] private float teachingAnimationRpm = 60f;
        [SerializeField, Range(0f, 360f)] private float previewCrankAngleDeg = 28f;
        [SerializeField] private EngineInspectionMode inspectionMode = EngineInspectionMode.FullEngine;

        [Header("Cast Assembly Assumptions (multiples of bore)")]
        [SerializeField, Min(1.05f)] private float blockDepthBoreMultiplier = 1.50f;
        [SerializeField, Min(0.25f)] private float crankcaseDepthBelowAxisBoreMultiplier = 0.72f;
        [SerializeField, Min(0.35f)] private float headHeightBoreMultiplier = 0.82f;
        [SerializeField, Min(1.1f)] private float headDepthBoreMultiplier = 1.68f;
        [SerializeField, Range(0.02f, 0.12f)] private float nominalCastingWallBoreMultiplier = 0.055f;
        [SerializeField, Range(0.01f, 0.08f)] private float deckThicknessBoreMultiplier = 0.045f;

        [Header("Moving-Part Assumptions (multiples of bore)")]
        [SerializeField, Range(0.82f, 0.98f)] private float pistonDiameterBoreMultiplier = 0.94f;
        [SerializeField, Range(0.18f, 0.42f)] private float pistonCompressionHeightBoreMultiplier = 0.30f;
        [SerializeField, Range(0.30f, 0.70f)] private float pistonSkirtLengthBoreMultiplier = 0.48f;
        [SerializeField, Range(0.12f, 0.30f)] private float connectingRodBigEndOuterBoreMultiplier = 0.22f;
        [SerializeField, Range(0.05f, 0.16f)] private float connectingRodSmallEndOuterBoreMultiplier = 0.105f;

        [Header("Cylinder-Head Assumptions")]
        [SerializeField, Range(8f, 28f)] private float valveIncludedAngleDeg = 20f;
        [SerializeField, Range(0.18f, 0.42f)] private float camshaftSpacingHeadDepthMultiplier = 0.40f;

        private Transform generatedRoot;
        private Transform fullBlockGroup;
        private Transform cutawayBlockGroup;
        private Transform blockInternalsGroup;
        private Transform fullHeadGroup;
        private Transform cutawayHeadGroup;
        private Transform headInternalsGroup;
        private Transform crankInternalGroup;
        private Transform crankExternalGroup;
        private Transform pistonsAndRodsGroup;
        private Transform valvetrainGroup;
        private Transform lightingGroup;

        private Transform[] throwRotors;
        private Transform[] pistonAssemblies;
        private Transform[] connectingRodAssemblies;
        private Transform[] camshaftRotors;
        private float[] cylinderXM;

        private readonly List<Mesh> generatedMeshes = new List<Mesh>();
        private readonly List<Renderer> opaqueBlockRenderers = new List<Renderer>();
        private readonly List<Renderer> opaqueHeadRenderers = new List<Renderer>();
        private readonly Dictionary<Renderer, Material> blockOriginalMaterials = new Dictionary<Renderer, Material>();
        private readonly Dictionary<Renderer, Material> headOriginalMaterials = new Dictionary<Renderer, Material>();

        private Material castIronMaterial;
        private Material castAluminumMaterial;
        private Material machinedSteelMaterial;
        private Material darkSteelMaterial;
        private Material pistonAluminumMaterial;
        private Material bearingMaterial;
        private Material gasketMaterial;
        private Material darkCavityMaterial;
        private Material transparentBlockMaterial;
        private Material transparentHeadMaterial;

        private float lastBoreMm = -1f;
        private float lastStrokeMm = -1f;
        private float lastRodLengthMm = -1f;
        private int lastCylinderCount = -1;
        private float lastBlockDepthMultiplier = -1f;
        private float lastHeadHeightMultiplier = -1f;
        private float lastHeadDepthMultiplier = -1f;
        private bool rebuildRequested = true;
        private float animatedCrankAngleDeg;

        private float boreM;
        private float strokeM;
        private float crankRadiusM;
        private float rodLengthM;
        private float spacingM;
        private float deckYM;
        private float blockBottomYM;
        private float blockLengthM;
        private float blockDepthM;
        private float headHeightM;
        private float headDepthM;
        private float pistonCompressionHeightM;
        private float pistonSkirtLengthM;

        public string GeneratedHierarchyName => GeneratedRootName;
        public bool IsTeachingAnimationPlaying => animateInPlayMode;
        public float TeachingAnimationRpm => teachingAnimationRpm;
        public float CurrentCrankAngleDeg => Application.isPlaying && animateInPlayMode
            ? animatedCrankAngleDeg
            : previewCrankAngleDeg;
        public EngineInspectionMode InspectionMode => inspectionMode;
        public float CylinderSpacingM => spacingM;
        public Vector3 RecommendedFocusPointLocal => inspectionMode == EngineInspectionMode.ValvetrainOnly
            ? new Vector3(0f, deckYM + headHeightM * 0.65f, 0f)
            : inspectionMode == EngineInspectionMode.RotatingAssemblyOnly
                ? new Vector3(0f, rodLengthM * 0.48f, 0f)
                : new Vector3(0f, 0.08f, 0f);
        public float RecommendedCameraDistanceM => inspectionMode == EngineInspectionMode.ValvetrainOnly
            ? 0.46f
            : inspectionMode == EngineInspectionMode.RotatingAssemblyOnly ? 0.62f : 0.78f;

        private void Reset()
        {
            controller = GetComponent<EngineLabController>();
            ValidatePresentationAssumptions();
            rebuildRequested = true;
        }

        private void OnEnable()
        {
            if (controller == null) controller = GetComponent<EngineLabController>();
            rebuildRequested = true;
        }

        private void OnDisable()
        {
            CleanupGenerated();
        }

        private void OnValidate()
        {
            if (controller == null) controller = GetComponent<EngineLabController>();
            ValidatePresentationAssumptions();
            rebuildRequested = true;
        }

        private void Update()
        {
            if (controller == null) return;
            if (GeometryChanged()) rebuildRequested = true;
            if (rebuildRequested) RebuildInternal();
            if (generatedRoot == null || pistonAssemblies == null) return;

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

            UpdateMovingAssembly(angleDeg);
        }

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
            if (generatedRoot != null) UpdateMovingAssembly(previewCrankAngleDeg);
        }

        public void SetInspectionMode(EngineInspectionMode mode)
        {
            inspectionMode = mode;
            ApplyInspectionMode();
        }

        [ContextMenu("Rebuild Visual Fidelity Assembly")]
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
                   || !Mathf.Approximately(lastBlockDepthMultiplier, blockDepthBoreMultiplier)
                   || !Mathf.Approximately(lastHeadHeightMultiplier, headHeightBoreMultiplier)
                   || !Mathf.Approximately(lastHeadDepthMultiplier, headDepthBoreMultiplier);
        }

        private void RebuildInternal()
        {
            rebuildRequested = false;
            CleanupGenerated();
            CaptureConfiguration();

            if (controller.CylinderCount != 4)
            {
                Debug.LogWarning("Visual Fidelity Pass v1 currently supports a four-cylinder inline layout only.", this);
                return;
            }

            CalculatePresentationDimensions();
            CreateMaterials();
            CreateHierarchy();
            CreateCylinderBlock();
            CreateBlockInternals();
            CreateCrankshaft();
            CreatePistonsAndConnectingRods();
            CreateCylinderHead();
            CreateValvetrain();
            CreateLightingRig();
            UpdateMovingAssembly(previewCrankAngleDeg);
            ApplyInspectionMode();
        }

        private void CalculatePresentationDimensions()
        {
            boreM = Mathf.Max(0.001f, controller.BoreMm / 1000f);
            strokeM = Mathf.Max(0.001f, controller.StrokeMm / 1000f);
            crankRadiusM = strokeM * 0.5f;
            rodLengthM = Mathf.Max(controller.ConnectingRodLengthMm / 1000f, crankRadiusM + 0.000001f);
            spacingM = boreM * 1.15f;
            pistonCompressionHeightM = boreM * pistonCompressionHeightBoreMultiplier;
            pistonSkirtLengthM = boreM * pistonSkirtLengthBoreMultiplier;

            float tdcPinYM = (float)SliderCrankKinematics.PistonPinHeightM(0.0, crankRadiusM, rodLengthM);
            deckYM = tdcPinYM + pistonCompressionHeightM + boreM * deckThicknessBoreMultiplier;
            blockBottomYM = -crankRadiusM - boreM * crankcaseDepthBelowAxisBoreMultiplier;
            blockLengthM = spacingM * 3f + boreM * 1.62f;
            blockDepthM = boreM * blockDepthBoreMultiplier;
            headHeightM = boreM * headHeightBoreMultiplier;
            headDepthM = boreM * headDepthBoreMultiplier;

            cylinderXM = new float[4];
            for (int i = 0; i < 4; i++) cylinderXM[i] = (i - 1.5f) * spacingM;
        }

        private void CreateHierarchy()
        {
            generatedRoot = new GameObject(GeneratedRootName).transform;
            generatedRoot.SetParent(transform, false);
            generatedRoot.gameObject.hideFlags = HideFlags.DontSaveInEditor;

            fullBlockGroup = CreateGroup("Cylinder Block - Full");
            cutawayBlockGroup = CreateGroup("Cylinder Block - Cutaway");
            blockInternalsGroup = CreateGroup("Block Internals and Liners");
            fullHeadGroup = CreateGroup("Cylinder Head - Full");
            cutawayHeadGroup = CreateGroup("Cylinder Head - Cutaway");
            headInternalsGroup = CreateGroup("Head Chambers and Ports");
            crankInternalGroup = CreateGroup("Crankshaft Internal");
            crankExternalGroup = CreateGroup("Crankshaft External");
            pistonsAndRodsGroup = CreateGroup("Pistons and Forged Rods");
            valvetrainGroup = CreateGroup("DOHC Valvetrain");
            lightingGroup = CreateGroup("Inspection Lighting");
        }

        private void CreateCylinderBlock()
        {
            float wallM = boreM * nominalCastingWallBoreMultiplier;
            float upperBottomYM = -crankRadiusM * 0.12f;
            float upperHeightM = deckYM - upperBottomYM;
            float lowerHeightM = upperBottomYM - blockBottomYM;
            float railThicknessM = Mathf.Max(0.006f, wallM * 1.5f);
            float bevelM = Mathf.Max(0.003f, wallM * 0.8f);

            CreateBlockBox("Upper cylinder-bank casting", fullBlockGroup,
                new Vector3(0f, upperBottomYM + upperHeightM * 0.5f, blockDepthM * 0.12f),
                new Vector3(blockLengthM, upperHeightM, blockDepthM * 0.72f), bevelM, castIronMaterial, true);
            CreateBlockBox("Deep-skirt crankcase casting", fullBlockGroup,
                new Vector3(0f, blockBottomYM + lowerHeightM * 0.5f, blockDepthM * 0.08f),
                new Vector3(blockLengthM * 1.035f, lowerHeightM, blockDepthM * 0.88f), bevelM, castIronMaterial, true);
            CreateBlockBox("Machined deck surface", fullBlockGroup,
                new Vector3(0f, deckYM, 0f),
                new Vector3(blockLengthM * 1.01f, wallM * 0.75f, blockDepthM * 1.02f),
                wallM * 0.25f, bearingMaterial, true);

            CreateEndFaces(fullBlockGroup, upperBottomYM, upperHeightM, lowerHeightM, bevelM, true);
            CreateFullBlockSculpting(fullBlockGroup, upperBottomYM, upperHeightM, lowerHeightM);
            CreateExternalBlockDetails(fullBlockGroup, upperBottomYM, upperHeightM, railThicknessM, true);
            CreateDeckBoreDetails(fullBlockGroup, true);
            CreateOilPanAndRail(fullBlockGroup, railThicknessM, true);

            float rearHalfDepthM = blockDepthM * 0.55f;
            float rearHalfZM = blockDepthM * 0.225f;
            CreateBlockBox("Sectioned cylinder-bank rear casting", cutawayBlockGroup,
                new Vector3(0f, upperBottomYM + upperHeightM * 0.5f, rearHalfZM),
                new Vector3(blockLengthM, upperHeightM, rearHalfDepthM), bevelM, castIronMaterial, false);
            CreateBlockBox("Sectioned deep-skirt rear casting", cutawayBlockGroup,
                new Vector3(0f, blockBottomYM + lowerHeightM * 0.5f, rearHalfZM),
                new Vector3(blockLengthM * 1.035f, lowerHeightM, rearHalfDepthM * 1.10f),
                bevelM, castIronMaterial, false);
            CreateBlockBox("Cutaway deck land", cutawayBlockGroup,
                new Vector3(0f, deckYM, blockDepthM * 0.12f),
                new Vector3(blockLengthM * 1.01f, wallM * 0.75f, blockDepthM * 0.76f),
                wallM * 0.25f, bearingMaterial, false);
            CreateBlockBox("Front lower skirt bridge", cutawayBlockGroup,
                new Vector3(0f, blockBottomYM + railThicknessM * 2.0f, -blockDepthM * 0.39f),
                new Vector3(blockLengthM * 0.92f, railThicknessM * 2.2f, wallM * 2.2f),
                wallM * 0.45f, castIronMaterial, false);
            CreateEndFaces(cutawayBlockGroup, upperBottomYM, upperHeightM, lowerHeightM, bevelM, false);
            CreateExternalBlockDetails(cutawayBlockGroup, upperBottomYM, upperHeightM, railThicknessM, false);
            CreateDeckBoreDetails(cutawayBlockGroup, false);
            CreateOilPanAndRail(cutawayBlockGroup, railThicknessM, false);
        }

        private void CreateFullBlockSculpting(
            Transform parent,
            float upperBottomYM,
            float upperHeightM,
            float lowerHeightM)
        {
            float barrelRadiusM = boreM * 0.54f;
            float barrelFrontZM = -blockDepthM * 0.24f;
            for (int i = 0; i < 4; i++)
            {
                CreateCylinderBetween($"Integrated cylinder barrel {i + 1}", parent,
                    new Vector3(cylinderXM[i], upperBottomYM + boreM * 0.03f, barrelFrontZM),
                    new Vector3(cylinderXM[i], deckYM - boreM * 0.035f, barrelFrontZM),
                    barrelRadiusM, castIronMaterial, opaqueBlockRenderers);
                CreateBeveledBox($"Barrel bridge boss {i + 1}", parent,
                    new Vector3(cylinderXM[i], upperBottomYM + upperHeightM * 0.35f, -blockDepthM * 0.46f),
                    new Vector3(boreM * 0.62f, upperHeightM * 0.48f, boreM * 0.16f),
                    boreM * 0.028f, castIronMaterial, opaqueBlockRenderers);
            }

            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2f) * spacingM;
                CreateBeveledBox($"Crankcase main-bay bulge {i + 1}", parent,
                    new Vector3(x, blockBottomYM + lowerHeightM * 0.56f, -blockDepthM * 0.39f),
                    new Vector3(spacingM * 0.74f, lowerHeightM * 0.68f, boreM * 0.22f),
                    boreM * 0.035f,
                    castIronMaterial, opaqueBlockRenderers);
            }

            for (int band = 0; band < 2; band++)
            {
                float y = upperBottomYM + upperHeightM * (band == 0 ? 0.34f : 0.72f);
                CreateBeveledBox($"Integrated water-jacket tie band {band + 1}", parent,
                    new Vector3(0f, y, -blockDepthM * 0.52f),
                    new Vector3(blockLengthM * 0.92f, boreM * 0.075f, boreM * 0.11f),
                    boreM * 0.018f, castIronMaterial, opaqueBlockRenderers);
            }
        }

        private void CreateEndFaces(
            Transform parent,
            float upperBottomYM,
            float upperHeightM,
            float lowerHeightM,
            float bevelM,
            bool trackAsOpaqueBlock)
        {
            float faceThicknessM = boreM * 0.075f;
            float totalHeightM = deckYM - blockBottomYM;
            float x = blockLengthM * 0.5f + faceThicknessM * 0.35f;
            CreateBlockBox("Front timing face", parent, new Vector3(-x, blockBottomYM + totalHeightM * 0.5f, 0f),
                new Vector3(faceThicknessM, totalHeightM * 0.96f, blockDepthM * 1.04f), bevelM, castIronMaterial,
                trackAsOpaqueBlock);
            CreateBlockBox("Rear bellhousing face", parent, new Vector3(x, blockBottomYM + totalHeightM * 0.5f, 0f),
                new Vector3(faceThicknessM, totalHeightM * 0.96f, blockDepthM * 1.08f), bevelM, castIronMaterial,
                trackAsOpaqueBlock);
        }

        private void CreateExternalBlockDetails(
            Transform parent,
            float upperBottomYM,
            float upperHeightM,
            float railThicknessM,
            bool trackAsOpaqueBlock)
        {
            float frontZM = -blockDepthM * 0.5f - railThicknessM * 0.28f;
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2f) * spacingM;
                CreateBlockBox($"Cast vertical rib {i + 1}", parent,
                    new Vector3(x, upperBottomYM + upperHeightM * 0.45f, frontZM),
                    new Vector3(railThicknessM * 0.75f, upperHeightM * 0.78f, railThicknessM),
                    railThicknessM * 0.3f, castIronMaterial, trackAsOpaqueBlock);
                CreateCylinderBetween($"Main-gallery boss {i + 1}", parent,
                    new Vector3(x, upperBottomYM + upperHeightM * 0.24f, frontZM - railThicknessM * 0.55f),
                    new Vector3(x, upperBottomYM + upperHeightM * 0.24f, frontZM + railThicknessM * 0.8f),
                    railThicknessM * 0.82f, castIronMaterial, trackAsOpaqueBlock ? opaqueBlockRenderers : null);
            }

            for (int i = 0; i < 4; i++)
            {
                float x = cylinderXM[i];
                CreateAngledRib($"Cylinder bay diagonal rib {i + 1}", parent,
                    new Vector3(x - spacingM * 0.28f, upperBottomYM + upperHeightM * 0.18f, frontZM),
                    new Vector3(x + spacingM * 0.28f, upperBottomYM + upperHeightM * 0.72f, frontZM),
                    railThicknessM * 0.55f, castIronMaterial, trackAsOpaqueBlock ? opaqueBlockRenderers : null);
            }
        }

        private void CreateDeckBoreDetails(Transform parent, bool trackAsOpaqueBlock)
        {
            float linerOuterRadiusM = boreM * 0.515f;
            for (int i = 0; i < 4; i++)
            {
                Mesh ring = TrackMesh(ProceduralEngineMeshFactory.CreateTubeSectorAlongY(
                    $"Deck bore ring mesh {i + 1}", boreM * 0.485f, linerOuterRadiusM,
                    boreM * 0.028f, 0f, 360f, 40));
                CreateMeshPart($"Machined bore opening {i + 1}", parent, ring,
                    new Vector3(cylinderXM[i], deckYM + boreM * 0.014f, 0f), Quaternion.identity,
                    bearingMaterial, trackAsOpaqueBlock ? opaqueBlockRenderers : null);
                CreateCylinderBetween($"Bore darkness {i + 1}", parent,
                    new Vector3(cylinderXM[i], deckYM + boreM * 0.014f, 0f),
                    new Vector3(cylinderXM[i], deckYM + boreM * 0.016f, 0f),
                    boreM * 0.475f, darkCavityMaterial, null);
            }
        }

        private void CreateOilPanAndRail(Transform parent, float railThicknessM, bool trackAsOpaqueBlock)
        {
            float railYM = blockBottomYM - railThicknessM * 0.2f;
            CreateBlockBox("Sump rail", parent, new Vector3(0f, railYM, 0f),
                new Vector3(blockLengthM * 1.04f, railThicknessM, blockDepthM * 1.17f),
                railThicknessM * 0.3f, bearingMaterial, trackAsOpaqueBlock);
            CreateBlockBox("Shallow oil pan", parent,
                new Vector3(0f, railYM - boreM * 0.18f, 0f),
                new Vector3(blockLengthM * 0.91f, boreM * 0.34f, blockDepthM * 0.92f),
                railThicknessM * 0.65f, darkSteelMaterial, trackAsOpaqueBlock);
        }

        private void CreateBlockInternals()
        {
            float linerBottomYM = rodLengthM - crankRadiusM - pistonSkirtLengthM * 0.75f;
            float linerHeightM = deckYM - linerBottomYM;
            for (int i = 0; i < 4; i++)
            {
                Mesh liner = TrackMesh(ProceduralEngineMeshFactory.CreateTubeSectorAlongY(
                    $"Cutaway liner mesh {i + 1}", boreM * 0.5f, boreM * 0.522f,
                    linerHeightM, -42f, 264f, 48));
                CreateMeshPart($"Cylinder liner {i + 1}", blockInternalsGroup, liner,
                    new Vector3(cylinderXM[i], linerBottomYM + linerHeightM * 0.5f, 0f),
                    Quaternion.identity, bearingMaterial, null);
            }

            float mainJournalRadiusM = Mathf.Max(0.008f, strokeM * 0.10f);
            float bulkheadThicknessM = Mathf.Max(0.008f, spacingM * 0.11f);
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2f) * spacingM;
                Transform bulkhead = CreateGroup($"Main-bearing bulkhead {i + 1}", blockInternalsGroup);
                CreateBeveledBox("Bulkhead rear web", bulkhead,
                    new Vector3(x, -crankRadiusM * 0.10f, blockDepthM * 0.20f),
                    new Vector3(bulkheadThicknessM, boreM * 0.82f, blockDepthM * 0.48f),
                    bulkheadThicknessM * 0.18f, castIronMaterial, null);
                Mesh saddle = TrackMesh(ProceduralEngineMeshFactory.CreateRingAlongX(
                    $"Main saddle mesh {i + 1}", mainJournalRadiusM * 1.08f,
                    mainJournalRadiusM * 1.62f, bulkheadThicknessM, 32));
                CreateMeshPart("Machined main saddle", bulkhead, saddle,
                    new Vector3(x, 0f, 0f), Quaternion.identity, bearingMaterial, null);
                CreateBeveledBox("Main bearing cap", bulkhead,
                    new Vector3(x, -mainJournalRadiusM * 1.32f, -blockDepthM * 0.05f),
                    new Vector3(bulkheadThicknessM * 1.16f, mainJournalRadiusM * 1.15f, blockDepthM * 0.48f),
                    bulkheadThicknessM * 0.2f, darkSteelMaterial, null);
                for (int side = -1; side <= 1; side += 2)
                {
                    float z = side * blockDepthM * 0.17f;
                    CreateCylinderBetween($"Main cap bolt {(side < 0 ? "front" : "rear")}", bulkhead,
                        new Vector3(x, -mainJournalRadiusM * 1.1f, z),
                        new Vector3(x, -mainJournalRadiusM * 1.1f - boreM * 0.055f, z),
                        boreM * 0.026f, machinedSteelMaterial, null);
                }
            }
        }

        private void CreateCrankshaft()
        {
            float mainJournalRadiusM = Mathf.Max(0.008f, strokeM * 0.10f);
            float rodJournalRadiusM = Mathf.Max(0.007f, strokeM * 0.085f);
            float mainJournalLengthM = spacingM * 0.24f;
            float rodJournalLengthM = boreM * 0.58f;
            float cheekThicknessM = boreM * 0.075f;

            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2f) * spacingM;
                CreateCylinderBetween($"Main journal {i + 1}", crankInternalGroup,
                    new Vector3(x - mainJournalLengthM * 0.5f, 0f, 0f),
                    new Vector3(x + mainJournalLengthM * 0.5f, 0f, 0f),
                    mainJournalRadiusM, machinedSteelMaterial, null);
                if (i < 4)
                {
                    float nextX = (i - 1f) * spacingM;
                    CreateCylinderBetween($"Crank core segment {i + 1}", crankInternalGroup,
                        new Vector3(x + mainJournalLengthM * 0.5f, 0f, 0f),
                        new Vector3(nextX - mainJournalLengthM * 0.5f, 0f, 0f),
                        mainJournalRadiusM * 0.62f, darkSteelMaterial, null);
                }
            }

            throwRotors = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                Transform rotor = CreateGroup($"Crank throw {i + 1}", crankInternalGroup);
                rotor.localPosition = new Vector3(cylinderXM[i], 0f, 0f);
                throwRotors[i] = rotor;

                CreateCylinderBetween("Rod journal", rotor,
                    new Vector3(-rodJournalLengthM * 0.5f, crankRadiusM, 0f),
                    new Vector3(rodJournalLengthM * 0.5f, crankRadiusM, 0f),
                    rodJournalRadiusM, machinedSteelMaterial, null);

                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (rodJournalLengthM * 0.5f + cheekThicknessM * 0.55f);
                    CreateCylinderBetween($"Broad crank cheek {(side < 0 ? "front" : "rear")}", rotor,
                        new Vector3(x - cheekThicknessM * 0.5f, crankRadiusM * 0.28f, 0f),
                        new Vector3(x + cheekThicknessM * 0.5f, crankRadiusM * 0.28f, 0f),
                        crankRadiusM * 1.12f, darkSteelMaterial, null);
                    CreateBeveledBox($"Counterweight {(side < 0 ? "front" : "rear")}", rotor,
                        new Vector3(x, -crankRadiusM * 0.86f, 0f),
                        new Vector3(cheekThicknessM * 1.15f, crankRadiusM * 1.28f, crankRadiusM * 1.42f),
                        cheekThicknessM * 0.38f, darkSteelMaterial, null);
                }
            }

            float frontX = -blockLengthM * 0.5f;
            CreateCylinderBetween("Crank snout", crankExternalGroup,
                new Vector3(frontX - boreM * 0.30f, 0f, 0f), new Vector3(frontX, 0f, 0f),
                mainJournalRadiusM * 0.72f, machinedSteelMaterial, null);
            for (int i = 0; i < 3; i++)
            {
                float x = frontX - boreM * (0.18f + i * 0.035f);
                CreateCylinderBetween($"Crank damper groove {i + 1}", crankExternalGroup,
                    new Vector3(x - boreM * 0.018f, 0f, 0f), new Vector3(x + boreM * 0.018f, 0f, 0f),
                    boreM * (0.27f - i * 0.018f), i == 1 ? gasketMaterial : darkSteelMaterial, null);
            }

            float rearX = blockLengthM * 0.5f;
            CreateCylinderBetween("Rear crank flange", crankExternalGroup,
                new Vector3(rearX, 0f, 0f), new Vector3(rearX + boreM * 0.10f, 0f, 0f),
                boreM * 0.24f, machinedSteelMaterial, null);
            CreateCylinderBetween("Flywheel", crankExternalGroup,
                new Vector3(rearX + boreM * 0.09f, 0f, 0f), new Vector3(rearX + boreM * 0.16f, 0f, 0f),
                boreM * 0.46f, darkSteelMaterial, null);
            CreateCylinderBetween("Flywheel friction face", crankExternalGroup,
                new Vector3(rearX + boreM * 0.16f, 0f, 0f), new Vector3(rearX + boreM * 0.172f, 0f, 0f),
                boreM * 0.40f, bearingMaterial, null);
        }

        private void CreatePistonsAndConnectingRods()
        {
            pistonAssemblies = new Transform[4];
            connectingRodAssemblies = new Transform[4];
            float pistonDiameterM = boreM * pistonDiameterBoreMultiplier;
            float bigOuterRadiusM = boreM * connectingRodBigEndOuterBoreMultiplier;
            float smallOuterRadiusM = boreM * connectingRodSmallEndOuterBoreMultiplier;
            float bigInnerRadiusM = bigOuterRadiusM * 0.56f;
            float smallInnerRadiusM = smallOuterRadiusM * 0.52f;
            float rodThicknessM = boreM * 0.095f;

            for (int i = 0; i < 4; i++)
            {
                Transform piston = CreateGroup($"Piston assembly {i + 1}", pistonsAndRodsGroup);
                pistonAssemblies[i] = piston;
                CreatePistonGeometry(piston, pistonDiameterM, i + 1);

                Transform rod = CreateGroup($"Forged connecting rod {i + 1}", pistonsAndRodsGroup);
                connectingRodAssemblies[i] = rod;

                Mesh bigEnd = TrackMesh(ProceduralEngineMeshFactory.CreateRingAlongX(
                    $"Big-end eye mesh {i + 1}", bigInnerRadiusM, bigOuterRadiusM, rodThicknessM, 36));
                CreateMeshPart("Big-end eye", rod, bigEnd, Vector3.zero, Quaternion.identity, darkSteelMaterial, null);
                Mesh smallEnd = TrackMesh(ProceduralEngineMeshFactory.CreateRingAlongX(
                    $"Small-end eye mesh {i + 1}", smallInnerRadiusM, smallOuterRadiusM,
                    rodThicknessM * 0.82f, 32));
                CreateMeshPart("Small-end eye", rod, smallEnd,
                    new Vector3(0f, rodLengthM, 0f), Quaternion.identity, darkSteelMaterial, null);

                float bodyStartM = bigOuterRadiusM * 0.78f;
                float bodyEndM = rodLengthM - smallOuterRadiusM * 0.82f;
                var rodProfile = new[]
                {
                    new Vector2(bodyStartM, -bigOuterRadiusM * 0.34f),
                    new Vector2(bodyEndM, -smallOuterRadiusM * 0.30f),
                    new Vector2(bodyEndM, smallOuterRadiusM * 0.30f),
                    new Vector2(bodyStartM, bigOuterRadiusM * 0.34f)
                };
                Mesh web = TrackMesh(ProceduralEngineMeshFactory.CreateExtrudedProfileAlongX(
                    $"I-beam rod web mesh {i + 1}", rodProfile, rodThicknessM * 0.42f));
                CreateMeshPart("Forged I-beam web", rod, web, Vector3.zero, Quaternion.identity,
                    machinedSteelMaterial, null);
                float railLengthM = bodyEndM - bodyStartM;
                float railCentreM = (bodyEndM + bodyStartM) * 0.5f;
                for (int side = -1; side <= 1; side += 2)
                {
                    CreateBeveledBox($"I-beam flange {(side < 0 ? "front" : "rear")}", rod,
                        new Vector3(0f, railCentreM, side * bigOuterRadiusM * 0.25f),
                        new Vector3(rodThicknessM, railLengthM, rodThicknessM * 0.22f),
                        rodThicknessM * 0.09f, machinedSteelMaterial, null);
                }
                CreateBeveledBox("Separate big-end cap", rod,
                    new Vector3(0f, -bigOuterRadiusM * 0.62f, 0f),
                    new Vector3(rodThicknessM * 1.10f, bigOuterRadiusM * 0.52f, bigOuterRadiusM * 1.42f),
                    rodThicknessM * 0.12f, machinedSteelMaterial, null);
                for (int side = -1; side <= 1; side += 2)
                {
                    CreateCylinderBetween($"Big-end cap bolt {(side < 0 ? "front" : "rear")}", rod,
                        new Vector3(-rodThicknessM * 0.63f, -bigOuterRadiusM * 0.42f,
                            side * bigOuterRadiusM * 0.58f),
                        new Vector3(rodThicknessM * 0.63f, -bigOuterRadiusM * 0.42f,
                            side * bigOuterRadiusM * 0.58f),
                        rodThicknessM * 0.13f, bearingMaterial, null);
                }
            }
        }

        private void CreatePistonGeometry(Transform parent, float diameterM, int cylinderNumber)
        {
            float crownThicknessM = boreM * 0.105f;
            float crownCentreYM = pistonCompressionHeightM - crownThicknessM * 0.5f;
            CreateCylinderBetween("Crowned piston top", parent,
                new Vector3(0f, crownCentreYM - crownThicknessM * 0.5f, 0f),
                new Vector3(0f, crownCentreYM + crownThicknessM * 0.5f, 0f),
                diameterM * 0.5f, pistonAluminumMaterial, null);

            float ringLandTopYM = pistonCompressionHeightM - crownThicknessM * 1.05f;
            for (int ring = 0; ring < 3; ring++)
            {
                float y = ringLandTopYM - ring * boreM * 0.045f;
                CreateCylinderBetween($"Ring land {ring + 1}", parent,
                    new Vector3(0f, y - boreM * 0.012f, 0f),
                    new Vector3(0f, y + boreM * 0.012f, 0f),
                    diameterM * (0.502f - ring * 0.004f), ring == 2 ? gasketMaterial : darkSteelMaterial, null);
            }

            CreateCylinderBetween("Pin-band body", parent,
                new Vector3(0f, -boreM * 0.10f, 0f),
                new Vector3(0f, ringLandTopYM - boreM * 0.09f, 0f),
                diameterM * 0.475f, pistonAluminumMaterial, null);

            float skirtCentreYM = -pistonSkirtLengthM * 0.48f;
            for (int side = -1; side <= 1; side += 2)
            {
                CreateBeveledBox($"Skirt thrust panel {(side < 0 ? "front" : "rear")}", parent,
                    new Vector3(0f, skirtCentreYM, side * diameterM * 0.37f),
                    new Vector3(diameterM * 0.72f, pistonSkirtLengthM, diameterM * 0.17f),
                    boreM * 0.025f, pistonAluminumMaterial, null);
            }

            float bossOuterRadiusM = boreM * 0.105f;
            float bossInnerRadiusM = boreM * 0.062f;
            for (int side = -1; side <= 1; side += 2)
            {
                Mesh boss = TrackMesh(ProceduralEngineMeshFactory.CreateRingAlongX(
                    $"Pin boss mesh {cylinderNumber}-{side}", bossInnerRadiusM, bossOuterRadiusM,
                    diameterM * 0.24f, 28));
                CreateMeshPart($"Wrist-pin boss {(side < 0 ? "left" : "right")}", parent, boss,
                    new Vector3(side * diameterM * 0.23f, 0f, 0f), Quaternion.identity,
                    pistonAluminumMaterial, null);
            }
            CreateCylinderBetween("Full wrist pin", parent,
                new Vector3(-diameterM * 0.42f, 0f, 0f), new Vector3(diameterM * 0.42f, 0f, 0f),
                bossInnerRadiusM * 0.92f, machinedSteelMaterial, null);
        }

        private void CreateCylinderHead()
        {
            float headCentreYM = deckYM + headHeightM * 0.5f;
            float bevelM = boreM * 0.055f;
            float coverHeightM = boreM * 0.34f;
            float coverYM = deckYM + headHeightM + coverHeightM * 0.47f;
            Vector3 headSize = new Vector3(blockLengthM * 1.025f, headHeightM, headDepthM);

            CreateHeadBox("Solid aluminium head casting", fullHeadGroup,
                new Vector3(0f, headCentreYM, headDepthM * 0.11f),
                new Vector3(headSize.x, headSize.y, headSize.z * 0.76f), bevelM, castAluminumMaterial, true);
            CreateHeadBox("DOHC cam cover", fullHeadGroup,
                new Vector3(0f, coverYM, 0f),
                new Vector3(blockLengthM * 0.91f, coverHeightM * 0.70f, headDepthM * 0.58f),
                bevelM * 1.3f, darkSteelMaterial, true);
            CreateHeadBox("Head gasket line", fullHeadGroup,
                new Vector3(0f, deckYM + boreM * 0.006f, 0f),
                new Vector3(blockLengthM * 1.02f, boreM * 0.012f, headDepthM * 0.95f),
                boreM * 0.003f, gasketMaterial, true);
            CreateHeadEndFaces(fullHeadGroup, headCentreYM, coverYM, coverHeightM, true);
            CreateFullHeadSculpting(fullHeadGroup, headCentreYM, coverYM, coverHeightM);
            CreatePortBosses(fullHeadGroup, true);

            float rearHalfDepthM = headDepthM * 0.54f;
            CreateHeadBox("Sectioned rear head casting", cutawayHeadGroup,
                new Vector3(0f, headCentreYM, headDepthM * 0.23f),
                new Vector3(blockLengthM * 1.025f, headHeightM, rearHalfDepthM),
                bevelM, castAluminumMaterial, false);
            CreateHeadBox("Sectioned rear cam cover", cutawayHeadGroup,
                new Vector3(0f, coverYM, headDepthM * 0.18f),
                new Vector3(blockLengthM * 0.99f, coverHeightM, headDepthM * 0.45f),
                bevelM, darkSteelMaterial, false);
            CreateHeadBox("Cutaway fire deck", cutawayHeadGroup,
                new Vector3(0f, deckYM + boreM * 0.012f, headDepthM * 0.10f),
                new Vector3(blockLengthM * 1.02f, boreM * 0.024f, headDepthM * 0.78f),
                boreM * 0.006f, bearingMaterial, false);
            CreateHeadEndFaces(cutawayHeadGroup, headCentreYM, coverYM, coverHeightM, false);
            CreatePortBosses(cutawayHeadGroup, false);
        }

        private void CreateFullHeadSculpting(
            Transform parent,
            float headCentreYM,
            float coverYM,
            float coverHeightM)
        {
            for (int i = 0; i < 4; i++)
            {
                CreateBeveledBox($"Combustion bay casting {i + 1}", parent,
                    new Vector3(cylinderXM[i], headCentreYM - headHeightM * 0.10f, -headDepthM * 0.34f),
                    new Vector3(spacingM * 0.88f, headHeightM * 0.58f, headDepthM * 0.34f),
                    boreM * 0.045f, castAluminumMaterial, opaqueHeadRenderers);
                for (int port = -1; port <= 1; port += 2)
                {
                    CreateBeveledBox($"Oval exhaust-port land {i + 1}-{port}", parent,
                        new Vector3(cylinderXM[i] + port * boreM * 0.17f,
                            headCentreYM - headHeightM * 0.08f, -headDepthM * 0.55f),
                        new Vector3(boreM * 0.18f, boreM * 0.23f, boreM * 0.11f),
                        boreM * 0.035f,
                        castAluminumMaterial, opaqueHeadRenderers);
                }
            }

            float crownRadiusM = headDepthM * 0.22f;
            CreateCylinderBetween("Rounded cam-cover crown", parent,
                new Vector3(-blockLengthM * 0.43f, coverYM + coverHeightM * 0.22f, 0f),
                new Vector3(blockLengthM * 0.43f, coverYM + coverHeightM * 0.22f, 0f),
                crownRadiusM, darkSteelMaterial, opaqueHeadRenderers);
            CreateCylinderBetween("Oil filler neck", parent,
                new Vector3(blockLengthM * 0.27f, coverYM + coverHeightM * 0.28f, -headDepthM * 0.12f),
                new Vector3(blockLengthM * 0.27f, coverYM + coverHeightM * 0.68f, -headDepthM * 0.12f),
                boreM * 0.12f, darkSteelMaterial, opaqueHeadRenderers);
            CreateCylinderBetween("Oil filler cap", parent,
                new Vector3(blockLengthM * 0.27f, coverYM + coverHeightM * 0.66f, -headDepthM * 0.12f),
                new Vector3(blockLengthM * 0.27f, coverYM + coverHeightM * 0.78f, -headDepthM * 0.12f),
                boreM * 0.16f, gasketMaterial, opaqueHeadRenderers);
        }

        private void CreateHeadEndFaces(
            Transform parent,
            float headCentreYM,
            float coverYM,
            float coverHeightM,
            bool trackAsOpaqueHead)
        {
            float faceThicknessM = boreM * 0.065f;
            float x = blockLengthM * 0.5f + faceThicknessM * 0.22f;
            for (int side = -1; side <= 1; side += 2)
            {
                CreateHeadBox(side < 0 ? "Front head face" : "Rear head face", parent,
                    new Vector3(side * x, headCentreYM, 0f),
                    new Vector3(faceThicknessM, headHeightM * 0.94f, headDepthM * 1.02f),
                    faceThicknessM * 0.32f, castAluminumMaterial, trackAsOpaqueHead);
                CreateHeadBox(side < 0 ? "Front cover end" : "Rear cover end", parent,
                    new Vector3(side * x, coverYM, 0f),
                    new Vector3(faceThicknessM, coverHeightM * 0.82f, headDepthM * 0.78f),
                    faceThicknessM * 0.32f, darkSteelMaterial, trackAsOpaqueHead);
            }
        }

        private void CreatePortBosses(Transform parent, bool trackAsOpaqueHead)
        {
            float y = deckYM + headHeightM * 0.40f;
            float portRadiusM = boreM * 0.105f;
            for (int i = 0; i < 4; i++)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float z0 = side * headDepthM * 0.46f;
                    float z1 = side * headDepthM * 0.60f;
                    CreateCylinderBetween($"{(side < 0 ? "Exhaust" : "Intake")} port boss {i + 1}", parent,
                        new Vector3(cylinderXM[i], y, z0), new Vector3(cylinderXM[i], y + boreM * 0.025f, z1),
                        portRadiusM, castAluminumMaterial, trackAsOpaqueHead ? opaqueHeadRenderers : null);
                }
            }
        }

        private void CreateValvetrain()
        {
            camshaftRotors = new Transform[2];
            float camSpacingM = headDepthM * camshaftSpacingHeadDepthMultiplier;
            float camYM = deckYM + headHeightM * 0.72f;
            float camRadiusM = boreM * 0.075f;
            float camLengthM = blockLengthM * 0.91f;

            for (int cam = 0; cam < 2; cam++)
            {
                float z = (cam == 0 ? -1f : 1f) * camSpacingM * 0.5f;
                Transform rotor = CreateGroup(cam == 0 ? "Exhaust camshaft" : "Intake camshaft", valvetrainGroup);
                rotor.localPosition = new Vector3(0f, camYM, z);
                camshaftRotors[cam] = rotor;
                CreateCylinderBetween("Hollow camshaft", rotor,
                    new Vector3(-camLengthM * 0.5f, 0f, 0f), new Vector3(camLengthM * 0.5f, 0f, 0f),
                    camRadiusM * 0.62f, darkSteelMaterial, null);

                for (int cylinder = 0; cylinder < 4; cylinder++)
                for (int valve = 0; valve < 2; valve++)
                {
                    float x = cylinderXM[cylinder] + (valve == 0 ? -1f : 1f) * boreM * 0.15f;
                    CreateEllipsoid($"Cam lobe {cylinder + 1}-{valve + 1}", rotor,
                        new Vector3(x, 0f, 0f),
                        new Vector3(boreM * 0.11f, boreM * 0.095f, boreM * 0.145f),
                        darkSteelMaterial, null);
                }
            }

            for (int bearing = 0; bearing < 5; bearing++)
            {
                float x = (bearing - 2f) * spacingM;
                for (int cam = 0; cam < 2; cam++)
                {
                    float z = (cam == 0 ? -1f : 1f) * camSpacingM * 0.5f;
                    CreateBeveledBox($"Cam cap {bearing + 1}-{cam + 1}", valvetrainGroup,
                        new Vector3(x, camYM + camRadiusM * 0.70f, z),
                        new Vector3(boreM * 0.12f, boreM * 0.07f, boreM * 0.20f),
                        boreM * 0.012f, bearingMaterial, null);
                }
            }

            float halfValveAngleRad = valveIncludedAngleDeg * 0.5f * Mathf.Deg2Rad;
            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                CreateCombustionChamber(cylinder);
                for (int bank = -1; bank <= 1; bank += 2)
                for (int valve = 0; valve < 2; valve++)
                {
                    float x = cylinderXM[cylinder] + (valve == 0 ? -1f : 1f) * boreM * 0.16f;
                    float bottomZ = bank * boreM * 0.15f;
                    float stemLengthM = headHeightM * 0.54f;
                    Vector3 bottom = new Vector3(x, deckYM + boreM * 0.055f, bottomZ);
                    Vector3 direction = new Vector3(0f, Mathf.Cos(halfValveAngleRad),
                        bank * Mathf.Sin(halfValveAngleRad));
                    Vector3 top = bottom + direction * stemLengthM;
                    string prefix = bank < 0 ? "Exhaust" : "Intake";
                    CreateCylinderBetween($"{prefix} valve stem {cylinder + 1}-{valve + 1}", valvetrainGroup,
                        bottom, top, boreM * 0.018f, machinedSteelMaterial, null);
                    CreateCylinderBetween($"{prefix} valve head {cylinder + 1}-{valve + 1}", valvetrainGroup,
                        bottom - direction * boreM * 0.014f, bottom + direction * boreM * 0.010f,
                        boreM * 0.095f, machinedSteelMaterial, null);
                    for (int spring = 0; spring < 4; spring++)
                    {
                        float t = 0.62f + spring * 0.085f;
                        Vector3 centre = Vector3.Lerp(bottom, top, t);
                        CreateCylinderBetween($"{prefix} spring coil {cylinder + 1}-{valve + 1}-{spring + 1}",
                            valvetrainGroup, centre - direction * boreM * 0.012f,
                            centre + direction * boreM * 0.012f,
                            boreM * (0.052f - spring * 0.002f), darkSteelMaterial, null);
                    }
                    CreateCylinderBetween($"{prefix} port throat {cylinder + 1}-{valve + 1}", headInternalsGroup,
                        bottom + new Vector3(0f, boreM * 0.03f, bank * boreM * 0.02f),
                        new Vector3(x, deckYM + headHeightM * 0.40f, bank * headDepthM * 0.61f),
                        boreM * 0.075f, darkCavityMaterial, null);
                }
            }
        }

        private void CreateCombustionChamber(int cylinder)
        {
            CreateEllipsoid($"Pent-roof combustion chamber {cylinder + 1}", headInternalsGroup,
                new Vector3(cylinderXM[cylinder], deckYM + boreM * 0.035f, 0f),
                new Vector3(boreM * 0.88f, boreM * 0.16f, boreM * 0.78f),
                darkCavityMaterial, null);
        }

        private void CreateLightingRig()
        {
            CreatePointLight("Warm inspection key", lightingGroup,
                new Vector3(-blockLengthM * 0.75f, deckYM + headHeightM * 2.8f, -blockDepthM * 3.0f),
                new Color(1f, 0.88f, 0.72f), 1.6f, 2.5f);
            CreatePointLight("Cool inspection fill", lightingGroup,
                new Vector3(blockLengthM * 0.65f, deckYM + headHeightM * 1.6f, blockDepthM * 2.2f),
                new Color(0.62f, 0.76f, 1f), 0.9f, 2.2f);
        }

        private void UpdateMovingAssembly(float baseCrankAngleDeg)
        {
            if (throwRotors == null || pistonAssemblies == null || connectingRodAssemblies == null) return;

            for (int i = 0; i < 4; i++)
            {
                float angleDeg = Mathf.Repeat(baseCrankAngleDeg + CrankPhaseDeg[i], 360f);
                double angleRad = angleDeg * Mathf.Deg2Rad;
                float crankY = (float)SliderCrankKinematics.CrankPinYM(angleRad, crankRadiusM);
                float crankZ = (float)SliderCrankKinematics.CrankPinZM(angleRad, crankRadiusM);
                float pistonPinY = (float)SliderCrankKinematics.PistonPinHeightM(
                    angleRad, crankRadiusM, rodLengthM);

                throwRotors[i].localRotation = Quaternion.Euler(angleDeg, 0f, 0f);
                Vector3 crankPin = new Vector3(cylinderXM[i], crankY, crankZ);
                Vector3 wristPin = new Vector3(cylinderXM[i], pistonPinY, 0f);
                pistonAssemblies[i].localPosition = wristPin;
                connectingRodAssemblies[i].localPosition = crankPin;
                connectingRodAssemblies[i].localRotation = Quaternion.FromToRotation(Vector3.up, wristPin - crankPin);
            }

            if (camshaftRotors == null) return;
            float camAngleDeg = baseCrankAngleDeg * 0.5f;
            foreach (Transform camshaft in camshaftRotors)
            {
                if (camshaft != null) camshaft.localRotation = Quaternion.Euler(camAngleDeg, 0f, 0f);
            }
        }

        private void ApplyInspectionMode()
        {
            if (generatedRoot == null) return;
            bool full = inspectionMode == EngineInspectionMode.FullEngine;
            bool cutaway = inspectionMode == EngineInspectionMode.Cutaway;
            bool transparent = inspectionMode == EngineInspectionMode.TransparentBlockAndHead;
            bool rotatingOnly = inspectionMode == EngineInspectionMode.RotatingAssemblyOnly;
            bool valvetrainOnly = inspectionMode == EngineInspectionMode.ValvetrainOnly;

            SetActive(fullBlockGroup, full || transparent);
            SetActive(cutawayBlockGroup, cutaway);
            SetActive(blockInternalsGroup, cutaway || transparent);
            SetActive(fullHeadGroup, full || transparent);
            SetActive(cutawayHeadGroup, cutaway);
            SetActive(headInternalsGroup, cutaway || transparent || valvetrainOnly);
            SetActive(crankInternalGroup, cutaway || transparent || rotatingOnly);
            SetActive(crankExternalGroup, full || cutaway || transparent || rotatingOnly);
            SetActive(pistonsAndRodsGroup, cutaway || transparent || rotatingOnly);
            SetActive(valvetrainGroup, cutaway || transparent || valvetrainOnly);
            SetActive(lightingGroup, true);

            foreach (Renderer renderer in opaqueBlockRenderers)
                if (renderer != null) renderer.sharedMaterial = transparent
                    ? transparentBlockMaterial
                    : blockOriginalMaterials[renderer];
            foreach (Renderer renderer in opaqueHeadRenderers)
                if (renderer != null) renderer.sharedMaterial = transparent
                    ? transparentHeadMaterial
                    : headOriginalMaterials[renderer];
        }

        private void CreateMaterials()
        {
            castIronMaterial = CreateMaterial("Cast iron", new Color(0.16f, 0.20f, 0.22f, 1f), 0.25f, 0.28f);
            castAluminumMaterial = CreateMaterial("Cast aluminium", new Color(0.34f, 0.38f, 0.40f, 1f), 0.35f, 0.30f);
            machinedSteelMaterial = CreateMaterial("Machined steel", new Color(0.55f, 0.58f, 0.60f, 1f), 0.80f, 0.70f);
            darkSteelMaterial = CreateMaterial("Dark internal steel", new Color(0.075f, 0.085f, 0.09f, 1f), 0.72f, 0.48f);
            pistonAluminumMaterial = CreateMaterial("Piston aluminium", new Color(0.67f, 0.69f, 0.68f, 1f), 0.50f, 0.52f);
            bearingMaterial = CreateMaterial("Machined bearing surface", new Color(0.66f, 0.58f, 0.34f, 1f), 0.78f, 0.78f);
            gasketMaterial = CreateMaterial("Gasket and seals", new Color(0.10f, 0.07f, 0.055f, 1f), 0.10f, 0.22f);
            darkCavityMaterial = CreateMaterial("Internal cavities", new Color(0.018f, 0.023f, 0.026f, 1f), 0.05f, 0.12f);
            transparentBlockMaterial = CreateMaterial("Transparent block", new Color(0.16f, 0.24f, 0.27f, 0.42f), 0.20f, 0.30f, true);
            transparentHeadMaterial = CreateMaterial("Transparent head", new Color(0.48f, 0.57f, 0.61f, 0.36f), 0.25f, 0.34f, true);
        }

        private static Material CreateMaterial(
            string materialName,
            Color color,
            float metallic,
            float smoothness,
            bool transparent = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var material = new Material(shader)
            {
                name = materialName,
                color = color,
                hideFlags = HideFlags.DontSave
            };
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);

            if (transparent)
            {
                material.SetOverrideTag("RenderType", "Transparent");
                material.SetFloat("_Surface", 1f);
                material.SetFloat("_Blend", 0f);
                material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("DepthOnly", false);
                material.renderQueue = (int)RenderQueue.Transparent;
            }

            return material;
        }

        private Transform CreateGroup(string groupName, Transform parent = null)
        {
            Transform group = new GameObject(groupName).transform;
            group.SetParent(parent != null ? parent : generatedRoot, false);
            return group;
        }

        private void CreateBlockBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            float bevel,
            Material material,
            bool trackAsOpaqueBlock)
        {
            CreateBeveledBox(name, parent, position, size, bevel, material,
                trackAsOpaqueBlock ? opaqueBlockRenderers : null);
        }

        private void CreateHeadBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            float bevel,
            Material material,
            bool trackAsOpaqueHead)
        {
            CreateBeveledBox(name, parent, position, size, bevel, material,
                trackAsOpaqueHead ? opaqueHeadRenderers : null);
        }

        private Transform CreateBeveledBox(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            float bevel,
            Material material,
            List<Renderer> rendererCollector)
        {
            Mesh mesh = TrackMesh(ProceduralEngineMeshFactory.CreateBeveledBox(name + " mesh", size, bevel));
            return CreateMeshPart(name, parent, mesh, position, Quaternion.identity, material, rendererCollector);
        }

        private Transform CreateMeshPart(
            string name,
            Transform parent,
            Mesh mesh,
            Vector3 position,
            Quaternion rotation,
            Material material,
            List<Renderer> rendererCollector)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            TrackRenderer(renderer, rendererCollector, material);
            return go.transform;
        }

        private Transform CreateCylinderBetween(
            string name,
            Transform parent,
            Vector3 a,
            Vector3 b,
            float radius,
            Material material,
            List<Renderer> rendererCollector)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            RemoveCollider(go);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            TrackRenderer(renderer, rendererCollector, material);

            Vector3 direction = b - a;
            float length = Mathf.Max(0.000001f, direction.magnitude);
            go.transform.localPosition = (a + b) * 0.5f;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            return go.transform;
        }

        private Transform CreateEllipsoid(
            string name,
            Transform parent,
            Vector3 position,
            Vector3 size,
            Material material,
            List<Renderer> rendererCollector)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            RemoveCollider(go);
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            TrackRenderer(renderer, rendererCollector, material);
            return go.transform;
        }

        private void TrackRenderer(Renderer renderer, List<Renderer> collector, Material originalMaterial)
        {
            if (collector == null) return;
            collector.Add(renderer);
            if (ReferenceEquals(collector, opaqueBlockRenderers)) blockOriginalMaterials[renderer] = originalMaterial;
            if (ReferenceEquals(collector, opaqueHeadRenderers)) headOriginalMaterials[renderer] = originalMaterial;
        }

        private void CreateAngledRib(
            string name,
            Transform parent,
            Vector3 a,
            Vector3 b,
            float thickness,
            Material material,
            List<Renderer> rendererCollector)
        {
            Vector3 direction = b - a;
            Transform rib = CreateBeveledBox(name, parent, (a + b) * 0.5f,
                new Vector3(thickness, direction.magnitude, thickness * 0.65f),
                thickness * 0.22f, material, rendererCollector);
            rib.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
        }

        private static void CreatePointLight(
            string name,
            Transform parent,
            Vector3 position,
            Color color,
            float intensity,
            float range)
        {
            var go = new GameObject(name, typeof(Light));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            Light light = go.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            // The scene's directional light provides the primary contact shadows. These
            // local inspection lights are fill/key sources; disabling their six-face point
            // shadows avoids exhausting URP's additional-light shadow atlas.
            light.shadows = LightShadows.None;
            light.shadowStrength = 0.45f;
        }

        private Mesh TrackMesh(Mesh mesh)
        {
            generatedMeshes.Add(mesh);
            return mesh;
        }

        private void CaptureConfiguration()
        {
            lastBoreMm = controller.BoreMm;
            lastStrokeMm = controller.StrokeMm;
            lastRodLengthMm = controller.ConnectingRodLengthMm;
            lastCylinderCount = controller.CylinderCount;
            lastBlockDepthMultiplier = blockDepthBoreMultiplier;
            lastHeadHeightMultiplier = headHeightBoreMultiplier;
            lastHeadDepthMultiplier = headDepthBoreMultiplier;
        }

        private void ValidatePresentationAssumptions()
        {
            teachingAnimationRpm = Mathf.Clamp(teachingAnimationRpm, 0f, 300f);
            blockDepthBoreMultiplier = Mathf.Max(1.05f, blockDepthBoreMultiplier);
            crankcaseDepthBelowAxisBoreMultiplier = Mathf.Max(0.25f, crankcaseDepthBelowAxisBoreMultiplier);
            headHeightBoreMultiplier = Mathf.Max(0.35f, headHeightBoreMultiplier);
            headDepthBoreMultiplier = Mathf.Max(1.1f, headDepthBoreMultiplier);
            nominalCastingWallBoreMultiplier = Mathf.Clamp(nominalCastingWallBoreMultiplier, 0.02f, 0.12f);
            deckThicknessBoreMultiplier = Mathf.Clamp(deckThicknessBoreMultiplier, 0.01f, 0.08f);
            pistonDiameterBoreMultiplier = Mathf.Clamp(pistonDiameterBoreMultiplier, 0.82f, 0.98f);
            pistonCompressionHeightBoreMultiplier = Mathf.Clamp(pistonCompressionHeightBoreMultiplier, 0.18f, 0.42f);
            pistonSkirtLengthBoreMultiplier = Mathf.Clamp(pistonSkirtLengthBoreMultiplier, 0.30f, 0.70f);
            connectingRodBigEndOuterBoreMultiplier = Mathf.Clamp(connectingRodBigEndOuterBoreMultiplier, 0.12f, 0.30f);
            connectingRodSmallEndOuterBoreMultiplier = Mathf.Clamp(connectingRodSmallEndOuterBoreMultiplier, 0.05f, 0.16f);
            valveIncludedAngleDeg = Mathf.Clamp(valveIncludedAngleDeg, 8f, 28f);
            camshaftSpacingHeadDepthMultiplier = Mathf.Clamp(camshaftSpacingHeadDepthMultiplier, 0.18f, 0.42f);
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
            opaqueBlockRenderers.Clear();
            opaqueHeadRenderers.Clear();
            blockOriginalMaterials.Clear();
            headOriginalMaterials.Clear();

            DestroyMaterial(ref castIronMaterial);
            DestroyMaterial(ref castAluminumMaterial);
            DestroyMaterial(ref machinedSteelMaterial);
            DestroyMaterial(ref darkSteelMaterial);
            DestroyMaterial(ref pistonAluminumMaterial);
            DestroyMaterial(ref bearingMaterial);
            DestroyMaterial(ref gasketMaterial);
            DestroyMaterial(ref darkCavityMaterial);
            DestroyMaterial(ref transparentBlockMaterial);
            DestroyMaterial(ref transparentHeadMaterial);
        }

        private static void SetActive(Transform group, bool active)
        {
            if (group != null) group.gameObject.SetActive(active);
        }

        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Destroy(collider);
            else DestroyImmediate(collider);
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
