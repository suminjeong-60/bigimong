#if UNITY_EDITOR
using System;
using UnityEditor.Build;
using UnityEngine.Rendering;

namespace Bigimong.Editor
{
    public static class GraphicsBuildEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            Reject(GraphicsDeviceType.Null, true);
            Reject(GraphicsDeviceType.Null, false);
            Reject(GraphicsDeviceType.Metal, false);
            BigimongAndroidBuild.RequireGraphicsDevice(GraphicsDeviceType.Metal, true);
        }

        private static void Reject(GraphicsDeviceType device, bool renderTextures)
        {
            try { BigimongAndroidBuild.RequireGraphicsDevice(device, renderTextures); }
            catch (BuildFailedException error)
            {
                if (!error.Message.Contains("-nographics") || !error.Message.Contains("RenderTexture"))
                    throw new InvalidOperationException("Graphics failure must explain the required remedy.");
                return;
            }
            throw new InvalidOperationException("Null graphics or missing RenderTexture support must abort the build.");
        }
    }
}
#endif
