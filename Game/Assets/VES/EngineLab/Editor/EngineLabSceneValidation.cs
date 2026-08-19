using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VehicleEngineeringSandbox.Core.ICE;
using VehicleEngineeringSandbox.EngineLab.Presentation;

namespace VehicleEngineeringSandbox.EngineLab.Editor
{
    public static class EngineLabSceneValidation
    {
        private const string ScenePath = "Assets/VES/EngineLab/Scenes/EngineLab.unity";
        private const string GeneratedRootName = "Generated I4 Visual Fidelity Assembly";
        private const string InteractiveValidationArgument = "-torqueFoundryValidateEngineLab";
        private const string InteractiveValidationSessionKey = "TorqueFoundry.EngineLabInteractiveValidationRanV2";
        private const float PositionToleranceM = 0.00001f;

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedInteractiveValidation()
        {
            if (Application.isBatchMode
                || SessionState.GetBool(InteractiveValidationSessionKey, false)
                || Array.IndexOf(Environment.GetCommandLineArgs(), InteractiveValidationArgument) < 0) return;
            EditorApplication.delayCall += RunRequestedInteractiveValidation;
        }

        [MenuItem("Torque Foundry/Validate Engine Lab Scene")]
        public static void RunFromMenu()
        {
            try
            {
                string report = ValidateScene();
                WriteReport(report);
                Debug.Log(report);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                throw;
            }
        }

        public static void RunBatch()
        {
            try
            {
                string report = ValidateScene();
                WriteReport(report);
                Debug.Log(report);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                string report = $"Engine Lab scene validation FAILED: {exception}";
                WriteReport(report);
                Debug.LogError(report);
                EditorApplication.Exit(1);
            }
        }

        private static void RunRequestedInteractiveValidation()
        {
            if (SessionState.GetBool(InteractiveValidationSessionKey, false)) return;
            SessionState.SetBool(InteractiveValidationSessionKey, true);
            RunInteractive();
        }

        public static void RunInteractive()
        {
            try
            {
                ClearConsole();
                string report = ValidateScene();
                WriteReport(report);
                Debug.Log(report);
                int consoleErrorCount = GetConsoleErrorCount();
                Require(consoleErrorCount == 0,
                    $"Unity Console contained {consoleErrorCount} red error(s) after scene validation.");
                string screenshotPath = CaptureInspectionScreenshots();
                AppendReport($"Interactive editor verification PASSED: Console red errors = 0.\n"
                             + $"Inspection screenshot: {screenshotPath}");
                Debug.Log("Interactive Engine Lab verification PASSED: validator passed and Console has zero red errors.");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                string report = $"Interactive Engine Lab verification FAILED: {exception}";
                WriteReport(report);
                Debug.LogError(report);
                EditorApplication.Exit(1);
            }
        }

        private static string ValidateScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid() && scene.isLoaded, "Dedicated Engine Lab scene did not open.");
            GameObject root = GameObject.Find("Engine Lab");
            Require(root != null && root.scene == scene, "Engine Lab root was not found in the dedicated scene.");
            Require(root.transform.parent == null, "Engine Lab must remain a scene root.");
            Require(Approximately(root.transform.localPosition, Vector3.zero), "Engine Lab root position is not zero.");
            Require(Approximately(root.transform.localRotation, Quaternion.identity), "Engine Lab root rotation is not identity.");
            Require(Approximately(root.transform.localScale, Vector3.one), "Engine Lab root scale is not one.");

            EngineLabController controller = root.GetComponent<EngineLabController>();
            InlineFourVisualizer prototypeMechanism = root.GetComponent<InlineFourVisualizer>();
            InlineFourEngineContextVisualizer prototypeContext = root.GetComponent<InlineFourEngineContextVisualizer>();
            InlineFourVisualFidelityAssembly visualAssembly = root.GetComponent<InlineFourVisualFidelityAssembly>();
            EngineLabInspectionPanel inspectionPanel = root.GetComponent<EngineLabInspectionPanel>();
            EngineLabInspectionCamera inspectionCamera = UnityEngine.Object.FindAnyObjectByType<EngineLabInspectionCamera>();
            Require(controller != null, "Engine Lab root is missing EngineLabController.");
            Require(prototypeMechanism != null && !prototypeMechanism.enabled,
                "Prototype mechanism must remain present as a disabled reference implementation.");
            Require(prototypeContext != null && !prototypeContext.enabled,
                "Prototype block context must remain present as a disabled reference implementation.");
            Require(visualAssembly != null, "Engine Lab root is missing InlineFourVisualFidelityAssembly.");
            Require(inspectionPanel != null, "Engine Lab root is missing EngineLabInspectionPanel.");
            Require(inspectionCamera != null, "Dedicated scene is missing EngineLabInspectionCamera.");
            Require(CountMissingScripts(scene) == 0, "The dedicated scene contains missing MonoBehaviour scripts.");

            var controllerObject = new SerializedObject(controller);
            float originalBoreMm = ReadFloat(controllerObject, "boreMm");
            float originalStrokeMm = ReadFloat(controllerObject, "strokeMm");
            float originalRodLengthMm = ReadFloat(controllerObject, "connectingRodLengthMm");
            float originalPreviewAngleDeg = visualAssembly.CurrentCrankAngleDeg;
            EngineInspectionMode originalInspectionMode = visualAssembly.InspectionMode;
            Transform previousGeneratedRoot = null;
            Mesh previousGeneratedMesh = null;
            const float previewAngleDeg = 37f;
            visualAssembly.SetCrankAngleDeg(previewAngleDeg);
            VerifyRebuild(controller, visualAssembly, controllerObject, 86f, 86f, 143f,
                previewAngleDeg, ref previousGeneratedRoot, ref previousGeneratedMesh);
            VerifyRebuild(controller, visualAssembly, controllerObject, 92f, 86f, 150f,
                previewAngleDeg, ref previousGeneratedRoot, ref previousGeneratedMesh);
            VerifyRebuild(controller, visualAssembly, controllerObject, 84f, 94f, 155f,
                previewAngleDeg, ref previousGeneratedRoot, ref previousGeneratedMesh);

            SetFloat(controllerObject, "boreMm", originalBoreMm);
            SetFloat(controllerObject, "strokeMm", originalStrokeMm);
            SetFloat(controllerObject, "connectingRodLengthMm", originalRodLengthMm);
            controllerObject.ApplyModifiedPropertiesWithoutUndo();
            controller.Recalculate();
            visualAssembly.SetCrankAngleDeg(originalPreviewAngleDeg);
            visualAssembly.RebuildPreview();
            visualAssembly.SetInspectionMode(originalInspectionMode);
            VerifyPresentationControls(controller, visualAssembly, inspectionCamera);
            Require(EditorSceneManager.SaveScene(scene), "Dedicated Engine Lab scene could not be saved after validation.");
            return "Engine Lab scene validation PASSED: scene opened, compile completed, root transform reset, "
                   + "no missing scripts, authoritative 86 x 86 x 143 mm state and slider-crank positions remained unchanged, "
                   + "the Visual Fidelity Pass v2 casting, ports, functional valvetrain, and timing drive rebuilt from all "
                   + "geometry cases, and the 720-degree cycle, all five inspection modes, bounded camera controls, and "
                   + "teaching-state isolation behaved deterministically.";
        }

        private static void VerifyRebuild(
            EngineLabController controller,
            InlineFourVisualFidelityAssembly visualAssembly,
            SerializedObject controllerObject,
            float boreMm,
            float strokeMm,
            float rodLengthMm,
            float previewAngleDeg,
            ref Transform previousGeneratedRoot,
            ref Mesh previousGeneratedMesh)
        {
            SetFloat(controllerObject, "boreMm", boreMm);
            SetFloat(controllerObject, "strokeMm", strokeMm);
            SetFloat(controllerObject, "connectingRodLengthMm", rodLengthMm);
            controllerObject.ApplyModifiedPropertiesWithoutUndo();
            controller.Recalculate();
            visualAssembly.RebuildPreview();
            Transform generatedRoot = controller.transform.Find(GeneratedRootName);
            Require(generatedRoot != null, $"Generated fidelity hierarchy missing for {boreMm} x {strokeMm} mm geometry.");
            Require(previousGeneratedRoot == null || generatedRoot != previousGeneratedRoot,
                "Visual-fidelity rebuild reused a stale generated hierarchy.");
            Require(previousGeneratedMesh == null,
                "Visual-fidelity rebuild left a stale generated procedural mesh alive.");
            previousGeneratedRoot = generatedRoot;
            MeshFilter generatedMeshFilter = generatedRoot.GetComponentInChildren<MeshFilter>(true);
            previousGeneratedMesh = generatedMeshFilter != null ? generatedMeshFilter.sharedMesh : null;

            Require(generatedRoot.childCount == 15, "Expected fifteen independently inspectable v2 groups.");
            RequireGroupMinimum(generatedRoot, "Cylinder Block - Full", 18);
            RequireGroupMinimum(generatedRoot, "Cylinder Block - Cutaway", 10);
            RequireGroupMinimum(generatedRoot, "Block Internals and Liners", 29);
            RequireGroupMinimum(generatedRoot, "Cylinder Head - Full", 10);
            RequireGroupMinimum(generatedRoot, "Cylinder Head - Cutaway", 10);
            RequireGroupMinimum(generatedRoot, "Head Chambers and Ports", 50);
            RequireGroupMinimum(generatedRoot, "Crankshaft Internal", 25);
            RequireGroupMinimum(generatedRoot, "Crankshaft External", 5);
            RequireGroupMinimum(generatedRoot, "Pistons and Forged Rods", 60);
            RequireGroupMinimum(generatedRoot, "DOHC Valvetrain", 150);
            RequireGroupMinimum(generatedRoot, "Timing Drive Internal", 120);
            RequireGroupMinimum(generatedRoot, "Timing Covers", 1);
            RequireGroupMinimum(generatedRoot, "Airflow Teaching Paths", 60);
            RequireGroupMinimum(generatedRoot, "Combustion Cycle Highlights", 4);
            Require(CountChildrenWithPrefix(generatedRoot, "Cylinder liner ") == 4, "Expected four cutaway liners.");
            Require(CountChildrenWithPrefix(generatedRoot, "Main bearing bulkhead ") == 5,
                "Expected five main-bearing bulkheads and saddles.");
            Require(CountChildrenWithPrefix(generatedRoot, "Main journal ") == 5, "Expected five main journals.");
            Require(CountChildrenWithPrefix(generatedRoot, "Curved crank throw ") == 4, "Expected four crank throws.");
            Require(CountChildrenWithPrefix(generatedRoot, "Piston assembly ") == 4, "Expected four detailed pistons.");
            Require(CountChildrenWithPrefix(generatedRoot, "Forged connecting rod ") == 4,
                "Expected four forged connecting rods.");
            Require(CountChildrenWithPrefix(generatedRoot, "Pent-roof combustion chamber ") == 4,
                "Expected four combustion-chamber regions.");
            Require(CountChildrenWithPrefix(generatedRoot, "Intake cam lobe ") == 8,
                "Expected eight intake cam lobes.");
            Require(CountChildrenWithPrefix(generatedRoot, "Exhaust cam lobe ") == 8,
                "Expected eight exhaust cam lobes.");
            Require(CountChildrenWithPrefix(generatedRoot, "Intake valve stem ") == 8, "Expected eight intake valves.");
            Require(CountChildrenWithPrefix(generatedRoot, "Exhaust valve stem ") == 8, "Expected eight exhaust valves.");
            Require(CountChildrenWithPrefix(generatedRoot, "Intake external port opening ") == 8,
                "Expected four paired intake-port openings.");
            Require(CountChildrenWithPrefix(generatedRoot, "Exhaust external port opening ") == 8,
                "Expected four paired exhaust-port openings.");
            Require(CountChildrenWithPrefix(generatedRoot, "Intake curved port runner ") == 8,
                "Expected eight traceable intake runners.");
            Require(CountChildrenWithPrefix(generatedRoot, "Exhaust curved port runner ") == 8,
                "Expected eight traceable exhaust runners.");
            Require(FindDescendant(generatedRoot, "Crank timing sprocket") != null,
                "Timing drive is missing the crank sprocket.");
            Require(FindDescendant(generatedRoot, "Intake cam sprocket and phaser") != null,
                "Timing drive is missing the intake cam sprocket/phaser.");
            Require(FindDescendant(generatedRoot, "Exhaust cam sprocket and phaser") != null,
                "Timing drive is missing the exhaust cam sprocket/phaser.");
            Require(FindDescendant(generatedRoot, "Continuous timing chain path") != null,
                "Timing drive is missing the chain path.");
            Require(FindDescendant(generatedRoot, "Fixed timing-chain guide") != null
                    && FindDescendant(generatedRoot, "Tensioning timing-chain guide") != null
                    && FindDescendant(generatedRoot, "Hydraulic chain tensioner") != null,
                "Timing drive is missing a guide or tensioner.");

            Transform piston1 = FindDescendant(generatedRoot, "Piston assembly 1");
            Transform piston2 = FindDescendant(generatedRoot, "Piston assembly 2");
            Transform connectingRod1 = FindDescendant(generatedRoot, "Forged connecting rod 1");
            Require(piston1 != null && piston2 != null && connectingRod1 != null,
                "Primary high-fidelity mechanism objects are missing.");
            float boreM = boreMm / 1000f;
            float strokeM = strokeMm / 1000f;
            float crankRadiusM = strokeM * 0.5f;
            float rodLengthM = rodLengthMm / 1000f;
            float expectedSpacingM = boreM * 1.15f;
            float expectedPistonPinYM = (float)SliderCrankKinematics.PistonPinHeightM(
                previewAngleDeg * Mathf.Deg2Rad, crankRadiusM, rodLengthM);
            float actualSpacingM = piston2.localPosition.x - piston1.localPosition.x;
            Require(Mathf.Abs(actualSpacingM - expectedSpacingM) <= PositionToleranceM,
                $"Cylinder spacing did not rebuild from bore: expected {expectedSpacingM:R} m, actual {actualSpacingM:R} m.");
            Require(Mathf.Abs(piston1.localPosition.y - expectedPistonPinYM) <= PositionToleranceM,
                "Piston pin position does not match authoritative slider-crank geometry.");
            Vector3 smallEndWorld = connectingRod1.TransformPoint(Vector3.up * rodLengthM);
            Require(Vector3.Distance(smallEndWorld, piston1.position) <= PositionToleranceM,
                "Forged connecting-rod eye centres do not preserve configured rod length.");
            Require(Mathf.Abs(visualAssembly.CylinderSpacingM - expectedSpacingM) <= PositionToleranceM,
                "Visual assembly cylinder spacing does not derive from bore.");
            Require(Mathf.Abs(visualAssembly.TimingCamToCrankSpeedRatio - 0.5f) <= PositionToleranceM,
                "Timing-drive tooth definition does not preserve the 2:1 crank/cam ratio.");

            EngineCalculatedState expectedState = EngineCalculator.Calculate(controller.CreateConfiguration());
            Require(Math.Abs(controller.DisplacementLitres - expectedState.TotalDisplacementLitres) <= 1e-12,
                "Controller displacement is stale after a geometry rebuild.");
            Require(Math.Abs(controller.RodStrokeRatio - expectedState.RodStrokeRatio) <= 1e-12,
                "Controller rod/stroke ratio is stale after a geometry rebuild.");
            if (Mathf.Approximately(boreMm, 86f) && Mathf.Approximately(strokeMm, 86f)
                && Mathf.Approximately(rodLengthMm, 143f))
            {
                Require(Math.Abs(controller.DisplacementLitres - 1.9982288568717088) <= 1e-12,
                    "Visual fidelity work changed the authoritative 86 x 86 mm displacement reference.");
                Require(Math.Abs(controller.RodStrokeRatio - 143.0 / 86.0) <= 1e-12,
                    "Visual fidelity work changed the authoritative 143/86 rod-stroke ratio.");
            }
        }

        private static void VerifyPresentationControls(
            EngineLabController controller,
            InlineFourVisualFidelityAssembly visualAssembly,
            EngineLabInspectionCamera inspectionCamera)
        {
            float simulatedOperatingRpm = controller.EngineSpeedRpm;
            bool originalPlaying = visualAssembly.IsTeachingAnimationPlaying;
            float originalTeachingRpm = visualAssembly.TeachingAnimationRpm;
            float originalAngleDeg = visualAssembly.CurrentCrankAngleDeg;
            EngineInspectionMode originalMode = visualAssembly.InspectionMode;
            visualAssembly.SetTeachingAnimationPlaying(false);
            visualAssembly.SetTeachingAnimationRpm(42f);
            visualAssembly.SetCrankAngleDeg(123f);
            Require(!visualAssembly.IsTeachingAnimationPlaying, "Teaching animation did not pause.");
            Require(Mathf.Approximately(visualAssembly.TeachingAnimationRpm, 42f), "Teaching RPM did not update.");
            Require(Mathf.Approximately(visualAssembly.CurrentCrankAngleDeg, 123f), "Crank-angle scrub state did not update.");
            Require(Mathf.Approximately(controller.EngineSpeedRpm, simulatedOperatingRpm),
                "Teaching controls changed the simulated engine operating RPM.");

            VerifyFunctionalValvetrain(visualAssembly);

            Transform generatedRoot = controller.transform.Find(GeneratedRootName);
            VerifyInspectionMode(visualAssembly, generatedRoot, EngineInspectionMode.FullEngine,
                "Cylinder Block - Full", "Cylinder Head - Full", "Crankshaft External", "Timing Covers");
            VerifyInspectionMode(visualAssembly, generatedRoot, EngineInspectionMode.Cutaway,
                "Cylinder Block - Cutaway", "Block Internals and Liners", "Cylinder Head - Cutaway",
                "Head Chambers and Ports", "Crankshaft Internal", "Crankshaft External",
                "Pistons and Forged Rods", "DOHC Valvetrain", "Timing Drive Internal", "Timing Covers",
                "Airflow Teaching Paths", "Combustion Cycle Highlights");
            VerifyInspectionMode(visualAssembly, generatedRoot, EngineInspectionMode.TransparentBlockAndHead,
                "Cylinder Block - Full", "Block Internals and Liners", "Cylinder Head - Full",
                "Head Chambers and Ports", "Crankshaft Internal", "Crankshaft External",
                "Pistons and Forged Rods", "DOHC Valvetrain", "Timing Drive Internal", "Timing Covers",
                "Airflow Teaching Paths", "Combustion Cycle Highlights");
            VerifyInspectionMode(visualAssembly, generatedRoot, EngineInspectionMode.RotatingAssemblyOnly,
                "Crankshaft Internal", "Crankshaft External", "Pistons and Forged Rods");
            VerifyInspectionMode(visualAssembly, generatedRoot, EngineInspectionMode.ValvetrainOnly,
                "Head Chambers and Ports", "DOHC Valvetrain", "Timing Drive Internal",
                "Airflow Teaching Paths", "Combustion Cycle Highlights");

            visualAssembly.SetTeachingAnimationPlaying(originalPlaying);
            visualAssembly.SetTeachingAnimationRpm(originalTeachingRpm);
            visualAssembly.SetCrankAngleDeg(originalAngleDeg);
            visualAssembly.SetInspectionMode(originalMode);
            inspectionCamera.ResetEngineView();
            Vector3 defaultPivot = inspectionCamera.Pivot;
            float defaultDistance = inspectionCamera.DistanceM;
            inspectionCamera.SetDistance(-1f);
            Require(Mathf.Approximately(inspectionCamera.DistanceM, inspectionCamera.MinimumDistanceM),
                "Inspection camera minimum zoom limit failed.");
            inspectionCamera.SetDistance(100f);
            Require(Mathf.Approximately(inspectionCamera.DistanceM, inspectionCamera.MaximumDistanceM),
                "Inspection camera maximum zoom limit failed.");
            inspectionCamera.SetOrbit(725f, 1000f);
            Require(inspectionCamera.YawDeg >= -180f && inspectionCamera.YawDeg < 180f,
                "Inspection camera yaw normalization failed.");
            Require(Mathf.Approximately(inspectionCamera.PitchDeg, inspectionCamera.MaximumPitchDeg),
                "Inspection camera maximum pitch limit failed.");
            inspectionCamera.SetOrbit(725f, -1000f);
            Require(Mathf.Approximately(inspectionCamera.PitchDeg, inspectionCamera.MinimumPitchDeg),
                "Inspection camera minimum pitch limit failed.");
            inspectionCamera.SetPivot(defaultPivot + Vector3.one * 100f);
            Require(Vector3.Distance(defaultPivot, inspectionCamera.Pivot) <= 0.65001f,
                "Inspection camera pan/focus limit failed.");
            inspectionCamera.ResetEngineView();
            Require(Vector3.Distance(defaultPivot, inspectionCamera.Pivot) <= PositionToleranceM,
                "Reset Engine View did not restore the engine focus.");
            Require(Mathf.Abs(defaultDistance - inspectionCamera.DistanceM) <= PositionToleranceM,
                "Reset Engine View did not restore the default zoom.");
        }

        private static void VerifyFunctionalValvetrain(InlineFourVisualFidelityAssembly visualAssembly)
        {
            Transform generatedRoot = visualAssembly.transform.Find(GeneratedRootName);
            Transform intakeValve = FindDescendant(generatedRoot, "Intake moving valve 1-1");
            Transform exhaustValve = FindDescendant(generatedRoot, "Exhaust moving valve 1-1");
            Require(intakeValve != null && exhaustValve != null,
                "Functional moving-valve transforms are missing.");

            visualAssembly.SetCrankAngleDeg(350f);
            Vector3 intakeClosedPosition = intakeValve.localPosition;
            Require(Math.Abs(visualAssembly.GetNormalizedValveLift(0, ValveSide.Intake)) <= 1e-12,
                "Intake valve did not close at its opening reference angle.");
            visualAssembly.SetCrankAngleDeg(407.5f);
            float openingTravelM = Vector3.Distance(intakeClosedPosition, intakeValve.localPosition);
            Require(openingTravelM > 0f && visualAssembly.GetNormalizedValveLift(0, ValveSide.Intake) > 0.0,
                "Intake valve did not translate during opening.");
            visualAssembly.SetCrankAngleDeg(465f);
            float peakTravelM = Vector3.Distance(intakeClosedPosition, intakeValve.localPosition);
            Require(peakTravelM > openingTravelM
                    && Math.Abs(visualAssembly.GetNormalizedValveLift(0, ValveSide.Intake) - 1.0) <= 1e-12,
                "Intake valve did not reach deterministic peak lift.");
            visualAssembly.SetCrankAngleDeg(522.5f);
            float closingTravelM = Vector3.Distance(intakeClosedPosition, intakeValve.localPosition);
            Require(closingTravelM > 0f && closingTravelM < peakTravelM,
                "Intake valve did not translate through closing.");
            visualAssembly.SetCrankAngleDeg(580f);
            Require(Vector3.Distance(intakeClosedPosition, intakeValve.localPosition) <= PositionToleranceM,
                "Intake valve did not return to its closed position.");

            visualAssembly.SetCrankAngleDeg(255f);
            Require(Math.Abs(visualAssembly.GetNormalizedValveLift(0, ValveSide.Exhaust) - 1.0) <= 1e-12,
                "Exhaust valve did not reach deterministic peak lift.");

            visualAssembly.SetCrankAngleDeg(40f);
            float intakeCamStartDeg = visualAssembly.IntakeCamAngleDeg;
            float exhaustCamStartDeg = visualAssembly.ExhaustCamAngleDeg;
            visualAssembly.SetCrankAngleDeg(220f);
            Require(Mathf.Abs(Mathf.DeltaAngle(intakeCamStartDeg, visualAssembly.IntakeCamAngleDeg) - 90f)
                    <= PositionToleranceM,
                "Intake camshaft did not rotate at half crankshaft speed.");
            Require(Mathf.Abs(Mathf.DeltaAngle(exhaustCamStartDeg, visualAssembly.ExhaustCamAngleDeg) - 90f)
                    <= PositionToleranceM,
                "Exhaust camshaft did not rotate at half crankshaft speed.");

            visualAssembly.SetCrankAngleDeg(90f);
            Require(visualAssembly.GetCylinderPhase(0) == FourStrokePhase.Power,
                "Cylinder 1 did not enter power phase at its reference angle.");
            visualAssembly.SetCrankAngleDeg(270f);
            Require(visualAssembly.GetCylinderPhase(0) == FourStrokePhase.Exhaust
                    && visualAssembly.GetCylinderPhase(2) == FourStrokePhase.Power,
                "Cylinder phases did not follow the 1-3 firing sequence.");
            visualAssembly.SetCrankAngleDeg(450f);
            Require(visualAssembly.GetCylinderPhase(0) == FourStrokePhase.Intake
                    && visualAssembly.GetCylinderPhase(3) == FourStrokePhase.Power,
                "Cylinder phases did not follow the 3-4 firing sequence.");
            visualAssembly.SetCrankAngleDeg(630f);
            Require(visualAssembly.GetCylinderPhase(0) == FourStrokePhase.Compression
                    && visualAssembly.GetCylinderPhase(1) == FourStrokePhase.Power,
                "Cylinder phases did not follow the 4-2 firing sequence.");
        }

        private static void VerifyInspectionMode(
            InlineFourVisualFidelityAssembly visualAssembly,
            Transform generatedRoot,
            EngineInspectionMode mode,
            params string[] expectedActiveGroups)
        {
            visualAssembly.SetInspectionMode(mode);
            Require(visualAssembly.InspectionMode == mode, $"Inspection mode {mode} did not apply.");
            foreach (Transform group in generatedRoot)
            {
                if (group.name == "Inspection Lighting") continue;
                bool shouldBeActive = Array.IndexOf(expectedActiveGroups, group.name) >= 0;
                Require(group.gameObject.activeSelf == shouldBeActive,
                    $"Inspection mode {mode} produced incorrect visibility for '{group.name}'.");
            }
        }

        private static int CountMissingScripts(Scene scene)
        {
            int missingCount = 0;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (Transform transform in sceneRoot.GetComponentsInChildren<Transform>(true))
                missingCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
            return missingCount;
        }

        private static int CountChildrenWithPrefix(Transform parent, string prefix)
        {
            int count = 0;
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child != parent && child.name.StartsWith(prefix, StringComparison.Ordinal)) count++;
            return count;
        }

        private static void RequireGroupMinimum(Transform root, string groupName, int minimumDescendants)
        {
            Transform group = root.Find(groupName);
            Require(group != null, $"Visual fidelity group '{groupName}' is missing.");
            int descendantCount = group.GetComponentsInChildren<Transform>(true).Length - 1;
            Require(descendantCount >= minimumDescendants,
                $"Visual fidelity group '{groupName}' expected at least {minimumDescendants} objects, found {descendantCount}.");
        }

        private static Transform FindDescendant(Transform root, string exactName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                if (child.name == exactName) return child;
            return null;
        }

        private static float ReadFloat(SerializedObject serializedObject, string propertyName)
        {
            serializedObject.Update();
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Require(property != null, $"Serialized property '{propertyName}' was not found.");
            return property.floatValue;
        }

        private static void SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            Require(property != null, $"Serialized property '{propertyName}' was not found.");
            property.floatValue = value;
        }

        private static bool Approximately(Vector3 a, Vector3 b) => (a - b).sqrMagnitude <= 1e-12f;
        private static bool Approximately(Quaternion a, Quaternion b)
            => Mathf.Abs(Quaternion.Dot(a, b)) >= 0.999999f;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void WriteReport(string report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/EngineLabSceneValidation.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, report + Environment.NewLine, System.Text.Encoding.UTF8);
        }

        private static void AppendReport(string report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/EngineLabSceneValidation.txt"));
            File.AppendAllText(path, report + Environment.NewLine, System.Text.Encoding.UTF8);
        }

        private static void ClearConsole()
        {
            Type type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo method = type?.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Require(method != null, "Unity Console clear API was unavailable.");
            method.Invoke(null, null);
        }

        private static int GetConsoleErrorCount()
        {
            Type type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo method = type?.GetMethod("GetCountsByType",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Require(method != null, "Unity Console count API was unavailable.");
            object[] counts = { 0, 0, 0 };
            method.Invoke(null, counts);
            return (int)counts[0];
        }

        private static string CaptureInspectionScreenshots()
        {
            Camera camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
            Require(camera != null, "Dedicated Engine Lab scene has no inspection camera.");
            InlineFourVisualFidelityAssembly visualAssembly =
                UnityEngine.Object.FindAnyObjectByType<InlineFourVisualFidelityAssembly>();
            Require(visualAssembly != null, "Dedicated Engine Lab scene has no visual-fidelity assembly.");
            const int width = 1280;
            const int height = 720;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            EngineInspectionMode previousMode = visualAssembly.InspectionMode;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var paths = new System.Collections.Generic.List<string>();
            try
            {
                camera.targetTexture = target;
                foreach (EngineInspectionMode mode in Enum.GetValues(typeof(EngineInspectionMode)))
                {
                    visualAssembly.SetInspectionMode(mode);
                    camera.GetComponent<EngineLabInspectionCamera>()?.SetPivot(
                        visualAssembly.transform.TransformPoint(visualAssembly.RecommendedFocusPointLocal));
                    camera.GetComponent<EngineLabInspectionCamera>()?.SetDistance(
                        visualAssembly.RecommendedCameraDistanceM);
                    camera.Render();
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture.Apply();
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                        $"../Logs/EngineLabValidation_{mode}.png"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    paths.Add(path);
                }

                return string.Join(", ", paths);
            }
            finally
            {
                visualAssembly.SetInspectionMode(previousMode);
                camera.GetComponent<EngineLabInspectionCamera>()?.ResetEngineView();
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
