#if UNITY_EDITOR
using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

namespace Bigimong.AR.EditorChecks
{
    public static class CombatPresentationEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            var host = new GameObject("Combat Pool Check");
            try
            {
                var vfx = host.AddComponent<ProceduralCombatVfx>();
                foreach (var profile in ElementSkillCatalog.All)
                {
                    vfx.PlaySkill(profile, host.transform, host.transform, "LEFT");
                    vfx.PlayImpact(profile, Vector3.zero, true);
                }
                Require(vfx.AllocatedCount == 32 && vfx.ActiveCount == 32, "pool must saturate at 32");
                Require(host.GetComponentsInChildren<Collider>(true).Length == 0, "VFX must not collide");
                vfx.Advance(2);
                Require(vfx.ActiveCount == 0, "all effects must expire in bounded time");
                vfx.PlayImpact(ElementSkillCatalog.Resolve(1), Vector3.zero);
                Require(vfx.AllocatedCount == 32 && vfx.ActiveCount == 6, "expired slots must be reused");
                vfx.enabled = false;
                Require(vfx.ActiveCount == 0, "disable must release effects");
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }

            foreach (var profile in ElementSkillCatalog.All)
            {
                var pet = ProceduralDragonFactory.Create(profile.artId, "YOUTH");
                try
                {
                    var actor = pet.GetComponent<ArBattleActor>();
                    actor.Initialize("A", new BattlePlayerMessage { artId = profile.artId, stage = "YOUTH", hp = 10 });
                    Require(actor.SkillProfile == profile, "actor must resolve its catalog profile");
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    var beforeScale = pet.transform.localScale;
                    typeof(ArBattleActor).GetMethod("GroundedIdleMotion", flags).Invoke(actor, new object[] { 1f });
                    Require(pet.transform.localScale.y > beforeScale.y, "ground idle must breathe");
                    var beforeY = pet.transform.localPosition.y;
                    typeof(ArBattleActor).GetMethod("WingedIdleMotion", flags).Invoke(actor, new object[] { 1f });
                    var winged = false;
                    foreach (var part in pet.GetComponentsInChildren<Transform>()) if (part.name.Contains("Wing")) winged = true;
                    Require(winged ? pet.transform.localPosition.y > beforeY : Mathf.Approximately(pet.transform.localPosition.y, beforeY), "hover must follow actual wing geometry");
                    typeof(ArBattleActor).GetMethod("RestoreHome", flags).Invoke(actor, null);
                    var animator = pet.AddComponent<Animator>();
                    typeof(ArBattleActor).GetField("animator", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(actor, animator);
                    Require(!actor.UsesAnimator, "empty Animator must retain procedural fallback");
                    var controller = new UnityEditor.Animations.AnimatorController();
                    try
                    {
                        animator.runtimeAnimatorController = controller;
                        Require(actor.UsesAnimator, "installed Animator controller must take priority");
                    }
                    finally { animator.runtimeAnimatorController = null; UnityEngine.Object.DestroyImmediate(controller); }
                    var home = pet.transform.localPosition;
                    var hit = (IEnumerator)typeof(ArBattleActor).GetMethod("HitMotion", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(actor, new object[] { Vector3.left });
                    Require(hit.MoveNext() && hit.Current is WaitForSecondsRealtime pause && Mathf.Approximately(pause.waitTime, .06f), "hit must yield actor-local 60ms stop first");
                    Require(pet.transform.localPosition == home, "hit stop must preserve pose");
                    actor.PrepareSummonRise();
                    actor.ResetSummonPose();
                    Require(pet.transform.localPosition == home, "summon cancellation restores home");
                }
                finally { UnityEngine.Object.DestroyImmediate(pet); }
            }
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
#endif
