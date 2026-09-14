#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.XR.ARCore;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.ARCore;
using Bigimong.AR;

namespace Bigimong.Editor
{
    public static class ArConfigurationEditorChecks
    {
        public static void Run()
        {
            BigimongArBuildConfiguration.Configure();
            var first = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            BigimongArBuildConfiguration.Configure();
            var second = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            Require(first == second && second.InitManagerOnStart, "XR configuration must be stable and start automatically");
            Require(second.Manager.activeLoaders.Count(loader => loader is ARCoreLoader) == 1, "Exactly one ARCore loader required");
            Require(ARCoreSettings.currentSettings.requirement == ARCoreSettings.Requirement.Optional, "Non-AR fallback must remain available");
            var camera = Camera.main;
            Require(camera != null && camera.GetComponent<ArCameraPoseDriver>() != null, "AR camera pose driver missing");
            var sample = new GameObject("Pose check");
            try
            {
                var position = new Vector3(1, 2, 3);
                var rotation = Quaternion.Euler(10, 20, 30);
                Require(ArCameraPoseDriver.TryApplyPose(sample.transform, true, position, rotation), "Tracked pose rejected");
                Require(sample.transform.localPosition == position && Quaternion.Angle(sample.transform.localRotation, rotation) < .001f, "Pose not applied");
                Require(!ArCameraPoseDriver.TryApplyPose(sample.transform, false, Vector3.zero, Quaternion.identity), "Lost tracking must preserve pose");
                Require(!ArCameraPoseDriver.TryApplyPose(sample.transform, true, new Vector3(float.NaN, 0, 0), rotation), "NaN position accepted");
                Require(!ArCameraPoseDriver.TryApplyPose(sample.transform, true, Vector3.zero, new Quaternion(0, 0, 0, 0)), "Invalid rotation accepted");
                Require(sample.transform.localPosition == position, "Invalid tracking moved camera");
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
            Debug.Log("AR configuration and camera pose checks passed");
        }
        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
#endif
