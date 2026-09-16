#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Bigimong.AR;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Bigimong.Editor
{
    public static class BigimongFontAssetBuilder
    {
        private const string Root = "Assets/BigimongAR/Fonts/";
        public static void Prepare()
        {
            PrepareEssentialResources();
            Directory.CreateDirectory(Root + "Generated");
            Directory.CreateDirectory("Assets/BigimongAR/Resources");
            AssetDatabase.Refresh();
            var glyphs = ShippedGlyphs();
            var bold = PrepareWeight("Bold", glyphs);
            var black = PrepareWeight("Black", glyphs);
            var settings = new SerializedObject(TMP_Settings.instance);
            settings.FindProperty("m_defaultFontAsset").objectReferenceValue = bold;
            settings.FindProperty("m_fallbackFontAssets").arraySize = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            const string path = "Assets/BigimongAR/Resources/BigimongTypeStyle.asset";
            var theme = AssetDatabase.LoadAssetAtPath<BigimongTypographyTheme>(path);
            if (theme == null)
            {
                theme = ScriptableObject.CreateInstance<BigimongTypographyTheme>();
                theme.name = "BigimongTypeStyle";
                AssetDatabase.CreateAsset(theme, path);
            }
            theme.Configure(bold, black, PreparePreset(bold, "Bold", .12f), PreparePreset(black, "Black", .22f));
            EditorUtility.SetDirty(theme);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        }

        private static void PrepareEssentialResources()
        {
            if (Resources.Load<TMP_Settings>("TMP Settings") != null && Shader.Find("TextMeshPro/Distance Field") != null) return;
            // Use the installed Unity/uGUI package only; no network, guessed package version, or modal import dialog.
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(TMP_Text).Assembly);
            var archive = package == null ? null : Directory.GetFiles(package.resolvedPath,
                "TMP Essential Resources.unitypackage", SearchOption.AllDirectories).FirstOrDefault();
            if (archive == null) throw new InvalidOperationException("Installed Unity package lacks TMP Essential Resources.");
            AssetDatabase.ImportPackage(archive, false);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            if (Resources.Load<TMP_Settings>("TMP Settings") == null || Shader.Find("TextMeshPro/Distance Field") == null)
                throw new InvalidOperationException("TMP Essential Resources did not import successfully.");
        }

        public static string ShippedGlyphs()
        {
            // Restricted union of shipped string literals, not the unrestricted CJK range.
            var characters = new HashSet<char>("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz /,:.+-()|%!?◀▶·");
            foreach (var path in Directory.GetFiles("Assets/BigimongAR", "*.cs", SearchOption.AllDirectories)
                .Where(p => !p.Replace('\\', '/').Contains("/Editor/") || p.EndsWith("BigimongArSceneBuilder.cs", StringComparison.Ordinal)))
                foreach (Match literal in Regex.Matches(File.ReadAllText(path), "\"(?:\\\\.|[^\"\\\\])*\""))
                    foreach (var character in literal.Value)
                        if (!char.IsControl(character)) characters.Add(character);
            for (var id = 1; id <= 30; id++)
                foreach (var character in BigimongSpeciesCatalog.Resolve(id).KoreanName) characters.Add(character);
            return new string(characters.OrderBy(c => c).ToArray());
        }

        private static TMP_FontAsset PrepareWeight(string weight, string glyphs)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(Root + "NotoSansKR-" + weight + ".ttf");
            if (source == null) throw new InvalidOperationException("Missing licensed Noto Sans KR " + weight);
            var path = Root + "Generated/BigimongKR-" + weight + ".asset";
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null)
            {
                font = TMP_FontAsset.CreateFontAsset(source, 64, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, false);
                font.name = "BigimongKR-" + weight;
                AssetDatabase.CreateAsset(font, path);
                AssetDatabase.AddObjectToAsset(font.material, font);
                foreach (var atlas in font.atlasTextures) AssetDatabase.AddObjectToAsset(atlas, font);
            }
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;
            if (!font.TryAddCharacters(glyphs, out string missing))
                throw new InvalidOperationException("Restricted Korean atlas missing glyphs: " + missing);
            font.atlasPopulationMode = AtlasPopulationMode.Static;
            font.isMultiAtlasTexturesEnabled = false;
            EditorUtility.SetDirty(font);
            EditorUtility.SetDirty(font.material);
            foreach (var atlas in font.atlasTextures) EditorUtility.SetDirty(atlas);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        }

        private static Material PreparePreset(TMP_FontAsset font, string weight, float outline)
        {
            var path = Root + "Generated/BigimongKR-" + weight + "-Cocoa.mat";
            var preset = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (preset == null)
            {
                preset = new Material(font.material) { name = "BigimongKR-" + weight + "-Cocoa" };
                AssetDatabase.CreateAsset(preset, path);
            }
            preset.SetTexture(ShaderUtilities.ID_MainTex, font.atlasTexture);
            preset.SetColor(ShaderUtilities.ID_OutlineColor, BigimongTypographyTheme.Cocoa);
            preset.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
            preset.EnableKeyword("OUTLINE_ON");
            preset.EnableKeyword("UNDERLAY_ON");
            preset.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(.22f, .12f, .08f, .5f));
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, .3f);
            preset.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -.4f);
            preset.SetFloat(ShaderUtilities.ID_UnderlaySoftness, .25f);
            EditorUtility.SetDirty(preset);
            return preset;
        }
    }
}
#endif
