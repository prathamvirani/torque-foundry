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

            var configuration = EngineConfiguration.FromMillimetres(86.0, 86.0, 4, 10.0, 7000.0);
            var state = EngineCalculator.Calculate(configuration);

            Add(report, "ENG-CALC-001", "Configuration-to-state displacement",
                1.9982288568717088,
                state.TotalDisplacementLitres, 1e-12);

            Add(report, "ENG-CALC-002", "Configuration-to-state mean piston speed",
                20.066666666666666,
                state.MeanPistonSpeedMps, 1e-12);

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
