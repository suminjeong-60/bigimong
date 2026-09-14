#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Bigimong.Editor
{
    public static class BigimongAndroidBuild
    {
        private const string ApplicationId = "com.bigimong.app";
        private const string BundleVersion = "0.13.0";
        private const int VersionCode = 13;
        private const string ScenePath = "Assets/BigimongAR/Scenes/ArBattle.unity";
        private const string ApkName = "Bigimong-AR-v0.13-debug.apk";

        public static void BuildDebugApk()
        {
            Bigimong.AR.EditorChecks.AvatarProfileStoreEditorChecks.Run();
            Bigimong.AR.EditorChecks.AvatarCreatorEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.SummonSequenceEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.CombatPresentationEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.OfflineBetaEditorChecks.RunBehaviorChecks();
            Bigimong.AR.EditorChecks.ReferenceVisualEditorChecks.RunBehaviorChecks();
            BigimongArSceneBuilder.CreateScene();
            Bigimong.AR.EditorChecks.AvatarCreatorEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.SummonSequenceEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.OfflineBetaEditorChecks.RunSceneChecks();
            Bigimong.AR.EditorChecks.ReferenceVisualEditorChecks.RunSceneChecks();
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
