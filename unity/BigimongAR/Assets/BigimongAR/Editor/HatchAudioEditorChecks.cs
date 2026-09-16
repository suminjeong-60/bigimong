#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Bigimong.AR.EditorChecks
{
    public static class HatchAudioEditorChecks
    {
        public static void RunBehaviorChecks()
        {
            RuntimeBindingGeneratesUsableCues();
            MuteChangesPresentationNotDurableTimeline();
        }

        public static void RunSceneChecks()
        {
            var view = UnityEngine.Object.FindObjectOfType<HatchHomeView>(true);
            Require(view != null, "shipped home exists");
            var binding = view.GetComponent<HatchAudioBinding>();
            var source = view.GetComponent<AudioSource>();
            Require(binding != null && binding.enabled && view.GetComponent<HatchSequenceDirector>() != null,
                "saved scene contains its runtime audio initializer on the director's object");
            Require(source != null && source.enabled && !source.playOnAwake && !source.loop &&
                source.spatialBlend == 0f && !source.ignoreListenerPause && !source.ignoreListenerVolume,
                "scene source is non-autoplay 2D and honors platform listener settings");
            Require(source.clip == null && !source.isPlaying, "scene generation does not start audio or serialize a transient cue");

            // Clone the actual serialized scene components, then exercise the runtime load callback.
            var copy = UnityEngine.Object.Instantiate(view.gameObject);
            AudioClip rock = null, crack = null, burst = null;
            try
            {
                var copiedBinding = copy.GetComponent<HatchAudioBinding>();
                Invoke(copiedBinding, "Awake");
                var director = copy.GetComponent<HatchSequenceDirector>();
                var copiedSource = copy.GetComponent<AudioSource>();
                rock = Field<AudioClip>(director, "rockSound"); crack = Field<AudioClip>(director, "crackSound"); burst = Field<AudioClip>(director, "burstSound");
                Require(Field<AudioSource>(director, "audioSource") == copiedSource && copiedSource != source,
                    "runtime scene binding targets its own source after serialization/clone");
                CheckClip(rock, 15435); CheckClip(crack, 2646); CheckClip(burst, 11025);
                Require(!EditorUtility.IsPersistent(rock) && !EditorUtility.IsPersistent(crack) && !EditorUtility.IsPersistent(burst),
                    "cues are created on load, not unsaved editor-only asset references");
                Require(!copiedSource.isPlaying && copiedSource.clip == null, "binding alone never plays a cue");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
            Require(rock == null && crack == null && burst == null, "unloading the runtime owner destroys all three generated clips");
        }

        private static void RuntimeBindingGeneratesUsableCues()
        {
            using var f = new Fixture(true);
            var rock = Field<AudioClip>(f.Sequence, "rockSound");
            var crack = Field<AudioClip>(f.Sequence, "crackSound");
            var burst = Field<AudioClip>(f.Sequence, "burstSound");
            CheckClip(rock, 15435); CheckClip(crack, 2646); CheckClip(burst, 11025);
            Require(rock != crack && crack != burst && rock != burst, "rock, crack and burst have distinct usable cues");
            var samples = new float[2646]; crack.GetData(samples, 0);
            using (var second = new Fixture(true))
            {
                var again = new float[2646]; Field<AudioClip>(second.Sequence, "crackSound").GetData(again, 0);
                for (var i = 0; i < samples.Length; i++) Require(samples[i] == again[i], "procedural cue is deterministic without consuming selection RNG");
            }
            f.Binding.EnsureBound();
            Require(rock == Field<AudioClip>(f.Sequence, "rockSound") && !f.Source.isPlaying,
                "repeat initialization reuses clips and never starts playback");
            f.Source.mute = true; f.Source.volume = .37f;
            f.Binding.EnsureBound();
            Require(f.Source.mute && f.Source.volume == .37f, "rebinding never resets caller/platform audio preferences");
            UnityEngine.Object.DestroyImmediate(f.Binding);
            Require(rock == null && crack == null && burst == null && Field<AudioSource>(f.Sequence, "audioSource") == null,
                "audio owner destruction releases generated clips and clears director bindings");
        }

        private static void MuteChangesPresentationNotDurableTimeline()
        {
            var listenerVolume = AudioListener.volume;
            var listenerPause = AudioListener.pause;
            try
            {
                AudioListener.volume = .63f; AudioListener.pause = false;
                foreach (var soundEnabled in new[] { false, true })
                {
                    using var f = new Fixture(soundEnabled);
                    f.Coordinator.BeginHatch();
                    Require(f.Source.isPlaying == soundEnabled, "saved mute gates real rock playback, not only a settings flag");
                    Require(f.Store.Durable.selectedArtId == 17 && f.Store.Writes.Count == 1 && f.Sequence.Elapsed == 0f,
                        "selection is committed before any audible rock and timing starts at zero");
                    f.Source.Stop(); f.Sequence.Advance(.7f);
                    Require(f.Source.isPlaying == soundEnabled && f.Sequence.CrackStage == 1, ".7 crack uses the same saved audio gate");
                    f.Source.Stop(); f.Sequence.Advance(.4f); f.Source.Stop(); f.Sequence.Advance(.4f);
                    f.Source.Stop(); f.Sequence.Advance(.3f);
                    Require(f.Source.isPlaying == soundEnabled && f.Sequence.Elapsed == 1.8f &&
                        f.Store.Durable.hatchCheckpoint == "SHELL_BURST" && f.Store.Writes.Count == 2,
                        "1.8 burst audio follows the accepted checkpoint without changing its boundary");
                    f.Sequence.Advance(2.4f);
                    Require(f.Sequence.Elapsed == 4.2f && f.Store.Durable.phase == "REVEAL" && f.Store.Writes.Count == 3,
                        "muted and unmuted reveal commit at literal 4.2");
                    f.Sequence.Advance(.79f);
                    Require(f.Store.Durable.phase == "REVEAL", "4.99 is not HOME with either sound preference");
                    f.Sequence.Advance(.01f);
                    Require(f.Sequence.Elapsed == 5f && f.Store.Durable.phase == "HOME" && f.Store.Writes.Count == 4 &&
                        f.Store.Durable.selectedArtId == 17 && f.Random.Draws == 1 && f.Store.Durable.hatchedAtUtcTicks == 638712864000000000L,
                        "both audio presentations complete the same identity, four writes and timestamp at 5.0");
                    Require(string.Join(",", f.Store.Writes) == "HATCHING:STARTED,HATCHING:SHELL_BURST,REVEAL:REVEALED,HOME:REVEALED",
                        "mute never adds, removes or reorders a durable commit");
                }

                using var live = new Fixture(true);
                live.Coordinator.BeginHatch();
                live.Sequence.SoundEnabled = false;
                Require(!live.Source.isPlaying && live.Sequence.Elapsed == 0f && live.Store.Writes.Count == 1,
                    "muting a playing cue stops audio immediately without advancing time/state");
                live.Sequence.Advance(.7f);
                Require(!live.Source.isPlaying && live.Sequence.CrackStage == 1, "muted crack stays inaudible");
                live.Sequence.SoundEnabled = true;
                live.Sequence.Advance(.4f);
                Require(live.Source.isPlaying && live.Sequence.Elapsed == 1.1f && live.Store.Writes.Count == 1,
                    "unmute changes the next cue, not the timeline or checkpoint count");
                live.Source.Stop(); live.Sequence.Settings.Audio = false;
                live.Sequence.Advance(.4f);
                Require(!live.Source.isPlaying && live.Sequence.Elapsed == 1.5f, "explicit presentation mute also gates actual playback");
                live.Sequence.Advance(3.5f);
                Require(live.Store.Durable.phase == "HOME" && live.Store.Writes.Count == 4 && live.Sequence.Elapsed == 5f,
                    "mid-sequence mute/unmute still commits the identical complete timeline");
                Require(AudioListener.volume == .63f && !AudioListener.pause, "audio binding and playback never rewrite platform listener preferences");
            }
            finally { AudioListener.volume = listenerVolume; AudioListener.pause = listenerPause; }
        }

        private static void CheckClip(AudioClip clip, int samples)
        {
            Require(clip != null && clip.channels == 1 && clip.frequency == 22050 && clip.samples == samples, "mono cue has literal bounded sample count");
            var data = new float[samples];
            Require(clip.GetData(data, 0), "generated cue has readable PCM data");
            var peak = 0f;
            foreach (var sample in data)
            { Require(!float.IsNaN(sample) && !float.IsInfinity(sample), "finite PCM"); peak = Mathf.Max(peak, Mathf.Abs(sample)); }
            Require(peak > .01f && peak <= .5f, "cue is audible and safely bounded, not a silent placeholder");
            Require(Mathf.Abs(data[0]) < .001f && Mathf.Abs(data[data.Length - 1]) < .001f, "cue envelope fades both ends");
        }

        private static T Field<T>(object value, string name) where T : UnityEngine.Object =>
            (T)value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);
        private static void Invoke(object value, string name) => value.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(value, null);
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("Hatch audio: " + message); }

        private sealed class Fixture : IDisposable
        {
            public readonly GameObject Host = new("Hatch audio fixture");
            public readonly AudioSource Source;
            public readonly HatchAudioBinding Binding;
            public readonly HatchSequenceDirector Sequence;
            public readonly HatchHomeCoordinator Coordinator;
            public readonly MemoryStore Store;
            public readonly CountingRandom Random = new();
            public Fixture(bool sound)
            {
                Source = Host.AddComponent<AudioSource>(); Source.playOnAwake = false; Source.spatialBlend = 0f;
                Sequence = Host.AddComponent<HatchSequenceDirector>();
                Binding = Host.AddComponent<HatchAudioBinding>(); Invoke(Binding, "Awake");
                Coordinator = Host.AddComponent<HatchHomeCoordinator>();
                Store = new MemoryStore { Durable = new HatchHomeSnapshot { phase = "HATCH_READY", eggProgress = 30000, soundEnabled = sound } };
                var clock = new FixedClock();
                Coordinator.Configure(Store, new HatchProgressService(Store, clock), new HatchSelectionService(Store, Random), null, Sequence, null, clock);
                Coordinator.Initialize();
            }
            public void Dispose() { if (Host != null) { Invoke(Coordinator, "OnDisable"); UnityEngine.Object.DestroyImmediate(Host); } }
        }
        private sealed class FixedClock : ICareClock { public DateTime UtcNow => new(638712864000000000L, DateTimeKind.Utc); }
        private sealed class CountingRandom : IArtIdRandomSource { public int Draws; public int NextArtId() { Draws++; return 17; } }
        private sealed class MemoryStore : IHatchHomeStore
        {
            public HatchHomeSnapshot Durable;
            public readonly List<string> Writes = new();
            public HatchLoadResult Load() => new(Durable.Clone(), false, "memory");
            public bool TrySave(HatchHomeSnapshot state)
            { Durable = state.Clone(); Writes.Add(state.phase + ":" + state.hatchCheckpoint); return true; }
        }
    }
}
#endif
