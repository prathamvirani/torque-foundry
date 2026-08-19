using System;

namespace VehicleEngineeringSandbox.Core.ICE
{
    /// <summary>
    /// User-selected reciprocating-engine geometry and operating-point inputs.
    /// Units are explicit and SI-native where practical.
    /// </summary>
    public sealed class EngineConfiguration
    {
        public double BoreM { get; }
        public double StrokeM { get; }
        public int CylinderCount { get; }
        public double CompressionRatio { get; }
        public double EngineSpeedRpm { get; }

        public EngineConfiguration(
            double boreM,
            double strokeM,
            int cylinderCount,
            double compressionRatio,
            double engineSpeedRpm)
        {
            if (boreM <= 0.0) throw new ArgumentOutOfRangeException(nameof(boreM));
            if (strokeM <= 0.0) throw new ArgumentOutOfRangeException(nameof(strokeM));
            if (cylinderCount <= 0) throw new ArgumentOutOfRangeException(nameof(cylinderCount));
            if (compressionRatio <= 1.0) throw new ArgumentOutOfRangeException(nameof(compressionRatio));
            if (engineSpeedRpm < 0.0) throw new ArgumentOutOfRangeException(nameof(engineSpeedRpm));

            BoreM = boreM;
            StrokeM = strokeM;
            CylinderCount = cylinderCount;
            CompressionRatio = compressionRatio;
            EngineSpeedRpm = engineSpeedRpm;
        }

        public static EngineConfiguration FromMillimetres(
            double boreMm,
            double strokeMm,
            int cylinderCount,
            double compressionRatio,
            double engineSpeedRpm)
        {
            return new EngineConfiguration(
                boreMm / 1000.0,
                strokeMm / 1000.0,
                cylinderCount,
                compressionRatio,
                engineSpeedRpm);
        }
    }
}
