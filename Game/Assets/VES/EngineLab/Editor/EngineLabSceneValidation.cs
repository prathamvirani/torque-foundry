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
    /// <summary>
    /// Repeatable editor validation for the dedicated Engine Lab scene and its
    /// disposable presentation hierarchy. This is intentionally outside Core.
    /// </summary>
    public static class EngineLabSceneValidation
    {
        private const string ScenePath = "Assets/VES/EngineLab/Scenes/EngineLab.unity";
        private const string GeneratedRootName = "Generated I4 Mechanism";
        private const string InteractiveValidationArgument = "-torqueFoundryValidateEngineLab";
        private const string InteractiveValidationSessionKey = "TorqueFoundry.EngineLabInteractiveValidationRan";
        private const float PositionToleranceM = 0.00001f;

        [InitializeOnLoadMethod]
        private static void ScheduleRequestedInteractiveValidation()
        {
            if (Application.isBatchMode
                || SessionState.GetBool(InteractiveValidationSessionKey, false)
                || Array.IndexOf(Environment.GetCommandLineArgs(), InteractiveValidationArgument) < 0)
            {
                return;
            }

            SessionState.SetBool(InteractiveValidationSessionKey, true);
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
            try
            {
                ClearConsole();
                Require(EditorApplication.ExecuteMenuItem("Torque Foundry/Validate Engine Lab Scene"),
                    "Engine Lab validation menu command could not be executed.");

                int consoleErrorCount = GetConsoleErrorCount();
                Require(consoleErrorCount == 0,
                    $"Unity Console contained {consoleErrorCount} red error(s) after scene validation.");

                string screenshotPath = CaptureInspectionScreenshot();
                AppendReport($"Interactive editor verification PASSED: Console red errors = 0.\n"
                             + $"Inspection screenshot: {screenshotPath}");
                Debug.Log("Interactive Engine Lab verification PASSED: validator passed and Console has zero red errors.");
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                string report = $"Interactive Engine Lab verification FAILED: {exception}";
                WriteReport(report);
                Debug.LogError(report);
                EditorApplication.delayCall += () => EditorApplication.Exit(1);
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
            InlineFourVisualizer visualizer = root.GetComponent<InlineFourVisualizer>();
            InlineFourEngineContextVisualizer contextVisualizer = root.GetComponent<InlineFourEngineContextVisualizer>();
            EngineLabInspectionPanel inspectionPanel = root.GetComponent<EngineLabInspectionPanel>();
            EngineLabInspectionCamera inspectionCamera = UnityEngine.Object.FindAnyObjectByType<EngineLabInspectionCamera>();
            Require(controller != null, "Engine Lab root is missing EngineLabController.");
            Require(visualizer != null, "Engine Lab root is missing InlineFourVisualizer.");
            Require(contextVisualizer != null, "Engine Lab root is missing InlineFourEngineContextVisualizer.");
            Require(inspectionPanel != null, "Engine Lab root is missing EngineLabInspectionPanel.");
            Require(inspectionCamera != null, "Dedicated scene is missing EngineLabInspectionCamera.");
            Require(CountMissingScripts(scene) == 0, "The dedicated scene contains missing MonoBehaviour scripts.");

            var controllerObject = new SerializedObject(controller);
            var visualizerObject = new SerializedObject(visualizer);
            float originalBoreMm = ReadFloat(controllerObject, "boreMm");
            float originalStrokeMm = ReadFloat(controllerObject, "strokeMm");
            float originalRodLengthMm = ReadFloat(controllerObject, "connectingRodLengthMm");
            float originalPreviewAngleDeg = ReadFloat(visualizerObject, "previewCrankAngleDeg");

            Transform previousGeneratedRoot = null;
            Transform previousContextRoot = null;
            const float previewAngleDeg = 37f;
            SetFloat(visualizerObject, "previewCrankAngleDeg", previewAngleDeg);
            visualizerObject.ApplyModifiedPropertiesWithoutUndo();

            VerifyRebuild(controller, visualizer, contextVisualizer, controllerObject, 86f, 86f, 143f,
                previewAngleDeg, ref previousGeneratedRoot, ref previousContextRoot);
            VerifyRebuild(controller, visualizer, contextVisualizer, controllerObject, 92f, 86f, 150f,
                previewAngleDeg, ref previousGeneratedRoot, ref previousContextRoot);
            VerifyRebuild(controller, visualizer, contextVisualizer, controllerObject, 84f, 94f, 155f,
                previewAngleDeg, ref previousGeneratedRoot, ref previousContextRoot);

            SetFloat(controllerObject, "boreMm", originalBoreMm);
            SetFloat(controllerObject, "strokeMm", originalStrokeMm);
            SetFloat(controllerObject, "connectingRodLengthMm", originalRodLengthMm);
            controllerObject.ApplyModifiedPropertiesWithoutUndo();
            SetFloat(visualizerObject, "previewCrankAngleDeg", originalPreviewAngleDeg);
            visualizerObject.ApplyModifiedPropertiesWithoutUndo();
            controller.Recalculate();
            visualizer.RebuildPreview();
            contextVisualizer.RebuildPreview();
            VerifyPresentationControls(controller, visualizer, contextVisualizer, inspectionCamera);

            Require(EditorSceneManager.SaveScene(scene), "Dedicated Engine Lab scene could not be saved after validation.");

            return "Engine Lab scene validation PASSED: scene opened, compile completed, root transform reset, "
                   + "no missing scripts, bore/stroke/rod-length rebuild cases replaced the generated I4 hierarchy correctly, "
                   + "and bounded camera, teaching-state, and inspection-visibility controls behaved deterministically.";
        }

        private static void VerifyRebuild(
            EngineLabController controller,
            InlineFourVisualizer visualizer,
            InlineFourEngineContextVisualizer contextVisualizer,
            SerializedObject controllerObject,
            float boreMm,
            float strokeMm,
            float rodLengthMm,
            float previewAngleDeg,
            ref Transform previousGeneratedRoot,
            ref Transform previousContextRoot)
        {
            SetFloat(controllerObject, "boreMm", boreMm);
            SetFloat(controllerObject, "strokeMm", strokeMm);
            SetFloat(controllerObject, "connectingRodLengthMm", rodLengthMm);
            controllerObject.ApplyModifiedPropertiesWithoutUndo();

            controller.Recalculate();
            visualizer.RebuildPreview();
            contextVisualizer.RebuildPreview();

            Transform generatedRoot = controller.transform.Find(GeneratedRootName);
            Require(generatedRoot != null, $"Generated hierarchy missing for {boreMm} x {strokeMm} mm geometry.");
            Require(previousGeneratedRoot == null || generatedRoot != previousGeneratedRoot,
                "Rebuild reused a stale generated hierarchy.");
            previousGeneratedRoot = generatedRoot;

            Require(generatedRoot.childCount == 3, "Expected three independently inspectable mechanism groups.");
            RequireGroupChildCount(generatedRoot, "Rotating Assembly", 25);
            RequireGroupChildCount(generatedRoot, "Pistons and Rods", 12);
            RequireGroupChildCount(generatedRoot, "Bore Guides", 16);
            Require(CountChildrenWithPrefix(generatedRoot, "Piston ") == 4, "Expected four pistons.");
            Require(CountChildrenWithPrefix(generatedRoot, "Connecting Rod ") == 4, "Expected four connecting rods.");
            Require(CountChildrenWithPrefix(generatedRoot, "Crank Pin ") == 4, "Expected four crank pins.");
            Require(CountChildrenWithPrefix(generatedRoot, "Main Journal ") == 5, "Expected five main journals.");
            Require(CountChildrenWithPrefix(generatedRoot, "Crank Web ") == 8, "Expected paired crank webs per throw.");
            Require(CountChildrenWithPrefix(generatedRoot, "Counterweight ") == 8, "Expected paired counterweights per throw.");
            Require(CountChildrenWithPrefix(generatedRoot, "Cylinder ") == 16, "Expected four bore guides per cylinder.");

            Transform contextRoot = controller.transform.Find(contextVisualizer.GeneratedHierarchyName);
            Require(contextRoot != null, "Generated block/head context hierarchy is missing.");
            Require(previousContextRoot == null || contextRoot != previousContextRoot,
                "Context rebuild reused a stale generated hierarchy.");
            previousContextRoot = contextRoot;
            Require(contextRoot.childCount == 4, "Expected four independently inspectable context groups.");
            RequireGroupChildCount(contextRoot, "Block Envelope", 4);
            RequireGroupChildCount(contextRoot, "Cylinder Liners", 4);
            RequireGroupChildCount(contextRoot, "Deck Plane", 7);
            RequireGroupChildCount(contextRoot, "Cylinder Head Envelope", 4);

            Transform piston1 = generatedRoot.Find("Pistons and Rods/Piston 1");
            Transform piston2 = generatedRoot.Find("Pistons and Rods/Piston 2");
            Transform connectingRod1 = generatedRoot.Find("Pistons and Rods/Connecting Rod 1");
            Require(piston1 != null && piston2 != null && connectingRod1 != null, "Primary mechanism objects are missing.");

            float boreM = boreMm / 1000f;
            float strokeM = strokeMm / 1000f;
            float crankRadiusM = strokeM * 0.5f;
            float rodLengthM = rodLengthMm / 1000f;
            float expectedSpacingM = boreM * 1.15f;
            float expectedPistonPinYM = (float)SliderCrankKinematics.PistonPinHeightM(
                previewAngleDeg * Mathf.Deg2Rad,
                crankRadiusM,
                rodLengthM);

            float actualSpacingM = piston2.localPosition.x - piston1.localPosition.x;
            Require(Mathf.Abs(actualSpacingM - expectedSpacingM) <= PositionToleranceM,
                $"Cylinder spacing did not rebuild from bore: expected {expectedSpacingM:R} m, actual {actualSpacingM:R} m.");
            Require(Mathf.Abs(piston1.localScale.x - boreM * 0.90f) <= PositionToleranceM,
                "Piston diameter did not rebuild from bore.");
            Require(Mathf.Abs(piston1.localPosition.y - expectedPistonPinYM) <= PositionToleranceM,
                "Piston position does not match authoritative slider-crank geometry.");
            Require(Mathf.Abs(connectingRod1.localScale.y * 2f - rodLengthM) <= PositionToleranceM,
                "Connecting-rod presentation length does not match configured geometry.");

            EngineCalculatedState expectedState = EngineCalculator.Calculate(controller.CreateConfiguration());
            Require(Math.Abs(controller.DisplacementLitres - expectedState.TotalDisplacementLitres) <= 1e-12,
                "Controller displacement is stale after a geometry rebuild.");
            Require(Math.Abs(controller.RodStrokeRatio - expectedState.RodStrokeRatio) <= 1e-12,
                "Controller rod/stroke ratio is stale after a geometry rebuild.");
        }

        private static int CountMissingScripts(Scene scene)
        {
            int missingCount = 0;
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            {
                foreach (Transform transform in sceneRoot.GetComponentsInChildren<Transform>(true))
                {
                    missingCount += GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject);
                }
            }

            return missingCount;
        }

        private static int CountChildrenWithPrefix(Transform parent, string prefix)
        {
            int count = 0;
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
            {
                if (child != parent && child.name.StartsWith(prefix, StringComparison.Ordinal)) count++;
            }

            return count;
        }

        private static void RequireGroupChildCount(Transform contextRoot, string groupName, int expectedCount)
        {
            Transform group = contextRoot.Find(groupName);
            Require(group != null, $"Context group '{groupName}' is missing.");
            Require(group.childCount == expectedCount,
                $"Context group '{groupName}' expected {expectedCount} objects, found {group.childCount}.");
        }

        private static void VerifyPresentationControls(
            EngineLabController controller,
            InlineFourVisualizer visualizer,
            InlineFourEngineContextVisualizer contextVisualizer,
            EngineLabInspectionCamera inspectionCamera)
        {
            float simulatedOperatingRpm = controller.EngineSpeedRpm;
            bool originalPlaying = visualizer.IsTeachingAnimationPlaying;
            float originalTeachingRpm = visualizer.TeachingAnimationRpm;
            float originalAngleDeg = visualizer.CurrentCrankAngleDeg;
            bool originalRotatingVisibility = visualizer.IsRotatingAssemblyVisible;
            bool originalPistonVisibility = visualizer.ArePistonsAndRodsVisible;
            bool originalGuideVisibility = visualizer.AreBoreGuidesVisible;
            bool originalBlockVisibility = contextVisualizer.IsBlockEnvelopeVisible;
            bool originalLinerVisibility = contextVisualizer.AreCylinderLinersVisible;
            bool originalDeckVisibility = contextVisualizer.IsDeckPlaneVisible;
            bool originalHeadVisibility = contextVisualizer.IsHeadEnvelopeVisible;

            visualizer.SetTeachingAnimationPlaying(false);
            visualizer.SetTeachingAnimationRpm(42f);
            visualizer.SetCrankAngleDeg(123f);
            Require(!visualizer.IsTeachingAnimationPlaying, "Teaching animation did not pause.");
            Require(Mathf.Approximately(visualizer.TeachingAnimationRpm, 42f), "Teaching RPM did not update.");
            Require(Mathf.Approximately(visualizer.CurrentCrankAngleDeg, 123f), "Crank-angle scrub state did not update.");
            Require(Mathf.Approximately(controller.EngineSpeedRpm, simulatedOperatingRpm),
                "Teaching controls changed the simulated engine operating RPM.");

            Transform mechanismRoot = controller.transform.Find(GeneratedRootName);
            Transform contextRoot = controller.transform.Find(contextVisualizer.GeneratedHierarchyName);
            visualizer.SetRotatingAssemblyVisible(false);
            visualizer.SetPistonsAndRodsVisible(false);
            visualizer.SetBoreGuidesVisible(false);
            contextVisualizer.SetBlockEnvelopeVisible(false);
            contextVisualizer.SetCylinderLinersVisible(false);
            contextVisualizer.SetDeckPlaneVisible(false);
            contextVisualizer.SetHeadEnvelopeVisible(false);
            RequireGroupsInactive(mechanismRoot, "Rotating Assembly", "Pistons and Rods", "Bore Guides");
            RequireGroupsInactive(contextRoot, "Block Envelope", "Cylinder Liners", "Deck Plane", "Cylinder Head Envelope");

            visualizer.SetTeachingAnimationPlaying(originalPlaying);
            visualizer.SetTeachingAnimationRpm(originalTeachingRpm);
            visualizer.SetCrankAngleDeg(originalAngleDeg);
            visualizer.SetRotatingAssemblyVisible(originalRotatingVisibility);
            visualizer.SetPistonsAndRodsVisible(originalPistonVisibility);
            visualizer.SetBoreGuidesVisible(originalGuideVisibility);
            contextVisualizer.SetBlockEnvelopeVisible(originalBlockVisibility);
            contextVisualizer.SetCylinderLinersVisible(originalLinerVisibility);
            contextVisualizer.SetDeckPlaneVisible(originalDeckVisibility);
            contextVisualizer.SetHeadEnvelopeVisible(originalHeadVisibility);

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
            Require(inspectionCamera.PitchDeg <= 80f, "Inspection camera pitch limit failed.");
            inspectionCamera.SetPivot(defaultPivot + Vector3.one * 100f);
            Require(Vector3.Distance(defaultPivot, inspectionCamera.Pivot) <= 0.65001f,
                "Inspection camera pan/focus limit failed.");
            inspectionCamera.ResetEngineView();
            Require(Vector3.Distance(defaultPivot, inspectionCamera.Pivot) <= PositionToleranceM,
                "Reset Engine View did not restore the engine focus.");
            Require(Mathf.Abs(defaultDistance - inspectionCamera.DistanceM) <= PositionToleranceM,
                "Reset Engine View did not restore the default zoom.");
        }

        private static void RequireGroupsInactive(Transform root, params string[] groupNames)
        {
            Require(root != null, "Generated inspection hierarchy is missing.");
            foreach (string groupName in groupNames)
            {
                Transform group = root.Find(groupName);
                Require(group != null && !group.gameObject.activeSelf,
                    $"Inspection group '{groupName}' did not hide independently.");
            }
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

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return (a - b).sqrMagnitude <= 1e-12f;
        }

        private static bool Approximately(Quaternion a, Quaternion b)
        {
            return Mathf.Abs(Quaternion.Dot(a, b)) >= 0.999999f;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void WriteReport(string report)
        {
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/EngineLabSceneValidation.txt"));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            File.WriteAllText(reportPath, report + Environment.NewLine, System.Text.Encoding.UTF8);
        }

        private static void AppendReport(string report)
        {
            string reportPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/EngineLabSceneValidation.txt"));
            File.AppendAllText(reportPath, report + Environment.NewLine, System.Text.Encoding.UTF8);
        }

        private static void ClearConsole()
        {
            Type logEntriesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo clearMethod = logEntriesType?.GetMethod(
                "Clear",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Require(clearMethod != null, "Unity Console clear API was unavailable.");
            clearMethod.Invoke(null, null);
        }

        private static int GetConsoleErrorCount()
        {
            Type logEntriesType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.LogEntries");
            MethodInfo countMethod = logEntriesType?.GetMethod(
                "GetCountsByType",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Require(countMethod != null, "Unity Console count API was unavailable.");

            object[] counts = { 0, 0, 0 };
            countMethod.Invoke(null, counts);
            return (int)counts[0];
        }

        private static string CaptureInspectionScreenshot()
        {
            Camera camera = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();
            Require(camera != null, "Dedicated Engine Lab scene has no inspection camera.");

            const int width = 1280;
            const int height = 720;
            RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);

            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();

                string screenshotPath = Path.GetFullPath(
                    Path.Combine(Application.dataPath, "../Logs/EngineLabInteractiveValidation.png"));
                Directory.CreateDirectory(Path.GetDirectoryName(screenshotPath));
                File.WriteAllBytes(screenshotPath, texture.EncodeToPNG());
                return screenshotPath;
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                UnityEngine.Object.DestroyImmediate(texture);
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }
}
