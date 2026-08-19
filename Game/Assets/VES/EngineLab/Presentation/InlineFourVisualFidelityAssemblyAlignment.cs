using UnityEngine;
using VehicleEngineeringSandbox.Core.ICE;

namespace VehicleEngineeringSandbox.EngineLab.Presentation
{
    /// <summary>
    /// Shared mechanical datums for the generated I4 presentation.
    /// Coordinate convention: X is the crankshaft/cylinder-number axis, Y is the
    /// cylinder-bore axis and positive piston travel, and Z runs exhaust to intake.
    /// All values are presentation-local metres and are rebuilt from authoritative
    /// bore, stroke, rod length, and cylinder spacing before any geometry is placed.
    /// </summary>
    public sealed partial class InlineFourVisualFidelityAssembly
    {
        [Header("Mechanical Alignment Debug")]
        [SerializeField] private bool showEngineeringDatums;

        private Transform engineeringDatumGroup;
        private Transform[] intakeFollowers;
        private Transform[] exhaustFollowers;
        private Vector3[] intakeSpringClosedScales;
        private Vector3[] exhaustSpringClosedScales;
        private float[] intakeSpringClosedLengthsM;
        private float[] exhaustSpringClosedLengthsM;

        private Vector3 crankshaftCenterLocal;
        private Vector3[] cylinderBoreCentersLocal;
        private Vector3[] mainBearingCentersLocal;
        private Vector3[] combustionChamberCentersLocal;
        private Vector3[] intakeValveSeatDatumsLocal;
        private Vector3[] exhaustValveSeatDatumsLocal;
        private Vector3[] intakeValveAxisDatumsLocal;
        private Vector3[] exhaustValveAxisDatumsLocal;
        private Vector3 intakeCamshaftAxisLocal;
        private Vector3 exhaustCamshaftAxisLocal;
        private float frontEngineFaceXM;
        private float rearEngineFaceXM;
        private float timingDrivePlaneXM;
        private float valveStemLengthM;
        private float valveFollowerThicknessM;
        private float camBaseRadiusM;

        public bool ShowEngineeringDatums => showEngineeringDatums;
        public Vector3 CrankshaftCenterLocal => crankshaftCenterLocal;
        public float DeckPlaneYM => deckYM;
        public float FrontEngineFaceXM => frontEngineFaceXM;
        public float RearEngineFaceXM => rearEngineFaceXM;
        public float TimingDrivePlaneXM => timingDrivePlaneXM;
        public Vector3 IntakeCamshaftAxisLocal => intakeCamshaftAxisLocal;
        public Vector3 ExhaustCamshaftAxisLocal => exhaustCamshaftAxisLocal;
        public float MaximumValveLiftM => maximumValveLiftM;
        public float ConfiguredRodLengthM => rodLengthM;
        public float CamBaseRadiusM => camBaseRadiusM;
        public float ValveFollowerThicknessM => valveFollowerThicknessM;

        public Vector3 GetCylinderBoreCenterLocal(int cylinderIndex)
        {
            ValidateI4Index(cylinderIndex, 4, nameof(cylinderIndex));
            return cylinderBoreCentersLocal[cylinderIndex];
        }

        public Vector3 GetMainBearingCenterLocal(int bearingIndex)
        {
            ValidateI4Index(bearingIndex, 5, nameof(bearingIndex));
            return mainBearingCentersLocal[bearingIndex];
        }

        public Vector3 GetCombustionChamberCenterLocal(int cylinderIndex)
        {
            ValidateI4Index(cylinderIndex, 4, nameof(cylinderIndex));
            return combustionChamberCentersLocal[cylinderIndex];
        }

        public Vector3 GetValveSeatLocal(int cylinderIndex, int valveIndex, ValveSide side)
        {
            int index = ValveFlatIndex(cylinderIndex, valveIndex);
            return side == ValveSide.Intake
                ? intakeValveSeatDatumsLocal[index]
                : exhaustValveSeatDatumsLocal[index];
        }

        public Vector3 GetValveAxisLocal(int cylinderIndex, int valveIndex, ValveSide side)
        {
            int index = ValveFlatIndex(cylinderIndex, valveIndex);
            return side == ValveSide.Intake
                ? intakeValveAxisDatumsLocal[index]
                : exhaustValveAxisDatumsLocal[index];
        }

        public float GetSpringClosedLengthM(int cylinderIndex, int valveIndex, ValveSide side)
        {
            int index = ValveFlatIndex(cylinderIndex, valveIndex);
            return side == ValveSide.Intake
                ? intakeSpringClosedLengthsM[index]
                : exhaustSpringClosedLengthsM[index];
        }

        public Vector3 GetPortPathStartLocal(int cylinderIndex, int valveIndex, ValveSide side)
        {
            PortPathDefinition definition = FindPortPath(cylinderIndex, valveIndex, side);
            return definition.Path[0];
        }

        public Vector3 GetPortPathEndLocal(int cylinderIndex, int valveIndex, ValveSide side)
        {
            PortPathDefinition definition = FindPortPath(cylinderIndex, valveIndex, side);
            return definition.Path[definition.Path.Length - 1];
        }

        public void SetEngineeringDatumsVisible(bool visible)
        {
            showEngineeringDatums = visible;
            SetActive(engineeringDatumGroup, visible);
        }

        private void BuildMechanicalDatums()
        {
            crankshaftCenterLocal = Vector3.zero;
            frontEngineFaceXM = -blockLengthM * 0.5f;
            rearEngineFaceXM = blockLengthM * 0.5f;
            timingFrontXM = frontEngineFaceXM - boreM * 0.13f;
            timingDrivePlaneXM = timingFrontXM - boreM * 0.095f;

            cylinderBoreCentersLocal = new Vector3[4];
            combustionChamberCentersLocal = new Vector3[4];
            mainBearingCentersLocal = new Vector3[5];
            intakeValveSeatDatumsLocal = new Vector3[8];
            exhaustValveSeatDatumsLocal = new Vector3[8];
            intakeValveAxisDatumsLocal = new Vector3[8];
            exhaustValveAxisDatumsLocal = new Vector3[8];

            valveStemLengthM = headHeightM * 0.52f;
            valveFollowerThicknessM = boreM * 0.045f;
            camBaseRadiusM = boreM * 0.055f;
            float halfAngleRad = valveIncludedAngleDeg * 0.5f * Mathf.Deg2Rad;

            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                cylinderBoreCentersLocal[cylinder] = new Vector3(cylinderXM[cylinder], 0f, 0f);
                combustionChamberCentersLocal[cylinder] =
                    new Vector3(cylinderXM[cylinder], deckYM + boreM * 0.035f, 0f);
                for (int valve = 0; valve < 2; valve++)
                {
                    int index = cylinder * 2 + valve;
                    float x = cylinderXM[cylinder] + (valve == 0 ? -1f : 1f) * boreM * 0.16f;
                    intakeValveSeatDatumsLocal[index] = new Vector3(x, deckYM + boreM * 0.06f, boreM * 0.13f);
                    exhaustValveSeatDatumsLocal[index] = new Vector3(x, deckYM + boreM * 0.06f, -boreM * 0.13f);
                    intakeValveAxisDatumsLocal[index] =
                        new Vector3(0f, Mathf.Cos(halfAngleRad), Mathf.Sin(halfAngleRad)).normalized;
                    exhaustValveAxisDatumsLocal[index] =
                        new Vector3(0f, Mathf.Cos(halfAngleRad), -Mathf.Sin(halfAngleRad)).normalized;
                }
            }

            for (int bearing = 0; bearing < 5; bearing++)
                mainBearingCentersLocal[bearing] = new Vector3((bearing - 2f) * spacingM, 0f, 0f);

            float contactDistanceM = valveStemLengthM + valveFollowerThicknessM + camBaseRadiusM;
            intakeCamshaftAxisLocal = intakeValveSeatDatumsLocal[0]
                                      + intakeValveAxisDatumsLocal[0] * contactDistanceM;
            exhaustCamshaftAxisLocal = exhaustValveSeatDatumsLocal[0]
                                       + exhaustValveAxisDatumsLocal[0] * contactDistanceM;
            camshaftYM = intakeCamshaftAxisLocal.y;
            intakeCamZM = intakeCamshaftAxisLocal.z;
            exhaustCamZM = exhaustCamshaftAxisLocal.z;
        }

        private void CreateEngineeringDatumVisualization()
        {
            float lineRadiusM = Mathf.Max(0.00045f, boreM * 0.006f);
            float axisExtentXM = blockLengthM * 0.72f;
            CreateCylinderBetween("DATUM X crankshaft axis", engineeringDatumGroup,
                crankshaftCenterLocal + Vector3.left * axisExtentXM,
                crankshaftCenterLocal + Vector3.right * axisExtentXM,
                lineRadiusM, exhaustAirflowMaterial, null);

            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                Vector3 bore = cylinderBoreCentersLocal[cylinder];
                CreateCylinderBetween($"DATUM Y bore axis C{cylinder + 1}", engineeringDatumGroup,
                    new Vector3(bore.x, blockBottomYM, bore.z),
                    new Vector3(bore.x, deckYM + headHeightM * 1.12f, bore.z),
                    lineRadiusM, intakeAirflowMaterial, null);
            }

            CreateDatumRectangle("DATUM deck plane", deckYM, frontEngineFaceXM, rearEngineFaceXM,
                -blockDepthM * 0.58f, blockDepthM * 0.58f, lineRadiusM, cycleCompressionMaterial);
            CreateDatumAxis("DATUM intake camshaft axis", intakeCamshaftAxisLocal,
                Vector3.right, blockLengthM * 0.62f, lineRadiusM, intakeAirflowMaterial);
            CreateDatumAxis("DATUM exhaust camshaft axis", exhaustCamshaftAxisLocal,
                Vector3.right, blockLengthM * 0.62f, lineRadiusM, exhaustAirflowMaterial);

            for (int index = 0; index < 8; index++)
            {
                CreateDatumAxis($"DATUM intake valve axis {index / 2 + 1}-{index % 2 + 1}",
                    intakeValveSeatDatumsLocal[index], intakeValveAxisDatumsLocal[index],
                    valveStemLengthM * 1.18f, lineRadiusM * 0.65f, intakeAirflowMaterial);
                CreateDatumAxis($"DATUM exhaust valve axis {index / 2 + 1}-{index % 2 + 1}",
                    exhaustValveSeatDatumsLocal[index], exhaustValveAxisDatumsLocal[index],
                    valveStemLengthM * 1.18f, lineRadiusM * 0.65f, exhaustAirflowMaterial);
            }

            CreateDatumPlaneOutline("DATUM timing-drive plane", timingDrivePlaneXM,
                blockBottomYM - boreM * 0.12f, camshaftYM + boreM * 0.34f,
                exhaustCamZM - boreM * 0.30f, intakeCamZM + boreM * 0.30f,
                lineRadiusM, cyclePowerMaterial);
            CreateDatumPlaneOutline("DATUM front engine face", frontEngineFaceXM,
                blockBottomYM, deckYM + headHeightM, -headDepthM * 0.52f, headDepthM * 0.52f,
                lineRadiusM, exhaustAirflowMaterial);
            CreateDatumPlaneOutline("DATUM rear engine face", rearEngineFaceXM,
                blockBottomYM, deckYM + headHeightM, -headDepthM * 0.52f, headDepthM * 0.52f,
                lineRadiusM, intakeAirflowMaterial);
        }

        private void CreateDatumAxis(
            string name, Vector3 origin, Vector3 direction, float halfLengthM, float radiusM, Material material)
        {
            Vector3 normalized = direction.normalized;
            CreateCylinderBetween(name, engineeringDatumGroup,
                origin - normalized * halfLengthM, origin + normalized * halfLengthM,
                radiusM, material, null);
        }

        private void CreateDatumRectangle(
            string name, float y, float minX, float maxX, float minZ, float maxZ,
            float radiusM, Material material)
        {
            CreateSweptPart(name, engineeringDatumGroup,
                new[]
                {
                    new Vector3(minX, y, minZ), new Vector3(maxX, y, minZ),
                    new Vector3(maxX, y, maxZ), new Vector3(minX, y, maxZ),
                    new Vector3(minX, y, minZ)
                }, radiusM, radiusM, material, null);
        }

        private void CreateDatumPlaneOutline(
            string name, float x, float minY, float maxY, float minZ, float maxZ,
            float radiusM, Material material)
        {
            CreateSweptPart(name, engineeringDatumGroup,
                new[]
                {
                    new Vector3(x, minY, minZ), new Vector3(x, maxY, minZ),
                    new Vector3(x, maxY, maxZ), new Vector3(x, minY, maxZ),
                    new Vector3(x, minY, minZ)
                }, radiusM, radiusM, material, null);
        }

        private static int ValveFlatIndex(int cylinderIndex, int valveIndex)
        {
            ValidateI4Index(cylinderIndex, 4, nameof(cylinderIndex));
            ValidateI4Index(valveIndex, 2, nameof(valveIndex));
            return cylinderIndex * 2 + valveIndex;
        }

        private PortPathDefinition FindPortPath(int cylinderIndex, int valveIndex, ValveSide side)
        {
            ValveFlatIndex(cylinderIndex, valveIndex);
            var paths = side == ValveSide.Intake ? intakePortPaths : exhaustPortPaths;
            foreach (PortPathDefinition path in paths)
                if (path.CylinderIndex == cylinderIndex && path.ValveIndex == valveIndex)
                    return path;
            throw new System.InvalidOperationException(
                $"Missing {side} port path for cylinder {cylinderIndex + 1}, valve {valveIndex + 1}.");
        }

        private static void ValidateI4Index(int index, int count, string parameterName)
        {
            if (index < 0 || index >= count)
                throw new System.ArgumentOutOfRangeException(parameterName);
        }
    }
}
