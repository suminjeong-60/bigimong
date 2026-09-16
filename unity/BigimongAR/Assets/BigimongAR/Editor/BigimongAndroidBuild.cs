#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Bigimong.Editor
{
    public static class BigimongAndroidBuild
    {
        private const string ApplicationId = "com.bigimong.app";
        private const string BundleVersion = "0.19.0";
        private const int VersionCode = 19;
        private const string ScenePath = "Assets/BigimongAR/Scenes/ArBattle.unity";
        private const string ApkName = "Bigimong-AR-v0.19-debug.apk";

        public static void BuildDebugApk()
        {
            RequireGraphicsDevice();
            GraphicsBuildEditorChecks.RunBehaviorChecks();
            BigimongFontAssetBuilder.Prepare();
            Bigimong.AR.EditorChecks.HatchHomeStateEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.HatchHomeStoreEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.HatchProgressEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.HatchSelectionEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.HomeCharacterResolverEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.HomeFocusStageEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.HatchSequenceEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.HatchAudioEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.HomeStagePerformanceEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.AvatarProfileStoreEditorChecks.Run();
            Bigimong.AR.EditorChecks.AvatarCreatorEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.SummonSequenceEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.CombatPresentationEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.OfflineBetaEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.ReferenceVisualEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.VarcoAssetImportEditorChecks.PrepareAndValidate();
            BigimongArSceneBuilder.CreateScene();
            Bigimong.AR.EditorChecks.AvatarCreatorEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.SummonSequenceEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.OfflineBetaEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.ReferenceVisualEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.BigimongReferenceUiEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.HatchHomeSceneEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.HatchAudioEditorChecks.RunSceneChecks();
            ConfigureAndroidPlayer();
            ArConfigurationEditorChecks.Run();

            var outputPath = RepositoryOutputPath();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? throw new InvalidOperationException("APK output directory is invalid."));

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            });

            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    $"Bigimong AR APK build failed: {report.summary.result}, " +
                    $"errors={report.summary.totalErrors}, warnings={report.summary.totalWarnings}"
                );
            }

            Debug.Log($"Bigimong AR debug APK created: {outputPath}");
        }

        private static void RequireGraphicsDevice()
        {
            Debug.Log($"Bigimong graphics gate: type={SystemInfo.graphicsDeviceType}, name={SystemInfo.graphicsDeviceName}, " +
                $"version={SystemInfo.graphicsDeviceVersion}, renderTextures={SystemInfo.supportsRenderTextures}");
            RequireGraphicsDevice(SystemInfo.graphicsDeviceType, SystemInfo.supportsRenderTextures);
        }

        internal static void RequireGraphicsDevice(GraphicsDeviceType device, bool renderTextures)
        {
            if (device == GraphicsDeviceType.Null || !renderTextures)
                throw new BuildFailedException("Bigimong requires a graphics device for mandatory RenderTexture/Camera.Render/ReadPixels checks. " +
                    "Remove -nographics; use a graphics-capable local editor or macOS GameCI with enableGpu: true. Checks cannot be skipped.");
        }

        private static void ConfigureAndroidPlayer()
        {
            PlayerSettings.companyName = "Bigimong";
            PlayerSettings.productName = "Bigimong AR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, ApplicationId);
            PlayerSettings.bundleVersion = BundleVersion;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel28;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
            EditorUserBuildSettings.exportAsGoogleAndroidProject = false;
        }

        private static string RepositoryOutputPath()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, "../../../build/Android", ApkName));
        }
    }
}
#endif
