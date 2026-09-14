using UnityEngine;

namespace Bigimong.AR
{
    public enum BigimongBodyPlan
    {
        Predator,
        Ceratopsian,
        Pterosaur,
        Plated,
        LongNeck,
        Spinosaur,
        Armored,
        Runner,
        Hadrosaur,
        Aquatic,
        Feathered,
    }

    public readonly struct BigimongAppearance
    {
        public readonly int artId;
        public readonly BigimongBodyPlan bodyPlan;
        public readonly Color bodyColor;
        public readonly Color accentColor;

        public BigimongAppearance(int artId, BigimongBodyPlan bodyPlan, Color bodyColor, Color accentColor)
        {
            this.artId = artId;
            this.bodyPlan = bodyPlan;
            this.bodyColor = bodyColor;
            this.accentColor = accentColor;
        }
    }

    public static class BigimongAppearanceCatalog
    {
        private static readonly BigimongAppearance[] Entries =
        {
            A(1, BigimongBodyPlan.Predator, 0xE94F45, 0xFFB35A),
            A(2, BigimongBodyPlan.Ceratopsian, 0x4DA9E8, 0xD8F2FF),
            A(3, BigimongBodyPlan.Pterosaur, 0x8D62C7, 0xF18AC5),
            A(4, BigimongBodyPlan.Plated, 0xF18A3D, 0xB94B37),
            A(5, BigimongBodyPlan.LongNeck, 0x65B95B, 0xC9E76D),
            A(6, BigimongBodyPlan.Spinosaur, 0x388DD0, 0xEF7896),
            A(7, BigimongBodyPlan.Armored, 0x9B7048, 0x79B3CE),
            A(8, BigimongBodyPlan.Runner, 0x3C9B83, 0xF16A43),
            A(9, BigimongBodyPlan.Hadrosaur, 0x65B96D, 0xE55371),
            A(10, BigimongBodyPlan.Predator, 0xD94A3B, 0x7A2736),
            A(11, BigimongBodyPlan.Aquatic, 0x3B91D5, 0xA6E7F4),
            A(12, BigimongBodyPlan.Predator, 0x3E8CBF, 0x233B62),
            A(13, BigimongBodyPlan.Pterosaur, 0x4D8EC8, 0xB895E3),
            A(14, BigimongBodyPlan.Feathered, 0x7D67B4, 0xE85F9D),
            A(15, BigimongBodyPlan.Armored, 0x8B6C4D, 0xC9A45F),
            A(16, BigimongBodyPlan.Predator, 0x56A95B, 0xB7DC84),
            A(17, BigimongBodyPlan.Feathered, 0x4D9AB5, 0xE85D9B),
            A(18, BigimongBodyPlan.Ceratopsian, 0xB9976A, 0xEEE1BD),
            A(19, BigimongBodyPlan.Feathered, 0xD78B3E, 0x3F86B9),
            A(20, BigimongBodyPlan.Predator, 0x7353A6, 0x55C5D1),
            A(21, BigimongBodyPlan.Predator, 0xA93432, 0xF16D42),
            A(22, BigimongBodyPlan.Hadrosaur, 0x59B76D, 0xE1A943),
            A(23, BigimongBodyPlan.Hadrosaur, 0x77C99B, 0xD8EAA2),
            A(24, BigimongBodyPlan.Plated, 0xB87445, 0xE7C15D),
            A(25, BigimongBodyPlan.Feathered, 0x806DB5, 0xC6A9E8),
            A(26, BigimongBodyPlan.Runner, 0x3F7F83, 0xC44D58),
            A(27, BigimongBodyPlan.Hadrosaur, 0x50AC72, 0xD45A9A),
            A(28, BigimongBodyPlan.Pterosaur, 0x438DC2, 0xA66AC7),
            A(29, BigimongBodyPlan.LongNeck, 0x67B65B, 0xD5E989),
            A(30, BigimongBodyPlan.LongNeck, 0xD85454, 0xF0A867),
        };

        public static BigimongAppearance Resolve(int artId) => Entries[Mathf.Clamp(artId, 1, 30) - 1];

        private static BigimongAppearance A(int artId, BigimongBodyPlan plan, int body, int accent) =>
            new BigimongAppearance(artId, plan, Hex(body), Hex(accent));

        private static Color Hex(int value) => new Color(
            ((value >> 16) & 0xff) / 255f,
            ((value >> 8) & 0xff) / 255f,
            (value & 0xff) / 255f,
            1f);
    }
}
