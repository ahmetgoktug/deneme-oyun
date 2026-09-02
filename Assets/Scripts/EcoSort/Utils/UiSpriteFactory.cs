using System.Collections.Generic;
using UnityEngine;

namespace EcoSort.Utils
{
    /// <summary>
    /// Sanat asseti beklemeden mobil hissi veren UI sprite'lari uretir:
    /// yuvarlak kose (9-slice), daire, yumusak golge ve dikey gradyan.
    ///
    /// Uretilen sprite'lar onbellege alinir; ayni yaricap tekrar istendiginde
    /// yeni doku olusturulmaz.
    /// </summary>
    public static class UiSpriteFactory
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>Yuvarlak koseli 9-slice sprite. Image.type = Sliced ile kullan.</summary>
        public static Sprite Rounded(int radius)
        {
            radius = Mathf.Clamp(radius, 2, 128);
            string key = "rounded_" + radius;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int size = radius * 2 + 4;
            var sprite = Build(size, (px, py, half) =>
            {
                float d = RoundedBoxDistance(px, py, half, half, radius);
                // 1 piksellik yumusak kenar: keskin merdiven yerine anti-alias.
                return Mathf.Clamp01(0.5f - d);
            }, radius + 1);

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Tam daire sprite (yaricap = boyut / 2).</summary>
        public static Sprite Circle(int size = 64)
        {
            size = Mathf.Clamp(size, 8, 256);
            string key = "circle_" + size;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Build(size, (px, py, half) =>
            {
                float d = Mathf.Sqrt(px * px + py * py) - (half - 1f);
                return Mathf.Clamp01(0.5f - d);
            }, 0);

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Yumusak kenarli golge sprite'i. Kartlarin zeminden kalkmis gorunmesini saglar.
        /// softness: golgenin dagilma yaricapi (piksel).
        /// </summary>
        public static Sprite Shadow(int radius, int softness = 12)
        {
            radius = Mathf.Clamp(radius, 2, 96);
            softness = Mathf.Clamp(softness, 2, 48);
            string key = "shadow_" + radius + "_" + softness;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            int size = (radius + softness) * 2 + 4;
            float inner = radius;
            var sprite = Build(size, (px, py, half) =>
            {
                float d = RoundedBoxDistance(px, py, half - softness, half - softness, inner);
                // Disari dogru dogrusal sonumlenme; kenarda sert kesim olmasin.
                float a = Mathf.Clamp01(1f - d / softness);
                return a * a;   // karesi: merkeze yakin daha yogun, disari dogru hizli acilir
            }, radius + softness + 1);

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Merkezden disari yumusakca sonen radyal isik. Slot seridinin arkasina
        /// konunca ekranin ustune dogal bir "sahne isigi" verir.
        /// </summary>
        public static Sprite RadialGlow(int size = 128)
        {
            size = Mathf.Clamp(size, 16, 512);
            string key = "glow_" + size;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var sprite = Build(size, (px, py, half) =>
            {
                float distance = Mathf.Sqrt(px * px + py * py) / half;
                // smoothstep benzeri egri: merkezde dolu, kenarda tam saydam.
                float a = Mathf.Clamp01(1f - distance);
                return a * a * (3f - 2f * a) * 0.5f;
            }, 0);

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Dikey gradyan zemin. Image.type = Simple, stretch ile kullan.</summary>
        public static Sprite VerticalGradient(Color bottom, Color top, int height = 256)
        {
            height = Mathf.Clamp(height, 8, 1024);
            string key = "grad_" + bottom.GetHashCode() + "_" + top.GetHashCode() + "_" + height;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = NewTexture(4, height);
            var pixels = new Color32[4 * height];

            for (int y = 0; y < height; y++)
            {
                var color = (Color32)Color.Lerp(bottom, top, y / (float)(height - 1));
                for (int x = 0; x < 4; x++) pixels[y * 4 + x] = color;
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Cache[key] = sprite;
            return sprite;
        }

        // ---------------------------------------------------------------- ic isleyis

        delegate float AlphaAt(float px, float py, float half);

        static Sprite Build(int size, AlphaAt alphaAt, int border)
        {
            var texture = NewTexture(size, size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f - half;
                    float py = y + 0.5f - half;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alphaAt(px, py, half)) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);

            var rect = new Rect(0, 0, size, size);
            var pivot = new Vector2(0.5f, 0.5f);
            var sprite = border > 0
                ? Sprite.Create(texture, rect, pivot, 100f, 0, SpriteMeshType.FullRect,
                    new Vector4(border, border, border, border))
                : Sprite.Create(texture, rect, pivot, 100f, 0, SpriteMeshType.FullRect);

            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        static Texture2D NewTexture(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            return texture;
        }

        /// <summary>Yuvarlak dikdortgenin isaretli mesafe fonksiyonu (SDF).</summary>
        static float RoundedBoxDistance(float px, float py, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(px) - (halfWidth - radius);
            float qy = Mathf.Abs(py) - (halfHeight - radius);
            float outsideX = Mathf.Max(qx, 0f);
            float outsideY = Mathf.Max(qy, 0f);
            float outside = Mathf.Sqrt(outsideX * outsideX + outsideY * outsideY);
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }
    }
}
