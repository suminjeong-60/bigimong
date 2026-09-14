#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

namespace Bigimong.AR.EditorChecks
{
    public static class BigimongReferenceUiEditorChecks
    {
        private static readonly string[] Screens = { "loading", "battle-loading", "avatar", "egg", "home" };

        public static void RunSceneChecks()
        {
            foreach (var screen in Screens)
            {
                var path = "ReferenceUi/" + screen;
                var art = Resources.Load<Texture2D>(path);
                if (art == null) throw new InvalidOperationException($"Reference image missing from Unity project: {path}");
                if (art.width != 1030 || art.height != 1536)
                    throw new InvalidOperationException($"Reference art has wrong size: {path} ({art.width}x{art.height})");
            }

            var view = UnityEngine.Object.FindObjectOfType<BigimongReferenceUi>();
            if (view == null) throw new InvalidOperationException("Reference screen controller is missing from the scene");
            var field = typeof(BigimongReferenceUi).GetField("flow", BindingFlags.NonPublic | BindingFlags.Instance);
            if (field == null || field.GetValue(view) is not OfflineBetaFlowController)
                throw new InvalidOperationException("Reference screen controller must connect to offline beta flow");
            foreach (var route in new[]
            {
                ("home", "상점", "shop"), ("home", "놀아주기", "play"),
                ("home", "1:1 대전", "battle-loading"), ("home", "도감", "codex"),
                ("home", "퀘스트", "quest"), ("home", "선물", "gift"),
                ("home", "설정", "settings"), ("avatar", "선택 완료", "egg")
            })
                if (BigimongReferenceUi.ResolveDestination(route.Item1, route.Item2) != route.Item3)
                    throw new InvalidOperationException($"Reference button route is invalid: {route.Item1}/{route.Item2}");
        }
    }
}
#endif
