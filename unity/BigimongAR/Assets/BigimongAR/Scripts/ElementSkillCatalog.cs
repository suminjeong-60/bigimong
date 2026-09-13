using System;
using System.Collections.Generic;
using UnityEngine;

namespace Bigimong.AR
{
    public enum Element
    {
        Water,
        Fire,
        Earth,
        Wind
    }

    [Serializable]
    public sealed class ElementSkillProfile
    {
        public readonly int artId;
        public readonly Element element;
        public readonly string skillId;
        public readonly string displayName;
        public readonly string dodgeProfile;
        public readonly string idleProfile;
        public readonly string attackProfile;
        public readonly Color primaryColor;
        public readonly Color secondaryColor;
        public readonly float projectileSpeed;
        public readonly float impactScale;
        public readonly float cameraImpulse;

        public ElementSkillProfile(
            int artId,
            Element element,
            string skillId,
            string displayName,
            string dodgeProfile,
            string idleProfile,
            string attackProfile,
            Color primaryColor,
            Color secondaryColor,
            float projectileSpeed,
            float impactScale,
            float cameraImpulse)
        {
            this.artId = artId;
            this.element = element;
            this.skillId = skillId;
            this.displayName = displayName;
            this.dodgeProfile = dodgeProfile;
            this.idleProfile = idleProfile;
            this.attackProfile = attackProfile;
            this.primaryColor = primaryColor;
            this.secondaryColor = secondaryColor;
            this.projectileSpeed = projectileSpeed;
            this.impactScale = impactScale;
            this.cameraImpulse = cameraImpulse;
        }
    }

    public static class ElementSkillCatalog
    {
        private static readonly Color WaterPrimary = new Color(0.12f, 0.68f, 1f);
        private static readonly Color WaterSecondary = new Color(0.76f, 0.95f, 1f);
        private static readonly Color FirePrimary = new Color(1f, 0.24f, 0.08f);
        private static readonly Color FireSecondary = new Color(1f, 0.76f, 0.12f);
        private static readonly Color EarthPrimary = new Color(0.48f, 0.28f, 0.08f);
        private static readonly Color EarthSecondary = new Color(0.84f, 0.66f, 0.25f);
        private static readonly Color WindPrimary = new Color(0.28f, 0.92f, 0.72f);
        private static readonly Color WindSecondary = new Color(0.9f, 1f, 0.96f);

        private static readonly ElementSkillProfile[] Profiles =
        {
            Profile(1, Element.Fire, "tyrant_flame_roar", "Tyrant Flame Roar", "heavy_pivot", "grounded_predator_breath", "roar_breath", FirePrimary, FireSecondary, 7.2f, 1.35f, 0.22f),
            Profile(2, Element.Earth, "earth_charge", "Earth Charge", "horn_guard", "heavy_horn_shift", "horn_charge", EarthPrimary, EarthSecondary, 6.1f, 1.25f, 0.18f),
            Profile(3, Element.Wind, "sky_dive", "Sky Dive", "vertical_ascent", "winged_hover", "dive_strike", WindPrimary, WindSecondary, 9.4f, 1.1f, 0.16f),
            Profile(4, Element.Fire, "flame_spike_storm", "Flame Spike Storm", "tail_hop", "plated_tail_sway", "tail_spike_burst", FirePrimary, FireSecondary, 7.6f, 1.2f, 0.18f),
            Profile(5, Element.Earth, "giant_tremor", "Giant Tremor", "stomp_recoil", "long_neck_breathe", "tremor_stomp", EarthPrimary, EarthSecondary, 5.4f, 1.5f, 0.28f),
            Profile(6, Element.Water, "abyss_water_blade", "Abyss Water Blade", "low_body_slide", "sail_back_sway", "water_blade_lunge", WaterPrimary, WaterSecondary, 8.1f, 1.15f, 0.15f),
            Profile(7, Element.Earth, "armored_quake_hammer", "Armored Quake Hammer", "armored_curl", "armored_weight_shift", "tail_hammer", EarthPrimary, EarthSecondary, 5.8f, 1.45f, 0.25f),
            Profile(8, Element.Fire, "forest_flame_claws", "Forest-Flame Claws", "aerial_side_roll", "raptor_ready", "claw_pounce", FirePrimary, FireSecondary, 8.8f, 1.05f, 0.14f),
            Profile(9, Element.Fire, "resonant_flame_wave", "Resonant Flame Wave", "flame_trail_leap", "crest_call", "resonant_wave", FirePrimary, FireSecondary, 7.4f, 1.18f, 0.17f),
            Profile(10, Element.Fire, "red_horn_detonation", "Red Horn Detonation", "low_horn_slide", "horned_stalk", "horn_detonation", FirePrimary, FireSecondary, 8f, 1.3f, 0.2f),
            Profile(11, Element.Water, "tidal_jaw", "Tidal Jaw", "dive_reappear", "aquatic_circle", "tidal_breach", WaterPrimary, WaterSecondary, 8.6f, 1.35f, 0.22f),
            Profile(12, Element.Water, "frost_predator_combo", "Frost Predator Combo", "ice_slide_retreat", "predator_head_tilt", "frost_combo", WaterPrimary, WaterSecondary, 8.3f, 1.2f, 0.17f),
            Profile(13, Element.Wind, "sky_spear", "Sky Spear", "spiral_ascent", "high_wing_glide", "sky_spear_throw", WindPrimary, WindSecondary, 9.8f, 1.2f, 0.18f),
            Profile(14, Element.Wind, "cyclone_sickle_claw", "Cyclone Sickle Claw", "lateral_somersault", "raptor_wind_stalk", "cyclone_claw", WindPrimary, WindSecondary, 9.1f, 1.08f, 0.14f),
            Profile(15, Element.Earth, "earth_hammer", "Earth Hammer", "tail_pivot_half_turn", "club_tail_breathe", "earth_hammer_swing", EarthPrimary, EarthSecondary, 6.3f, 1.28f, 0.2f),
            Profile(16, Element.Water, "river_hunter", "River Hunter", "crouched_quick_step", "river_stalk", "river_lunge", WaterPrimary, WaterSecondary, 8.2f, 1.14f, 0.16f),
            Profile(17, Element.Earth, "guardian_stone_pulse", "Guardian Stone Pulse", "shell_shield_hide", "nest_guard", "stone_pulse", EarthPrimary, EarthSecondary, 6f, 1.22f, 0.18f),
            Profile(18, Element.Earth, "sand_shield_charge", "Sand Shield Charge", "sand_burrow", "small_horn_scan", "sand_charge", EarthPrimary, EarthSecondary, 6.7f, 1.18f, 0.17f),
            Profile(19, Element.Wind, "gale_kick", "Gale Kick", "afterimage_sprint", "runner_twitch", "gale_kick_combo", WindPrimary, WindSecondary, 10.2f, 1.05f, 0.13f),
            Profile(20, Element.Wind, "storm_crown", "Storm Crown", "vortex_ascent", "crown_head_bob", "storm_crown_burst", WindPrimary, WindSecondary, 8.9f, 1.22f, 0.18f),
            Profile(21, Element.Fire, "doom_flame_jaw", "Doom Flame Jaw", "flame_wing_side_roll", "giant_predator_breathe", "doom_jaw_bite", FirePrimary, FireSecondary, 8.5f, 1.45f, 0.26f),
            Profile(22, Element.Water, "mist_twin_crest_mirage", "Mist Twin-Crest Mirage", "mist_clone_swap", "crest_fan", "mist_mirage", WaterPrimary, WaterSecondary, 8.7f, 1.1f, 0.14f),
            Profile(23, Element.Earth, "stone_thumb_spear", "Stone Thumb Spear", "turning_deflection", "thumb_guard", "stone_spear", EarthPrimary, EarthSecondary, 7.1f, 1.17f, 0.16f),
            Profile(24, Element.Earth, "twin_earth_spikes", "Twin Earth Spikes", "tail_pole_vault", "spike_tail_sway", "twin_spike_launch", EarthPrimary, EarthSecondary, 7.3f, 1.24f, 0.19f),
            Profile(25, Element.Water, "moon_tide_claws", "Moon-Tide Claws", "wave_backbend", "long_claw_breathe", "moon_tide_slash", WaterPrimary, WaterSecondary, 8.4f, 1.18f, 0.17f),
            Profile(26, Element.Wind, "shadow_sprint", "Shadow Sprint", "zigzag_dash", "small_runner_blink", "shadow_sprint_dash", WindPrimary, WindSecondary, 10.5f, 1.02f, 0.12f),
            Profile(27, Element.Water, "resonant_pressure_wave", "Resonant Pressure Wave", "circular_water_slide", "crest_resonance", "pressure_wave", WaterPrimary, WaterSecondary, 7.8f, 1.25f, 0.19f),
            Profile(28, Element.Wind, "four_wing_storm", "Four-Wing Storm", "folded_wing_snap_roll", "four_wing_hover", "four_wing_gust", WindPrimary, WindSecondary, 9.6f, 1.12f, 0.15f),
            Profile(29, Element.Earth, "green_giant_step", "Green Giant Step", "low_body_impact_flow", "long_neck_weight_shift", "giant_step", EarthPrimary, EarthSecondary, 5.6f, 1.48f, 0.27f),
            Profile(30, Element.Fire, "crimson_titan_descent", "Crimson Titan Descent", "fire_column_ascent", "titan_breathe", "crimson_descent", FirePrimary, FireSecondary, 7f, 1.55f, 0.3f)
        };

        private static readonly IReadOnlyList<ElementSkillProfile> ReadOnlyProfiles = Array.AsReadOnly(Profiles);
        public static IReadOnlyList<ElementSkillProfile> All => ReadOnlyProfiles;

        public static ElementSkillProfile Resolve(int artId)
        {
            return Profiles[Mathf.Clamp(artId, 1, Profiles.Length) - 1];
        }

        private static ElementSkillProfile Profile(
            int artId,
            Element element,
            string skillId,
            string displayName,
            string dodgeProfile,
            string idleProfile,
            string attackProfile,
            Color primaryColor,
            Color secondaryColor,
            float projectileSpeed,
            float impactScale,
            float cameraImpulse)
        {
            return new ElementSkillProfile(
                artId, element, skillId, displayName, dodgeProfile, idleProfile, attackProfile,
                primaryColor, secondaryColor, projectileSpeed, impactScale, cameraImpulse);
        }
    }
}
