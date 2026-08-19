using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VehicleEngineeringSandbox.Core.ICE;
using VehicleEngineeringSandbox.Core.Validation;
using VehicleEngineeringSandbox.EngineLab.Presentation;

namespace VehicleEngineeringSandbox.EngineLab.Editor
{
    public static class EngineLabSceneValidation
    {
        private const string ScenePath = "Assets/VES/EngineLab/Scenes/EngineLab.unity";
        private const string GeneratedRootName = "Generated I4 Visual Fidelity Assembly";
        private const string InteractiveValidationArgument = "-torqueFoundryValidateEngineLab";
        private const string InteractiveValidationSessionKey = "TorqueFoundry.EngineLabInteractiveValidationRanAlignmentV2";
        private const string PlayAuditArgument = "-torqueFoundryPlayAuditEngineLab";
        private const string PlayAuditActiveKey = "TorqueFoundry.EngineLabPlayAudit.AlignmentV4.Active";
        private const string PlayAuditPhaseKey = "TorqueFoundry.EngineLabPlayAudit.AlignmentV4.Phase";
        private const float PositionToleranceM = 0.00001f;
        private static string InteractiveValidationSessionKeyForProcess =>
            InteractiveValidationSessionKey + "." + System.Diagnostics.Process.GetCurrentProcess().Id;

        private static InlineFourVisualFidelityAssembly playAuditAssembly;
        private static Transform[] playAuditValves;
        private static Transform[] playAuditValveHeads;
        private static Vector3[] playAuditSeats;
        private static bool[] playAuditSawPeak;
        private static bool[] playAuditSawClosed;
        private static float playAuditLastAngleDeg;
        private static float playAuditUnwrappedAngleDeg;
        private static int playAuditCaptureIndex;
        private static double playAuditStartTime;
        private static readonly float[] PlayAuditCaptureAnglesDeg = { 0f, 90f, 180f, 360f, 540f, 720f };

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedInteractiveValidation()
        {
            if (Application.isBatchMode) return;
            string[] arguments = Environment.GetCommandLineArgs();
            if (Array.IndexOf(arguments, PlayAuditArgument) >= 0)
            {
                if (!SessionState.GetBool(PlayAuditActiveKey, false))
                {
                    SessionState.SetBool(PlayAuditActiveKey, true);
                    SessionState.SetInt(PlayAuditPhaseKey, 0);
                }
                EditorApplication.delayCall += AdvanceRequestedPlayAudit;
                return;
            }

            if (SessionState.GetBool(InteractiveValidationSessionKeyForProcess, false)
                || Array.IndexOf(arguments, InteractiveValidationArgument) < 0) return;
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
            if (SessionState.GetBool(InteractiveValidationSessionKeyForProcess, false)) return;
            SessionState.SetBool(InteractiveValidationSessionKeyForProcess, true);
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

        private static void AdvanceRequestedPlayAudit()
        {
            try
            {
                int phase = SessionState.GetInt(PlayAuditPhaseKey, 0);
                if (phase == 0 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    ClearConsole();
                    string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../Logs/EngineLabPlayModeCycleAudit.txt"));
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                    File.WriteAllText(reportPath, string.Empty);
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                    SessionState.SetInt(PlayAuditPhaseKey, 1);
                    EditorApplication.isPlaying = true;
                    return;
                }

                if ((phase == 1 || phase == 2) && EditorApplication.isPlaying)
                {
                    InitializePlayAudit();
                    SessionState.SetInt(PlayAuditPhaseKey, 2);
                    EditorApplication.update -= UpdatePlayAudit;
                    EditorApplication.update += UpdatePlayAudit;
                    return;
                }

                if (phase == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    bool succeeded = SessionState.GetBool(PlayAuditActiveKey + ".Succeeded", false);
                    int consoleErrorCount = GetConsoleErrorCount();
                    string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath,
                        "../Logs/EngineLabPlayModeCycleAudit.txt"));
                    string summary = succeeded && consoleErrorCount == 0
                        ? "Engine Lab Play Mode 720-degree audit PASSED: every rendered intake and exhaust valve "
                          + "reached visible peak lift and returned to its seat during one continuous cycle; Console red errors = 0."
                        : $"Engine Lab Play Mode 720-degree audit FAILED: success={succeeded}, Console red errors={consoleErrorCount}.";
                    Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                    File.AppendAllText(reportPath, summary + Environment.NewLine);
                    SessionState.SetBool(PlayAuditActiveKey, false);
                    SessionState.SetBool(PlayAuditActiveKey + ".Succeeded", false);
                    SessionState.SetInt(PlayAuditPhaseKey, 0);
                    EditorApplication.playModeStateChanged -= OnPlayAuditModeChanged;
                    if (succeeded && consoleErrorCount == 0) Debug.Log(summary); else Debug.LogError(summary);
                    EditorApplication.Exit(succeeded && consoleErrorCount == 0 ? 0 : 1);
                }
            }
            catch (Exception exception)
            {
                FailPlayAudit(exception);
            }
        }

        private static void InitializePlayAudit()
        {
            playAuditAssembly = UnityEngine.Object.FindAnyObjectByType<InlineFourVisualFidelityAssembly>();
            Require(playAuditAssembly != null, "Play Mode audit could not find the visual-fidelity assembly.");
            Transform generatedRoot = playAuditAssembly.transform.Find(GeneratedRootName);
            Require(generatedRoot != null, "Play Mode audit could not find generated engine geometry.");

            playAuditValves = new Transform[16];
            playAuditValveHeads = new Transform[16];
            playAuditSeats = new Vector3[16];
            playAuditSawPeak = new bool[16];
            playAuditSawClosed = new bool[16];
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                ValveSide side = sideIndex == 0 ? ValveSide.Intake : ValveSide.Exhaust;
                string prefix = side == ValveSide.Intake ? "Intake" : "Exhaust";
                for (int cylinder = 0; cylinder < 4; cylinder++)
                for (int valve = 0; valve < 2; valve++)
                {
                    int index = sideIndex * 8 + cylinder * 2 + valve;
                    playAuditValves[index] = FindDescendant(generatedRoot,
                        $"{prefix} moving valve {cylinder + 1}-{valve + 1}");
                    playAuditValveHeads[index] = FindDescendant(generatedRoot,
                        $"{prefix} valve head {cylinder + 1}-{valve + 1}");
                    playAuditSeats[index] = playAuditAssembly.GetValveSeatLocal(cylinder, valve, side);
                    Require(playAuditValves[index] != null && playAuditValveHeads[index] != null,
                        $"Play Mode audit is missing {prefix} valve {cylinder + 1}-{valve + 1}.");
                }
            }

            playAuditAssembly.SetInspectionMode(EngineInspectionMode.Cutaway);
            playAuditAssembly.SetTeachingAnimationPlaying(false);
            playAuditAssembly.SetCrankAngleDeg(0f);
            playAuditAssembly.SetTeachingAnimationRpm(15f);
            playAuditAssembly.SetTeachingAnimationPlaying(true);
            playAuditLastAngleDeg = playAuditAssembly.CurrentCrankAngleDeg;
            playAuditUnwrappedAngleDeg = 0f;
            playAuditCaptureIndex = 0;
            playAuditStartTime = EditorApplication.timeSinceStartup;
            EditorApplication.playModeStateChanged -= OnPlayAuditModeChanged;
            EditorApplication.playModeStateChanged += OnPlayAuditModeChanged;
            CapturePlayAuditFrame(0);
            playAuditCaptureIndex = 1;
        }

        private static void UpdatePlayAudit()
        {
            try
            {
                if (!EditorApplication.isPlaying || playAuditAssembly == null) return;
                float currentAngleDeg = playAuditAssembly.CurrentCrankAngleDeg;
                float deltaDeg = Mathf.Repeat(currentAngleDeg - playAuditLastAngleDeg, 720f);
                if (deltaDeg < 90f) playAuditUnwrappedAngleDeg += deltaDeg;
                playAuditLastAngleDeg = currentAngleDeg;

                for (int index = 0; index < playAuditValves.Length; index++)
                {
                    Vector3 renderedHeadSeat = ToAssemblyLocal(playAuditAssembly,
                        playAuditValveHeads[index].TransformPoint(Vector3.up));
                    float displacementM = Vector3.Distance(renderedHeadSeat, playAuditSeats[index]);
                    if (displacementM >= playAuditAssembly.MaximumValveLiftM * 0.98f)
                        playAuditSawPeak[index] = true;
                    // A valve event can cross the 720/0 boundary, so closed and peak
                    // observations are intentionally order-independent within the cycle.
                    if (displacementM <= playAuditAssembly.MaximumValveLiftM * 0.01f)
                        playAuditSawClosed[index] = true;
                }

                while (playAuditCaptureIndex < PlayAuditCaptureAnglesDeg.Length
                       && playAuditUnwrappedAngleDeg >= PlayAuditCaptureAnglesDeg[playAuditCaptureIndex])
                {
                    CapturePlayAuditFrame(Mathf.RoundToInt(PlayAuditCaptureAnglesDeg[playAuditCaptureIndex]));
                    playAuditCaptureIndex++;
                }

                if (playAuditUnwrappedAngleDeg >= 720f)
                {
                    for (int index = 0; index < playAuditSawPeak.Length; index++)
                        Require(playAuditSawPeak[index] && playAuditSawClosed[index],
                            $"Rendered valve {index + 1} did not visibly open and close during the continuous Play Mode cycle.");
                    SessionState.SetBool(PlayAuditActiveKey + ".Succeeded", true);
                    SessionState.SetInt(PlayAuditPhaseKey, 3);
                    EditorApplication.update -= UpdatePlayAudit;
                    EditorApplication.isPlaying = false;
                    EditorApplication.delayCall += AdvanceRequestedPlayAudit;
                    return;
                }

                Require(EditorApplication.timeSinceStartup - playAuditStartTime < 20.0,
                    "Play Mode 720-degree audit exceeded its time limit.");
            }
            catch (Exception exception)
            {
                FailPlayAudit(exception);
            }
        }

        private static void CapturePlayAuditFrame(int angleLabel)
        {
            Camera camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
            Require(camera != null, "Play Mode audit has no camera.");
            EngineLabInspectionCamera inspectionCamera = camera.GetComponent<EngineLabInspectionCamera>();
            if (inspectionCamera != null)
            {
                inspectionCamera.SetPivot(playAuditAssembly.transform.TransformPoint(
                    playAuditAssembly.RecommendedFocusPointLocal));
                inspectionCamera.SetOrbit(32f, 12f);
                inspectionCamera.SetDistance(playAuditAssembly.RecommendedCameraDistanceM);
            }

            const int width = 1280;
            const int height = 720;
            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                    $"../Logs/EngineLabPlayCycle_{angleLabel:000}.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private static void FailPlayAudit(Exception exception)
        {
            EditorApplication.update -= UpdatePlayAudit;
            SessionState.SetBool(PlayAuditActiveKey + ".Succeeded", false);
            SessionState.SetInt(PlayAuditPhaseKey, 3);
            string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                "../Logs/EngineLabPlayModeCycleAudit.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, $"Engine Lab Play Mode 720-degree audit FAILED: {exception}{Environment.NewLine}");
            Debug.LogException(exception);
            if (EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.isPlaying = false;
            else EditorApplication.delayCall += AdvanceRequestedPlayAudit;
        }

        private static void OnPlayAuditModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode
                || SessionState.GetInt(PlayAuditPhaseKey, 0) != 3) return;
            EditorApplication.playModeStateChanged -= OnPlayAuditModeChanged;
            EditorApplication.delayCall += AdvanceRequestedPlayAudit;
        }

        private static string ValidateScene()
        {
            ValidationReport coreReport = ValidationRunner.RunFoundationChecks();
            Require(coreReport.AllPassed,
                $"Core foundation validation failed ({coreReport.FailedCount}/{coreReport.results.Count}).");
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

            Require(generatedRoot.childCount == 16, "Expected fifteen inspection groups plus engineering datums.");
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
            RequireGroupMinimum(generatedRoot, "Engineering Datums", 20);
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

            VerifyMechanicalAlignment(visualAssembly, generatedRoot, boreM, strokeM, rodLengthM, previewAngleDeg);

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

        private static void VerifyMechanicalAlignment(
            InlineFourVisualFidelityAssembly visualAssembly,
            Transform generatedRoot,
            float boreM,
            float strokeM,
            float rodLengthM,
            float previewAngleDeg)
        {
            float crankRadiusM = strokeM * 0.5f;
            float[] crankPhaseDeg = { 0f, 180f, 180f, 0f };
            visualAssembly.SetCrankAngleDeg(previewAngleDeg);
            Require(visualAssembly.CrankcaseAvailableHalfDepthM
                    >= visualAssembly.RotatingAssemblyRequiredHalfDepthM - PositionToleranceM,
                "Crankcase retained-wall envelope does not clear the connecting-rod big-end sweep.");
            float bigEndOuterRadiusM = boreM * 0.22f;
            for (int auditAngleDeg = 0; auditAngleDeg < 360; auditAngleDeg += 15)
            {
                float crankPinZM = Mathf.Abs((float)SliderCrankKinematics.CrankPinZM(
                    auditAngleDeg * Mathf.Deg2Rad, crankRadiusM));
                Require(crankPinZM + bigEndOuterRadiusM
                        <= visualAssembly.CrankcaseAvailableHalfDepthM + PositionToleranceM,
                    $"Connecting-rod big-end sweep intersects the retained crankcase wall at {auditAngleDeg} degrees.");
            }

            for (int cylinder = 0; cylinder < 4; cylinder++)
            {
                Vector3 boreCentre = visualAssembly.GetCylinderBoreCenterLocal(cylinder);
                Transform piston = FindDescendant(generatedRoot, $"Piston assembly {cylinder + 1}");
                Transform wristPin = FindDescendant(generatedRoot, $"Wrist pin {cylinder + 1}");
                Transform rod = FindDescendant(generatedRoot, $"Forged connecting rod {cylinder + 1}");
                Transform bigEnd = FindDescendant(generatedRoot, $"Big-end eye {cylinder + 1}");
                Transform smallEnd = FindDescendant(generatedRoot, $"Small-end eye {cylinder + 1}");
                Transform crankThrow = FindDescendant(generatedRoot, $"Curved crank throw {cylinder + 1}");
                Transform rodJournal = FindDescendant(generatedRoot, $"Rod journal {cylinder + 1}");
                Transform liner = FindDescendant(generatedRoot, $"Cylinder liner {cylinder + 1}");
                Require(piston != null && wristPin != null && rod != null && bigEnd != null && smallEnd != null
                        && crankThrow != null && rodJournal != null && liner != null,
                    $"Cylinder {cylinder + 1} is missing part of its bore-to-crank mechanical chain.");

                Require(Mathf.Abs(piston.localPosition.x - boreCentre.x) <= PositionToleranceM
                        && Mathf.Abs(piston.localPosition.z - boreCentre.z) <= PositionToleranceM,
                    $"Cylinder {cylinder + 1} piston is not concentric with its bore datum.");
                Require(Mathf.Abs(liner.localPosition.x - boreCentre.x) <= PositionToleranceM
                        && Mathf.Abs(liner.localPosition.z - boreCentre.z) <= PositionToleranceM,
                    $"Cylinder {cylinder + 1} liner is not concentric with its bore datum.");

                Vector3 wristCentre = ToAssemblyLocal(visualAssembly, wristPin.position);
                Vector3 rodSmallEnd = ToAssemblyLocal(visualAssembly,
                    rod.TransformPoint(Vector3.up * rodLengthM));
                Vector3 smallEndEye = ToAssemblyLocal(visualAssembly, smallEnd.position);
                Require(Vector3.Distance(wristCentre, piston.localPosition) <= PositionToleranceM,
                    $"Cylinder {cylinder + 1} wrist pin is not centered on the piston pin datum.");
                Require(Vector3.Distance(rodSmallEnd, wristCentre) <= PositionToleranceM
                        && Vector3.Distance(smallEndEye, wristCentre) <= PositionToleranceM,
                    $"Cylinder {cylinder + 1} rod small end is not centered on the wrist pin.");

                float throwAngleDeg = Mathf.Repeat(previewAngleDeg + crankPhaseDeg[cylinder], 360f);
                double throwAngleRad = throwAngleDeg * Mathf.Deg2Rad;
                Vector3 expectedCrankPin = new Vector3(boreCentre.x,
                    (float)SliderCrankKinematics.CrankPinYM(throwAngleRad, crankRadiusM),
                    (float)SliderCrankKinematics.CrankPinZM(throwAngleRad, crankRadiusM));
                Vector3 rodBigEnd = ToAssemblyLocal(visualAssembly, bigEnd.position);
                Vector3 journalCentre = ToAssemblyLocal(visualAssembly, rodJournal.position);
                Require(Vector3.Distance(rod.localPosition, expectedCrankPin) <= PositionToleranceM
                        && Vector3.Distance(rodBigEnd, expectedCrankPin) <= PositionToleranceM
                        && Vector3.Distance(journalCentre, expectedCrankPin) <= PositionToleranceM,
                    $"Cylinder {cylinder + 1} rod big end is not centered on its authoritative crank journal.");
                Require(Mathf.Abs(Vector3.Distance(rodSmallEnd, rodBigEnd) - rodLengthM) <= PositionToleranceM,
                    $"Cylinder {cylinder + 1} connecting-rod center distance does not equal configured rod length.");
            }

            Transform piston1 = FindDescendant(generatedRoot, "Piston assembly 1");
            Transform piston2 = FindDescendant(generatedRoot, "Piston assembly 2");
            Transform piston3 = FindDescendant(generatedRoot, "Piston assembly 3");
            Transform piston4 = FindDescendant(generatedRoot, "Piston assembly 4");
            Require(Mathf.Abs(piston1.localPosition.y - piston4.localPosition.y) <= PositionToleranceM
                    && Mathf.Abs(piston2.localPosition.y - piston3.localPosition.y) <= PositionToleranceM,
                "Conventional I4 piston pairing (1/4 and 2/3) was not preserved.");
            Require(Mathf.Abs(piston1.localPosition.y - piston2.localPosition.y) > PositionToleranceM,
                "The two I4 piston pairs are not 180 crank degrees apart.");

            for (int bearing = 0; bearing < 5; bearing++)
            {
                Transform mainJournal = FindDescendant(generatedRoot, $"Main journal {bearing + 1}");
                Transform saddle = FindDescendant(generatedRoot, $"Main bearing saddle {bearing + 1}");
                Vector3 datum = visualAssembly.GetMainBearingCenterLocal(bearing);
                Require(mainJournal != null && saddle != null
                        && Vector3.Distance(ToAssemblyLocal(visualAssembly, mainJournal.position), datum) <= PositionToleranceM
                        && Vector3.Distance(ToAssemblyLocal(visualAssembly, saddle.position), datum) <= PositionToleranceM,
                    $"Main journal/bearing {bearing + 1} is not centered on the crankshaft datum.");
            }

            int[] expectedFiringOrder = { 0, 2, 3, 1 };
            for (int firingIndex = 0; firingIndex < expectedFiringOrder.Length; firingIndex++)
            {
                int cylinder = expectedFiringOrder[firingIndex];
                double firingAngleDeg = firingIndex * 180.0;
                Require(ValveTimingKinematics.FiringOrderCylinderIndex(firingIndex) == cylinder
                        && Math.Abs(ValveTimingKinematics.CylinderFiringTdcCrankDeg(cylinder) - firingAngleDeg) <= 1e-12
                        && ValveTimingKinematics.CylinderAtFiringTdc(firingAngleDeg, 1e-9) == cylinder,
                    "Firing TDC sequence is not 1-3-4-2 at 0/180/360/540 degrees.");
            }

            VerifyValveTransformChain(visualAssembly, generatedRoot, ValveSide.Intake);
            VerifyValveTransformChain(visualAssembly, generatedRoot, ValveSide.Exhaust);
            VerifyTimingDriveAlignment(visualAssembly, generatedRoot);
            visualAssembly.SetCrankAngleDeg(previewAngleDeg);
        }

        private static void VerifyValveTransformChain(
            InlineFourVisualFidelityAssembly visualAssembly,
            Transform generatedRoot,
            ValveSide side)
        {
            bool intake = side == ValveSide.Intake;
            string prefix = intake ? "Intake" : "Exhaust";
            double openingReferenceDeg = intake
                ? ValveTimingKinematics.IntakeOpeningCrankDeg
                : ValveTimingKinematics.ExhaustOpeningCrankDeg;
            double peakReferenceDeg = intake
                ? ValveTimingKinematics.IntakePeakLiftCrankDeg
                : ValveTimingKinematics.ExhaustPeakLiftCrankDeg;
            double closingReferenceDeg = intake
                ? ValveTimingKinematics.IntakeClosingCrankDeg
                : ValveTimingKinematics.ExhaustClosingCrankDeg;

            for (int cylinder = 0; cylinder < 4; cylinder++)
            for (int valve = 0; valve < 2; valve++)
            {
                Transform movingValve = FindDescendant(generatedRoot,
                    $"{prefix} moving valve {cylinder + 1}-{valve + 1}");
                Transform renderedHead = FindDescendant(generatedRoot,
                    $"{prefix} valve head {cylinder + 1}-{valve + 1}");
                Transform spring = FindDescendant(generatedRoot,
                    $"{prefix} compressing spring {cylinder + 1}-{valve + 1}");
                Transform follower = FindDescendant(generatedRoot,
                    $"{prefix} direct bucket follower {cylinder + 1}-{valve + 1}");
                Transform bucketBody = FindDescendant(generatedRoot,
                    $"{prefix} bucket body {cylinder + 1}-{valve + 1}");
                Transform lobe = FindDescendant(generatedRoot,
                    $"{prefix} cam lobe {cylinder + 1}-{valve + 1}");
                Transform throat = FindDescendant(generatedRoot,
                    $"{prefix} valve-seat throat {cylinder + 1}-{valve + 1}");
                Require(movingValve != null && renderedHead != null && spring != null && follower != null
                        && bucketBody != null && lobe != null && throat != null,
                    $"{prefix} valve {cylinder + 1}-{valve + 1} lacks a complete cam-to-chamber chain.");

                Vector3 seat = visualAssembly.GetValveSeatLocal(cylinder, valve, side);
                Vector3 axis = visualAssembly.GetValveAxisLocal(cylinder, valve, side);
                double firingTdcDeg = ValveTimingKinematics.CylinderFiringTdcCrankDeg(cylinder);
                float closedAngleDeg = (float)ValveTimingKinematics.Normalize720(firingTdcDeg + openingReferenceDeg);
                visualAssembly.SetCrankAngleDeg(closedAngleDeg);
                Vector3 closedValvePosition = movingValve.localPosition;
                Vector3 closedHeadSeat = ToAssemblyLocal(visualAssembly,
                    renderedHead.TransformPoint(Vector3.up));
                float closedSpringScaleY = spring.localScale.y;
                Require(Vector3.Distance(closedValvePosition, seat) <= PositionToleranceM
                        && Vector3.Distance(closedHeadSeat, seat) <= PositionToleranceM,
                    $"{prefix} valve {cylinder + 1}-{valve + 1} is not exactly seated when closed.");

                float openingAngleDeg = (float)ValveTimingKinematics.Normalize720(
                    firingTdcDeg + (openingReferenceDeg + peakReferenceDeg) * 0.5);
                visualAssembly.SetCrankAngleDeg(openingAngleDeg);
                float openingLiftM = Vector3.Distance(seat, movingValve.localPosition);
                Require(openingLiftM > 0f && openingLiftM < visualAssembly.MaximumValveLiftM,
                    $"{prefix} valve {cylinder + 1}-{valve + 1} did not visibly begin opening.");

                float peakAngleDeg = (float)ValveTimingKinematics.Normalize720(firingTdcDeg + peakReferenceDeg);
                visualAssembly.SetCrankAngleDeg(peakAngleDeg);
                Vector3 peakValvePosition = movingValve.localPosition;
                Vector3 peakDisplacement = peakValvePosition - seat;
                Require(Mathf.Abs(peakDisplacement.magnitude - visualAssembly.MaximumValveLiftM) <= PositionToleranceM
                        && Vector3.Distance(peakDisplacement.normalized, -axis) <= 0.0001f,
                    $"{prefix} valve {cylinder + 1}-{valve + 1} actual transform did not reach peak lift on its stem axis.");
                Vector3 peakHeadPosition = ToAssemblyLocal(visualAssembly,
                    renderedHead.TransformPoint(Vector3.up));
                Require(Vector3.Distance(peakHeadPosition, seat - axis * visualAssembly.MaximumValveLiftM)
                        <= PositionToleranceM,
                    $"{prefix} valve {cylinder + 1}-{valve + 1} rendered head did not move by configured lift.");

                float expectedSpringRatio = 1f - visualAssembly.MaximumValveLiftM
                    / visualAssembly.GetSpringClosedLengthM(cylinder, valve, side);
                Require(Mathf.Abs(spring.localScale.y / closedSpringScaleY - expectedSpringRatio) <= 0.0001f,
                    $"{prefix} valve {cylinder + 1}-{valve + 1} spring compression does not follow lift.");

                Vector3 bucketContact = ToAssemblyLocal(visualAssembly, bucketBody.TransformPoint(Vector3.up));
                Vector3 lobeNose = ToAssemblyLocal(visualAssembly, lobe.TransformPoint(Vector3.up * 0.5f));
                Require(Vector3.Distance(bucketContact, lobeNose) <= PositionToleranceM,
                    $"{prefix} cam lobe {cylinder + 1}-{valve + 1} does not meet its bucket at peak lift.");

                float closingAngleDeg = (float)ValveTimingKinematics.Normalize720(
                    firingTdcDeg + (peakReferenceDeg + closingReferenceDeg) * 0.5);
                visualAssembly.SetCrankAngleDeg(closingAngleDeg);
                float closingLiftM = Vector3.Distance(seat, movingValve.localPosition);
                Require(closingLiftM > 0f && closingLiftM < visualAssembly.MaximumValveLiftM,
                    $"{prefix} valve {cylinder + 1}-{valve + 1} did not visibly return through closing.");

                float finalClosedAngleDeg = (float)ValveTimingKinematics.Normalize720(
                    firingTdcDeg + closingReferenceDeg);
                visualAssembly.SetCrankAngleDeg(finalClosedAngleDeg);
                Require(Vector3.Distance(movingValve.localPosition, seat) <= PositionToleranceM
                        && Mathf.Abs(spring.localScale.y - closedSpringScaleY) <= PositionToleranceM,
                    $"{prefix} valve {cylinder + 1}-{valve + 1} did not return exactly to its closed state.");
                Require(Vector3.Distance(visualAssembly.GetPortPathEndLocal(cylinder, valve, side), seat)
                        <= PositionToleranceM,
                    $"{prefix} port {cylinder + 1}-{valve + 1} does not terminate at its valve-seat datum.");
            }
        }

        private static void VerifyTimingDriveAlignment(
            InlineFourVisualFidelityAssembly visualAssembly,
            Transform generatedRoot)
        {
            Transform crankSprocket = FindDescendant(generatedRoot, "Crank timing sprocket");
            Transform intakeSprocket = FindDescendant(generatedRoot, "Intake cam sprocket and phaser");
            Transform exhaustSprocket = FindDescendant(generatedRoot, "Exhaust cam sprocket and phaser");
            Require(crankSprocket != null && intakeSprocket != null && exhaustSprocket != null,
                "Timing sprockets are missing from the mechanical alignment audit.");

            Vector3 crankCentre = ToAssemblyLocal(visualAssembly, crankSprocket.position);
            Vector3 intakeCentre = ToAssemblyLocal(visualAssembly, intakeSprocket.position);
            Vector3 exhaustCentre = ToAssemblyLocal(visualAssembly, exhaustSprocket.position);
            Vector3 expectedCrank = new Vector3(visualAssembly.TimingDrivePlaneXM,
                visualAssembly.CrankshaftCenterLocal.y, visualAssembly.CrankshaftCenterLocal.z);
            Vector3 expectedIntake = new Vector3(visualAssembly.TimingDrivePlaneXM,
                visualAssembly.IntakeCamshaftAxisLocal.y, visualAssembly.IntakeCamshaftAxisLocal.z);
            Vector3 expectedExhaust = new Vector3(visualAssembly.TimingDrivePlaneXM,
                visualAssembly.ExhaustCamshaftAxisLocal.y, visualAssembly.ExhaustCamshaftAxisLocal.z);
            Require(Vector3.Distance(crankCentre, expectedCrank) <= PositionToleranceM
                    && Vector3.Distance(intakeCentre, expectedIntake) <= PositionToleranceM
                    && Vector3.Distance(exhaustCentre, expectedExhaust) <= PositionToleranceM,
                "Timing sprockets are not concentric with their crank/cam axes on the shared timing plane.");
            Require(Mathf.Abs(crankCentre.x - intakeCentre.x) <= PositionToleranceM
                    && Mathf.Abs(crankCentre.x - exhaustCentre.x) <= PositionToleranceM,
                "Timing sprockets are not coplanar.");
        }

        private static Vector3 ToAssemblyLocal(InlineFourVisualFidelityAssembly visualAssembly, Vector3 worldPosition)
        {
            return visualAssembly.transform.InverseTransformPoint(worldPosition);
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
            float previousAngleDeg = visualAssembly.CurrentCrankAngleDeg;
            bool previousDatumsVisible = visualAssembly.ShowEngineeringDatums;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            var paths = new System.Collections.Generic.List<string>();
            try
            {
                camera.targetTexture = target;
                visualAssembly.SetEngineeringDatumsVisible(false);
                float[] crankAnglesDeg = { 0f, 90f, 180f, 360f, 540f, 720f };
                var views = new[]
                {
                    (Label: "Cutaway", Mode: EngineInspectionMode.Cutaway, TimingCloseup: false),
                    (Label: "RotatingAssembly", Mode: EngineInspectionMode.RotatingAssemblyOnly, TimingCloseup: false),
                    (Label: "Valvetrain", Mode: EngineInspectionMode.ValvetrainOnly, TimingCloseup: false),
                    (Label: "TimingDriveCloseup", Mode: EngineInspectionMode.ValvetrainOnly, TimingCloseup: true)
                };
                EngineLabInspectionCamera inspectionCamera = camera.GetComponent<EngineLabInspectionCamera>();
                foreach (float angleDeg in crankAnglesDeg)
                foreach (var view in views)
                {
                    visualAssembly.SetCrankAngleDeg(angleDeg);
                    visualAssembly.SetInspectionMode(view.Mode);
                    if (inspectionCamera != null)
                    {
                        if (view.TimingCloseup)
                        {
                            inspectionCamera.SetPivot(visualAssembly.transform.TransformPoint(
                                new Vector3(visualAssembly.TimingDrivePlaneXM,
                                    visualAssembly.IntakeCamshaftAxisLocal.y * 0.52f, 0f)));
                            inspectionCamera.SetOrbit(90f, 4f);
                            inspectionCamera.SetDistance(0.42f);
                        }
                        else
                        {
                            inspectionCamera.SetPivot(visualAssembly.transform.TransformPoint(
                                visualAssembly.RecommendedFocusPointLocal));
                            inspectionCamera.SetOrbit(32f, 12f);
                            inspectionCamera.SetDistance(visualAssembly.RecommendedCameraDistanceM);
                        }
                    }

                    camera.Render();
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture.Apply();
                    string angleLabel = Mathf.RoundToInt(angleDeg).ToString("000");
                    string path = Path.GetFullPath(Path.Combine(Application.dataPath,
                        $"../Logs/EngineLabAlignment_{view.Label}_{angleLabel}.png"));
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, texture.EncodeToPNG());
                    paths.Add(path);
                }

                return string.Join(", ", paths);
            }
            finally
            {
                visualAssembly.SetCrankAngleDeg(previousAngleDeg);
                visualAssembly.SetInspectionMode(previousMode);
                visualAssembly.SetEngineeringDatumsVisible(previousDatumsVisible);
                camera.GetComponent<EngineLabInspectionCamera>()?.ResetEngineView();
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
