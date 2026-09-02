using System.Collections.Generic;
using System.IO;
using System.Text;
using EcoSort.Data;
using UnityEditor;
using UnityEngine;

namespace EcoSort.EditorTools
{
    /// <summary>
    /// Proje disindan gelen kart ikonlarini ice aktarir.
    ///
    /// Neden ozel bir arac gerekiyor: kaynak gorseller JPEG (.jfif). JPEG alfa
    /// kanali tasiyamadigi icin, cizim programindaki "seffaf" bolgeler dosyaya
    /// SATRANC TAHTASI DESENI olarak gomulmus durumda. Duz ice aktarma yapilirsa
    /// kartlarin arkasinda gri-beyaz kareler gorunur.
    ///
    /// Akis:
    ///   1. Gorseli oku,
    ///   2. Kenarlardan tasma (flood fill) ile DIS zemini isaretle,
    ///   3. Sanatin ICINDE kalan kapali zemin bolgelerini (yuzugun ortasi,
    ///      fincanin kulpu, atkinin dugumu) desen analiziyle bul ve sil,
    ///   4. JPEG sikistirmasinin biraktigi acik renk halesini temizle,
    ///   5. Bos kenarlari kirpip kareye tamamla (tum ikonlar ayni olcekte dursun),
    ///   6. 512 piksele indirip PNG olarak yaz,
    ///   7. Sprite import ayarlarini uygula ve CardData'lara bagla.
    ///
    /// Kullanim: Eco-Sort > Pixel Art Ikonlari Ice Aktar
    /// </summary>
    public static class PixelArtCardImporter
    {
        const string TargetFolder = "Assets/EcoSort/Art/CardIcons";
        const int OutputSize = 512;

        /// <summary>Kaynak klasoru hatirla: her calistirmada yeniden secmeye gerek kalmasin.</summary>
        const string SourceFolderPrefKey = "EcoSort.PixelArtSourceFolder";

        static string DefaultSourceFolder =>
            Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop),
                "ikonlar png");

        /// <summary>
        /// Kaynak dosya adi -> hedef CardData kimligi.
        ///
        /// Acik bir tablo tutuyoruz; bulanik isim eslestirmesi "kolye/kulaklik" gibi
        /// benzer adlarda sessizce yanlis karta baglanabilir.
        /// </summary>
        static readonly Dictionary<string, string> CardIdByFileKey = new Dictionary<string, string>
        {
            { "denizkabugu", "deniz_denizkabugu" },
            { "dalga",       "deniz_dalga" },
            { "semsiye",     "deniz_plajsemsiyesi" },

            { "kahvecekirdegi", "kahve_kahvecekirdegi" },
            { "kahvefincani",   "kahve_fincan" },
            // Yeni sanat "sicak su" yerine french press cizmis; kartin adi da
            // ona gore guncelleniyor (asagida DisplayNameOverride).
            { "frenchpress",    "kahve_sicaksu" },

            { "yaprak",    "sonbahar_kuruyaprak" },
            { "atki",      "sonbahar_yunatki" },
            { "balkabagi", "sonbahar_balkabagi" },

            { "kolye",    "taki_kolye" },
            { "bileklik", "taki_bileklik" },
            { "yuzuk",    "taki_yuzuk" },

            { "oyunkolu", "oyun_oyunkolu" },
            { "kulaklik", "oyun_kulaklik" },
            { "klavye",   "oyun_klavye" }
        };

        /// <summary>Sanat degisince kart adi da degismesi gerekenler.</summary>
        static readonly Dictionary<string, string> DisplayNameOverride = new Dictionary<string, string>
        {
            { "kahve_sicaksu", "French Press" }
        };

        // ---------------------------------------------------------------- menu

        [MenuItem("Eco-Sort/Pixel Art Ikonlari Ice Aktar")]
        static void ImportMenu()
        {
            var folder = EditorPrefs.GetString(SourceFolderPrefKey, DefaultSourceFolder);
            if (!Directory.Exists(folder)) folder = DefaultSourceFolder;

            Debug.Log(Import(folder));
        }

        [MenuItem("Eco-Sort/Pixel Art Kaynak Klasorunu Sec...")]
        static void PickFolderMenu()
        {
            var current = EditorPrefs.GetString(SourceFolderPrefKey, DefaultSourceFolder);
            var picked = EditorUtility.OpenFolderPanel("Ikon klasorunu sec", current, string.Empty);
            if (string.IsNullOrEmpty(picked)) return;

            EditorPrefs.SetString(SourceFolderPrefKey, picked);
            Debug.Log(Import(picked));
        }

        // ---------------------------------------------------------------- akis

        public static string Import(string sourceFolder)
        {
            var log = new StringBuilder();

            if (!Directory.Exists(sourceFolder))
                return $"Kaynak klasor bulunamadi: {sourceFolder}";

            EnsureFolders();

            var cards = LoadCardsById();
            var written = new List<KeyValuePair<string, string>>();

            foreach (var file in Directory.GetFiles(sourceFolder))
            {
                var extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension != ".jfif" && extension != ".jpg" && extension != ".jpeg" &&
                    extension != ".png") continue;

                var key = Normalize(Path.GetFileNameWithoutExtension(file));

                if (!CardIdByFileKey.TryGetValue(key, out var cardId))
                {
                    log.AppendLine($"[eslesmedi] {Path.GetFileName(file)}  (anahtar: {key})");
                    continue;
                }

                var assetPath = $"{TargetFolder}/{cardId}.png";
                if (!ConvertAndWrite(file, assetPath, log)) continue;

                written.Add(new KeyValuePair<string, string>(cardId, assetPath));
            }

            if (written.Count == 0)
            {
                log.AppendLine("Hicbir gorsel yazilamadi.");
                return log.ToString();
            }

            AssetDatabase.Refresh();

            // Import ayarlari ve karta baglama, dosyalar diske yazildiktan sonra.
            int bound = 0;
            foreach (var entry in written)
            {
                var cardId = entry.Key;
                var assetPath = entry.Value;

                ApplySpriteImportSettings(assetPath);

                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
                if (sprite == null)
                {
                    log.AppendLine($"HATA (sprite yuklenemedi): {assetPath}");
                    continue;
                }

                if (!cards.TryGetValue(cardId, out var card))
                {
                    log.AppendLine($"HATA (kart bulunamadi): {cardId}");
                    continue;
                }

                var so = new SerializedObject(card);
                so.FindProperty("_artwork").objectReferenceValue = sprite;
                so.FindProperty("_tint").colorValue = Color.white;

                if (DisplayNameOverride.TryGetValue(cardId, out var newName))
                    so.FindProperty("_displayName").stringValue = newName;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(card);

                bound++;
                log.AppendLine($"{cardId}  <-  {Path.GetFileName(assetPath)}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            log.AppendLine($"Baglanan kart: {bound}/{CardIdByFileKey.Count}");
            return log.ToString();
        }

        static bool ConvertAndWrite(string sourceFile, string assetPath, StringBuilder log)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

            try
            {
                if (!texture.LoadImage(File.ReadAllBytes(sourceFile)))
                {
                    log.AppendLine($"HATA (okunamadi): {Path.GetFileName(sourceFile)}");
                    return false;
                }

                var pixels = texture.GetPixels32();
                int width = texture.width;
                int height = texture.height;

                // 1) Kenara bagli zemini isaretle (henuz silme: ton ornegi lazim).
                var outside = MarkBorderConnectedBackdrop(pixels, width, height);

                // 2) Satranc deseninin iki tonunu bu zeminden ogren.
                SampleBackdropTones(pixels, outside, out int toneA, out int toneB);

                // 3) Sanatin icinde kalan kapali satranc bolgelerini sil.
                ClearEnclosedCheckerRegions(pixels, outside, width, height, toneA, toneB);

                // 4) Dis zemini sil.
                for (int i = 0; i < pixels.Length; i++)
                    if (outside[i]) pixels[i] = new Color32(0, 0, 0, 0);

                ErodeCompressionHalo(pixels, width, height, passes: 2);

                var squared = TrimAndSquare(pixels, width, height, out int squareSize);
                var scaled = Downscale(squared, squareSize, OutputSize, out int finalSize);

                var output = new Texture2D(finalSize, finalSize, TextureFormat.RGBA32, false);
                output.SetPixels32(scaled);
                output.Apply(false, false);

                File.WriteAllBytes(assetPath, output.EncodeToPNG());
                Object.DestroyImmediate(output);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }

        // ---------------------------------------------------------------- goruntu isleme

        /// <summary>
        /// Bir piksel satranc zeminine ait olabilir mi?
        /// Zemin gri tonlaridir: renk doygunlugu yok ve yeterince acik.
        /// </summary>
        static bool IsBackdropColor(Color32 c)
        {
            int max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            int min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));

            if (max < 158) return false;      // koyu: sanat
            if (max - min > 22) return false; // renkli: sanat
            return true;
        }

        static int Brightness(Color32 c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b));

        /// <summary>
        /// Kenarlardan baslayan tasma ile DIS zemini isaretler.
        ///
        /// Kenardan baglanti sarti onemli: sanatin icindeki acik renkler
        /// (yuzugun tasi, klavye tuslari) zemine bitisik olmadigi icin bu adimda
        /// hicbir sekilde secilmez.
        /// </summary>
        static bool[] MarkBorderConnectedBackdrop(Color32[] pixels, int width, int height)
        {
            var outside = new bool[pixels.Length];
            var queue = new Queue<int>();

            for (int x = 0; x < width; x++)
            {
                Enqueue(pixels, outside, queue, x, 0, width, height);
                Enqueue(pixels, outside, queue, x, height - 1, width, height);
            }

            for (int y = 0; y < height; y++)
            {
                Enqueue(pixels, outside, queue, 0, y, width, height);
                Enqueue(pixels, outside, queue, width - 1, y, width, height);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % width;
                int y = index / width;

                Enqueue(pixels, outside, queue, x - 1, y, width, height);
                Enqueue(pixels, outside, queue, x + 1, y, width, height);
                Enqueue(pixels, outside, queue, x, y - 1, width, height);
                Enqueue(pixels, outside, queue, x, y + 1, width, height);
            }

            return outside;
        }

        static void Enqueue(Color32[] pixels, bool[] outside, Queue<int> queue,
            int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;

            int index = y * width + x;
            if (outside[index]) return;
            if (!IsBackdropColor(pixels[index])) return;

            outside[index] = true;
            queue.Enqueue(index);
        }

        /// <summary>
        /// Dis zeminin parlaklik histogramindan satrancin iki tonunu cikarir
        /// (tipik olarak beyaz ~255 ve acik gri ~190).
        /// </summary>
        static void SampleBackdropTones(Color32[] pixels, bool[] outside, out int toneA, out int toneB)
        {
            var histogram = new int[256];
            for (int i = 0; i < pixels.Length; i++)
                if (outside[i]) histogram[Brightness(pixels[i])]++;

            toneA = 0;
            for (int v = 0; v < 256; v++)
                if (histogram[v] > histogram[toneA]) toneA = v;

            toneB = 0;
            for (int v = 0; v < 256; v++)
                if (Mathf.Abs(v - toneA) >= 20 && histogram[v] > histogram[toneB]) toneB = v;
        }

        /// <summary>
        /// Sanatin ICINDE kalan, kenara baglanmayan satranc bolgelerini temizler.
        /// (Ornek: yuzugun ortasindaki bosluk - bant her tarafini sardigi icin
        /// kenardan gelen tasma oraya ulasamaz.)
        ///
        /// Ayirt etme olcutu KONUM DEGIL DAGILIM: satranc bolgesi neredeyse
        /// tamamen iki tondan olusur ve ikisi kabaca esit paydadir. Sanattaki
        /// acik gri alanlar (metal parlamasi, tas) ise tek tonda toplanir.
        /// Boylece desenin fazini takip etmeye gerek kalmaz - JPEG'de olcek
        /// kesirli oldugu icin faz zaten goruntu boyunca kayiyor.
        /// </summary>
        static void ClearEnclosedCheckerRegions(Color32[] pixels, bool[] outside,
            int width, int height, int toneA, int toneB)
        {
            const int MinComponentPixels = 1024;   // ~1 satranc hucresi
            const int ToneTolerance = 12;
            const float MinTwoToneRatio = 0.80f;
            const float MinBalance = 0.35f;

            if (Mathf.Abs(toneA - toneB) < 20) return;   // desen yok

            var visited = new bool[pixels.Length];
            var component = new List<int>();
            var queue = new Queue<int>();

            for (int start = 0; start < pixels.Length; start++)
            {
                if (visited[start] || outside[start]) continue;
                if (!IsBackdropColor(pixels[start])) continue;

                component.Clear();
                queue.Clear();
                visited[start] = true;
                queue.Enqueue(start);

                int countA = 0;
                int countB = 0;

                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    component.Add(index);

                    int brightness = Brightness(pixels[index]);
                    if (Mathf.Abs(brightness - toneA) <= ToneTolerance) countA++;
                    else if (Mathf.Abs(brightness - toneB) <= ToneTolerance) countB++;

                    int x = index % width;
                    int y = index / width;

                    EnqueueEnclosed(pixels, outside, visited, queue, x - 1, y, width, height);
                    EnqueueEnclosed(pixels, outside, visited, queue, x + 1, y, width, height);
                    EnqueueEnclosed(pixels, outside, visited, queue, x, y - 1, width, height);
                    EnqueueEnclosed(pixels, outside, visited, queue, x, y + 1, width, height);
                }

                if (component.Count < MinComponentPixels) continue;

                float twoToneRatio = (countA + countB) / (float)component.Count;
                float balance = Mathf.Min(countA, countB) / (float)Mathf.Max(1, Mathf.Max(countA, countB));

                if (twoToneRatio < MinTwoToneRatio || balance < MinBalance) continue;

                foreach (var index in component) pixels[index] = new Color32(0, 0, 0, 0);
            }
        }

        static void EnqueueEnclosed(Color32[] pixels, bool[] outside, bool[] visited, Queue<int> queue,
            int x, int y, int width, int height)
        {
            if (x < 0 || y < 0 || x >= width || y >= height) return;

            int index = y * width + x;
            if (visited[index] || outside[index]) return;
            if (!IsBackdropColor(pixels[index])) return;

            visited[index] = true;
            queue.Enqueue(index);
        }

        /// <summary>
        /// JPEG sikistirmasi, sanat ile zemin sinirinda birkac piksellik acik gri
        /// bir hale birakir. Seffaf piksellere komsu olan zemin renkli pikselleri
        /// tur tur temizliyoruz.
        /// </summary>
        static void ErodeCompressionHalo(Color32[] pixels, int width, int height, int passes)
        {
            for (int pass = 0; pass < passes; pass++)
            {
                var toClear = new List<int>();

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        if (pixels[index].a == 0) continue;
                        if (!IsBackdropColor(pixels[index])) continue;
                        if (!HasTransparentNeighbour(pixels, width, height, x, y)) continue;

                        toClear.Add(index);
                    }
                }

                if (toClear.Count == 0) return;
                foreach (var index in toClear) pixels[index] = new Color32(0, 0, 0, 0);
            }
        }

        static bool HasTransparentNeighbour(Color32[] pixels, int width, int height, int x, int y)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dy == 0) continue;

                    int nx = x + dx;
                    int ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;

                    if (pixels[ny * width + nx].a == 0) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Bos kenarlari kirpar ve sonucu KARE tuvale ortalar.
        /// Boylece kartlarda her ikon ayni kutuyu doldurur; genis olan (klavye)
        /// ile dar olan (yuzuk) arasindaki olcek farki kaybolur.
        /// </summary>
        static Color32[] TrimAndSquare(Color32[] pixels, int width, int height, out int squareSize)
        {
            int minX = width, minY = height, maxX = -1, maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a == 0) continue;

                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            // Hicbir sey kalmadiysa (beklenmedik) orijinali oldugu gibi dondur.
            if (maxX < minX || maxY < minY)
            {
                squareSize = Mathf.Max(width, height);
                return Fit(pixels, width, height, 0, 0, width, height, squareSize);
            }

            int contentWidth = maxX - minX + 1;
            int contentHeight = maxY - minY + 1;

            // Kenarda biraz nefes payi birak.
            int padding = Mathf.RoundToInt(Mathf.Max(contentWidth, contentHeight) * 0.04f);
            squareSize = Mathf.Max(contentWidth, contentHeight) + padding * 2;

            return Fit(pixels, width, height, minX, minY, contentWidth, contentHeight, squareSize);
        }

        /// <summary>Kaynagin bir bolgesini kare tuvalin ortasina kopyalar.</summary>
        static Color32[] Fit(Color32[] source, int sourceWidth, int sourceHeight,
            int cropX, int cropY, int cropWidth, int cropHeight, int squareSize)
        {
            var result = new Color32[squareSize * squareSize];

            int offsetX = (squareSize - cropWidth) / 2;
            int offsetY = (squareSize - cropHeight) / 2;

            for (int y = 0; y < cropHeight; y++)
            {
                int sourceY = cropY + y;
                if (sourceY < 0 || sourceY >= sourceHeight) continue;

                for (int x = 0; x < cropWidth; x++)
                {
                    int sourceX = cropX + x;
                    if (sourceX < 0 || sourceX >= sourceWidth) continue;

                    result[(offsetY + y) * squareSize + offsetX + x] =
                        source[sourceY * sourceWidth + sourceX];
                }
            }

            return result;
        }

        /// <summary>
        /// Kare gorseli hedef olcuye indirir (kutu filtresi).
        /// Renkler alfa ile agirliklandirilarak toplanir; aksi halde seffaf
        /// piksellerin siyah rengi kenarlarda koyu bir hale birakir.
        /// </summary>
        static Color32[] Downscale(Color32[] source, int sourceSize, int maxSize, out int targetSize)
        {
            if (sourceSize <= maxSize)
            {
                targetSize = sourceSize;
                return source;
            }

            targetSize = maxSize;
            var result = new Color32[targetSize * targetSize];
            float ratio = sourceSize / (float)targetSize;

            for (int y = 0; y < targetSize; y++)
            {
                int y0 = Mathf.FloorToInt(y * ratio);
                int y1 = Mathf.Min(Mathf.FloorToInt((y + 1) * ratio), sourceSize);

                for (int x = 0; x < targetSize; x++)
                {
                    int x0 = Mathf.FloorToInt(x * ratio);
                    int x1 = Mathf.Min(Mathf.FloorToInt((x + 1) * ratio), sourceSize);

                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    int count = 0;

                    for (int sy = y0; sy < y1; sy++)
                    {
                        for (int sx = x0; sx < x1; sx++)
                        {
                            var c = source[sy * sourceSize + sx];
                            float alpha = c.a / 255f;

                            r += c.r * alpha;
                            g += c.g * alpha;
                            b += c.b * alpha;
                            a += alpha;
                            count++;
                        }
                    }

                    if (count == 0 || a <= 0.0001f)
                    {
                        result[y * targetSize + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    result[y * targetSize + x] = new Color32(
                        (byte)Mathf.Clamp(Mathf.RoundToInt(r / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(g / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(b / a), 0, 255),
                        (byte)Mathf.Clamp(Mathf.RoundToInt(a / count * 255f), 0, 255));
                }
            }

            return result;
        }

        // ---------------------------------------------------------------- yardimcilar

        static Dictionary<string, CardData> LoadCardsById()
        {
            var result = new Dictionary<string, CardData>();

            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(CardData)))
            {
                var card = AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid));
                if (card != null) result[card.CardId] = card;
            }

            return result;
        }

        /// <summary>Turkce karakterleri sadelestirir, harf/rakam disini atar.</summary>
        static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            var builder = new StringBuilder(value.Length);

            foreach (var raw in value)
            {
                char c = char.ToLowerInvariant(raw);

                // Kaynagi ASCII tutmak icin unicode kacislari kullaniyoruz.
                switch (c)
                {
                    case 'ı': c = 'i'; break;   // noktasiz i
                    case 'ğ': c = 'g'; break;   // yumusak g
                    case 'ü': c = 'u'; break;   // u umlaut
                    case 'ş': c = 's'; break;   // s cedilla
                    case 'ö': c = 'o'; break;   // o umlaut
                    case 'ç': c = 'c'; break;   // c cedilla
                }

                if (char.IsLetterOrDigit(c)) builder.Append(c);
            }

            return builder.ToString();
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
            importer.maxTextureSize = OutputSize;
            // Pixel art kenarlarinda sikistirma kirliligi hemen goze carpar.
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            importer.SaveAndReimport();
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/EcoSort"))
                AssetDatabase.CreateFolder("Assets", "EcoSort");

            if (!AssetDatabase.IsValidFolder("Assets/EcoSort/Art"))
                AssetDatabase.CreateFolder("Assets/EcoSort", "Art");

            if (!AssetDatabase.IsValidFolder(TargetFolder))
                AssetDatabase.CreateFolder("Assets/EcoSort/Art", "CardIcons");
        }
    }
}
