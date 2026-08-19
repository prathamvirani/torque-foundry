using System;
using System.Collections.Generic;
using UnityEngine;
using VehicleEngineeringSandbox.Core.ICE;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    public enum TimingDriveType
    {
        Chain,
        Belt,
        Gear
    }

    [Serializable]
    public sealed class TimingDrivePresentationDefinition
    {
        [SerializeField] private TimingDriveType driveType = TimingDriveType.Chain;
        [SerializeField, Min(8)] private int crankSprocketTeeth = 24;
        [SerializeField, Min(16)] private int camSprocketTeeth = 48;
        [SerializeField, Min(0.001f)] private float visualChainThicknessM = 0.0045f;
        [SerializeField, Range(12, 48)] private int movingMarkerCount = 28;

        public TimingDriveType DriveType => driveType;
        public int CrankSprocketTeeth => crankSprocketTeeth;
        public int CamSprocketTeeth => camSprocketTeeth;
        public float VisualChainThicknessM => visualChainThicknessM;
        public int MovingMarkerCount => movingMarkerCount;
        public float CamToCrankSpeedRatio => crankSprocketTeeth / (float)camSprocketTeeth;

        public void Validate()
        {
            crankSprocketTeeth = Mathf.Max(8, crankSprocketTeeth);
            camSprocketTeeth = Mathf.Max(16, camSprocketTeeth);
            visualChainThicknessM = Mathf.Max(0.001f, visualChainThicknessM);
            movingMarkerCount = Mathf.Clamp(movingMarkerCount, 12, 48);
        }
    }

    public sealed partial class InlineFourVisualFidelityAssembly
    {
        [Header("Visual Fidelity v2 Teaching Assumptions")]
        [SerializeField, Range(0.05f, 0.16f)] private float maximumValveLiftBoreMultiplier = 0.105f;
        [SerializeField] private float intakeCamPhaseOffsetDeg = 105f;
        [SerializeField] private float exhaustCamPhaseOffsetDeg = -15f;
        [SerializeField] private bool showAirflowPaths = true;
        [SerializeField] private bool showCycleHighlights = true;
        [SerializeField] private TimingDrivePresentationDefinition timingDriveDefinition = new TimingDrivePresentationDefinition();

        private Transform timingDriveGroup;
        private Transform timingCoverGroup;
        private Transform airflowPathGroup;
        private Transform cycleHighlightGroup;
        private Transform crankTimingSprocket;
        private Transform intakeCamSprocket;
        private Transform exhaustCamSprocket;
        private Transform[] timingChainMarkers;
        private Vector3[] timingChainPath;
        private float[] timingChainCumulativeLengthM;
        private float timingChainLengthM;
        private float crankTimingSprocketRadiusM;
        private Transform[] intakeValves;
        private Transform[] exhaustValves;
        private Transform[] intakeSprings;
        private Transform[] exhaustSprings;
        private Vector3[] intakeValveClosedPositions;
        private Vector3[] exhaustValveClosedPositions;
        private Vector3[] intakeValveAxes;
        private Vector3[] exhaustValveAxes;
        private Renderer[] cycleHighlightRenderers;
        private readonly List<PortPathDefinition> intakePortPaths = new List<PortPathDefinition>();
        private readonly List<PortPathDefinition> exhaustPortPaths = new List<PortPathDefinition>();

        private Material castAluminumDarkMaterial;
        private Material chainMaterial;
        private Material intakePortMaterial;
        private Material exhaustPortMaterial;
        private Material intakeAirflowMaterial;
        private Material exhaustAirflowMaterial;
        private Material cycleIntakeMaterial;
        private Material cycleCompressionMaterial;
        private Material cyclePowerMaterial;
        private Material cycleExhaustMaterial;

        private float timingFrontXM;
        private float camshaftYM;
        private float intakeCamZM;
        private float exhaustCamZM;
        private float maximumValveLiftM;

        public float IntakeCamAngleDeg { get; private set; }
        public float ExhaustCamAngleDeg { get; private set; }
        public TimingDriveType ActiveTimingDriveType => timingDriveDefinition?.DriveType ?? TimingDriveType.Chain;
        public float TimingCamToCrankSpeedRatio => timingDriveDefinition?.CamToCrankSpeedRatio ?? 0.5f;

        public double GetNormalizedValveLift(int cylinderIndex, ValveSide side)
        {
            return ValveTimingKinematics.NormalizedValveLift(CurrentCrankAngleDeg, cylinderIndex, side);
        }

        public FourStrokePhase GetCylinderPhase(int cylinderIndex)
        {
            return ValveTimingKinematics.CylinderPhase(CurrentCrankAngleDeg, cylinderIndex);
        }

        private readonly struct PortPathDefinition
        {
            public PortPathDefinition(int cylinderIndex, int valveIndex, ValveSide side, Vector3[] path)
            {
                CylinderIndex = cylinderIndex;
                ValveIndex = valveIndex;
                Side = side;
                Path = path;
            }

            public int CylinderIndex { get; }
            public int ValveIndex { get; }
            public ValveSide Side { get; }
            public Vector3[] Path { get; }
        }

        private void CreateCylinderBlock()
        {
            float halfLengthM = blockLengthM * 0.5f;
            float splitYM = -crankRadiusM - boreM * 0.13f;
            float halfDepthM = blockDepthM * 0.5f;
            Vector2[] blockProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(deckYM, halfDepthM * 0.88f),
                    new Vector2(deckYM - boreM * 0.12f, halfDepthM),
                    new Vector2(crankRadiusM * 0.55f, halfDepthM * 0.92f),
                    new Vector2(-crankRadiusM * 0.45f, halfDepthM * 1.05f),
                    new Vector2(splitYM + boreM * 0.08f, halfDepthM * 0.90f),
                    new Vector2(splitYM, halfDepthM * 0.67f),
                    new Vector2(splitYM, -halfDepthM * 0.67f),
                    new Vector2(splitYM + boreM * 0.08f, -halfDepthM * 0.90f),
                    new Vector2(-crankRadiusM * 0.45f, -halfDepthM * 1.05f),
                    new Vector2(crankRadiusM * 0.55f, -halfDepthM * 0.92f),
                    new Vector2(deckYM - boreM * 0.12f, -halfDepthM),
                    new Vector2(deckYM, -halfDepthM * 0.88f)
                }, 2);

            CreateLoftPart("Continuous rounded cylinder-block casting", fullBlockGroup,
                CreateCylinderBankLoftRings(blockProfile, halfLengthM),
                Vector3.zero, castAluminumDarkMaterial, opaqueBlockRenderers);

            Vector2[] cutawayProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(deckYM, -boreM * 0.035f),
                    new Vector2(deckYM, halfDepthM * 0.88f),
                    new Vector2(deckYM - boreM * 0.12f, halfDepthM),
                    new Vector2(crankRadiusM * 0.55f, halfDepthM * 0.92f),
                    new Vector2(-crankRadiusM * 0.45f, halfDepthM * 1.05f),
                    new Vector2(splitYM, halfDepthM * 0.67f),
                    new Vector2(splitYM, -boreM * 0.035f)
                }, 2);
            CreateLoftPart("Machined half-section block casting", cutawayBlockGroup,
                new[]
                {
                    new ProfileLoftRing(-halfLengthM, ScaleProfile(cutawayProfile, 0.94f, 0.93f)),
                    new ProfileLoftRing(-halfLengthM + boreM * 0.11f, cutawayProfile),
                    new ProfileLoftRing(halfLengthM - boreM * 0.10f, cutawayProfile),
                    new ProfileLoftRing(halfLengthM, ScaleProfile(cutawayProfile, 0.95f, 0.94f))
                }, Vector3.zero, castAluminumDarkMaterial, null);

            CreateCylinderBankExternalSculpting(fullBlockGroup, true);
            CreateDeckAndBoreLandsV2(fullBlockGroup, true);
            CreateDeckAndBoreLandsV2(cutawayBlockGroup, false);
            CreateLowerLadderAndOilPan(fullBlockGroup, splitYM, true);
            CreateLowerLadderAndOilPan(cutawayBlockGroup, splitYM, false);
            CreateTimingAndBellhousingCastings(fullBlockGroup, true);
            CreateTimingAndBellhousingCastings(cutawayBlockGroup, false);
            CreateCastBlockRibsAndBosses(fullBlockGroup, true);
        }

        private void CreateCylinderBankExternalSculpting(Transform parent, bool fullCasting)
        {
            float frontSurfaceZ = -blockDepthM * 0.49f;
            float upperBottomYM = crankRadiusM * 0.30f;
            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                CreateSweptPart($"Integrated rounded cylinder bay {cylinder + 1}", parent,
                    new[]
                    {
                        new Vector3(cylinderXM[cylinder], upperBottomYM, frontSurfaceZ),
                        new Vector3(cylinderXM[cylinder], deckYM - boreM * 0.12f, frontSurfaceZ)
                    }, boreM * 0.16f, boreM * 0.43f, castAluminumDarkMaterial,
                    fullCasting ? opaqueBlockRenderers : null);
            }

            CreateSweptPart("Upper water-jacket tie rib", parent,
                new[]
                {
                    new Vector3(-blockLengthM * 0.46f, deckYM - boreM * 0.20f, frontSurfaceZ - boreM * 0.05f),
                    new Vector3(0f, deckYM - boreM * 0.18f, frontSurfaceZ - boreM * 0.06f),
                    new Vector3(blockLengthM * 0.46f, deckYM - boreM * 0.20f, frontSurfaceZ - boreM * 0.05f)
                }, boreM * 0.048f, boreM * 0.032f, castAluminumDarkMaterial,
                fullCasting ? opaqueBlockRenderers : null);

            for (int bearing = 0; bearing < 5; bearing++)
            {
                float x = (bearing - 2f) * spacingM;
                CreateSweptPart($"Rounded lower main bay {bearing + 1}", parent,
                    new[]
                    {
                        new Vector3(x, blockBottomYM + boreM * 0.10f, frontSurfaceZ + boreM * 0.01f),
                        new Vector3(x, -boreM * 0.22f, frontSurfaceZ - boreM * 0.015f)
                    }, boreM * 0.13f, boreM * 0.28f, castAluminumDarkMaterial,
                    fullCasting ? opaqueBlockRenderers : null);
            }
        }

        private ProfileLoftRing[] CreateCylinderBankLoftRings(
            IReadOnlyList<Vector2> blockProfile,
            float halfLengthM)
        {
            var rings = new List<ProfileLoftRing>(10)
            {
                new ProfileLoftRing(-halfLengthM, ScaleProfile(blockProfile, 0.94f, 0.90f))
            };
            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                rings.Add(new ProfileLoftRing(cylinderXM[cylinder],
                    ScaleProfile(blockProfile, 1.0f, 1.035f)));
                if (cylinder < 3)
                {
                    float valleyX = (cylinderXM[cylinder] + cylinderXM[cylinder + 1]) * 0.5f;
                    rings.Add(new ProfileLoftRing(valleyX,
                        ScaleProfile(blockProfile, 0.995f, 0.90f)));
                }
            }
            rings.Add(new ProfileLoftRing(halfLengthM, ScaleProfile(blockProfile, 0.95f, 0.91f)));
            return rings.ToArray();
        }

        private void CreateDeckAndBoreLandsV2(Transform parent, bool fullCasting)
        {
            Vector2[] deckProfile = ProceduralEngineMeshFactory.CreateRoundedRectangleProfile(
                boreM * 0.055f, blockDepthM * 1.015f, boreM * 0.025f, 5);
            CreateLoftPart("Machined deck land", parent,
                CreateCentredLoftRings(deckProfile, blockLengthM * 1.01f, deckYM),
                Vector3.zero, bearingMaterial, fullCasting ? opaqueBlockRenderers : null);

            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                Mesh ring = TrackMesh(ProceduralEngineMeshFactory.CreateRingAlongX(
                    $"Integrated bore lip mesh {cylinder + 1}", boreM * 0.485f, boreM * 0.535f,
                    boreM * 0.035f, 48));
                Transform lip = CreateMeshPart($"Integrated bore opening {cylinder + 1}", parent, ring,
                    new Vector3(cylinderXM[cylinder], deckYM + boreM * 0.025f, 0f),
                    Quaternion.Euler(0f, 0f, 90f), bearingMaterial,
                    fullCasting ? opaqueBlockRenderers : null);
                lip.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private void CreateLowerLadderAndOilPan(Transform parent, float splitYM, bool fullCasting)
        {
            float halfDepthM = blockDepthM * 0.5f;
            Vector2[] ladderProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(splitYM + boreM * 0.055f, halfDepthM * 0.82f),
                    new Vector2(splitYM - boreM * 0.18f, halfDepthM * 0.76f),
                    new Vector2(blockBottomYM - boreM * 0.03f, halfDepthM * 0.58f),
                    new Vector2(blockBottomYM - boreM * 0.03f, -halfDepthM * 0.58f),
                    new Vector2(splitYM - boreM * 0.18f, -halfDepthM * 0.76f),
                    new Vector2(splitYM + boreM * 0.055f, -halfDepthM * 0.82f)
                }, 2);
            CreateLoftPart("Curved lower main-bearing ladder", parent,
                CreateTaperedLoftRings(ladderProfile, blockLengthM * 1.035f, 0.94f),
                Vector3.zero, castAluminumDarkMaterial, fullCasting ? opaqueBlockRenderers : null);

            Vector2[] panProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(blockBottomYM - boreM * 0.01f, halfDepthM * 0.62f),
                    new Vector2(blockBottomYM - boreM * 0.16f, halfDepthM * 0.56f),
                    new Vector2(blockBottomYM - boreM * 0.43f, halfDepthM * 0.38f),
                    new Vector2(blockBottomYM - boreM * 0.49f, -halfDepthM * 0.30f),
                    new Vector2(blockBottomYM - boreM * 0.23f, -halfDepthM * 0.56f),
                    new Vector2(blockBottomYM - boreM * 0.01f, -halfDepthM * 0.62f)
                }, 3);
            CreateLoftPart("Curved wet-sump oil pan", parent,
                CreateTaperedLoftRings(panProfile, blockLengthM * 0.96f, 0.86f),
                Vector3.zero, darkSteelMaterial, fullCasting ? opaqueBlockRenderers : null);

            Vector2[] railProfile = ProceduralEngineMeshFactory.CreateRoundedRectangleProfile(
                boreM * 0.055f, blockDepthM * 0.91f, boreM * 0.022f, 4);
            CreateLoftPart("Curved sump rail", parent,
                CreateCentredLoftRings(railProfile, blockLengthM * 1.07f, blockBottomYM - boreM * 0.015f),
                Vector3.zero, bearingMaterial, fullCasting ? opaqueBlockRenderers : null);
        }

        private void CreateTimingAndBellhousingCastings(Transform parent, bool fullCasting)
        {
            timingFrontXM = -blockLengthM * 0.5f - boreM * 0.13f;
            float halfDepthM = blockDepthM * 0.5f;
            Vector2[] timingProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(deckYM + headHeightM * 0.84f, halfDepthM * 0.62f),
                    new Vector2(deckYM + headHeightM * 0.98f, 0f),
                    new Vector2(deckYM + headHeightM * 0.84f, -halfDepthM * 0.62f),
                    new Vector2(deckYM - boreM * 0.14f, -halfDepthM * 0.74f),
                    new Vector2(blockBottomYM + boreM * 0.08f, -halfDepthM * 0.58f),
                    new Vector2(blockBottomYM - boreM * 0.02f, 0f),
                    new Vector2(blockBottomYM + boreM * 0.08f, halfDepthM * 0.58f),
                    new Vector2(deckYM - boreM * 0.14f, halfDepthM * 0.74f)
                }, 2);
            CreateLoftPart("Front timing-case casting continuity", parent,
                new[]
                {
                    new ProfileLoftRing(timingFrontXM - boreM * 0.07f, ScaleProfile(timingProfile, 0.93f, 0.92f)),
                    new ProfileLoftRing(timingFrontXM + boreM * 0.12f, timingProfile)
                }, Vector3.zero, castAluminumMaterial, fullCasting ? opaqueBlockRenderers : null);

            float rearX = blockLengthM * 0.5f;
            Vector2[] rearProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(deckYM * 0.78f, halfDepthM * 0.82f),
                    new Vector2(deckYM * 0.94f, 0f),
                    new Vector2(deckYM * 0.78f, -halfDepthM * 0.82f),
                    new Vector2(-boreM * 0.34f, -halfDepthM),
                    new Vector2(blockBottomYM - boreM * 0.02f, -halfDepthM * 0.61f),
                    new Vector2(blockBottomYM - boreM * 0.08f, 0f),
                    new Vector2(blockBottomYM - boreM * 0.02f, halfDepthM * 0.61f),
                    new Vector2(-boreM * 0.34f, halfDepthM)
                }, 2);
            CreateLoftPart("Rear flywheel flange casting", parent,
                new[]
                {
                    new ProfileLoftRing(rearX - boreM * 0.035f, rearProfile),
                    new ProfileLoftRing(rearX + boreM * 0.09f, ScaleProfile(rearProfile, 1.02f, 1.04f))
                }, Vector3.zero, castAluminumDarkMaterial, fullCasting ? opaqueBlockRenderers : null);
        }

        private void CreateCastBlockRibsAndBosses(Transform parent, bool fullCasting)
        {
            float frontSurfaceZ = -blockDepthM * 0.51f;
            for (int bearing = 0; bearing < 5; bearing++)
            {
                float x = (bearing - 2f) * spacingM;
                CreateSweptPart($"Cast crankcase rib {bearing + 1}", parent,
                    new[]
                    {
                        new Vector3(x, blockBottomYM + boreM * 0.08f, frontSurfaceZ),
                        new Vector3(x, -boreM * 0.17f, frontSurfaceZ - boreM * 0.015f),
                        new Vector3(x + (bearing % 2 == 0 ? 1f : -1f) * boreM * 0.14f,
                            deckYM - boreM * 0.17f, frontSurfaceZ)
                    }, boreM * 0.050f, boreM * 0.030f, castAluminumDarkMaterial,
                    fullCasting ? opaqueBlockRenderers : null);
            }

            for (int boss = 0; boss < 3; boss++)
            {
                float x = (boss - 1f) * spacingM * 1.15f;
                CreateSweptPart($"Accessory mounting boss {boss + 1}", parent,
                    new[]
                    {
                        new Vector3(x, -boreM * 0.10f, frontSurfaceZ - boreM * 0.035f),
                        new Vector3(x, -boreM * 0.10f, frontSurfaceZ - boreM * 0.11f)
                    }, boreM * 0.12f, boreM * 0.095f, castAluminumDarkMaterial,
                    fullCasting ? opaqueBlockRenderers : null);
            }

            CreateSweptPart("Starter mounting boss", parent,
                new[]
                {
                    new Vector3(blockLengthM * 0.38f, -boreM * 0.28f, blockDepthM * 0.49f),
                    new Vector3(blockLengthM * 0.38f, -boreM * 0.28f, blockDepthM * 0.63f)
                }, boreM * 0.15f, boreM * 0.11f, castAluminumDarkMaterial,
                fullCasting ? opaqueBlockRenderers : null);

            for (int plug = 0; plug < 3; plug++)
            {
                float x = (plug - 1f) * spacingM * 1.25f;
                CreateSweptPart($"Machined side core plug boss {plug + 1}", parent,
                    new[]
                    {
                        new Vector3(x, boreM * 0.20f, frontSurfaceZ - boreM * 0.02f),
                        new Vector3(x, boreM * 0.20f, frontSurfaceZ - boreM * 0.10f)
                    }, boreM * 0.105f, boreM * 0.105f, bearingMaterial, null);
            }
        }

        private void CreateBlockInternals()
        {
            float linerBottomYM = rodLengthM - crankRadiusM - pistonSkirtLengthM * 0.78f;
            float linerHeightM = deckYM - linerBottomYM;
            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                Mesh liner = TrackMesh(ProceduralEngineMeshFactory.CreateTubeSectorAlongY(
                    $"Sectioned liner mesh {cylinder + 1}", boreM * 0.5f, boreM * 0.516f,
                    linerHeightM, -32f, 248f, 56));
                CreateMeshPart($"Cylinder liner {cylinder + 1}", blockInternalsGroup, liner,
                    new Vector3(cylinderXM[cylinder], linerBottomYM + linerHeightM * 0.5f, 0f),
                    Quaternion.identity, machinedSteelMaterial, null);
            }

            float mainRadiusM = Mathf.Max(0.008f, strokeM * 0.10f);
            for (int bearing = 0; bearing < 5; bearing++)
            {
                float x = (bearing - 2f) * spacingM;
                Vector2[] bulkheadProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                    new[]
                    {
                        new Vector2(boreM * 0.34f, blockDepthM * 0.34f),
                        new Vector2(-boreM * 0.02f, blockDepthM * 0.42f),
                        new Vector2(blockBottomYM + boreM * 0.08f, blockDepthM * 0.31f),
                        new Vector2(blockBottomYM + boreM * 0.08f, -blockDepthM * 0.31f),
                        new Vector2(-boreM * 0.02f, -blockDepthM * 0.42f),
                        new Vector2(boreM * 0.34f, -blockDepthM * 0.34f)
                    }, 2);
                Mesh bulkheadMesh = TrackMesh(ProceduralEngineMeshFactory.CreateExtrudedProfileAlongX(
                    $"Main bulkhead mesh {bearing + 1}", bulkheadProfile, boreM * 0.095f));
                CreateMeshPart($"Main bearing bulkhead {bearing + 1}", blockInternalsGroup, bulkheadMesh,
                    new Vector3(x, 0f, 0f), Quaternion.identity, castIronMaterial, null);

                Mesh saddle = TrackMesh(ProceduralEngineMeshFactory.CreateRingAlongX(
                    $"Main saddle mesh {bearing + 1}", mainRadiusM * 1.02f, mainRadiusM * 1.32f,
                    boreM * 0.12f, 40));
                CreateMeshPart($"Main bearing saddle {bearing + 1}", blockInternalsGroup, saddle,
                    new Vector3(x, 0f, 0f), Quaternion.identity, bearingMaterial, null);

                Vector2[] capProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                    new[]
                    {
                        new Vector2(mainRadiusM * 0.20f, mainRadiusM * 1.45f),
                        new Vector2(-mainRadiusM * 1.72f, mainRadiusM * 1.25f),
                        new Vector2(-mainRadiusM * 2.15f, mainRadiusM * 0.78f),
                        new Vector2(-mainRadiusM * 2.15f, -mainRadiusM * 0.78f),
                        new Vector2(-mainRadiusM * 1.72f, -mainRadiusM * 1.25f),
                        new Vector2(mainRadiusM * 0.20f, -mainRadiusM * 1.45f)
                    }, 2);
                Mesh capMesh = TrackMesh(ProceduralEngineMeshFactory.CreateExtrudedProfileAlongX(
                    $"Main cap mesh {bearing + 1}", capProfile, boreM * 0.14f));
                CreateMeshPart($"Main bearing cap {bearing + 1}", blockInternalsGroup, capMesh,
                    new Vector3(x, 0f, 0f), Quaternion.identity, castAluminumDarkMaterial, null);
                for (int bolt = -1; bolt <= 1; bolt += 2)
                    CreateCylinderBetween($"Main cap bolt {bearing + 1}-{(bolt + 3) / 2}", blockInternalsGroup,
                        new Vector3(x, -mainRadiusM * 1.55f, bolt * mainRadiusM * 1.22f),
                        new Vector3(x, -mainRadiusM * 2.15f, bolt * mainRadiusM * 1.22f),
                        boreM * 0.025f, machinedSteelMaterial, null);
            }
        }

        private void CreateCrankshaft()
        {
            throwRotors = new Transform[4];
            float mainRadiusM = Mathf.Max(0.008f, strokeM * 0.10f);
            float rodRadiusM = Mathf.Max(0.007f, strokeM * 0.085f);
            float mainLengthM = boreM * 0.20f;
            float rodLengthVisualM = boreM * 0.56f;

            for (int bearing = 0; bearing < 5; bearing++)
            {
                float x = (bearing - 2f) * spacingM;
                CreateCylinderBetween($"Main journal {bearing + 1}", crankInternalGroup,
                    new Vector3(x - mainLengthM * 0.5f, 0f, 0f),
                    new Vector3(x + mainLengthM * 0.5f, 0f, 0f), mainRadiusM,
                    machinedSteelMaterial, null);
            }

            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                var rotor = new GameObject($"Curved crank throw {cylinder + 1}").transform;
                rotor.SetParent(crankInternalGroup, false);
                rotor.localPosition = new Vector3(cylinderXM[cylinder], 0f, 0f);
                throwRotors[cylinder] = rotor;

                CreateCylinderBetween($"Rod journal {cylinder + 1}", rotor,
                    new Vector3(-rodLengthVisualM * 0.5f, crankRadiusM, 0f),
                    new Vector3(rodLengthVisualM * 0.5f, crankRadiusM, 0f), rodRadiusM,
                    machinedSteelMaterial, null);

                Vector2[] webProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                    new[]
                    {
                        new Vector2(crankRadiusM + rodRadiusM * 1.55f, rodRadiusM * 0.85f),
                        new Vector2(crankRadiusM * 0.48f, mainRadiusM * 1.65f),
                        new Vector2(-mainRadiusM * 1.45f, mainRadiusM * 1.28f),
                        new Vector2(-mainRadiusM * 2.75f, mainRadiusM * 0.62f),
                        new Vector2(-mainRadiusM * 2.95f, -mainRadiusM * 0.62f),
                        new Vector2(-mainRadiusM * 1.28f, -mainRadiusM * 1.38f),
                        new Vector2(crankRadiusM * 0.52f, -mainRadiusM * 1.50f),
                        new Vector2(crankRadiusM + rodRadiusM * 1.55f, -rodRadiusM * 0.85f)
                    }, 2);
                Mesh webMesh = TrackMesh(ProceduralEngineMeshFactory.CreateExtrudedProfileAlongX(
                    $"Forged web mesh {cylinder + 1}", webProfile, boreM * 0.085f));
                for (int side = -1; side <= 1; side += 2)
                {
                    float x = side * (rodLengthVisualM * 0.5f + boreM * 0.055f);
                    CreateMeshPart($"Curved crank cheek {cylinder + 1}-{(side + 3) / 2}", rotor,
                        webMesh, new Vector3(x, 0f, 0f), Quaternion.identity, darkSteelMaterial, null);
                    CreateCylinderBetween($"Rod journal fillet {cylinder + 1}-{(side + 3) / 2}", rotor,
                        new Vector3(x - boreM * 0.018f, crankRadiusM, 0f),
                        new Vector3(x + boreM * 0.018f, crankRadiusM, 0f), rodRadiusM * 1.14f,
                        machinedSteelMaterial, null);
                }
            }

            float frontX = -blockLengthM * 0.5f;
            CreateCylinderBetween("Crankshaft front snout", crankExternalGroup,
                new Vector3(frontX - boreM * 0.34f, 0f, 0f), new Vector3(frontX, 0f, 0f),
                mainRadiusM * 0.72f, machinedSteelMaterial, null);
            for (int ring = 0; ring < 3; ring++)
            {
                float x = frontX - boreM * (0.20f + ring * 0.035f);
                CreateCylinderBetween($"Crank damper ring {ring + 1}", crankExternalGroup,
                    new Vector3(x - boreM * 0.018f, 0f, 0f), new Vector3(x + boreM * 0.018f, 0f, 0f),
                    boreM * (0.27f - ring * 0.025f), ring == 1 ? gasketMaterial : darkSteelMaterial, null);
            }

            float rearX = blockLengthM * 0.5f;
            CreateCylinderBetween("Rear crank flange", crankExternalGroup,
                new Vector3(rearX, 0f, 0f), new Vector3(rearX + boreM * 0.12f, 0f, 0f),
                boreM * 0.25f, machinedSteelMaterial, null);
            CreateCylinderBetween("Flywheel", crankExternalGroup,
                new Vector3(rearX + boreM * 0.11f, 0f, 0f), new Vector3(rearX + boreM * 0.17f, 0f, 0f),
                boreM * 0.45f, darkSteelMaterial, null);
        }

        private void CreatePistonsAndConnectingRods()
        {
            pistonAssemblies = new Transform[4];
            connectingRodAssemblies = new Transform[4];
            float pistonDiameterM = boreM * Mathf.Max(0.965f, pistonDiameterBoreMultiplier);
            float bigRadiusM = boreM * connectingRodBigEndOuterBoreMultiplier;
            float smallRadiusM = boreM * connectingRodSmallEndOuterBoreMultiplier;
            float wristRadiusM = boreM * 0.055f;
            float rodThicknessM = boreM * 0.105f;

            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                Transform piston = CreateGroup($"Piston assembly {cylinder + 1}", pistonsAndRodsGroup);
                pistonAssemblies[cylinder] = piston;
                CreatePistonV2(cylinder, piston, pistonDiameterM, wristRadiusM);

                Transform rod = CreateGroup($"Forged connecting rod {cylinder + 1}", pistonsAndRodsGroup);
                connectingRodAssemblies[cylinder] = rod;
                CreateCylinderBetween($"Big-end eye {cylinder + 1}", rod,
                    new Vector3(-rodThicknessM * 0.5f, 0f, 0f), new Vector3(rodThicknessM * 0.5f, 0f, 0f),
                    bigRadiusM, darkSteelMaterial, null);
                CreateCylinderBetween($"Big-end bearing {cylinder + 1}", rod,
                    new Vector3(-rodThicknessM * 0.55f, 0f, 0f), new Vector3(rodThicknessM * 0.55f, 0f, 0f),
                    bigRadiusM * 0.61f, bearingMaterial, null);
                CreateCylinderBetween($"Small-end eye {cylinder + 1}", rod,
                    new Vector3(-rodThicknessM * 0.40f, rodLengthM, 0f),
                    new Vector3(rodThicknessM * 0.40f, rodLengthM, 0f),
                    smallRadiusM, darkSteelMaterial, null);
                CreateCylinderBetween($"Small-end bushing {cylinder + 1}", rod,
                    new Vector3(-rodThicknessM * 0.44f, rodLengthM, 0f),
                    new Vector3(rodThicknessM * 0.44f, rodLengthM, 0f),
                    smallRadiusM * 0.58f, bearingMaterial, null);

                Vector2[] beamProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                    new[]
                    {
                        new Vector2(bigRadiusM * 0.72f, bigRadiusM * 0.55f),
                        new Vector2(rodLengthM - smallRadiusM * 0.72f, smallRadiusM * 0.70f),
                        new Vector2(rodLengthM - smallRadiusM * 0.72f, -smallRadiusM * 0.70f),
                        new Vector2(bigRadiusM * 0.72f, -bigRadiusM * 0.55f)
                    }, 2);
                Mesh beamMesh = TrackMesh(ProceduralEngineMeshFactory.CreateExtrudedProfileAlongX(
                    $"Tapered I-beam rod mesh {cylinder + 1}", beamProfile, rodThicknessM * 0.44f));
                CreateMeshPart($"Tapered I-beam web {cylinder + 1}", rod, beamMesh,
                    Vector3.zero, Quaternion.identity, machinedSteelMaterial, null);

                for (int flange = -1; flange <= 1; flange += 2)
                    CreateSweptPart($"Forged rod flange {cylinder + 1}-{(flange + 3) / 2}", rod,
                        new[]
                        {
                            new Vector3(flange * rodThicknessM * 0.36f, bigRadiusM * 0.72f, flange * bigRadiusM * 0.46f),
                            new Vector3(flange * rodThicknessM * 0.36f, rodLengthM * 0.50f, flange * smallRadiusM * 0.54f),
                            new Vector3(flange * rodThicknessM * 0.36f, rodLengthM - smallRadiusM * 0.72f,
                                flange * smallRadiusM * 0.48f)
                        }, rodThicknessM * 0.14f, rodThicknessM * 0.11f, machinedSteelMaterial, null);

                Vector2[] capProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                    new[]
                    {
                        new Vector2(bigRadiusM * 0.06f, bigRadiusM * 1.02f),
                        new Vector2(-bigRadiusM * 0.82f, bigRadiusM * 0.82f),
                        new Vector2(-bigRadiusM * 1.18f, 0f),
                        new Vector2(-bigRadiusM * 0.82f, -bigRadiusM * 0.82f),
                        new Vector2(bigRadiusM * 0.06f, -bigRadiusM * 1.02f)
                    }, 2);
                Mesh capMesh = TrackMesh(ProceduralEngineMeshFactory.CreateExtrudedProfileAlongX(
                    $"Rod cap mesh {cylinder + 1}", capProfile, rodThicknessM * 1.12f));
                CreateMeshPart($"Separate big-end cap {cylinder + 1}", rod, capMesh,
                    Vector3.zero, Quaternion.identity, darkSteelMaterial, null);
                for (int bolt = -1; bolt <= 1; bolt += 2)
                    CreateCylinderBetween($"Rod cap bolt {cylinder + 1}-{(bolt + 3) / 2}", rod,
                        new Vector3(-rodThicknessM * 0.62f, -bigRadiusM * 0.52f, bolt * bigRadiusM * 0.82f),
                        new Vector3(rodThicknessM * 0.62f, -bigRadiusM * 0.52f, bolt * bigRadiusM * 0.82f),
                        boreM * 0.018f, machinedSteelMaterial, null);
            }
        }

        private void CreatePistonV2(int cylinder, Transform parent, float diameterM, float wristRadiusM)
        {
            float radiusM = diameterM * 0.5f;
            float crownThicknessM = boreM * 0.105f;
            float ringBandHeightM = boreM * 0.19f;
            CreateCylinderBetween($"Piston crown {cylinder + 1}", parent,
                new Vector3(0f, pistonCompressionHeightM - crownThicknessM, 0f),
                new Vector3(0f, pistonCompressionHeightM, 0f), radiusM, pistonAluminumMaterial, null);
            CreateCylinderBetween($"Piston ring-land band {cylinder + 1}", parent,
                new Vector3(0f, pistonCompressionHeightM - crownThicknessM - ringBandHeightM, 0f),
                new Vector3(0f, pistonCompressionHeightM - crownThicknessM, 0f),
                radiusM * 0.985f, pistonAluminumMaterial, null);
            for (int ring = 0; ring < 3; ring++)
            {
                float y = pistonCompressionHeightM - crownThicknessM - boreM * (0.025f + ring * 0.047f);
                CreateCylinderBetween($"Piston ring {cylinder + 1}-{ring + 1}", parent,
                    new Vector3(0f, y - boreM * 0.009f, 0f),
                    new Vector3(0f, y + boreM * 0.009f, 0f), radiusM * 1.006f,
                    ring == 2 ? bearingMaterial : darkSteelMaterial, null);
            }
            Mesh skirt = TrackMesh(ProceduralEngineMeshFactory.CreateTubeSectorAlongY(
                $"Thrust skirt mesh {cylinder + 1}", radiusM * 0.78f, radiusM * 0.98f,
                pistonSkirtLengthM, -50f, 280f, 44));
            CreateMeshPart($"Curved piston skirt {cylinder + 1}", parent, skirt,
                new Vector3(0f, -pistonSkirtLengthM * 0.5f, 0f), Quaternion.identity,
                pistonAluminumMaterial, null);
            for (int boss = -1; boss <= 1; boss += 2)
                CreateCylinderBetween($"Piston pin boss {cylinder + 1}-{(boss + 3) / 2}", parent,
                    new Vector3(boss * diameterM * 0.12f, -boreM * 0.10f, 0f),
                    new Vector3(boss * diameterM * 0.34f, -boreM * 0.10f, 0f),
                    boreM * 0.105f, pistonAluminumMaterial, null);
            CreateCylinderBetween($"Wrist pin {cylinder + 1}", parent,
                new Vector3(-diameterM * 0.42f, 0f, 0f), new Vector3(diameterM * 0.42f, 0f, 0f),
                wristRadiusM, machinedSteelMaterial, null);
        }

        private void CreateCylinderHead()
        {
            float halfLengthM = blockLengthM * 0.51f;
            float headBottomYM = deckYM + boreM * 0.018f;
            float headTopYM = deckYM + headHeightM;
            float halfDepthM = headDepthM * 0.5f;
            Vector2[] headProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(headBottomYM, halfDepthM * 0.86f),
                    new Vector2(headBottomYM + headHeightM * 0.28f, halfDepthM),
                    new Vector2(headBottomYM + headHeightM * 0.58f, halfDepthM * 0.93f),
                    new Vector2(headTopYM - headHeightM * 0.12f, halfDepthM * 0.69f),
                    new Vector2(headTopYM, halfDepthM * 0.47f),
                    new Vector2(headTopYM, -halfDepthM * 0.47f),
                    new Vector2(headTopYM - headHeightM * 0.12f, -halfDepthM * 0.69f),
                    new Vector2(headBottomYM + headHeightM * 0.58f, -halfDepthM * 0.93f),
                    new Vector2(headBottomYM + headHeightM * 0.28f, -halfDepthM),
                    new Vector2(headBottomYM, -halfDepthM * 0.86f)
                }, 3);
            CreateLoftPart("Continuous curved cylinder-head casting", fullHeadGroup,
                new[]
                {
                    new ProfileLoftRing(-halfLengthM, ScaleProfile(headProfile, 0.96f, 0.92f)),
                    new ProfileLoftRing(-halfLengthM + boreM * 0.12f, headProfile),
                    new ProfileLoftRing(halfLengthM - boreM * 0.10f, headProfile),
                    new ProfileLoftRing(halfLengthM, ScaleProfile(headProfile, 0.96f, 0.93f))
                }, Vector3.zero, castAluminumMaterial, opaqueHeadRenderers);

            Vector2[] cutawayProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(headBottomYM, -boreM * 0.025f),
                    new Vector2(headBottomYM, halfDepthM * 0.86f),
                    new Vector2(headBottomYM + headHeightM * 0.28f, halfDepthM),
                    new Vector2(headBottomYM + headHeightM * 0.58f, halfDepthM * 0.93f),
                    new Vector2(headTopYM, halfDepthM * 0.47f),
                    new Vector2(headTopYM, -boreM * 0.025f)
                }, 2);
            CreateLoftPart("Machined half-section cylinder head", cutawayHeadGroup,
                new[]
                {
                    new ProfileLoftRing(-halfLengthM, ScaleProfile(cutawayProfile, 0.96f, 0.92f)),
                    new ProfileLoftRing(-halfLengthM + boreM * 0.12f, cutawayProfile),
                    new ProfileLoftRing(halfLengthM - boreM * 0.10f, cutawayProfile),
                    new ProfileLoftRing(halfLengthM, ScaleProfile(cutawayProfile, 0.96f, 0.93f))
                }, Vector3.zero, castAluminumMaterial, null);

            CreateHeadDeckV2(fullHeadGroup, true);
            CreateHeadDeckV2(cutawayHeadGroup, false);
            CreateCurvedCamCover(fullHeadGroup, true);
            CreateCurvedCamCover(cutawayHeadGroup, false);
            CreateHeadPortSideBulges(fullHeadGroup, true);
            CreateHeadPortSideBulges(cutawayHeadGroup, false);
        }

        private void CreateHeadDeckV2(Transform parent, bool fullCasting)
        {
            Vector2[] profile = ProceduralEngineMeshFactory.CreateRoundedRectangleProfile(
                boreM * 0.065f, headDepthM * 0.91f, boreM * 0.025f, 4);
            CreateLoftPart("Cylinder-head deck thickness", parent,
                CreateCentredLoftRings(profile, blockLengthM * 1.025f, deckYM + boreM * 0.035f),
                Vector3.zero, bearingMaterial, fullCasting ? opaqueHeadRenderers : null);
        }

        private void CreateCurvedCamCover(Transform parent, bool fullCasting)
        {
            float baseY = deckYM + headHeightM * 0.90f;
            float halfDepthM = headDepthM * 0.5f;
            Vector2[] coverProfile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(baseY, halfDepthM * 0.66f),
                    new Vector2(baseY + boreM * 0.20f, halfDepthM * 0.60f),
                    new Vector2(baseY + boreM * 0.31f, halfDepthM * 0.30f),
                    new Vector2(baseY + boreM * 0.34f, 0f),
                    new Vector2(baseY + boreM * 0.31f, -halfDepthM * 0.30f),
                    new Vector2(baseY + boreM * 0.20f, -halfDepthM * 0.60f),
                    new Vector2(baseY, -halfDepthM * 0.66f)
                }, 3);
            CreateLoftPart("Swept magnesium cam cover", parent,
                CreateTaperedLoftRings(coverProfile, blockLengthM * 0.98f, 0.88f),
                Vector3.zero, darkSteelMaterial, fullCasting ? opaqueHeadRenderers : null);
            CreateSweptPart("Cam-cover perimeter seal", parent,
                new[]
                {
                    new Vector3(-blockLengthM * 0.45f, baseY, -halfDepthM * 0.67f),
                    new Vector3(0f, baseY - boreM * 0.01f, -halfDepthM * 0.69f),
                    new Vector3(blockLengthM * 0.45f, baseY, -halfDepthM * 0.67f)
                }, boreM * 0.018f, boreM * 0.012f, gasketMaterial,
                fullCasting ? opaqueHeadRenderers : null);
        }

        private void CreateHeadPortSideBulges(Transform parent, bool fullCasting)
        {
            for (int cylinder = 0; cylinder < 4; cylinder++)
            for (int side = -1; side <= 1; side += 2)
            {
                float zSurface = side * headDepthM * 0.50f;
                CreateSweptPart($"{(side > 0 ? "Intake" : "Exhaust")} port-side cast bulge {cylinder + 1}", parent,
                    new[]
                    {
                        new Vector3(cylinderXM[cylinder] - boreM * 0.31f,
                            deckYM + headHeightM * 0.32f, zSurface * 0.92f),
                        new Vector3(cylinderXM[cylinder], deckYM + headHeightM * 0.43f, zSurface * 1.04f),
                        new Vector3(cylinderXM[cylinder] + boreM * 0.31f,
                            deckYM + headHeightM * 0.32f, zSurface * 0.92f)
                    }, boreM * 0.16f, boreM * 0.115f, castAluminumMaterial,
                    fullCasting ? opaqueHeadRenderers : null);

                for (int valve = 0; valve < 2; valve++)
                {
                    float x = cylinderXM[cylinder] + (valve == 0 ? -1f : 1f) * boreM * 0.16f;
                    float y = deckYM + headHeightM * (side > 0 ? 0.43f : 0.36f);
                    Vector3 opening = new Vector3(x, y, side * headDepthM * 0.56f);
                    CreateSweptPart($"{(side > 0 ? "Intake" : "Exhaust")} visible port mouth {cylinder + 1}-{valve + 1}",
                        parent, new[] { opening, opening + Vector3.forward * (side * boreM * 0.07f) },
                        boreM * (side > 0 ? 0.090f : 0.078f), boreM * (side > 0 ? 0.070f : 0.062f),
                        darkCavityMaterial, null);
                }
            }
        }

        private void CreateValvetrain()
        {
            maximumValveLiftM = boreM * maximumValveLiftBoreMultiplier;
            camshaftYM = deckYM + headHeightM * 0.74f;
            intakeCamZM = headDepthM * camshaftSpacingHeadDepthMultiplier * 0.5f;
            exhaustCamZM = -intakeCamZM;
            camshaftRotors = new Transform[2];
            intakeValves = new Transform[8];
            exhaustValves = new Transform[8];
            intakeSprings = new Transform[8];
            exhaustSprings = new Transform[8];
            intakeValveClosedPositions = new Vector3[8];
            exhaustValveClosedPositions = new Vector3[8];
            intakeValveAxes = new Vector3[8];
            exhaustValveAxes = new Vector3[8];
            cycleHighlightRenderers = new Renderer[4];
            intakePortPaths.Clear();
            exhaustPortPaths.Clear();

            CreateCamshaftV2(ValveSide.Intake, 0, intakeCamZM);
            CreateCamshaftV2(ValveSide.Exhaust, 1, exhaustCamZM);

            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                CreateCombustionChamberV2(cylinder);
                for (int valve = 0; valve < 2; valve++)
                {
                    CreateValveAndPort(cylinder, valve, ValveSide.Intake);
                    CreateValveAndPort(cylinder, valve, ValveSide.Exhaust);
                }
            }
        }

        private void CreateCamshaftV2(ValveSide side, int camIndex, float z)
        {
            string prefix = side == ValveSide.Intake ? "Intake" : "Exhaust";
            Transform rotor = CreateGroup($"{prefix} camshaft rotor", valvetrainGroup);
            rotor.localPosition = new Vector3(0f, camshaftYM, z);
            camshaftRotors[camIndex] = rotor;
            float radiusM = boreM * 0.065f;
            CreateCylinderBetween($"{prefix} camshaft", rotor,
                new Vector3(-blockLengthM * 0.54f, 0f, 0f),
                new Vector3(blockLengthM * 0.54f, 0f, 0f), radiusM,
                darkSteelMaterial, null);
            for (int cap = 0; cap < 5; cap++)
            {
                float x = (cap - 2f) * spacingM;
                CreateCylinderBetween($"{prefix} cam journal {cap + 1}", rotor,
                    new Vector3(x - boreM * 0.075f, 0f, 0f),
                    new Vector3(x + boreM * 0.075f, 0f, 0f), radiusM * 1.15f,
                    bearingMaterial, null);
            }
            for (int cylinder = 0; cylinder < 4; cylinder++)
            for (int valve = 0; valve < 2; valve++)
            {
                float x = cylinderXM[cylinder] + (valve == 0 ? -1f : 1f) * boreM * 0.16f;
                Transform lobe = CreateEllipsoid($"{prefix} cam lobe {cylinder + 1}-{valve + 1}", rotor,
                    new Vector3(x, 0f, 0f), new Vector3(boreM * 0.12f, boreM * 0.19f, boreM * 0.13f),
                    darkSteelMaterial, null);
                double peakCrankDeg = side == ValveSide.Intake
                    ? ValveTimingKinematics.IntakePeakLiftCrankDeg
                    : ValveTimingKinematics.ExhaustPeakLiftCrankDeg;
                float lobeClockingDeg = (float)(ValveTimingKinematics.CylinderFiringTdcCrankDeg(cylinder) * 0.5
                                                + peakCrankDeg * 0.5);
                lobe.localRotation = Quaternion.Euler(lobeClockingDeg, 0f, 0f);
            }
        }

        private void CreateValveAndPort(int cylinder, int valve, ValveSide side)
        {
            bool intake = side == ValveSide.Intake;
            int flatIndex = cylinder * 2 + valve;
            float bankSign = intake ? 1f : -1f;
            float x = cylinderXM[cylinder] + (valve == 0 ? -1f : 1f) * boreM * 0.16f;
            float halfAngleRad = valveIncludedAngleDeg * 0.5f * Mathf.Deg2Rad;
            Vector3 axis = new Vector3(0f, Mathf.Cos(halfAngleRad), bankSign * Mathf.Sin(halfAngleRad)).normalized;
            Vector3 seat = new Vector3(x, deckYM + boreM * 0.06f, bankSign * boreM * 0.13f);
            string prefix = intake ? "Intake" : "Exhaust";

            Transform movingValve = CreateGroup($"{prefix} moving valve {cylinder + 1}-{valve + 1}", valvetrainGroup);
            movingValve.localPosition = seat;
            movingValve.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            float stemLengthM = headHeightM * 0.70f;
            CreateCylinderBetween($"{prefix} valve stem {cylinder + 1}-{valve + 1}", movingValve,
                Vector3.zero, Vector3.up * stemLengthM, boreM * 0.016f, machinedSteelMaterial, null);
            CreateCylinderBetween($"{prefix} valve head {cylinder + 1}-{valve + 1}", movingValve,
                Vector3.down * boreM * 0.025f, Vector3.up * boreM * 0.006f,
                boreM * (intake ? 0.105f : 0.092f), machinedSteelMaterial, null);
            CreateCylinderBetween($"{prefix} spring retainer {cylinder + 1}-{valve + 1}", movingValve,
                Vector3.up * stemLengthM * 0.81f, Vector3.up * stemLengthM * 0.86f,
                boreM * 0.075f, bearingMaterial, null);

            float springBottomM = stemLengthM * 0.46f;
            float springLengthM = stemLengthM * 0.38f;
            Transform springRoot = CreateGroup($"{prefix} compressing spring {cylinder + 1}-{valve + 1}", valvetrainGroup);
            springRoot.localPosition = seat + axis * springBottomM;
            springRoot.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            for (int coil = 0; coil < 7; coil++)
            {
                float y = springLengthM * coil / 6f;
                CreateCylinderBetween($"{prefix} spring coil {cylinder + 1}-{valve + 1}-{coil + 1}", springRoot,
                    new Vector3(0f, y - boreM * 0.008f, 0f), new Vector3(0f, y + boreM * 0.008f, 0f),
                    boreM * 0.052f, darkSteelMaterial, null);
            }

            if (intake)
            {
                intakeValves[flatIndex] = movingValve;
                intakeSprings[flatIndex] = springRoot;
                intakeValveClosedPositions[flatIndex] = seat;
                intakeValveAxes[flatIndex] = axis;
            }
            else
            {
                exhaustValves[flatIndex] = movingValve;
                exhaustSprings[flatIndex] = springRoot;
                exhaustValveClosedPositions[flatIndex] = seat;
                exhaustValveAxes[flatIndex] = axis;
            }

            Vector3 opening = new Vector3(x, deckYM + headHeightM * (intake ? 0.43f : 0.36f),
                bankSign * headDepthM * 0.58f);
            Vector3 bowl = seat + new Vector3(0f, boreM * 0.055f, bankSign * boreM * 0.025f);
            Vector3[] path = CreateQuadraticPath(opening,
                new Vector3(x, deckYM + headHeightM * (intake ? 0.52f : 0.44f),
                    bankSign * headDepthM * 0.37f), bowl, 7);
            var definition = new PortPathDefinition(cylinder, valve, side, path);
            if (intake) intakePortPaths.Add(definition); else exhaustPortPaths.Add(definition);

            CreateSweptPart($"{prefix} curved port runner {cylinder + 1}-{valve + 1}", headInternalsGroup,
                path, boreM * (intake ? 0.078f : 0.068f), boreM * (intake ? 0.065f : 0.058f),
                intake ? intakePortMaterial : exhaustPortMaterial, null);
            CreateSweptPart($"{prefix} external port opening {cylinder + 1}-{valve + 1}", headInternalsGroup,
                new[] { opening, opening + Vector3.forward * (bankSign * boreM * 0.055f) },
                boreM * (intake ? 0.095f : 0.082f), boreM * (intake ? 0.072f : 0.064f),
                darkCavityMaterial, null);
            CreateEllipsoid($"{prefix} valve bowl {cylinder + 1}-{valve + 1}", headInternalsGroup,
                bowl, new Vector3(boreM * 0.19f, boreM * 0.11f, boreM * 0.17f),
                darkCavityMaterial, null);
        }

        private void CreateCombustionChamberV2(int cylinder)
        {
            Vector3 centre = new Vector3(cylinderXM[cylinder], deckYM + boreM * 0.035f, 0f);
            CreateEllipsoid($"Pent-roof combustion chamber {cylinder + 1}", headInternalsGroup,
                centre, new Vector3(boreM * 0.90f, boreM * 0.16f, boreM * 0.79f),
                darkCavityMaterial, null);
            Transform highlight = CreateEllipsoid($"Cycle phase highlight {cylinder + 1}", cycleHighlightGroup,
                centre - Vector3.up * boreM * 0.012f,
                new Vector3(boreM * 0.72f, boreM * 0.06f, boreM * 0.61f),
                cycleCompressionMaterial, null);
            cycleHighlightRenderers[cylinder] = highlight.GetComponent<Renderer>();
        }

        private void CreateTimingDrive()
        {
            timingDriveDefinition ??= new TimingDrivePresentationDefinition();
            timingDriveDefinition.Validate();
            if (timingDriveDefinition.DriveType != TimingDriveType.Chain)
            {
                Debug.LogWarning("Visual Fidelity Pass v2 currently renders the Chain timing-drive definition only.", this);
                return;
            }

            crankTimingSprocketRadiusM = boreM * 0.17f;
            float camSprocketRadiusM = crankTimingSprocketRadiusM * 2f;
            float x = timingFrontXM - boreM * 0.095f;
            crankTimingSprocket = CreateSprocket("Crank timing sprocket", timingDriveGroup,
                new Vector3(x, 0f, 0f), crankTimingSprocketRadiusM, timingDriveDefinition.CrankSprocketTeeth);
            intakeCamSprocket = CreateSprocket("Intake cam sprocket and phaser", timingDriveGroup,
                new Vector3(x, camshaftYM, intakeCamZM), camSprocketRadiusM,
                timingDriveDefinition.CamSprocketTeeth);
            exhaustCamSprocket = CreateSprocket("Exhaust cam sprocket and phaser", timingDriveGroup,
                new Vector3(x, camshaftYM, exhaustCamZM), camSprocketRadiusM,
                timingDriveDefinition.CamSprocketTeeth);

            timingChainPath = BuildTimingChainPath(x, crankTimingSprocketRadiusM, camSprocketRadiusM);
            Mesh chainMesh = TrackMesh(ProceduralEngineMeshFactory.CreateTubeAlongPath(
                "Reduced timing chain path mesh", timingChainPath,
                timingDriveDefinition.VisualChainThicknessM, 8, false, false,
                timingDriveDefinition.VisualChainThicknessM * 0.72f));
            CreateMeshPart("Continuous timing chain path", timingDriveGroup, chainMesh,
                Vector3.zero, Quaternion.identity, chainMaterial, null);
            CreateSweptPart("Fixed timing-chain guide", timingDriveGroup,
                new[] { timingChainPath[1], timingChainPath[2], timingChainPath[3] },
                boreM * 0.025f, boreM * 0.013f, gasketMaterial, null);
            CreateSweptPart("Tensioning timing-chain guide", timingDriveGroup,
                new[] { timingChainPath[7], timingChainPath[8], timingChainPath[9] },
                boreM * 0.025f, boreM * 0.013f, gasketMaterial, null);
            CreateCylinderBetween("Hydraulic chain tensioner", timingDriveGroup,
                timingChainPath[8] + new Vector3(-boreM * 0.03f, -boreM * 0.03f, boreM * 0.02f),
                timingChainPath[8] + new Vector3(-boreM * 0.03f, boreM * 0.08f, boreM * 0.02f),
                boreM * 0.035f, machinedSteelMaterial, null);

            CalculateTimingChainLengths();
            timingChainMarkers = new Transform[timingDriveDefinition.MovingMarkerCount];
            for (int marker = 0; marker < timingChainMarkers.Length; marker++)
            {
                Transform link = CreateEllipsoid($"Moving timing-chain marker {marker + 1}", timingDriveGroup,
                    timingChainPath[0], new Vector3(boreM * 0.040f, boreM * 0.025f, boreM * 0.018f),
                    bearingMaterial, null);
                timingChainMarkers[marker] = link;
            }

            CreateFrontTimingCoverV2();
        }

        private Transform CreateSprocket(string name, Transform parent, Vector3 position, float radius, int toothCount)
        {
            Transform rotor = CreateGroup(name, parent);
            rotor.localPosition = position;
            CreateCylinderBetween(name + " hub", rotor,
                Vector3.left * boreM * 0.035f, Vector3.right * boreM * 0.035f,
                radius * 0.52f, darkSteelMaterial, null);
            CreateCylinderBetween(name + " pitch body", rotor,
                Vector3.left * boreM * 0.027f, Vector3.right * boreM * 0.027f,
                radius * 0.88f, machinedSteelMaterial, null);
            for (int tooth = 0; tooth < toothCount; tooth++)
            {
                float angle = tooth * Mathf.PI * 2f / toothCount;
                Vector3 centre = new Vector3(0f, Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
                CreateEllipsoid(name + $" tooth {tooth + 1}", rotor, centre,
                    new Vector3(boreM * 0.045f, boreM * 0.025f, boreM * 0.025f), chainMaterial, null);
            }
            return rotor;
        }

        private Vector3[] BuildTimingChainPath(float x, float crankRadius, float camRadius)
        {
            return new[]
            {
                new Vector3(x, -crankRadius, 0f),
                new Vector3(x, -crankRadius * 0.35f, exhaustCamZM - crankRadius * 0.92f),
                new Vector3(x, camshaftYM - camRadius * 0.72f, exhaustCamZM - camRadius * 0.74f),
                new Vector3(x, camshaftYM, exhaustCamZM - camRadius),
                new Vector3(x, camshaftYM + camRadius, exhaustCamZM),
                new Vector3(x, camshaftYM + camRadius * 1.08f, 0f),
                new Vector3(x, camshaftYM + camRadius, intakeCamZM),
                new Vector3(x, camshaftYM, intakeCamZM + camRadius),
                new Vector3(x, camshaftYM - camRadius * 0.72f, intakeCamZM + camRadius * 0.74f),
                new Vector3(x, -crankRadius * 0.35f, intakeCamZM + crankRadius * 0.92f),
                new Vector3(x, -crankRadius, 0f)
            };
        }

        private void CreateFrontTimingCoverV2()
        {
            float xFront = timingFrontXM - boreM * 0.14f;
            float xRear = timingFrontXM + boreM * 0.025f;
            Vector2[] profile = ProceduralEngineMeshFactory.CreateRoundedPolygonProfile(
                new[]
                {
                    new Vector2(camshaftYM + boreM * 0.34f, intakeCamZM + boreM * 0.31f),
                    new Vector2(camshaftYM + boreM * 0.39f, 0f),
                    new Vector2(camshaftYM + boreM * 0.34f, exhaustCamZM - boreM * 0.31f),
                    new Vector2(deckYM - boreM * 0.15f, -blockDepthM * 0.46f),
                    new Vector2(blockBottomYM - boreM * 0.03f, -blockDepthM * 0.34f),
                    new Vector2(blockBottomYM - boreM * 0.10f, 0f),
                    new Vector2(blockBottomYM - boreM * 0.03f, blockDepthM * 0.34f),
                    new Vector2(deckYM - boreM * 0.15f, blockDepthM * 0.46f)
                }, 3);
            CreateLoftPart("Rounded front timing-chain cover", timingCoverGroup,
                new[]
                {
                    new ProfileLoftRing(xFront, ScaleProfile(profile, 0.95f, 0.93f)),
                    new ProfileLoftRing(xRear, profile)
                }, Vector3.zero, castAluminumMaterial, opaqueHeadRenderers);
        }

        private void CreateAirflowTeachingPaths()
        {
            foreach (PortPathDefinition port in intakePortPaths)
                CreateAirflowPath(port, intakeAirflowMaterial);
            foreach (PortPathDefinition port in exhaustPortPaths)
                CreateAirflowPath(port, exhaustAirflowMaterial);
        }

        private void CreateAirflowPath(PortPathDefinition port, Material material)
        {
            string prefix = port.Side == ValveSide.Intake ? "Intake" : "Exhaust";
            CreateSweptPart($"{prefix} presentation airflow path {port.CylinderIndex + 1}-{port.ValveIndex + 1}",
                airflowPathGroup, port.Path, boreM * 0.030f, boreM * 0.022f, material, null);
            for (int marker = 1; marker <= 3; marker++)
            {
                int index = Mathf.Clamp(marker * (port.Path.Length - 1) / 4, 0, port.Path.Length - 1);
                CreateEllipsoid($"{prefix} airflow direction marker {port.CylinderIndex + 1}-{port.ValveIndex + 1}-{marker}",
                    airflowPathGroup, port.Path[index],
                    new Vector3(boreM * 0.045f, boreM * 0.030f, boreM * 0.030f), material, null);
            }
        }

        private void UpdateFunctionalValvetrain(float crankCycleAngleDeg)
        {
            IntakeCamAngleDeg = (float)ValveTimingKinematics.CamshaftAngleDeg(
                crankCycleAngleDeg, intakeCamPhaseOffsetDeg);
            ExhaustCamAngleDeg = (float)ValveTimingKinematics.CamshaftAngleDeg(
                crankCycleAngleDeg, exhaustCamPhaseOffsetDeg);
            if (camshaftRotors != null && camshaftRotors.Length >= 2)
            {
                if (camshaftRotors[0] != null) camshaftRotors[0].localRotation = Quaternion.Euler(IntakeCamAngleDeg, 0f, 0f);
                if (camshaftRotors[1] != null) camshaftRotors[1].localRotation = Quaternion.Euler(ExhaustCamAngleDeg, 0f, 0f);
            }

            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                float intakeLiftM = (float)ValveTimingKinematics.ValveLiftM(
                    crankCycleAngleDeg, cylinder, ValveSide.Intake, maximumValveLiftM);
                float exhaustLiftM = (float)ValveTimingKinematics.ValveLiftM(
                    crankCycleAngleDeg, cylinder, ValveSide.Exhaust, maximumValveLiftM);
                for (int valve = 0; valve < 2; valve++)
                {
                    int index = cylinder * 2 + valve;
                    UpdateValveState(intakeValves, intakeSprings, intakeValveClosedPositions,
                        intakeValveAxes, index, intakeLiftM);
                    UpdateValveState(exhaustValves, exhaustSprings, exhaustValveClosedPositions,
                        exhaustValveAxes, index, exhaustLiftM);
                }
            }
        }

        private static void UpdateValveState(
            Transform[] valves,
            Transform[] springs,
            Vector3[] closedPositions,
            Vector3[] axes,
            int index,
            float liftM)
        {
            if (valves == null || index >= valves.Length || valves[index] == null) return;
            valves[index].localPosition = closedPositions[index] - axes[index] * liftM;
            if (springs == null || springs[index] == null) return;
            float closedVisualLengthM = 0.04f;
            float compressionRatio = Mathf.Clamp01(1f - liftM / closedVisualLengthM);
            Vector3 scale = springs[index].localScale;
            scale.y = Mathf.Max(0.58f, compressionRatio);
            springs[index].localScale = scale;
        }

        private void UpdateTimingDrive(float crankCycleAngleDeg)
        {
            if (crankTimingSprocket != null)
                crankTimingSprocket.localRotation = Quaternion.Euler(Mathf.Repeat(crankCycleAngleDeg, 360f), 0f, 0f);
            if (intakeCamSprocket != null)
                intakeCamSprocket.localRotation = Quaternion.Euler(IntakeCamAngleDeg, 0f, 0f);
            if (exhaustCamSprocket != null)
                exhaustCamSprocket.localRotation = Quaternion.Euler(ExhaustCamAngleDeg, 0f, 0f);
            if (timingChainMarkers == null || timingChainLengthM <= 0f) return;

            float chainTravelM = Mathf.Repeat(crankCycleAngleDeg, 360f) / 360f
                                 * (Mathf.PI * 2f * crankTimingSprocketRadiusM);
            for (int marker = 0; marker < timingChainMarkers.Length; marker++)
            {
                if (timingChainMarkers[marker] == null) continue;
                float distanceM = Mathf.Repeat(
                    marker * timingChainLengthM / timingChainMarkers.Length + chainTravelM,
                    timingChainLengthM);
                Vector3 tangent;
                timingChainMarkers[marker].localPosition = SampleTimingChainPath(distanceM, out tangent);
                timingChainMarkers[marker].localRotation = Quaternion.LookRotation(tangent, Vector3.right);
            }
        }

        private void UpdateCycleHighlights(float crankCycleAngleDeg)
        {
            if (cycleHighlightRenderers == null) return;
            for (int cylinder = 0; cylinder < cycleHighlightRenderers.Length; cylinder++)
            {
                Renderer renderer = cycleHighlightRenderers[cylinder];
                if (renderer == null) continue;
                switch (ValveTimingKinematics.CylinderPhase(crankCycleAngleDeg, cylinder))
                {
                    case FourStrokePhase.Intake:
                        renderer.sharedMaterial = cycleIntakeMaterial;
                        break;
                    case FourStrokePhase.Compression:
                        renderer.sharedMaterial = cycleCompressionMaterial;
                        break;
                    case FourStrokePhase.Power:
                        renderer.sharedMaterial = cyclePowerMaterial;
                        break;
                    case FourStrokePhase.Exhaust:
                        renderer.sharedMaterial = cycleExhaustMaterial;
                        break;
                }
            }
        }

        private void CreateV2Materials()
        {
            castAluminumDarkMaterial = CreateMaterial("Dark cast aluminium lower structure",
                new Color(0.22f, 0.25f, 0.27f, 1f), 0.30f, 0.26f);
            chainMaterial = CreateMaterial("Timing chain steel",
                new Color(0.26f, 0.28f, 0.29f, 1f), 0.82f, 0.62f);
            intakePortMaterial = CreateMaterial("Intake port cavity",
                new Color(0.035f, 0.075f, 0.095f, 1f), 0.10f, 0.18f);
            exhaustPortMaterial = CreateMaterial("Exhaust port cavity",
                new Color(0.10f, 0.045f, 0.025f, 1f), 0.12f, 0.16f);
            intakeAirflowMaterial = CreateMaterial("Presentation intake airflow",
                new Color(0.16f, 0.58f, 0.95f, 0.52f), 0.05f, 0.58f, true);
            exhaustAirflowMaterial = CreateMaterial("Presentation exhaust airflow",
                new Color(1f, 0.30f, 0.08f, 0.50f), 0.05f, 0.54f, true);
            cycleIntakeMaterial = CreateMaterial("Cycle intake highlight",
                new Color(0.10f, 0.48f, 0.92f, 0.40f), 0.05f, 0.42f, true);
            cycleCompressionMaterial = CreateMaterial("Cycle compression highlight",
                new Color(0.74f, 0.66f, 0.14f, 0.36f), 0.05f, 0.42f, true);
            cyclePowerMaterial = CreateMaterial("Cycle power highlight",
                new Color(1f, 0.22f, 0.04f, 0.44f), 0.05f, 0.46f, true);
            cycleExhaustMaterial = CreateMaterial("Cycle exhaust highlight",
                new Color(0.55f, 0.20f, 0.12f, 0.38f), 0.05f, 0.40f, true);
        }

        private void CleanupV2State()
        {
            intakePortPaths.Clear();
            exhaustPortPaths.Clear();
            timingDriveGroup = null;
            timingCoverGroup = null;
            airflowPathGroup = null;
            cycleHighlightGroup = null;
            crankTimingSprocket = null;
            intakeCamSprocket = null;
            exhaustCamSprocket = null;
            timingChainMarkers = null;
            timingChainPath = null;
            timingChainCumulativeLengthM = null;
            timingChainLengthM = 0f;
            intakeValves = null;
            exhaustValves = null;
            intakeSprings = null;
            exhaustSprings = null;
            cycleHighlightRenderers = null;
            DestroyMaterial(ref castAluminumDarkMaterial);
            DestroyMaterial(ref chainMaterial);
            DestroyMaterial(ref intakePortMaterial);
            DestroyMaterial(ref exhaustPortMaterial);
            DestroyMaterial(ref intakeAirflowMaterial);
            DestroyMaterial(ref exhaustAirflowMaterial);
            DestroyMaterial(ref cycleIntakeMaterial);
            DestroyMaterial(ref cycleCompressionMaterial);
            DestroyMaterial(ref cyclePowerMaterial);
            DestroyMaterial(ref cycleExhaustMaterial);
        }

        private Transform CreateLoftPart(
            string name,
            Transform parent,
            IReadOnlyList<ProfileLoftRing> rings,
            Vector3 position,
            Material material,
            List<Renderer> collector)
        {
            Mesh mesh = TrackMesh(ProceduralEngineMeshFactory.CreateProfileLoftAlongX(name + " mesh", rings));
            return CreateMeshPart(name, parent, mesh, position, Quaternion.identity, material, collector);
        }

        private Transform CreateSweptPart(
            string name,
            Transform parent,
            IReadOnlyList<Vector3> path,
            float radius,
            float secondaryRadius,
            Material material,
            List<Renderer> collector)
        {
            Mesh mesh = TrackMesh(ProceduralEngineMeshFactory.CreateTubeAlongPath(
                name + " mesh", path, radius, 12, true, false, secondaryRadius));
            return CreateMeshPart(name, parent, mesh, Vector3.zero, Quaternion.identity, material, collector);
        }

        private static Vector2[] ScaleProfile(IReadOnlyList<Vector2> profile, float yScale, float zScale)
        {
            return ProceduralEngineMeshFactory.TransformProfile(profile,
                new Vector2(yScale, zScale), Vector2.zero);
        }

        private static ProfileLoftRing[] CreateCentredLoftRings(
            IReadOnlyList<Vector2> localProfile,
            float lengthM,
            float yOffsetM)
        {
            Vector2[] profile = ProceduralEngineMeshFactory.TransformProfile(
                localProfile, Vector2.one, new Vector2(yOffsetM, 0f));
            return new[]
            {
                new ProfileLoftRing(-lengthM * 0.5f, profile),
                new ProfileLoftRing(lengthM * 0.5f, profile)
            };
        }

        private static ProfileLoftRing[] CreateTaperedLoftRings(
            IReadOnlyList<Vector2> profile,
            float lengthM,
            float endScale)
        {
            return new[]
            {
                new ProfileLoftRing(-lengthM * 0.5f, ScaleProfile(profile, endScale, endScale)),
                new ProfileLoftRing(-lengthM * 0.43f, profile),
                new ProfileLoftRing(lengthM * 0.43f, profile),
                new ProfileLoftRing(lengthM * 0.5f, ScaleProfile(profile, endScale, endScale))
            };
        }

        private static Vector3[] CreateQuadraticPath(Vector3 start, Vector3 control, Vector3 end, int points)
        {
            points = Mathf.Max(3, points);
            var path = new Vector3[points];
            for (int i = 0; i < points; i++)
            {
                float t = i / (float)(points - 1);
                float u = 1f - t;
                path[i] = u * u * start + 2f * u * t * control + t * t * end;
            }
            return path;
        }

        private void CalculateTimingChainLengths()
        {
            if (timingChainPath == null || timingChainPath.Length < 2) return;
            timingChainCumulativeLengthM = new float[timingChainPath.Length];
            timingChainLengthM = 0f;
            for (int i = 1; i < timingChainPath.Length; i++)
            {
                timingChainLengthM += Vector3.Distance(timingChainPath[i - 1], timingChainPath[i]);
                timingChainCumulativeLengthM[i] = timingChainLengthM;
            }
        }

        private Vector3 SampleTimingChainPath(float distanceM, out Vector3 tangent)
        {
            if (timingChainPath == null || timingChainPath.Length < 2)
            {
                tangent = Vector3.up;
                return Vector3.zero;
            }
            distanceM = Mathf.Repeat(distanceM, timingChainLengthM);
            for (int i = 1; i < timingChainPath.Length; i++)
            {
                if (distanceM > timingChainCumulativeLengthM[i]) continue;
                float segmentStartM = timingChainCumulativeLengthM[i - 1];
                float segmentLengthM = timingChainCumulativeLengthM[i] - segmentStartM;
                float t = segmentLengthM <= 0f ? 0f : (distanceM - segmentStartM) / segmentLengthM;
                tangent = (timingChainPath[i] - timingChainPath[i - 1]).normalized;
                return Vector3.Lerp(timingChainPath[i - 1], timingChainPath[i], t);
            }
            tangent = (timingChainPath[1] - timingChainPath[0]).normalized;
            return timingChainPath[0];
        }
    }
}
