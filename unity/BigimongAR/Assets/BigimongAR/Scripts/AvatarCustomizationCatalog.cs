using UnityEngine;

namespace Bigimong.AR
{
    public static class AvatarCustomizationCatalog
    {
        public static readonly Color[] SkinTones = new Color[8]
        {
            new Color(1.00f, 0.84f, 0.72f),
            new Color(0.96f, 0.75f, 0.61f),
            new Color(0.88f, 0.64f, 0.48f),
            new Color(0.76f, 0.51f, 0.36f),
            new Color(0.64f, 0.40f, 0.27f),
            new Color(0.51f, 0.30f, 0.20f),
            new Color(0.39f, 0.22f, 0.15f),
            new Color(0.28f, 0.15f, 0.11f),
        };

        public static readonly Color[] EyeColors = new Color[8]
        {
            new Color(0.20f, 0.11f, 0.06f),
            new Color(0.37f, 0.22f, 0.10f),
            new Color(0.52f, 0.34f, 0.14f),
            new Color(0.16f, 0.34f, 0.19f),
            new Color(0.12f, 0.31f, 0.43f),
            new Color(0.23f, 0.42f, 0.58f),
            new Color(0.36f, 0.28f, 0.50f),
            new Color(0.36f, 0.38f, 0.40f),
        };

        public static readonly Color[] HairColors = new Color[10]
        {
            new Color(0.07f, 0.05f, 0.04f),
            new Color(0.16f, 0.09f, 0.06f),
            new Color(0.28f, 0.14f, 0.08f),
            new Color(0.43f, 0.25f, 0.14f),
            new Color(0.67f, 0.46f, 0.24f),
            new Color(0.84f, 0.68f, 0.38f),
            new Color(0.64f, 0.18f, 0.12f),
            new Color(0.16f, 0.19f, 0.25f),
            new Color(0.32f, 0.18f, 0.38f),
            new Color(0.78f, 0.73f, 0.67f),
        };

        public static Color ColorForSkinTone(int skinToneId) => ColorFor(SkinTones, skinToneId);
        public static Color ColorForEye(int eyeColorId) => ColorFor(EyeColors, eyeColorId);
        public static Color ColorForHair(int hairColorId) => ColorFor(HairColors, hairColorId);

        private static Color ColorFor(Color[] colors, int oneBasedId)
        {
            return colors[Mathf.Clamp(oneBasedId, 1, colors.Length) - 1];
        }
    }
}
