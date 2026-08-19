using System;

namespace VehicleEngineeringSandbox.Core.ICE
{
    public static class EngineGeometry
    {
        public static double TotalDisplacementM3(double boreM, double strokeM, int cylinders)
        {
            Positive(boreM, nameof(boreM));
            Positive(strokeM, nameof(strokeM));
            if (cylinders <= 0) throw new ArgumentOutOfRangeException(nameof(cylinders));
            return Math.PI / 4.0 * boreM * boreM * strokeM * cylinders;
        }

        public static double TotalDisplacementLitres(double boreMm, double strokeMm, int cylinders)
            => TotalDisplacementM3(boreMm / 1000.0, strokeMm / 1000.0, cylinders) * 1000.0;

        public static double MeanPistonSpeedMps(double strokeM, double rpm)
        {
            Positive(strokeM, nameof(strokeM));
            if (rpm < 0.0) throw new ArgumentOutOfRangeException(nameof(rpm));
            return 2.0 * strokeM * rpm / 60.0;
        }

        public static double CompressionRatio(double sweptVolumeM3, double clearanceVolumeM3)
        {
            Positive(sweptVolumeM3, nameof(sweptVolumeM3));
            Positive(clearanceVolumeM3, nameof(clearanceVolumeM3));
            return (sweptVolumeM3 + clearanceVolumeM3) / clearanceVolumeM3;
        }

        public static double ClearanceVolumeFromCompressionRatio(double sweptVolumeM3, double compressionRatio)
        {
            Positive(sweptVolumeM3, nameof(sweptVolumeM3));
            if (compressionRatio <= 1.0) throw new ArgumentOutOfRangeException(nameof(compressionRatio));
            return sweptVolumeM3 / (compressionRatio - 1.0);
        }

        private static void Positive(double value, string name)
        {
            if (value <= 0.0) throw new ArgumentOutOfRangeException(name);
        }
    }
}
