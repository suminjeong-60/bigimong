#if UNITY_EDITOR
using System;
using System.Reflection;
using System.Collections.Generic;
using Bigimong.AR;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Bigimong.AR.EditorChecks
{
    public static class SummonSequenceEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            VerifyMagicCircleGeometry();
            VerifyMedallionCloneOwnership();
            VerifyCancellationGeneration();
            VerifyHudLocking();
            VerifyPetSummonPoseLifecycle();
            VerifyBattleUpdateQueueing();
            VerifyTrackingLossCancellation();
            VerifySummonerPlacementUsesPetHome();
        }

        public static void RunSceneChecks()
        {
            var sequence = RequiredSceneComponent<SummonSequenceDirector>();
            var director = RequiredSceneComponent<ArBattleDirector>();
            AssertTrue(sequence != null, "scene requires a summon sequence director");
            AssertTrue(GetField<SummonSequenceDirector>(director, "summonDirector") == sequence,
                "battle director must reference the scene summon sequence");
        }

        private static void VerifyMagicCircleGeometry()
        {
            var arena = new GameObject("Summon Check Arena");
            try
            {
                var circle = ProceduralMagicCircle.Create(arena.transform);
                AssertEqual(ProceduralMagicCircle.RingCount, NamedChildren(circle.transform, "Rotating Ring"), "ring count");
                AssertEqual(ProceduralMagicCircle.RuneCount, NamedChildren(circle.transform, "Radial Rune"), "rune count");
                AssertTrue(circle.transform.Find("Center Disc") != null, "magic circle requires a center disc");
                AssertEqual(0, circle.GetComponentsInChildren<Collider>(true).Length, "magic circle collider count");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(arena);
            }
        }

        private static void VerifyMedallionCloneOwnership()
        {
            var arena = new GameObject("Summon Check Arena");
            var avatar = ProceduralAvatarFactory.Create(AvatarProfile.CreateDefault());
            try
            {
                var original = avatar.transform.Find("SummoningMedallion");
                var originalParent = original.parent;
                var summoner = avatar.AddComponent<SummonerActor>();
                summoner.Initialize(arena.transform);
                var thrown = summoner.PrepareThrownMedallion();
                AssertTrue(thrown != null && thrown != original, "throw must create a medallion clone");
                AssertTrue(original.parent == originalParent, "worn medallion must retain its avatar parent");
                AssertTrue(thrown.parent == arena.transform, "thrown clone must move with the arena");
                AssertTrue(!original.gameObject.activeSelf, "worn medallion hides while its clone is thrown");
                summoner.ResetPose();
                AssertTrue(original.gameObject.activeSelf, "cancellation restores the worn medallion");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(avatar);
                UnityEngine.Object.DestroyImmediate(arena);
            }
        }

        private static void VerifyCancellationGeneration()
        {
            var root = new GameObject("Summon Sequence Check");
            try
            {
                var sequence = root.AddComponent<SummonSequenceDirector>();
                var initial = sequence.ActiveGeneration;
                sequence.CancelAndReset();
                AssertTrue(sequence.ActiveGeneration > initial, "cancellation must invalidate captured generations");
                AssertTrue(!sequence.IsPlaying, "cancellation must clear the active sequence");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifyHudLocking()
        {
            var root = new GameObject("Summon HUD Check");
            try
            {
                var hud = root.AddComponent<ArBattleHud>();
                hud.SetInputLocked(true);
                AssertTrue(hud.InputLocked, "summoning must expose a locked HUD state");
                hud.SetInputLocked(false);
                AssertTrue(!hud.InputLocked, "completion must release the HUD state");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifyPetSummonPoseLifecycle()
        {
            var pet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            try
            {
                var actor = pet.AddComponent<ArBattleActor>();
                actor.Initialize("A", new BattlePlayerMessage { hp = 5, artId = 1, stage = "BABY" });
                var homePosition = pet.transform.localPosition;
                var homeScale = pet.transform.localScale;
                var renderer = pet.GetComponent<Renderer>();
                actor.PrepareSummonRise();
                AssertTrue(!renderer.enabled, "prepared pet must stay hidden before its rise");
                AssertTrue(pet.transform.localPosition.y < homePosition.y, "prepared pet must start below its home");
                AssertTrue(pet.transform.localScale.x < homeScale.x, "prepared pet must start at reduced scale");
                pet.transform.localPosition = homePosition + Vector3.down * 0.2f;
                pet.transform.localScale = homeScale * 0.5f;
                actor.ResetSummonPose();
                AssertEqual(homePosition, pet.transform.localPosition, "cancelled summon position");
                AssertEqual(homeScale, pet.transform.localScale, "cancelled summon scale");
                AssertTrue(renderer.enabled, "cancelled summon must restore renderer visibility");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(pet);
            }
        }

        private static void VerifyBattleUpdateQueueing()
        {
            var root = new GameObject("Summon Queue Check");
            var petA = new GameObject("Pet A");
            var petB = new GameObject("Pet B");
            try
            {
                var director = root.AddComponent<ArBattleDirector>();
                var actorA = petA.AddComponent<ArBattleActor>();
                var actorB = petB.AddComponent<ArBattleActor>();
                actorA.Initialize("A", new BattlePlayerMessage { hp = 5, artId = 1, stage = "BABY" });
                actorB.Initialize("B", new BattlePlayerMessage { hp = 5, artId = 2, stage = "BABY" });
                SetField(director, "actorA", actorA);
                SetField(director, "actorB", actorB);
                SetField(director, "summoning", true);
                var later = new BattleRoundMessage { round = 3 };
                var earlier = new BattleRoundMessage { round = 2 };
                var snapshot = new BattleSnapshotMessage { round = 3, hpA = 4, hpB = 3 };
                director.ApplyRound(later);
                director.ApplyRound(earlier);
                director.ApplySnapshot(snapshot);
                var rounds = GetField<List<BattleRoundMessage>>(director, "pendingRounds");
                AssertEqual(2, rounds.Count, "queued round count");
                AssertEqual(2, rounds[0].round, "first queued round");
                AssertEqual(3, rounds[1].round, "second queued round");
                AssertTrue(GetField<BattleSnapshotMessage>(director, "pendingSnapshot") == snapshot,
                    "latest authoritative snapshot must remain queued");
                AssertEqual(0, GetValue<int>(director, "lastPlayedRound"), "queued rounds must not advance playback state");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(petA);
                UnityEngine.Object.DestroyImmediate(petB);
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifyTrackingLossCancellation()
        {
            var root = new GameObject("Tracking Loss Check");
            try
            {
                var director = root.AddComponent<ArBattleDirector>();
                var sequence = root.AddComponent<SummonSequenceDirector>();
                var hud = root.AddComponent<ArBattleHud>();
                SetField(director, "summonDirector", sequence);
                SetField(director, "hud", hud);
                SetField(director, "summoning", true);
                director.SetTrackingLost(true);
                AssertTrue(!GetValue<bool>(director, "summoning"), "tracking loss must leave summon state");
                AssertTrue(hud.InputLocked, "tracking loss must keep input locked");
                AssertTrue(sequence.ActiveGeneration > 0, "tracking loss must invalidate the summon generation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void VerifySummonerPlacementUsesPetHome()
        {
            var arena = new GameObject("Summoner Placement Arena");
            var pet = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var sequenceObject = new GameObject("Summoner Placement Sequence");
            try
            {
                pet.transform.SetParent(arena.transform, false);
                pet.transform.localPosition = new Vector3(0.4f, 0.2f, -0.1f);
                var homePosition = pet.transform.localPosition;
                var actor = pet.AddComponent<ArBattleActor>();
                actor.Initialize("A", new BattlePlayerMessage { hp = 5, artId = 1, stage = "BABY" });
                var sequence = sequenceObject.AddComponent<SummonSequenceDirector>();
                sequence.Configure(arena.transform, null);
                var play = sequence.Play(AvatarProfile.CreateDefault(), actor);
                AssertTrue(play.MoveNext(), "summon setup must reach the medallion throw");
                var avatar = GetField<GameObject>(sequence, "avatar");
                AssertEqual(homePosition.y, avatar.transform.localPosition.y,
                    "summoner height must use pet home before the pet is prepared below the circle");
                (play as IDisposable)?.Dispose();
                sequence.CancelAndReset();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(sequenceObject);
                UnityEngine.Object.DestroyImmediate(arena);
            }
        }

        private static int NamedChildren(Transform root, string prefix)
        {
            var count = 0;
            for (var index = 0; index < root.childCount; index++)
                if (root.GetChild(index).name.StartsWith(prefix, StringComparison.Ordinal)) count++;
            return count;
        }

        private static T RequiredSceneComponent<T>() where T : Component
        {
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            throw new InvalidOperationException($"Scene component '{typeof(T).Name}' was not created.");
        }

        private static T GetField<T>(object target, string name) where T : class
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field?.GetValue(target) as T;
        }

        private static T GetValue<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Missing field '{name}'.");
            return (T)field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            if (field == null) throw new InvalidOperationException($"Missing field '{name}'.");
            field.SetValue(target, value);
        }

        private static void AssertTrue(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        private static void AssertEqual<T>(T expected, T actual, string field)
        {
            if (!Equals(expected, actual))
                throw new InvalidOperationException($"Unexpected {field}: expected '{expected}', got '{actual}'.");
        }
    }
}
#endif
