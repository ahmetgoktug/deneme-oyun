using System.Collections.Generic;
using UnityEngine;

namespace EcoSort.Utils
{
    /// <summary>Prosedurel olarak cizilebilen ikon siluetleri.</summary>
    public enum EcoIcon
    {
        Bean,      // kahve cekirdegi
        Droplet,   // su damlasi
        Cup,       // fincan
        Leaf,      // yaprak
        Pumpkin,   // bal kabagi
        Scarf,     // atki
        Wave,      // dalga
        Shell,     // deniz kabugu
        Bird       // marti
    }

    /// <summary>
    /// Isaretli mesafe fonksiyonlari (SDF) ile ikon siluetleri uretir.
    /// Cikti beyaz + alfa maskesidir: Image.color ile istenen renge boyanir.
    ///
    /// Amac gecici sanat: uretim ikonlari geldiginde CardData.Artwork alanina
    /// gercek sprite atmak yeterli, hicbir kod degismez.
    /// </summary>
    public static class IconFactory
    {
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite GetSprite(EcoIcon icon, int size = 256)
        {
            string key = icon + "_" + size;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = CreateTexture(icon, size);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Ikonu doku olarak uretir. PNG'ye cevirmek isteyen editor kodu bunu kullanir.</summary>
        public static Texture2D CreateTexture(EcoIcon icon, int size = 256)
        {
            size = Mathf.Clamp(size, 32, 1024);

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Normalize edilmis koordinat: [-1, 1]
                    var p = new Vector2((x + 0.5f - half) / half, (y + 0.5f - half) / half);

                    float d = Evaluate(icon, p);

                    // Normalize birimden piksele: 1 birim = half piksel.
                    float alpha = Mathf.Clamp01(0.5f - d * half);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
        }

        // ---------------------------------------------------------------- ikon tanimlari

        static float Evaluate(EcoIcon icon, Vector2 p)
        {
            switch (icon)
            {
                case EcoIcon.Bean: return Bean(p);
                case EcoIcon.Droplet: return Droplet(p);
                case EcoIcon.Cup: return Cup(p);
                case EcoIcon.Leaf: return Leaf(p);
                case EcoIcon.Pumpkin: return Pumpkin(p);
                case EcoIcon.Scarf: return Scarf(p);
                case EcoIcon.Wave: return Wave(p);
                case EcoIcon.Shell: return Shell(p);
                case EcoIcon.Bird: return Bird(p);
                default: return Circle(p, 0.6f);
            }
        }

        /// <summary>Kahve cekirdegi: egik elips, ortasindan ince bir yarik cikarilmis.</summary>
        static float Bean(Vector2 p)
        {
            // Yapraktan ayrismasi icin sisman oval: yaprak sivri, cekirdek yuvarlak.
            var q = Rotate(p, 40f);
            float body = Ellipse(q, new Vector2(0.76f, 0.60f));
            float slit = Ellipse(q, new Vector2(0.58f, 0.10f));
            return Subtract(body, slit);
        }

        /// <summary>Su damlasi: buyuk daire ile kucuk tepe dairesinin yumusak birlesimi.</summary>
        static float Droplet(Vector2 p)
        {
            float bulb = Circle(p - new Vector2(0f, -0.18f), 0.56f);
            float tip = Circle(p - new Vector2(0f, 0.58f), 0.10f);
            return SmoothUnion(bulb, tip, 0.38f);
        }

        /// <summary>Fincan: govde + kulp halkasi + tabak.</summary>
        static float Cup(Vector2 p)
        {
            float body = RoundedBox(p - new Vector2(-0.06f, -0.02f), new Vector2(0.44f, 0.40f), 0.14f);
            float handle = Annulus(p - new Vector2(0.50f, 0.02f), 0.26f, 0.075f);
            // Kulp sadece disarida gorunsun: govdenin icine tasan kismi kesilir.
            handle = Mathf.Max(handle, -(p.x - 0.36f));
            float saucer = RoundedBox(p - new Vector2(0f, -0.60f), new Vector2(0.72f, 0.075f), 0.075f);
            return Union(Union(body, handle), saucer);
        }

        /// <summary>Yaprak: iki dairenin kesisimi (mekik) + sap + orta damar.</summary>
        static float Leaf(Vector2 p)
        {
            var q = Rotate(p, -40f);
            float a = Circle(q - new Vector2(0.58f, 0f), 0.90f);
            float b = Circle(q + new Vector2(0.58f, 0f), 0.90f);
            float blade = Mathf.Max(a, b);

            // Damar ve sap yapragin UZUN ekseni boyunca uzanir (dikey, donusten once).
            float vein = Segment(q, new Vector2(0f, -0.52f), new Vector2(0f, 0.52f), 0.035f);
            blade = Subtract(blade, vein);

            float stem = Segment(q, new Vector2(0f, -0.62f), new Vector2(0f, -0.88f), 0.05f);
            return Union(blade, stem);
        }

        /// <summary>Bal kabagi: genis elips + iki dikey oluk + sap.</summary>
        static float Pumpkin(Vector2 p)
        {
            float body = Ellipse(p - new Vector2(0f, -0.10f), new Vector2(0.82f, 0.64f));

            float grooveL = Ellipse(p - new Vector2(-0.30f, -0.10f), new Vector2(0.055f, 0.56f));
            float grooveR = Ellipse(p - new Vector2(0.30f, -0.10f), new Vector2(0.055f, 0.56f));
            body = Subtract(Subtract(body, grooveL), grooveR);

            float stem = RoundedBox(p - new Vector2(0f, 0.60f), new Vector2(0.075f, 0.16f), 0.05f);
            return Union(body, stem);
        }

        /// <summary>Atki: boyunda donen kavis + asagi sarkan iki uc.</summary>
        static float Scarf(Vector2 p)
        {
            // Ust kavis (boyun cevresi)
            var loopCenter = new Vector2(0f, 0.28f);
            float loop = Annulus(p - loopCenter, 0.34f, 0.115f);
            loop = Mathf.Max(loop, loopCenter.y - p.y);   // sadece ust yari

            // Kavisin uclarindan sarkan iki serit
            float tailL = RoundedBox(Rotate(p - new Vector2(-0.26f, -0.26f), 7f), new Vector2(0.125f, 0.55f), 0.11f);
            float tailR = RoundedBox(Rotate(p - new Vector2(0.26f, -0.30f), -7f), new Vector2(0.125f, 0.50f), 0.11f);

            return Union(Union(loop, tailL), tailR);
        }

        /// <summary>Dalga: ust uste iki sinus bandi.</summary>
        static float Wave(Vector2 p)
        {
            float top = SineBand(p, 0.26f, 0.20f, 3.4f, 0.115f);
            float bottom = SineBand(p, -0.30f, 0.20f, 3.4f, 0.115f);
            // Kenarlardan kirp: bantlar kart disina tasmis gibi durmasin.
            float clip = Mathf.Abs(p.x) - 0.86f;
            return Mathf.Max(Union(top, bottom), clip);
        }

        /// <summary>Deniz kabugu: yarim daire yelpaze + radyal oluklar + mentese.</summary>
        static float Shell(Vector2 p)
        {
            var c = new Vector2(0f, -0.42f);
            float fan = Circle(p - c, 0.86f);
            fan = Mathf.Max(fan, -(p.y - c.y));   // ust yariyi tut

            // Merkezden disari acilan oluklar
            for (int i = -1; i <= 1; i++)
            {
                float angle = i * 32f;
                var dir = Rotate(new Vector2(0f, 1f), angle);
                float groove = Segment(p - c, dir * 0.18f, dir * 0.92f, 0.045f);
                fan = Subtract(fan, groove);
            }

            float hinge = Circle(p - c, 0.14f);
            return Union(fan, hinge);
        }

        /// <summary>Marti: asagi bakan iki yay, klasik "m" silueti.</summary>
        static float Bird(Vector2 p)
        {
            // Iki yayin UST yarisi tutulur: ortada hafif cukur, uclarda dusen kanatlar.
            const float baseY = -0.06f;

            float left = Annulus(p - new Vector2(-0.42f, baseY), 0.46f, 0.085f);
            left = Mathf.Max(left, baseY - p.y);

            float right = Annulus(p - new Vector2(0.42f, baseY), 0.46f, 0.085f);
            right = Mathf.Max(right, baseY - p.y);

            return Union(left, right);
        }

        // ---------------------------------------------------------------- SDF araclari

        static float Circle(Vector2 p, float r) => p.magnitude - r;

        static float Ellipse(Vector2 p, Vector2 radii)
        {
            // Yaklasik ama ikon olcusunde yeterli.
            var scaled = new Vector2(p.x / radii.x, p.y / radii.y);
            return (scaled.magnitude - 1f) * Mathf.Min(radii.x, radii.y);
        }

        static float RoundedBox(Vector2 p, Vector2 halfExtents, float radius)
        {
            float qx = Mathf.Abs(p.x) - (halfExtents.x - radius);
            float qy = Mathf.Abs(p.y) - (halfExtents.y - radius);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b, float radius)
        {
            Vector2 pa = p - a;
            Vector2 ba = b - a;
            float denominator = Vector2.Dot(ba, ba);
            float h = denominator > 0f ? Mathf.Clamp01(Vector2.Dot(pa, ba) / denominator) : 0f;
            return (pa - ba * h).magnitude - radius;
        }

        static float Annulus(Vector2 p, float radius, float thickness) =>
            Mathf.Abs(p.magnitude - radius) - thickness;

        static float SineBand(Vector2 p, float yOffset, float amplitude, float frequency, float thickness)
        {
            float curve = amplitude * Mathf.Sin(p.x * frequency);
            float slope = amplitude * frequency * Mathf.Cos(p.x * frequency);
            // Dikey farki egime bolerek gercek mesafeye yaklasiyoruz.
            return Mathf.Abs(p.y - yOffset - curve) / Mathf.Sqrt(1f + slope * slope) - thickness;
        }

        static float Union(float a, float b) => Mathf.Min(a, b);

        static float Subtract(float shape, float hole) => Mathf.Max(shape, -hole);

        static float SmoothUnion(float a, float b, float k)
        {
            float h = Mathf.Clamp01(0.5f + 0.5f * (b - a) / k);
            return Mathf.Lerp(b, a, h) - k * h * (1f - h);
        }

        static Vector2 Rotate(Vector2 p, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(p.x * cos - p.y * sin, p.x * sin + p.y * cos);
        }
    }
}
