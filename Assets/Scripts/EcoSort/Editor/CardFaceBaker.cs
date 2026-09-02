using System.IO;
using System.Text;
using EcoSort.Data;
using EcoSort.Utils;
using UnityEditor;
using UnityEngine;

namespace EcoSort.EditorTools
{
    /// <summary>
    /// Gorseli olmayan kartlar icin, calisma zamaninda uretilen kart yuzunun
    /// AYNISINI bir PNG assetine yazar ve karta atar.
    ///
    /// Neden faydali:
    ///   - Yeni kategoriler (Taki, Oyun) icin sanat gelene kadar tutarli bir yuz olur,
    ///   - Cikan PNG'ler projede gercek dosya olarak durur; sanatci ayni dosyayi
    ///     kendi cizimiyle degistirir ve hicbir kod/ayar degismez,
    ///   - Zaten Artwork'u olan kartlara DOKUNMAZ (elle cizilmis gorseller korunur).
    /// </summary>
    public static class CardFaceBaker
    {
        const string FaceFolder = "Assets/EcoSort/Art/CardFaces";
        const int FaceSize = 256;

        [MenuItem("Eco-Sort/Eksik Kart Yuzlerini Uret (PNG)")]
        static void BakeMenu()
        {
            Debug.Log(Bake());
        }

        public static string Bake()
        {
            EnsureFolder("Assets/EcoSort", "Assets", "EcoSort");
            EnsureFolder("Assets/EcoSort/Art", "Assets/EcoSort", "Art");
            EnsureFolder(FaceFolder, "Assets/EcoSort/Art", "CardFaces");

            var log = new StringBuilder();
            var guids = AssetDatabase.FindAssets("t:" + nameof(CardData));

            // 1) PNG'leri yaz.
            var pending = new System.Collections.Generic.List<(CardData card, string path)>();

            foreach (var guid in guids)
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                if (card == null) continue;

                if (card.Artwork != null)
                {
                    log.AppendLine($"atlandi (zaten gorseli var): {card.DisplayName}");
                    continue;
                }

                var path = $"{FaceFolder}/Face_{card.CardId}.png";
                WriteFacePng(card, path);
                pending.Add((card, path));
            }

            if (pending.Count == 0)
            {
                log.AppendLine("Uretilecek kart yuzu yok: tum kartlarin gorseli mevcut.");
                return log.ToString();
            }

            AssetDatabase.Refresh();

            // 2) Import ayarlarini uygula ve kartlara ata.
            foreach (var (card, path) in pending)
            {
                ApplySpriteImportSettings(path);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (sprite == null)
                {
                    log.AppendLine($"HATA (sprite yuklenemedi): {path}");
                    continue;
                }

                var so = new SerializedObject(card);
                so.FindProperty("_artwork").objectReferenceValue = sprite;
                so.FindProperty("_tint").colorValue = Color.white;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(card);
                log.AppendLine($"{card.DisplayName} -> {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"Uretilen kart yuzu: {pending.Count}");
            return log.ToString();
        }

        static void WriteFacePng(CardData card, string path)
        {
            var texture = IconFactory.CreateTileTexture(card.IconShape, card.AccentColor, FaceSize);

            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        static void ApplySpriteImportSettings(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = FaceSize;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        static void EnsureFolder(string fullPath, string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
