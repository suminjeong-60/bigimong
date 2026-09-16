using TMPro;
using UnityEngine;

namespace Bigimong.AR
{
    public enum BigimongTextRole { TITLE, ACTION, BODY, COUNTER }

    [CreateAssetMenu(menuName = "Bigimong/Typography Theme")]
    public sealed class BigimongTypographyTheme : ScriptableObject
    {
        public static readonly Color Cocoa = new(.22f, .12f, .08f, 1f);
        public static readonly Color Cream = new(1f, .94f, .78f, 1f);
        public static readonly Color Gold = new(1f, .70f, .13f, 1f);
        [SerializeField] private TMP_FontAsset bold;
        [SerializeField] private TMP_FontAsset black;
        [SerializeField] private Material boldPreset;
        [SerializeField] private Material blackPreset;
        public static BigimongTypographyTheme Shared => Resources.Load<BigimongTypographyTheme>("BigimongTypeStyle");

        public void Configure(TMP_FontAsset body, TMP_FontAsset heading, Material bodyMaterial, Material headingMaterial)
        { bold = body; black = heading; boldPreset = bodyMaterial; blackPreset = headingMaterial; }

        public void Apply(TMP_Text text, BigimongTextRole role)
        {
            var heavy = role is BigimongTextRole.TITLE or BigimongTextRole.ACTION;
            text.font = heavy ? black : bold;
            // fontMaterial/outlineWidth setters instantiate materials: always use the shared preset.
            text.fontSharedMaterial = heavy ? blackPreset : boldPreset;
            text.color = role == BigimongTextRole.BODY ? Cocoa : Cream;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
        }
    }
}
