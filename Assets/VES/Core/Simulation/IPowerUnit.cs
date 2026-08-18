namespace VehicleEngineeringSandbox.Core.Simulation
{
    public interface IPowerUnit
    {
        string Id { get; }
        void ResetState();
        PowerUnitResponse Step(in PowerUnitRequest request, in SimulationEnvironment environment);
    }

    public struct PowerUnitRequest
    {
        public double shaftSpeedRadPerSec;
        public double accelerator01;
        public double requestedWheelPowerW;
        public double deltaTimeS;
    }

    public struct PowerUnitResponse
    {
        public double shaftTorqueNm;
        public double shaftPowerW;
        public double fuelMassFlowKgPerS;
        public double wasteHeatW;
        public bool limited;
        public string limitingReason;
    }

    public struct SimulationEnvironment
    {
        public double ambientTemperatureC;
        public double ambientPressurePa;
        public double relativeHumidity01;
        public double airDensityKgPerM3;
        public double altitudeM;

        public static SimulationEnvironment StandardSeaLevel() => new SimulationEnvironment
        {
            ambientTemperatureC = 15.0,
            ambientPressurePa = 101325.0,
            relativeHumidity01 = 0.0,
            airDensityKgPerM3 = 1.225,
            altitudeM = 0.0
        };
    }
}
