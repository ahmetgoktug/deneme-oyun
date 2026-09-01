using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace EcoSort.EditorTools
{
    /// <summary>
    /// Tek bir "ikon sayfasi" (grid halinde birden fazla ikon iceren gorsel)
    /// icindeki kareleri ayri PNG dosyalarina boler.
    ///
    /// Cikti dosyalari CardArtImporter'in taradigi klasore yazilir, boylece
    /// dilimledikten sonra tek komutla kartlara baglanabilir.
    ///
    /// Koordinatlar SOL UST kosedan olculur (gorsel duzenleyicilerdeki gibi),
    /// Unity'nin alt-sol kokenine cevirme isi burada yapilir.
    /// </summary>
    public static class IconSheetSlicer
    {
        const string OutputFolder = "Assets/EcoSort/Art/CardIcons";

        /// <summary>Sayfa hakkinda olcu almak icin: boyut ve okunabilirlik durumu.</summary>
        public static string Inspect(string sheetPath)
        {
            var texture = LoadReadable(sheetPath);
            if (texture == null) return $"Gorsel okunamadi: {sheetPath}";

            return $"{sheetPath}\nBoyut: {texture.width} x {texture.height} piksel";
        }

        /// <summary>
        /// Sayfayi duzgun bir grid varsayarak boler.
        /// originX/originY: ilk karenin sol ust kosesi.
        /// tileWidth/tileHeight: bir karenin olcusu.
        /// stepX/stepY: kareler arasi mesafe (kare olcusu + bosluk).
        /// names: okuma sirasina gore (soldan saga, yukaridan asagi) cikti adlari.
        /// </summary>
        public static string SliceGrid(string sheetPath, int originX, int originY,
            int tileWidth, int tileHeight, int stepX, int stepY,
            int columns, int rows, string[] names, bool trimTransparent = true)
        {
            var texture = LoadReadable(sheetPath);
            if (texture == null) return $"Gorsel okunamadi: {sheetPath}";

            EnsureOutputFolder();

            var log = new StringBuilder();
            var written = new List<string>();
            int index = 0;

            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < columns; col++, index++)
                {
                    if (index >= names.Length)
                    {
                        log.AppendLine($"[atlandi] {row}.satir {col}.sutun icin isim verilmedi.");
                        continue;
                    }

                    int left = originX + stepX * col;
                    int top = originY + stepY * row;

                    // Sol-ust kokeni Unity'nin alt-sol kokenine cevir.
                    int bottom = texture.height - (top + tileHeight);

                    if (left < 0 || bottom < 0 ||
                        left + tileWidth > texture.width ||
                        bottom + tileHeight > texture.height)
                    {
                        log.AppendLine($"[hata] '{names[index]}' sayfa disina tasiyor " +
                                       $"(left={left}, top={top}). Grid degerlerini kontrol et.");
                        continue;
                    }

                    var pixels = texture.GetPixels(left, bottom, tileWidth, tileHeight);
                    var tile = new Texture2D(tileWidth, tileHeight, TextureFormat.RGBA32, false);
                    tile.SetPixels(pixels);
                    tile.Apply(false, false);

                    if (trimTransparent) tile = TrimTransparent(tile);

                    var path = $"{OutputFolder}/{names[index]}.png";
                    File.WriteAllBytes(path, tile.EncodeToPNG());
                    Object.DestroyImmediate(tile);

                    written.Add(path);
                    log.AppendLine($"{names[index]}.png  <-  ({left},{top}) {tileWidth}x{tileHeight}");
                }
            }

            AssetDatabase.Refresh();
            log.AppendLine($"Yazilan dosya: {written.Count}");
            log.AppendLine("Simdi 'Eco-Sort > Kart Ikonlarini Bagla' calistir.");
            return log.ToString();
        }

        /// <summary>
        /// Sayfayi kare kare, elle verilen koordinatlarla boler.
        /// Duzgun grid olmayan (elle/AI ile dizilmis) sayfalar icin.
        ///
        /// rects: her kare icin [sol, ust, genislik, yukseklik] - sol ust kokenli.
        /// inset: her kenardan iceri kirpma (arka plan sizintisini onler).
        /// cornerRadiusRatio: kose yuvarlatma orani (0 = kapali). Kare koseli kirpimda
        /// arka plan kalintisi kalmasin diye kullanilir.
        /// </summary>
        public static string SliceRects(string sheetPath, string[] names, int[][] rects,
            int inset = 4, float cornerRadiusRatio = 0.16f)
        {
            var texture = LoadReadable(sheetPath);
            if (texture == null) return $"Gorsel okunamadi: {sheetPath}";

            if (names.Length != rects.Length)
                return $"Isim sayisi ({names.Length}) ile kare sayisi ({rects.Length}) uyusmuyor.";

            EnsureOutputFolder();

            var log = new StringBuilder();
            int written = 0;

            for (int i = 0; i < rects.Length; i++)
            {
                int left = rects[i][0] + inset;
                int top = rects[i][1] + inset;
                int width = rects[i][2] - inset * 2;
                int height = rects[i][3] - inset * 2;

                // Sol-ust kokeni Unity'nin alt-sol kokenine cevir.
                int bottom = texture.height - (top + height);

                if (left < 0 || bottom < 0 || width <= 0 || height <= 0 ||
                    left + width > texture.width || bottom + height > texture.height)
                {
                    log.AppendLine($"[hata] '{names[i]}' sayfa disinda: " +
                                   $"({left},{top}) {width}x{height}");
                    continue;
                }

                var tile = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tile.SetPixels(texture.GetPixels(left, bottom, width, height));

                if (cornerRadiusRatio > 0f)
                    RoundCorners(tile, Mathf.Min(width, height) * cornerRadiusRatio);

                tile.Apply(false, false);

                var path = $"{OutputFolder}/{names[i]}.png";
                File.WriteAllBytes(path, tile.EncodeToPNG());
                Object.DestroyImmediate(tile);

                written++;
                log.AppendLine($"{names[i]}.png  <-  ({left},{top}) {width}x{height}");
            }

            AssetDatabase.Refresh();
            log.AppendLine($"Yazilan dosya: {written}/{rects.Length}");
            return log.ToString();
        }

        /// <summary>Kare kirpimin koselerini saydamlastirir (yuvarlak kose maskesi).</summary>
        static void RoundCorners(Texture2D tile, float radius)
        {
            int width = tile.width;
            int height = tile.height;
            var pixels = tile.GetPixels();

            float halfW = width * 0.5f;
            float halfH = height * 0.5f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float px = x + 0.5f - halfW;
                    float py = y + 0.5f - halfH;

                    float qx = Mathf.Abs(px) - (halfW - radius);
                    float qy = Mathf.Abs(py) - (halfH - radius);
                    float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
                    float distance = outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;

                    float alpha = Mathf.Clamp01(0.5f - distance);
                    if (alpha >= 1f) continue;

                    var c = pixels[y * width + x];
                    c.a = alpha;
                    pixels[y * width + x] = c;
                }
            }

            tile.SetPixels(pixels);
        }

        /// <summary>Tamamen saydam kenarlari kirpar; ikon kare icinde ortalanmis kalir.</summary>
        static Texture2D TrimTransparent(Texture2D source)
        {
            var pixels = source.GetPixels();
            int width = source.width;
            int height = source.height;

            int minX = width, minY = height, maxX = -1, maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (pixels[y * width + x].a <= 0.01f) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }

            // Tamamen saydam ya da zaten dolu: dokunma.
            if (maxX < 0 || (minX == 0 && minY == 0 && maxX == width - 1 && maxY == height - 1))
                return source;

            int newWidth = maxX - minX + 1;
            int newHeight = maxY - minY + 1;

            var trimmed = new Texture2D(newWidth, newHeight, TextureFormat.RGBA32, false);
            trimmed.SetPixels(source.GetPixels(minX, minY, newWidth, newHeight));
            trimmed.Apply(false, false);

            Object.DestroyImmediate(source);
            return trimmed;
        }

        /// <summary>Gorseli okunabilir hale getirip yukler (importer ayarini kalici degistirir).</summary>
        static Texture2D LoadReadable(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError($"[EcoSort] '{path}' bir doku degil ya da proje icinde degil.");
                return null;
            }

            bool needsReimport = false;

            if (!importer.isReadable)
            {
                importer.isReadable = true;
                needsReimport = true;
            }

            // Sikistirma dilimlemede kenarlari bozar.
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                needsReimport = true;
            }

            if (needsReimport) importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void EnsureOutputFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/EcoSort"))
                AssetDatabase.CreateFolder("Assets", "EcoSort");

            if (!AssetDatabase.IsValidFolder("Assets/EcoSort/Art"))
                AssetDatabase.CreateFolder("Assets/EcoSort", "Art");

            if (!AssetDatabase.IsValidFolder(OutputFolder))
                AssetDatabase.CreateFolder("Assets/EcoSort/Art", "CardIcons");
        }
    }
}
