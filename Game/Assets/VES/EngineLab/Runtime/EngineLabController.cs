using UnityEngine;
using VehicleEngineeringSandbox.Core.ICE;

namespace VehicleEngineeringSandbox.EngineLab
{
    /// <summary>
    /// Thin Unity-facing adapter for the pure engine-geometry model.
    /// Scene/UI code should talk to this component rather than duplicating physics math.
    /// </summary>
    public sealed class EngineLabController : MonoBehaviour
    {
        [Header("Geometry Inputs")]
        [SerializeField, Min(1f)] private float boreMm = 86f;
        [SerializeField, Min(1f)] private float strokeMm = 86f;
        [SerializeField, Min(1)] private int cylinderCount = 4;
        [SerializeField, Min(1.01f)] private float compressionRatio = 10f;

        [Header("Operating Point")]
        [SerializeField, Min(0f)] private float engineSpeedRpm = 7000f;

        [Header("Calculated State")]
        [SerializeField] private double displacementLitres;
        [SerializeField] private double meanPistonSpeedMps;
        [SerializeField] private double boreStrokeRatio;
        [SerializeField] private double clearanceVolumePerCylinderCc;

        public double DisplacementLitres => displacementLitres;
        public double MeanPistonSpeedMps => meanPistonSpeedMps;
        public double BoreStrokeRatio => boreStrokeRatio;
        public double ClearanceVolumePerCylinderCc => clearanceVolumePerCylinderCc;

        private void Reset()
        {
            Recalculate();
        }

        private void OnValidate()
        {
            Recalculate();
        }

        [ContextMenu("Recalculate Engine")]
        public void Recalculate()
        {
            var configuration = EngineConfiguration.FromMillimetres(
                Mathf.Max(1f, boreMm),
                Mathf.Max(1f, strokeMm),
                Mathf.Max(1, cylinderCount),
                Mathf.Max(1.01f, compressionRatio),
                Mathf.Max(0f, engineSpeedRpm));

            var state = EngineCalculator.Calculate(configuration);

            displacementLitres = state.TotalDisplacementLitres;
            meanPistonSpeedMps = state.MeanPistonSpeedMps;
            boreStrokeRatio = state.BoreStrokeRatio;
            clearanceVolumePerCylinderCc = state.ClearanceVolumePerCylinderM3 * 1_000_000.0;
        }
    }
}
