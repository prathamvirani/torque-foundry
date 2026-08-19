using System;

namespace VehicleEngineeringSandbox.Core.ICE
{
    /// <summary>
    /// Exact rigid slider-crank geometry for an inline reciprocating engine.
    /// The crankshaft centre is the origin; piston travel is along +Y and crank rotation is in the YZ plane.
    /// </summary>
    public static class SliderCrankKinematics
    {
        public static double PistonPinHeightM(double crankAngleRad, double crankRadiusM, double connectingRodLengthM)
        {
            Validate(crankRadiusM, connectingRodLengthM);

            double sin = Math.Sin(crankAngleRad);
            double cos = Math.Cos(crankAngleRad);
            double underRoot = connectingRodLengthM * connectingRodLengthM
                               - crankRadiusM * crankRadiusM * sin * sin;

            return crankRadiusM * cos + Math.Sqrt(underRoot);
        }

        public static double CrankPinYM(double crankAngleRad, double crankRadiusM)
        {
            if (crankRadiusM <= 0.0) throw new ArgumentOutOfRangeException(nameof(crankRadiusM));
            return crankRadiusM * Math.Cos(crankAngleRad);
        }

        public static double CrankPinZM(double crankAngleRad, double crankRadiusM)
        {
            if (crankRadiusM <= 0.0) throw new ArgumentOutOfRangeException(nameof(crankRadiusM));
            return crankRadiusM * Math.Sin(crankAngleRad);
        }

        private static void Validate(double crankRadiusM, double connectingRodLengthM)
        {
            if (crankRadiusM <= 0.0) throw new ArgumentOutOfRangeException(nameof(crankRadiusM));
            if (connectingRodLengthM <= crankRadiusM)
                throw new ArgumentOutOfRangeException(nameof(connectingRodLengthM), "Connecting rod length must exceed crank radius.");
        }
    }
}
