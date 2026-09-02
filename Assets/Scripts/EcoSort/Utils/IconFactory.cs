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
        Bird,      // marti

        // --- 5 kategoriye gecisle eklenenler (siralama bozulmasin diye sona eklendi)
        Umbrella,     // plaj semsiyesi
        Necklace,     // kolye
        Bracelet,     // bileklik
        Ring,         // yuzuk
        Gamepad,      // oyun kolu
        Headphones,   // kulaklik
        Keyboard,     // klavye
        Sparkle       // slot amblemi / genel yildiz
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

        /// <summary>
        /// Elle cizilmis kart gorseli olmayan kartlar icin "kendi zeminini tasiyan"
        /// bir kart yuzu uretir: yuvarlak kare, kategori renginde dikey gradyan,
        /// ic kenar isigi ve uzerinde acik renkli siluet.
        ///
        /// Boylece gecici kartlar, sanatcinin cizdigi kartlarla ayni siluete oturur;
        /// gercek gorsel gelince CardData.Artwork doldurulur ve bu uretim devre disi kalir.
        /// </summary>
        public static Sprite GetCardTile(EcoIcon icon, Color accent, int size = 256)
        {
            string key = "tile_" + icon + "_" + ColorKey(accent) + "_" + size;
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = CreateTileTexture(icon, accent, size);
            var sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect);
            sprite.hideFlags = HideFlags.HideAndDontSave;

            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Kart yuzunu doku olarak uretir (PNG'ye yazmak isteyen editor kodu icin).</summary>
        public static Texture2D CreateTileTexture(EcoIcon icon, Color accent, int size = 256)
        {
            size = Mathf.Clamp(size, 32, 1024);

            var texture = NewTexture(size);
            var pixels = new Color32[size * size];
            float half = size * 0.5f;

            // Zemin gradyani: ustte acilmis, altta koyulmus kategori rengi.
            var top = Color.Lerp(accent, Color.white, 0.46f);
            var bottom = Color.Lerp(accent, Color.black, 0.16f);
            var silhouette = Color.Lerp(accent, Color.white, 0.94f);
            var silhouetteShadow = Color.Lerp(accent, Color.black, 0.42f);

            // Ikonu karenin icine sigdirmak icin kucult (1 = tam kenar).
            const float iconScale = 1.42f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    var p = new Vector2((x + 0.5f - half) / half, (y + 0.5f - half) / half);

                    // --- kart govdesi (yuvarlak kare)
                    float tileDistance = RoundedBox(p, new Vector2(0.965f, 0.965f), 0.30f);
                    float tileAlpha = Mathf.Clamp01(0.5f - tileDistance * half);
                    if (tileAlpha <= 0f)
                    {
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    var color = Color.Lerp(bottom, top, (p.y + 1f) * 0.5f);

                    // --- ic kenar isigi: govdenin hemen icinde ince bir halka
                    float rimPixels = tileDistance * half;
                    float rim = Mathf.Clamp01(1f - Mathf.Abs(rimPixels + size * 0.028f) / (size * 0.020f));
                    color = Color.Lerp(color, Color.white, rim * 0.30f);

                    // --- siluetin altina yumusak golge: ikon zeminden kalksin
                    float shadowDistance = Evaluate(icon, (p - new Vector2(0f, -0.045f)) * iconScale);
                    float shadowAlpha = Mathf.Clamp01(0.5f - shadowDistance * half / iconScale);
                    color = Color.Lerp(color, silhouetteShadow, shadowAlpha * 0.30f);

                    // --- siluet
                    float iconDistance = Evaluate(icon, p * iconScale);
                    float iconAlpha = Mathf.Clamp01(0.5f - iconDistance * half / iconScale);
                    color = Color.Lerp(color, silhouette, iconAlpha);

                    color.a = tileAlpha;
                    pixels[y * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            return texture;
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
                case EcoIcon.Umbrella: return Umbrella(p);
                case EcoIcon.Necklace: return Necklace(p);
                case EcoIcon.Bracelet: return Bracelet(p);
                case EcoIcon.Ring: return Ring(p);
                case EcoIcon.Gamepad: return Gamepad(p);
                case EcoIcon.Headphones: return Headphones(p);
                case EcoIcon.Keyboard: return Keyboard(p);
                case EcoIcon.Sparkle: return Sparkle(p);
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

        /// <summary>Plaj semsiyesi: kubbe + fistolu etek + direk.</summary>
        static float Umbrella(Vector2 p)
        {
            var c = new Vector2(0f, -0.16f);

            // Kubbe: genis elipsin ust yarisi.
            float canopy = Ellipse(p - c, new Vector2(0.88f, 0.60f));
            canopy = Mathf.Max(canopy, c.y - p.y);

            // Etek: alt kenardan cikarilan daireler klasik semsiye fistosunu verir.
            for (int i = -2; i <= 2; i++)
                canopy = Subtract(canopy, Circle(p - new Vector2(i * 0.355f, c.y - 0.03f), 0.165f));

            float pole = RoundedBox(p - new Vector2(0f, -0.54f), new Vector2(0.048f, 0.40f), 0.045f);
            float knob = Circle(p - new Vector2(0f, 0.46f), 0.08f);

            return Union(Union(canopy, pole), knob);
        }

        /// <summary>Kolye: acik ust uclu zincir yayi + damla ucluk.</summary>
        static float Necklace(Vector2 p)
        {
            var c = new Vector2(0f, 0.32f);

            float chain = Annulus(p - c, 0.60f, 0.055f);
            chain = Mathf.Max(chain, p.y - (c.y + 0.12f));   // ust ucu acik biraksin

            float bulb = Circle(p - new Vector2(0f, -0.46f), 0.20f);
            float tip = Circle(p - new Vector2(0f, -0.24f), 0.065f);
            float pendant = SmoothUnion(bulb, tip, 0.17f);

            return Union(chain, pendant);
        }

        /// <summary>
        /// Bileklik: halka seklinde dizilmis ayri boncuklar.
        /// Yuzukten ilk bakista ayrilsin diye duz bant yerine boncuk zinciri.
        /// </summary>
        static float Bracelet(Vector2 p)
        {
            const int beadCount = 10;
            const float ringRadius = 0.60f;

            float shape = 10f;
            for (int i = 0; i < beadCount; i++)
            {
                float angle = i * (360f / beadCount) * Mathf.Deg2Rad;
                var center = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * ringRadius;
                // Bir boncuk digerlerinden buyuk: takiya odak noktasi verir.
                float radius = i == beadCount / 4 ? 0.24f : 0.155f;
                shape = Union(shape, Circle(p - center, radius));
            }

            return shape;
        }

        /// <summary>Yuzuk: ince bant + tepesinde belirgin baklava kesim tas.</summary>
        static float Ring(Vector2 p)
        {
            // Bant, tasin altinda kalsin diye asagi kaydirilmis ve inceltilmis.
            float band = Annulus(p - new Vector2(0f, -0.34f), 0.46f, 0.075f);

            // Tas: 45 derece dondurulmus buyuk baklava.
            var q = Rotate(p - new Vector2(0f, 0.42f), 45f);
            float gem = RoundedBox(q, new Vector2(0.30f, 0.30f), 0.05f);

            // Tasi tutan tirnaklar: bantla tasi gorsel olarak birlestirir.
            float prongL = Segment(p, new Vector2(-0.26f, 0.16f), new Vector2(-0.34f, -0.10f), 0.045f);
            float prongR = Segment(p, new Vector2(0.26f, 0.16f), new Vector2(0.34f, -0.10f), 0.045f);

            return Union(Union(band, gem), Union(prongL, prongR));
        }

        /// <summary>Oyun kolu: govde + iki tutamak, uzerinde d-pad ve tus oyuklari.</summary>
        static float Gamepad(Vector2 p)
        {
            float body = RoundedBox(p - new Vector2(0f, 0.04f), new Vector2(0.52f, 0.28f), 0.20f);
            float gripL = Circle(p - new Vector2(-0.54f, -0.12f), 0.29f);
            float gripR = Circle(p - new Vector2(0.54f, -0.12f), 0.29f);

            float shape = SmoothUnion(SmoothUnion(body, gripL, 0.20f), gripR, 0.20f);

            // Sol tarafta arti seklinde yon tusu.
            var dpad = new Vector2(-0.38f, 0.06f);
            float horizontal = RoundedBox(p - dpad, new Vector2(0.19f, 0.058f), 0.028f);
            float vertical = RoundedBox(p - dpad, new Vector2(0.058f, 0.19f), 0.028f);
            shape = Subtract(shape, Union(horizontal, vertical));

            // Sagda iki yuvarlak aksiyon tusu.
            shape = Subtract(shape, Circle(p - new Vector2(0.30f, 0.17f), 0.085f));
            shape = Subtract(shape, Circle(p - new Vector2(0.49f, -0.01f), 0.085f));

            return shape;
        }

        /// <summary>Kulaklik: ust kemer yayi + iki kulak yastigi.</summary>
        static float Headphones(Vector2 p)
        {
            const float baseY = 0.0f;

            float band = Annulus(p - new Vector2(0f, baseY), 0.62f, 0.085f);
            band = Mathf.Max(band, baseY - p.y);   // sadece ust yari

            float cupL = RoundedBox(p - new Vector2(-0.62f, -0.30f), new Vector2(0.165f, 0.28f), 0.145f);
            float cupR = RoundedBox(p - new Vector2(0.62f, -0.30f), new Vector2(0.165f, 0.28f), 0.145f);

            return Union(band, Union(cupL, cupR));
        }

        /// <summary>Klavye: govdeden oyulmus iki sira tus + bosluk cubugu.</summary>
        static float Keyboard(Vector2 p)
        {
            float body = RoundedBox(p, new Vector2(0.90f, 0.50f), 0.13f);

            // Uzak baslangic degeri: Union(min) ile ilk gercek tus kazansin.
            float keys = 10f;

            for (int row = 0; row < 2; row++)
            {
                float y = 0.26f - row * 0.25f;
                for (int col = 0; col < 5; col++)
                {
                    float x = -0.60f + col * 0.30f;
                    keys = Union(keys, RoundedBox(p - new Vector2(x, y), new Vector2(0.105f, 0.08f), 0.032f));
                }
            }

            keys = Union(keys, RoundedBox(p - new Vector2(0f, -0.26f), new Vector2(0.42f, 0.08f), 0.032f));

            return Subtract(body, keys);
        }

        /// <summary>Dort kollu parilti: kutlama ve slot amblemi icin.</summary>
        static float Sparkle(Vector2 p)
        {
            // Iki elipsin birlesimi carpi yerine yumusak bir yildiz verir.
            float vertical = Ellipse(p, new Vector2(0.17f, 0.92f));
            float horizontal = Ellipse(p, new Vector2(0.92f, 0.17f));
            float core = Circle(p, 0.20f);

            return SmoothUnion(SmoothUnion(vertical, horizontal, 0.10f), core, 0.10f);
        }

        // ---------------------------------------------------------------- doku yardimcilari

        static Texture2D NewTexture(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                // Onbellekteki sprite'lar sahne degisiminde bosaltilmasin.
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        /// <summary>Onbellek anahtari icin rengin kisa, kararli bir temsili.</summary>
        static string ColorKey(Color color)
        {
            var c = (Color32)color;
            return $"{c.r:x2}{c.g:x2}{c.b:x2}";
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
