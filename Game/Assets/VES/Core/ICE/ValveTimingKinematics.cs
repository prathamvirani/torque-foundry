using System;

namespace VehicleEngineeringSandbox.Core.ICE
{
    public enum FourStrokePhase
    {
        Intake,
        Compression,
        Power,
        Exhaust
    }

    public enum ValveSide
    {
        Intake,
        Exhaust
    }

    /// <summary>
    /// Deterministic reduced-order valve timing for the Engine Lab teaching model.
    /// Angles use a 720-degree four-stroke crank cycle. Lift is a smooth normalized
    /// cam profile, not a dynamic valvetrain, VTC, VTEC, lash, or compliance model.
    /// </summary>
    public static class ValveTimingKinematics
    {
        public const double IntakeOpeningCrankDeg = 350.0;
        public const double IntakePeakLiftCrankDeg = 465.0;
        public const double IntakeClosingCrankDeg = 580.0;
        public const double ExhaustOpeningCrankDeg = 140.0;
        public const double ExhaustPeakLiftCrankDeg = 255.0;
        public const double ExhaustClosingCrankDeg = 370.0;

        // Original compact-I4 teaching order: cylinders 1-3-4-2 fire 180 degrees apart.
        private static readonly double[] FiringTdcCrankDeg = { 0.0, 540.0, 180.0, 360.0 };
        private static readonly int[] FiringOrderCylinderIndices = { 0, 2, 3, 1 };

        public static double CamshaftAngleDeg(double crankCycleAngleDeg, double camPhaseOffsetDeg = 0.0)
        {
            return Normalize360(crankCycleAngleDeg * 0.5 + camPhaseOffsetDeg);
        }

        public static double CylinderCycleAngleDeg(double crankCycleAngleDeg, int cylinderIndex)
        {
            ValidateCylinderIndex(cylinderIndex);
            return Normalize720(crankCycleAngleDeg - FiringTdcCrankDeg[cylinderIndex]);
        }

        public static double CylinderFiringTdcCrankDeg(int cylinderIndex)
        {
            ValidateCylinderIndex(cylinderIndex);
            return FiringTdcCrankDeg[cylinderIndex];
        }

        public static int FiringOrderCylinderIndex(int firingIndex)
        {
            if (firingIndex < 0 || firingIndex >= FiringOrderCylinderIndices.Length)
                throw new ArgumentOutOfRangeException(nameof(firingIndex));
            return FiringOrderCylinderIndices[firingIndex];
        }

        public static int CylinderAtFiringTdc(double crankCycleAngleDeg, double toleranceDeg = 0.5)
        {
            if (toleranceDeg < 0.0 || toleranceDeg > 90.0)
                throw new ArgumentOutOfRangeException(nameof(toleranceDeg));
            double normalized = Normalize720(crankCycleAngleDeg);
            for (int cylinder = 0; cylinder < FiringTdcCrankDeg.Length; cylinder++)
            {
                double difference = Math.Abs(normalized - FiringTdcCrankDeg[cylinder]);
                difference = Math.Min(difference, 720.0 - difference);
                if (difference <= toleranceDeg) return cylinder;
            }
            return -1;
        }

        public static FourStrokePhase CylinderPhase(double crankCycleAngleDeg, int cylinderIndex)
        {
            double localAngleDeg = CylinderCycleAngleDeg(crankCycleAngleDeg, cylinderIndex);
            if (localAngleDeg < 180.0) return FourStrokePhase.Power;
            if (localAngleDeg < 360.0) return FourStrokePhase.Exhaust;
            if (localAngleDeg < 540.0) return FourStrokePhase.Intake;
            return FourStrokePhase.Compression;
        }

        public static double NormalizedValveLift(
            double crankCycleAngleDeg,
            int cylinderIndex,
            ValveSide valveSide)
        {
            double localAngleDeg = CylinderCycleAngleDeg(crankCycleAngleDeg, cylinderIndex);
            switch (valveSide)
            {
                case ValveSide.Intake:
                    return SmoothLift(localAngleDeg,
                        IntakeOpeningCrankDeg, IntakePeakLiftCrankDeg, IntakeClosingCrankDeg);
                case ValveSide.Exhaust:
                    return SmoothLift(localAngleDeg,
                        ExhaustOpeningCrankDeg, ExhaustPeakLiftCrankDeg, ExhaustClosingCrankDeg);
                default:
                    throw new ArgumentOutOfRangeException(nameof(valveSide));
            }
        }

        public static double ValveLiftM(
            double crankCycleAngleDeg,
            int cylinderIndex,
            ValveSide valveSide,
            double maximumLiftM)
        {
            if (maximumLiftM < 0.0) throw new ArgumentOutOfRangeException(nameof(maximumLiftM));
            return NormalizedValveLift(crankCycleAngleDeg, cylinderIndex, valveSide) * maximumLiftM;
        }

        public static double Normalize720(double angleDeg)
        {
            double wrapped = angleDeg % 720.0;
            return wrapped < 0.0 ? wrapped + 720.0 : wrapped;
        }

        public static double Normalize360(double angleDeg)
        {
            double wrapped = angleDeg % 360.0;
            return wrapped < 0.0 ? wrapped + 360.0 : wrapped;
        }

        private static double SmoothLift(double localAngleDeg, double openingDeg, double peakDeg, double closingDeg)
        {
            if (localAngleDeg <= openingDeg || localAngleDeg >= closingDeg) return 0.0;
            if (localAngleDeg <= peakDeg)
            {
                double progress = (localAngleDeg - openingDeg) / (peakDeg - openingDeg);
                return 0.5 - 0.5 * Math.Cos(Math.PI * progress);
            }

            double closingProgress = (localAngleDeg - peakDeg) / (closingDeg - peakDeg);
            return 0.5 + 0.5 * Math.Cos(Math.PI * closingProgress);
        }

        private static void ValidateCylinderIndex(int cylinderIndex)
        {
            if (cylinderIndex < 0 || cylinderIndex >= FiringTdcCrankDeg.Length)
                throw new ArgumentOutOfRangeException(nameof(cylinderIndex));
        }
    }
}
