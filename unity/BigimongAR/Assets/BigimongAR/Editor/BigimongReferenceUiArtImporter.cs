#if UNITY_EDITOR
using System;
using UnityEditor;

namespace Bigimong.AR.EditorChecks
{
    /// <summary>Keep the exact source-image dimensions for portrait UI artwork.</summary>
    public sealed class BigimongReferenceUiArtImporter : AssetPostprocessor
    {
        private const string ReferenceFolder = "Assets/BigimongAR/Resources/ReferenceUi/";

        private void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ReferenceFolder, StringComparison.Ordinal)) return;
            var importer = (TextureImporter)assetImporter;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.mipmapEnabled = false;
        }
    }
}
#endif
