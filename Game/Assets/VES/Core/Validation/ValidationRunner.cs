using System;
using System.Collections.Generic;
using VehicleEngineeringSandbox.Core.ICE;

namespace VehicleEngineeringSandbox.Core.Validation
{
    public enum ValidationSeverity { Info, Warning, Critical }

    public sealed class ValidationResult
    {
        public string id = "";
        public string description = "";
        public bool passed;
        public double expected;
        public double actual;
        public double tolerance;
        public ValidationSeverity severity;
    }

    public sealed class ValidationReport
    {
        public readonly List<ValidationResult> results = new List<ValidationResult>();

        public int PassedCount
        {
            get
            {
                int n = 0;
                foreach (var r in results) if (r.passed) n++;
                return n;
            }
        }

        public int FailedCount => results.Count - PassedCount;
        public bool AllPassed => FailedCount == 0;
    }

    public static class ValidationRunner
    {
        public static ValidationReport RunFoundationChecks()
        {
            var report = new ValidationReport();

            Add(report, "ENG-GEO-001", "86 x 86 mm four-cylinder displacement",
                1.9982288568717088,
                EngineGeometry.TotalDisplacementLitres(86.0, 86.0, 4), 1e-12);

            Add(report, "ENG-GEO-002", "Mean piston speed: 86 mm stroke @ 7000 rpm",
                20.066666666666666,
                EngineGeometry.MeanPistonSpeedMps(0.086, 7000.0), 1e-12);

            double swept = 0.0005;
            double clearance = EngineGeometry.ClearanceVolumeFromCompressionRatio(swept, 10.0);
            Add(report, "ENG-GEO-003", "Compression-ratio round trip",
                10.0,
                EngineGeometry.CompressionRatio(swept, clearance), 1e-12);

            var configuration = EngineConfiguration.FromMillimetres(86.0, 86.0, 143.0, 4, 10.0, 7000.0);
            var state = EngineCalculator.Calculate(configuration);

            Add(report, "ENG-CALC-001", "Configuration-to-state displacement",
                1.9982288568717088,
                state.TotalDisplacementLitres, 1e-12);

            Add(report, "ENG-CALC-002", "Configuration-to-state mean piston speed",
                20.066666666666666,
                state.MeanPistonSpeedMps, 1e-12);

            Add(report, "ENG-CALC-003", "143 mm rod / 86 mm stroke ratio",
                143.0 / 86.0,
                state.RodStrokeRatio, 1e-12);

            double crankRadiusM = 0.086 * 0.5;
            double rodLengthM = 0.143;
            double tdcHeightM = SliderCrankKinematics.PistonPinHeightM(0.0, crankRadiusM, rodLengthM);
            double bdcHeightM = SliderCrankKinematics.PistonPinHeightM(Math.PI, crankRadiusM, rodLengthM);

            Add(report, "ENG-KIN-001", "Slider-crank TDC-to-BDC travel equals stroke",
                0.086,
                tdcHeightM - bdcHeightM, 1e-12);

            Add(report, "ENG-VALVE-001", "Camshaft advances at half crankshaft speed",
                90.0,
                ValveTimingKinematics.CamshaftAngleDeg(180.0), 1e-12);

            Add(report, "ENG-VALVE-002", "Intake valve is closed at opening reference",
                0.0,
                ValveTimingKinematics.NormalizedValveLift(350.0, 0, ValveSide.Intake), 1e-12);

            Add(report, "ENG-VALVE-003", "Intake valve has deterministic opening lift",
                0.5,
                ValveTimingKinematics.NormalizedValveLift(407.5, 0, ValveSide.Intake), 1e-12);

            Add(report, "ENG-VALVE-004", "Intake valve reaches normalized peak lift",
                1.0,
                ValveTimingKinematics.NormalizedValveLift(465.0, 0, ValveSide.Intake), 1e-12);

            Add(report, "ENG-VALVE-005", "Intake valve has deterministic closing lift",
                0.5,
                ValveTimingKinematics.NormalizedValveLift(522.5, 0, ValveSide.Intake), 1e-12);

            Add(report, "ENG-VALVE-006", "Intake valve is closed at closing reference",
                0.0,
                ValveTimingKinematics.NormalizedValveLift(580.0, 0, ValveSide.Intake), 1e-12);

            Add(report, "ENG-VALVE-007", "Exhaust valve reaches normalized peak lift",
                1.0,
                ValveTimingKinematics.NormalizedValveLift(255.0, 0, ValveSide.Exhaust), 1e-12);

            Add(report, "ENG-CYCLE-001", "Cylinder 1 phase at 90 degrees is power",
                (double)FourStrokePhase.Power,
                (double)ValveTimingKinematics.CylinderPhase(90.0, 0), 0.0);

            Add(report, "ENG-CYCLE-002", "Cylinder 1 phase at 270 degrees is exhaust",
                (double)FourStrokePhase.Exhaust,
                (double)ValveTimingKinematics.CylinderPhase(270.0, 0), 0.0);

            Add(report, "ENG-CYCLE-003", "Cylinder 1 phase at 450 degrees is intake",
                (double)FourStrokePhase.Intake,
                (double)ValveTimingKinematics.CylinderPhase(450.0, 0), 0.0);

            Add(report, "ENG-CYCLE-004", "Cylinder 1 phase at 630 degrees is compression",
                (double)FourStrokePhase.Compression,
                (double)ValveTimingKinematics.CylinderPhase(630.0, 0), 0.0);

            Add(report, "ENG-CYCLE-005", "Cylinder 3 fires 180 degrees after cylinder 1",
                180.0,
                ValveTimingKinematics.CylinderFiringTdcCrankDeg(2), 1e-12);

            Add(report, "ENG-CYCLE-006", "Cylinder 4 fires 360 degrees after cylinder 1",
                360.0,
                ValveTimingKinematics.CylinderFiringTdcCrankDeg(3), 1e-12);

            Add(report, "ENG-CYCLE-007", "Cylinder 2 fires 540 degrees after cylinder 1",
                540.0,
                ValveTimingKinematics.CylinderFiringTdcCrankDeg(1), 1e-12);

            return report;
        }

        private static void Add(ValidationReport report, string id, string description,
            double expected, double actual, double tolerance)
        {
            report.results.Add(new ValidationResult
            {
                id = id,
                description = description,
                expected = expected,
                actual = actual,
                tolerance = tolerance,
                severity = ValidationSeverity.Critical,
                passed = Math.Abs(expected - actual) <= tolerance
            });
        }
    }
}
