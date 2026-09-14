#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.XR.ARCore;
using UnityEngine.XR.Management;

namespace Bigimong.Editor
{
    public static class BigimongArBuildConfiguration
    {
        public static void Configure()
        {
            const string root = "Assets/XR";
            const string settingsPath = root + "/BigimongXRGeneralSettings.asset";
            if (!AssetDatabase.IsValidFolder(root)) AssetDatabase.CreateFolder("Assets", "XR");
            if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out XRGeneralSettingsPerBuildTarget targets) || targets == null)
            {
                targets = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(settingsPath);
                if (targets == null)
                {
                    targets = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(targets, settingsPath);
                }
                EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, targets, true);
            }
            const BuildTargetGroup target = BuildTargetGroup.Android;
            if (!targets.HasSettingsForBuildTarget(target)) targets.CreateDefaultSettingsForBuildTarget(target);
            if (!targets.HasManagerSettingsForBuildTarget(target)) targets.CreateDefaultManagerSettingsForBuildTarget(target);
            var settings = targets.SettingsForBuildTarget(target);
            var manager = targets.ManagerSettingsForBuildTarget(target);
            settings.InitManagerOnStart = true;
            manager.automaticLoading = true;
            manager.automaticRunning = true;
            if (!manager.activeLoaders.Any(loader => loader is ARCoreLoader) &&
                !XRPackageMetadataStore.AssignLoader(manager, typeof(ARCoreLoader).FullName, target))
                throw new InvalidOperationException("Could not assign Android ARCore loader");
            if (!manager.activeLoaders.Any(loader => loader is ARCoreLoader))
                throw new InvalidOperationException("Android ARCore loader missing after configuration");

            const string arCorePath = root + "/BigimongARCoreSettings.asset";
            var arCore = ARCoreSettings.currentSettings;
            if (arCore == null) arCore = AssetDatabase.LoadAssetAtPath<ARCoreSettings>(arCorePath);
            if (arCore == null) arCore = ARCoreSettings.GetOrCreateSettings();
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(arCore))) AssetDatabase.CreateAsset(arCore, arCorePath);
            arCore.requirement = ARCoreSettings.Requirement.Optional;
            arCore.depth = ARCoreSettings.Requirement.Optional;
            ARCoreSettings.currentSettings = arCore;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3 });
            EditorUtility.SetDirty(targets);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(arCore);
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
