namespace VehicleEngineeringSandbox.Core.ICE
{
    /// <summary>
    /// Deterministic derived values calculated from an EngineConfiguration.
    /// This contains no Unity types and no hidden mutable simulation state.
    /// </summary>
    public readonly struct EngineCalculatedState
    {
        public double TotalDisplacementM3 { get; }
        public double TotalDisplacementLitres { get; }
        public double SweptVolumePerCylinderM3 { get; }
        public double ClearanceVolumePerCylinderM3 { get; }
        public double MeanPistonSpeedMps { get; }
        public double BoreStrokeRatio { get; }
        public double RodStrokeRatio { get; }

        public EngineCalculatedState(
            double totalDisplacementM3,
            double sweptVolumePerCylinderM3,
            double clearanceVolumePerCylinderM3,
            double meanPistonSpeedMps,
            double boreStrokeRatio,
            double rodStrokeRatio)
        {
            TotalDisplacementM3 = totalDisplacementM3;
            TotalDisplacementLitres = totalDisplacementM3 * 1000.0;
            SweptVolumePerCylinderM3 = sweptVolumePerCylinderM3;
            ClearanceVolumePerCylinderM3 = clearanceVolumePerCylinderM3;
            MeanPistonSpeedMps = meanPistonSpeedMps;
            BoreStrokeRatio = boreStrokeRatio;
            RodStrokeRatio = rodStrokeRatio;
        }
    }
}
