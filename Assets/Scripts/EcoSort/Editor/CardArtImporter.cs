using System.Collections.Generic;
using System.IO;
using System.Text;
using EcoSort.Data;
using UnityEditor;
using UnityEngine;

namespace EcoSort.EditorTools
{
    /// <summary>
    /// Elle cizilmis kart ikonlarini projeye baglar.
    ///
    /// Kullanim: gorselleri Assets/EcoSort/Art/CardIcons/ klasorune at, sonra
    /// menuden "Eco-Sort > Kart Ikonlarini Bagla" calistir.
    ///
    /// Dosya adi eslestirmesi toleranslidir: buyuk/kucuk harf, Turkce karakter,
    /// bosluk, alt tire ve "icon_" gibi onekler dikkate alinmaz.
    /// "ATKI.png", "yun_atki.png", "Icon_Yun Atki.png" ayni karta gider.
    /// </summary>
    public static class CardArtImporter
    {
        const string SourceFolder = "Assets/EcoSort/Art/CardIcons";
        const int MaxTextureSize = 512;

        [MenuItem("Eco-Sort/Kart Ikonlarini Bagla")]
        static void BindMenu()
        {
            Debug.Log(Bind());
        }

        [MenuItem("Eco-Sort/Kart Ikonlari Klasorunu Ac")]
        static void OpenFolder()
        {
            EnsureFolders();
            EditorUtility.RevealInFinder(SourceFolder + "/");
        }

        public static string Bind()
        {
            EnsureFolders();

            var log = new StringBuilder();

            // ---- kaynak gorseller
            var files = new List<string>();
            foreach (var path in Directory.GetFiles(SourceFolder))
            {
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".psd" || ext == ".tga")
                    files.Add(path.Replace('\\', '/'));
            }

            if (files.Count == 0)
            {
                return $"'{SourceFolder}' klasorunde gorsel yok.\n" +
                       "Ikonlari oraya kopyalayip tekrar calistir.";
            }

            // ---- kartlar
            var cards = new List<CardData>();
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CardData)))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null) cards.Add(card);
            }

            var usedFiles = new HashSet<string>();
            int bound = 0;

            foreach (var card in cards)
            {
                var match = FindBestMatch(card, files, usedFiles);
                if (match == null)
                {
                    log.AppendLine($"[eslesmedi] {card.DisplayName}  (aranan: {Normalize(card.DisplayName)})");
                    continue;
                }

                usedFiles.Add(match);
                ApplySpriteImportSettings(match);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(match);
                if (sprite == null)
                {
                    log.AppendLine($"[hata] sprite yuklenemedi: {match}");
                    continue;
                }

                var so = new SerializedObject(card);
                so.FindProperty("_artwork").objectReferenceValue = sprite;
                // Elle cizilmis ikon kendi renklerini tasir: tonlama yapma.
                so.FindProperty("_tint").colorValue = Color.white;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);

                bound++;
                log.AppendLine($"{card.DisplayName}  <-  {Path.GetFileName(match)}");
            }

            // Kullanilmayan dosyalari da bildir: yanlis isimlendirme boylece hemen gorulur.
            foreach (var file in files)
                if (!usedFiles.Contains(file))
                    log.AppendLine($"[kullanilmadi] {Path.GetFileName(file)}  (normalize: {Normalize(Path.GetFileNameWithoutExtension(file))})");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"Baglanan kart: {bound}/{cards.Count}");
            return log.ToString();
        }

        /// <summary>Karta en iyi uyan dosyayi bulur; once tam, sonra kismi eslesme arar.</summary>
        static string FindBestMatch(CardData card, List<string> files, HashSet<string> used)
        {
            string byName = Normalize(card.DisplayName);
            string byId = Normalize(card.CardId);

            string partial = null;

            foreach (var file in files)
            {
                if (used.Contains(file)) continue;

                string fileKey = Normalize(Path.GetFileNameWithoutExtension(file));
                if (fileKey.Length == 0) continue;

                if (fileKey == byName || fileKey == byId) return file;   // tam eslesme

                if (partial == null &&
                    (fileKey.Contains(byName) || byName.Contains(fileKey) ||
                     fileKey.Contains(byId) || byId.Contains(fileKey)))
                    partial = file;
            }

            return partial;
        }

        /// <summary>
        /// Turkce karakterleri sadelestirir, harf/rakam disini atar.
        /// "Yun Atki", "YUN_ATKI" ve Turkce yazimi ayni anahtara duser: "yunatki".
        /// </summary>
        static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var builder = new StringBuilder(value.Length);

            foreach (var raw in value)
            {
                char c = char.ToLowerInvariant(raw);

                // Kaynak dosyayi ASCII tutmak icin unicode kacislari kullaniyoruz.
                switch (c)
                {
                    case '\u0131': c = 'i'; break;   // noktasiz i
                    case '\u011F': c = 'g'; break;   // yumusak g
                    case '\u00FC': c = 'u'; break;   // u umlaut
                    case '\u015F': c = 's'; break;   // s cedilla
                    case '\u00F6': c = 'o'; break;   // o umlaut
                    case '\u00E7': c = 'c'; break;   // c cedilla
                }

                if (char.IsLetterOrDigit(c)) builder.Append(c);
            }

            var key = builder.ToString();

            // Yaygin onekleri at: "icon_atki" -> "atki"
            foreach (var prefix in new[] { "icon", "ikon", "card", "kart" })
                if (key.Length > prefix.Length && key.StartsWith(prefix))
                    key = key.Substring(prefix.Length);

            return key;
        }

        static void ApplySpriteImportSettings(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;

            if (importer.textureType != TextureImporterType.Sprite)
            {
                importer.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (importer.spriteImportMode != SpriteImportMode.Single)
            {
                importer.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!importer.alphaIsTransparency)
            {
                importer.alphaIsTransparency = true;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (importer.maxTextureSize > MaxTextureSize)
            {
                importer.maxTextureSize = MaxTextureSize;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/EcoSort"))
                AssetDatabase.CreateFolder("Assets", "EcoSort");

            if (!AssetDatabase.IsValidFolder("Assets/EcoSort/Art"))
                AssetDatabase.CreateFolder("Assets/EcoSort", "Art");

            if (!AssetDatabase.IsValidFolder(SourceFolder))
                AssetDatabase.CreateFolder("Assets/EcoSort/Art", "CardIcons");
        }
    }
}
