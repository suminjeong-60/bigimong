using System;
using UnityEngine;

namespace Bigimong.AR
{
    /// <summary>Scene-owned procedural cues, rebuilt on runtime load instead of saving transient editor clips.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource), typeof(HatchSequenceDirector))]
    public sealed class HatchAudioBinding : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private AudioClip rock, crack, burst;
        private AudioSource source;
        private HatchSequenceDirector director;

        private void Awake() => EnsureBound();

        public void EnsureBound()
        {
            source = GetComponent<AudioSource>();
            director = GetComponent<HatchSequenceDirector>();
            try
            {
                rock ??= CreateCue("Hatch Rock", 15435, 0);
                crack ??= CreateCue("Hatch Crack", 2646, 1);
                burst ??= CreateCue("Hatch Burst", 11025, 2);
                director.ConfigureAudio(source, rock, crack, burst);
            }
            catch (Exception error)
            {
                ReleaseClips();
                // Optional audio device/allocation failure cannot change durable hatch timing.
                Debug.LogWarning("Hatch audio unavailable: " + error.Message, this);
            }
        }

        private static AudioClip CreateCue(string name, int count, int kind)
        {
            var data = new float[count];
            uint noiseState = 173;
            var filteredNoise = 0f;
            for (var i = 0; i < count; i++)
            {
                var t = i / (float)SampleRate;
                var u = i / (float)(count - 1);
                // Local deterministic noise must not consume Unity's hatch-selection random source.
                noiseState = unchecked(noiseState * 1664525u + 1013904223u);
                var noise = (noiseState >> 8) / 8388607.5f - 1f;
                filteredNoise = Mathf.Lerp(filteredNoise, noise, .18f);
                var envelope = Mathf.Sin(Mathf.PI * u);
                if (kind == 0)
                    data[i] = .22f * envelope * Mathf.Sin(2f * Mathf.PI * (110f * t + 120f * t * t));
                else if (kind == 1)
                    data[i] = .4f * envelope * Mathf.Exp(-5f * u) * (.7f * noise + .3f * Mathf.Sin(2f * Mathf.PI * 780f * t));
                else
                    data[i] = .18f * envelope * (Mathf.Sin(2f * Mathf.PI * 220f * t) +
                        .5f * Mathf.Sin(2f * Mathf.PI * 330f * t) + .5f * filteredNoise);
            }
            var clip = AudioClip.Create(name, count, 1, SampleRate, false);
            try
            {
                if (!clip.SetData(data, 0)) throw new InvalidOperationException("Unable to populate hatch cue PCM.");
                return clip;
            }
            catch { Release(clip); throw; }
        }

        private void OnDestroy() => ReleaseClips();

        private void ReleaseClips()
        {
            if (source != null) source.Stop();
            if (director != null) director.ConfigureAudio(null, null, null, null);
            Release(rock); Release(crack); Release(burst);
            rock = crack = burst = null;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
        }
    }
}
