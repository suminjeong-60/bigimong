using System;
using UnityEngine;

namespace Bigimong.AR
{
    [Serializable]
    public sealed class AvatarProfile
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string displayName = "플레이어";
        public string bodyType = "MASCULINE";
        public int faceShapeId = 1;
        public int skinToneId = 1;
        public int eyebrowId = 1;
        public int eyeColorId = 1;
        public int hairStyleId = 1;
        public int hairColorId = 1;

        public bool IsComplete => !string.IsNullOrWhiteSpace(displayName);

        public static AvatarProfile CreateDefault() => new();

        public void Normalize()
        {
            schemaVersion = CurrentSchemaVersion;
            displayName = string.IsNullOrWhiteSpace(displayName) ? "플레이어" : displayName.Trim();
            bodyType = bodyType == "FEMININE" ? "FEMININE" : "MASCULINE";
            faceShapeId = Mathf.Clamp(faceShapeId, 1, 5);
            skinToneId = Mathf.Clamp(skinToneId, 1, 8);
            eyebrowId = Mathf.Clamp(eyebrowId, 1, 6);
            eyeColorId = Mathf.Clamp(eyeColorId, 1, 8);
            hairStyleId = Mathf.Clamp(hairStyleId, 1, 12);
            hairColorId = Mathf.Clamp(hairColorId, 1, 10);
        }
    }
}
