using System.Collections.Generic;
using System.IO;
using System.Text;
using EcoSort.Data;
using EcoSort.Utils;
using UnityEditor;
using UnityEngine;

namespace EcoSort.EditorTools
{
    /// <summary>
    /// IconFactory siluetlerini PNG asseti olarak yazar ve ilgili CardData'nin
    /// Artwork alanina atar.
    ///
    /// Boylece ikonlar projede gercek asset olarak durur: sanatci bir ikonu
    /// begenmezse ayni dosyayi kendi cizimiyle degistirir, hicbir kod degismez.
    /// </summary>
    public static class EcoIconGenerator
    {
        const string RootFolder = "Assets/EcoSort";
        const string ArtFolder = "Assets/EcoSort/Art";
        const string IconFolder = "Assets/EcoSort/Art/Icons";
        const int IconSize = 256;

        /// <summary>Kart kimligi -> ikon silueti.</summary>
        static readonly Dictionary<string, EcoIcon> IconByCardId = new Dictionary<string, EcoIcon>
        {
            { "kahve_kahvecekirdegi", EcoIcon.Bean },
            { "kahve_sicaksu", EcoIcon.Droplet },
            { "kahve_fincan", EcoIcon.Cup },

            { "sonbahar_kuruyaprak", EcoIcon.Leaf },
            { "sonbahar_balkabagi", EcoIcon.Pumpkin },
            { "sonbahar_yunatki", EcoIcon.Scarf },

            { "deniz_dalga", EcoIcon.Wave },
            { "deniz_denizkabugu", EcoIcon.Shell },
            { "deniz_marti", EcoIcon.Bird },
            { "deniz_plajsemsiyesi", EcoIcon.Umbrella },

            { "taki_kolye", EcoIcon.Necklace },
            { "taki_bileklik", EcoIcon.Bracelet },
            { "taki_yuzuk", EcoIcon.Ring },

            { "oyun_oyunkolu", EcoIcon.Gamepad },
            { "oyun_kulaklik", EcoIcon.Headphones },
            { "oyun_klavye", EcoIcon.Keyboard }
        };

        [MenuItem("Eco-Sort/Ikonlari Uret ve Kartlara Ata")]
        static void GenerateMenu()
        {
            Debug.Log(Generate());
        }

        public static string Generate()
        {
            EnsureFolder(RootFolder, "Assets", "EcoSort");
            EnsureFolder(ArtFolder, RootFolder, "Art");
            EnsureFolder(IconFolder, ArtFolder, "Icons");

            var log = new StringBuilder();
            int assigned = 0;
            int skipped = 0;

            var guids = AssetDatabase.FindAssets("t:" + nameof(CardData));

            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in guids)
                {
                    var cardPath = AssetDatabase.GUIDToAssetPath(guid);
                    var card = AssetDatabase.LoadAssetAtPath<CardData>(cardPath);
                    if (card == null) continue;

                    if (!IconByCardId.TryGetValue(card.CardId, out var icon))
                    {
                        log.AppendLine($"ATLANDI (eslesme yok): {card.CardId}");
                        skipped++;
                        continue;
                    }

                    WriteIconPng(icon);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            // Import ayarlari ve atama, toplu duzenleme bittikten sonra yapilir.
            foreach (var guid in guids)
            {
                var cardPath = AssetDatabase.GUIDToAssetPath(guid);
                var card = AssetDatabase.LoadAssetAtPath<CardData>(cardPath);
                if (card == null) continue;
                if (!IconByCardId.TryGetValue(card.CardId, out var icon)) continue;

                var iconPath = IconPath(icon);
                ApplySpriteImportSettings(iconPath);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(iconPath);
                if (sprite == null)
                {
                    log.AppendLine($"HATA (sprite yuklenemedi): {iconPath}");
                    continue;
                }

                var so = new SerializedObject(card);
                so.FindProperty("_artwork").objectReferenceValue = sprite;
                // Ikon beyaz siluet: kategori rengini arkadaki daire tasiyor.
                so.FindProperty("_tint").colorValue = Color.white;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(card);
                assigned++;
                log.AppendLine($"{card.DisplayName} -> {icon}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"Toplam atanan: {assigned}, atlanan: {skipped}");
            return log.ToString();
        }

        static void WriteIconPng(EcoIcon icon)
        {
            var path = IconPath(icon);
            var texture = IconFactory.CreateTexture(icon, IconSize);

            try
            {
                var bytes = texture.EncodeToPNG();
                File.WriteAllBytes(path, bytes);
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
            importer.maxTextureSize = IconSize;
            // Alfa maskesinde sikistirma kirliligi kenarlarda hemen belli olur.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        static string IconPath(EcoIcon icon) => $"{IconFolder}/Icon_{icon}.png";

        static void EnsureFolder(string fullPath, string parent, string child)
        {
            if (!AssetDatabase.IsValidFolder(fullPath))
                AssetDatabase.CreateFolder(parent, child);
        }
    }
}
