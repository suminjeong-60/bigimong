using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    /// <summary>Original runtime geometry. A slot includes its mesh, line and particle emitter.</summary>
    public sealed class ProceduralCombatVfx : MonoBehaviour
    {
        public const int PoolCapacity = 32;
        private sealed class Effect
        {
            public GameObject root;
            public Renderer mesh;
            public LineRenderer line;
            public ParticleSystem particles;
            public TrailRenderer trail;
            public Material material;
            public Vector3 start, end, scale;
            public float age, duration, arc;
            public bool active, ring;
            public Color color;
        }
        private readonly List<Effect> pool = new List<Effect>();
        public int AllocatedCount => pool.Count;
        public int ActiveCount { get { var count = 0; foreach (var item in pool) if (item.active) count++; return count; } }

        public static float TravelTime(ElementSkillProfile profile, Vector3 from, Vector3 to)
            => Mathf.Clamp(Vector3.Distance(from, to) / profile.projectileSpeed, .12f, .7f);

        public void PlaySkill(ElementSkillProfile profile, Transform attacker, Transform defender, string direction)
        {
            if (!isActiveAndEnabled || attacker == null || defender == null) return;
            var from = attacker.position + Vector3.up * .25f;
            var to = defender.position + Vector3.up * .25f;
            var bend = direction == "LEFT" ? -.15f : direction == "RIGHT" ? .15f : 0;
            from += attacker.right * bend;
            switch (profile.element)
            {
                case Element.Fire: PlayFire(profile, from, to); break;
                case Element.Water: PlayWater(profile, from, to); break;
                case Element.Earth: PlayEarth(profile, from, to); break;
                case Element.Wind: PlayWind(profile, from, to); break;
            }
        }

        private void PlayFire(ElementSkillProfile p, Vector3 from, Vector3 to)
        {
            Emit(p, from, to, new Vector3(.12f, .12f, .12f), false, true, .08f);
            Emit(p, from, to, new Vector3(.07f, .07f, .2f), false, true, .2f);
        }
        private void PlayWater(ElementSkillProfile p, Vector3 from, Vector3 to)
        {
            Emit(p, from, to, new Vector3(.08f, .04f, .25f), false, true, .06f);
            Emit(p, from, to, new Vector3(.2f, .025f, .2f), true, false, 0);
        }
        private void PlayEarth(ElementSkillProfile p, Vector3 from, Vector3 to)
        {
            Emit(p, from, to, new Vector3(.13f, .09f, .1f), false, true, .28f);
            Emit(p, from - Vector3.up * .2f, to - Vector3.up * .2f, new Vector3(.25f, .015f, .25f), true, false, 0);
        }
        private void PlayWind(ElementSkillProfile p, Vector3 from, Vector3 to)
        {
            Emit(p, from, to, new Vector3(.23f, .2f, .23f), true, false, .15f);
            Emit(p, from, to, new Vector3(.035f, .01f, .2f), false, true, .1f);
        }

        public void PlayImpact(ElementSkillProfile profile, Vector3 position, bool critical = false)
        {
            if (!isActiveAndEnabled) return;
            var size = profile.impactScale * (critical ? 1.4f : 1f);
            Emit(profile, position, position, Vector3.one * .25f * size, true, false, 0, .45f);
            for (var i = 0; i < 5; i++)
            {
                var angle = i * Mathf.PI * 2 / 5 + profile.artId;
                var end = position + new Vector3(Mathf.Cos(angle), .25f, Mathf.Sin(angle)) * .3f * size;
                var shard = profile.element == Element.Water || profile.element == Element.Wind;
                Emit(profile, position, end, shard ? new Vector3(.025f, .1f, .025f) : Vector3.one * .07f, false, true, .1f, .45f);
            }
        }

        private void Emit(ElementSkillProfile p, Vector3 from, Vector3 to, Vector3 scale, bool ring, bool particles, float arc, float duration = 0)
        {
            var item = Acquire();
            if (item == null) return; // Saturation drops decoration, never allocates beyond the cap.
            item.start = from; item.end = to; item.scale = scale; item.ring = ring;
            item.duration = duration > 0 ? duration : TravelTime(p, from, to);
            item.arc = arc; item.age = 0; item.color = p.primaryColor;
            item.root.transform.position = from;
            item.root.transform.localScale = scale;
            item.mesh.enabled = !ring;
            item.line.enabled = ring;
            item.trail.Clear();
            item.trail.emitting = !ring && (p.element == Element.Water || p.element == Element.Wind);
            item.trail.startColor = p.primaryColor;
            item.trail.endColor = new Color(p.secondaryColor.r, p.secondaryColor.g, p.secondaryColor.b, 0);
            item.material.color = p.primaryColor;
            item.line.startColor = item.line.endColor = new Color(p.secondaryColor.r, p.secondaryColor.g, p.secondaryColor.b, .65f);
            if (ring)
            {
                for (var i = 0; i < 32; i++)
                {
                    var angle = i * Mathf.PI * 2 / 31;
                    var spiral = p.element == Element.Wind;
                    var radius = spiral ? .5f + i / 62f : 1;
                    // Earth ring has irregular radial spokes, giving a cracked-ground silhouette.
                    if (p.element == Element.Earth && i % 3 == 0) radius *= .65f;
                    item.line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, spiral ? i / 31f - .5f : 0, Mathf.Sin(angle) * radius));
                }
            }
            if (particles)
            {
                var main = item.particles.main;
                main.startColor = p.secondaryColor;
                item.particles.Emit(8);
            }
        }

        private Effect Acquire()
        {
            Effect item = null;
            foreach (var candidate in pool) if (!candidate.active) { item = candidate; break; }
            if (item == null)
            {
                if (pool.Count >= PoolCapacity) return null;
                var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                root.name = "CombatEffect";
                root.transform.SetParent(transform, false);
                var collider = root.GetComponent<Collider>();
                collider.enabled = false;
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
                var shader = Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit");
                var material = new Material(shader);
                var renderer = root.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                var line = root.AddComponent<LineRenderer>();
                line.sharedMaterial = material; line.useWorldSpace = false;
                line.positionCount = 32; line.widthMultiplier = .055f;
                var trail = root.AddComponent<TrailRenderer>();
                trail.sharedMaterial = material; trail.time = .18f; trail.startWidth = .07f; trail.endWidth = 0;
                trail.minVertexDistance = .03f; trail.emitting = false;
                var particles = root.AddComponent<ParticleSystem>();
                particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = particles.main;
                main.playOnAwake = false; main.loop = false; main.useUnscaledTime = true;
                main.maxParticles = 16; main.startLifetime = .25f; main.startSpeed = .3f; main.startSize = .035f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                var emission = particles.emission; emission.enabled = false;
                particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                item = new Effect { root = root, mesh = renderer, line = line, particles = particles, trail = trail, material = material };
                pool.Add(item);
            }
            item.active = true; item.root.SetActive(true);
            return item;
        }

        private void Update() => Advance(Time.unscaledDeltaTime);

        public void Advance(float seconds)
        {
            foreach (var item in pool)
            {
                if (!item.active) continue;
                item.age += Mathf.Max(0, seconds);
                if (item.age >= item.duration) { Release(item); continue; }
                var t = item.age / item.duration;
                item.root.transform.position = Vector3.Lerp(item.start, item.end, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * item.arc;
                item.root.transform.localScale = item.scale * (item.ring ? 1 + t * 2 : 1 + t * .5f);
                item.material.color = new Color(item.color.r, item.color.g, item.color.b, 1 - t);
            }
        }
        private static void Release(Effect item)
        {
            item.particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            item.trail.emitting = false; item.trail.Clear();
            item.active = false; item.root.SetActive(false);
        }
        public void ReleaseAll() { foreach (var item in pool) Release(item); }
        private void OnDisable() => ReleaseAll();
        private void OnDestroy()
        {
            foreach (var item in pool)
                if (Application.isPlaying) Destroy(item.material); else DestroyImmediate(item.material);
            pool.Clear();
        }
    }
}
