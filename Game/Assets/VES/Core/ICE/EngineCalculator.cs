namespace VehicleEngineeringSandbox.Core.ICE
{
    public static class EngineCalculator
    {
        public static EngineCalculatedState Calculate(EngineConfiguration configuration)
        {
            var totalDisplacementM3 = EngineGeometry.TotalDisplacementM3(configuration.BoreM, configuration.StrokeM, configuration.CylinderCount);
            var sweptPerCylinderM3 = totalDisplacementM3 / configuration.CylinderCount;
            var clearancePerCylinderM3 = EngineGeometry.ClearanceVolumeFromCompressionRatio(sweptPerCylinderM3, configuration.CompressionRatio);
            var meanPistonSpeedMps = EngineGeometry.MeanPistonSpeedMps(configuration.StrokeM, configuration.EngineSpeedRpm);
            var boreStrokeRatio = configuration.BoreM / configuration.StrokeM;
            var rodStrokeRatio = configuration.ConnectingRodLengthM / configuration.StrokeM;

            return new EngineCalculatedState(
                totalDisplacementM3,
                sweptPerCylinderM3,
                clearancePerCylinderM3,
                meanPistonSpeedMps,
                boreStrokeRatio,
                rodStrokeRatio);
        }
    }
}
