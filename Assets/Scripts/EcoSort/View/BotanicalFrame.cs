using System.Collections.Generic;
using EcoSort.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace EcoSort.View
{
    /// <summary>
    /// Ekranin cercevesini saran proseduel botanik dekor: iki kenardan yukselen
    /// sarmasiklar, uzerlerindeki yapraklar ve kucuk cicekler, artı zemine
    /// dagilmis pirilti.
    ///
    /// Sanat asseti gerektirmez; govde parcalari <see cref="EcoUi"/>, yaprak ve
    /// pirilti siluetleri <see cref="IconFactory"/> ile uretilir.
    ///
    /// Iki katmanda calisir:
    ///   arka  -> sarmasiklar + pirilti, oyunun ARKASINDA
    ///   on    -> kose yapraklari, oyunun ONUNDE ama soluk
    ///
    /// Hicbir parca raycast almaz: dekor tek bir dokunusu bile yutmaz.
    ///
    /// Animasyon tek bir Update dongusunde yurur; her parca kendi faziyla
    /// salindigi icin ruzgar dogal gorunur, hicbir coroutine/tween harcanmaz.
    /// </summary>
    [DisallowMultipleComponent]
    public class BotanicalFrame : MonoBehaviour
    {
        [Header("Sarmasik")]
        [Tooltip("Bir sarmasigin govde parcasi sayisi. Cok olursa egri yumusar.")]
        [SerializeField, Range(4, 24)] int _segmentsPerVine = 16;
        [Tooltip("Sarmasigin genel saydamligi.")]
        [SerializeField, Range(0f, 1f)] float _vineOpacity = 0.9f;

        [Header("Pirilti")]
        [SerializeField, Range(0, 40)] int _sparkleCount = 18;

        [Header("Salinim")]
        [Tooltip("Yapraklarin saga sola egilme genligi (derece).")]
        [SerializeField, Range(0f, 20f)] float _swayAmplitude = 7f;
        [Tooltip("Salinim hizi. Dusuk tut: dekor dikkat dagitmasin.")]
        [SerializeField, Range(0.1f, 3f)] float _swaySpeed = 0.7f;

        /// <summary>Kendi ekseninde sallanan bir parca (yaprak, cicek).</summary>
        class Swayer
        {
            public RectTransform Rect;
            public float BaseAngle;
            public float Amplitude;
            public float Speed;
            public float Phase;
        }

        /// <summary>Alfasi ve olcegi nefes alan bir parca (pirilti).</summary>
        class Twinkler
        {
            public Graphic Target;
            public RectTransform Rect;
            public float BaseAlpha;
            public float BaseScale;
            public float Speed;
            public float Phase;
        }

        readonly List<Swayer> _sway = new List<Swayer>();
        readonly List<Twinkler> _twinkle = new List<Twinkler>();

        System.Random _random;
        int _leafIndex;

        // ---------------------------------------------------------------- kurulum

        /// <summary>
        /// Dekoru kurar.
        /// </summary>
        /// <param name="backLayer">Oyunun arkasinda cizilen katman.</param>
        /// <param name="frontLayer">Oyunun onunde cizilen katman (bos gecilebilir).</param>
        /// <param name="width">Kaplanacak alanin genisligi (piksel).</param>
        /// <param name="height">Kaplanacak alanin yuksekligi (piksel).</param>
        /// <param name="seed">Sabit tohum: her acilista ayni kompozisyon.</param>
        public void Build(RectTransform backLayer, RectTransform frontLayer,
            float width, float height, int seed = 20260905)
        {
            _random = new System.Random(seed);

            if (backLayer != null)
            {
                BuildVine(backLayer, -1f, width, height);
                BuildVine(backLayer, +1f, width, height);
                BuildSparkles(backLayer, width, height);
            }

            if (frontLayer == null) return;

            BuildCornerCluster(frontLayer, -1f, +1f, width, height);
            BuildCornerCluster(frontLayer, +1f, +1f, width, height);
            BuildCornerCluster(frontLayer, -1f, -1f, width, height);
            BuildCornerCluster(frontLayer, +1f, -1f, width, height);
        }

        // ---------------------------------------------------------------- sarmasik

        /// <summary>
        /// Bir kenar boyunca asagidan yukari uzanan sarmasik.
        /// sign: -1 sol kenar, +1 sag kenar.
        /// </summary>
        void BuildVine(RectTransform parent, float sign, float width, float height)
        {
            var root = EcoUi.Rect(sign < 0 ? "Vine_Left" : "Vine_Right", parent,
                new Vector2(width, height));

            float phase = Random01() * Mathf.PI * 2f;
            float thickness = width * 0.0095f;

            var previous = VinePoint(sign, 0f, width, height, phase);

            for (int i = 1; i <= _segmentsPerVine; i++)
            {
                var point = VinePoint(sign, i / (float)_segmentsPerVine, width, height, phase);

                var delta = point - previous;
                float length = delta.magnitude;

                // Parca dik duruyor (yuksekligi = uzunluk); +Y'yi delta yonune cevir.
                float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;

                var stem = EcoUi.Panel("Stem_" + i, root,
                    new Vector2(thickness, length + thickness),
                    Mathf.RoundToInt(thickness * 0.5f),
                    EcoPalette.VineStem.WithAlpha(_vineOpacity));

                stem.rectTransform.anchoredPosition = (previous + point) * 0.5f;
                stem.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);

                // Her bogumda bir yaprak, cicekler daha seyrek.
                AddLeaf(root, point, sign, width, i);
                if (i % 4 == 2) AddBlossom(root, point, sign, width);

                previous = point;
            }
        }

        /// <summary>
        /// Sarmasigin p (0 = alt, 1 = ust) noktasindaki konumu. Govde kenara
        /// yapisik durmaz; ice dogru yumusak bir yilan cizer.
        /// </summary>
        static Vector2 VinePoint(float sign, float p, float width, float height, float phase)
        {
            // Iki farkli frekans: tek sinus "dalga" gibi duruyordu, ikincisi
            // egriyi duzensizlestirip organik bir sarmasik hissi veriyor.
            float inset = width * (0.058f
                + 0.036f * Mathf.Sin(p * 4.1f + phase)
                + 0.014f * Mathf.Sin(p * 9.7f + phase * 1.7f));

            return new Vector2(sign * (width * 0.5f - inset), -height * 0.5f + p * height);
        }

        /// <summary>Govdenin uzerinde, ekranin ICINE dogru bakan bir yaprak.</summary>
        void AddLeaf(RectTransform root, Vector2 point, float sign, float width, int node)
        {
            float size = width * (0.048f + Random01() * 0.042f);

            // Bogumlar sirayla yukari ve asagi bakar: duz bir tarak yerine
            // dogal bir dallanma cikar.
            float spread = node % 2 == 0
                ? 28f + Random01() * 34f
                : 74f + Random01() * 40f;

            // Sol sarmasikta yaprak saga (negatif donus), sagda sola bakar.
            float baseAngle = sign < 0 ? -spread : spread;

            var leaf = EcoUi.Icon("Leaf_" + _leafIndex, root, Vector2.one * size,
                IconFactory.GetSprite(EcoIcon.Leaf),
                EcoPalette.LeafTone(_leafIndex).WithAlpha(_vineOpacity));

            // Sapindan sallansin diye pivot yapragin dibine cekilir.
            leaf.rectTransform.pivot = new Vector2(0.5f, 0.08f);
            leaf.rectTransform.anchoredPosition = point;

            RegisterSway(leaf.rectTransform, baseAngle, _swayAmplitude);
            _leafIndex++;
        }

        /// <summary>Bes yapraklikli kucuk cicek: renk lekesi ve botanik his.</summary>
        void AddBlossom(RectTransform root, Vector2 point, float sign, float width)
        {
            float size = width * 0.048f;

            var flower = EcoUi.Rect("Blossom", root, Vector2.one * size);
            flower.anchoredPosition = point + new Vector2(-sign * size * 0.5f, size * 0.3f);

            var petalColor = _random.Next(2) == 0
                ? EcoPalette.BlossomPetal
                : EcoPalette.BlossomPetalAlt;

            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f;
                var petal = EcoUi.Disc("Petal_" + i, flower, size * 0.54f,
                    petalColor.WithAlpha(_vineOpacity));
                petal.rectTransform.anchoredPosition =
                    new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * size * 0.27f;
            }

            EcoUi.Disc("Heart", flower, size * 0.32f, EcoPalette.BlossomHeart.WithAlpha(_vineOpacity));

            RegisterSway(flower, 0f, _swayAmplitude * 0.55f);
        }

        // ---------------------------------------------------------------- kose demetleri

        /// <summary>
        /// Kosede duran, ekranin merkezine dogru acilan birkac yaprak.
        /// On katmanda ve soluk cizilir: cerceve hissi verir, okunurlugu bozmaz.
        /// </summary>
        void BuildCornerCluster(RectTransform parent, float signX, float signY,
            float width, float height)
        {
            var root = EcoUi.Rect("Corner", parent, new Vector2(width, height));
            var origin = new Vector2(signX * width * 0.5f, signY * height * 0.5f);

            // Kosden merkeze bakan yon; yaprak bu yone acilir.
            float toCenter = Mathf.Atan2(-signY, -signX) * Mathf.Rad2Deg - 90f;

            // Kucuk ve koseye yapisik: HUD metinlerinin (baslik, sayac hapi,
            // alt cubuk) uzerine tasmasin.
            int count = 3 + _random.Next(2);
            for (int i = 0; i < count; i++)
            {
                float size = width * (0.052f + Random01() * 0.040f);

                var leaf = EcoUi.Icon("CornerLeaf_" + i, root, Vector2.one * size,
                    IconFactory.GetSprite(EcoIcon.Leaf),
                    EcoPalette.LeafTone(_leafIndex).WithAlpha(0.5f));

                leaf.rectTransform.pivot = new Vector2(0.5f, 0.05f);
                leaf.rectTransform.anchoredPosition = origin + new Vector2(
                    -signX * width * (0.002f + Random01() * 0.020f),
                    -signY * height * (0.001f + Random01() * 0.012f));

                RegisterSway(leaf.rectTransform,
                    toCenter + (Random01() - 0.5f) * 70f,
                    _swayAmplitude * 0.8f);

                _leafIndex++;
            }
        }

        // ---------------------------------------------------------------- pirilti

        void BuildSparkles(RectTransform parent, float width, float height)
        {
            var sprite = IconFactory.GetSprite(EcoIcon.Sparkle);

            for (int i = 0; i < _sparkleCount; i++)
            {
                float size = width * (0.016f + Random01() * 0.026f);
                var color = i % 3 == 0 ? EcoPalette.SparkleMint : EcoPalette.SparkleWarm;

                var spark = EcoUi.Icon("Sparkle_" + i, parent, Vector2.one * size, sprite,
                    color.WithAlpha(0.75f));

                spark.rectTransform.anchoredPosition = new Vector2(
                    (Random01() - 0.5f) * width * 0.96f,
                    (Random01() - 0.5f) * height * 0.96f);

                _twinkle.Add(new Twinkler
                {
                    Target = spark,
                    Rect = spark.rectTransform,
                    BaseAlpha = 0.75f,
                    BaseScale = 1f,
                    Speed = 0.6f + Random01() * 1.4f,
                    Phase = Random01() * Mathf.PI * 2f
                });
            }
        }

        // ---------------------------------------------------------------- animasyon

        void RegisterSway(RectTransform rect, float baseAngle, float amplitude)
        {
            rect.localRotation = Quaternion.Euler(0f, 0f, baseAngle);

            _sway.Add(new Swayer
            {
                Rect = rect,
                BaseAngle = baseAngle,
                Amplitude = amplitude,
                Speed = _swaySpeed * (0.7f + Random01() * 0.7f),
                Phase = Random01() * Mathf.PI * 2f
            });
        }

        void Update()
        {
            // Zaman olceginden bagimsiz: oyun duraklatilsa bile ruzgar esmeye devam eder.
            float time = Time.unscaledTime;

            for (int i = 0; i < _sway.Count; i++)
            {
                var s = _sway[i];
                if (s.Rect == null) continue;

                float angle = s.BaseAngle + Mathf.Sin(time * s.Speed + s.Phase) * s.Amplitude;
                s.Rect.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            for (int i = 0; i < _twinkle.Count; i++)
            {
                var t = _twinkle[i];
                if (t.Target == null) continue;

                // 0..1 arasi yumusak nabiz.
                float k = (Mathf.Sin(time * t.Speed + t.Phase) + 1f) * 0.5f;

                var color = t.Target.color;
                color.a = t.BaseAlpha * (0.12f + 0.88f * k);
                t.Target.color = color;

                t.Rect.localScale = Vector3.one * (t.BaseScale * (0.65f + 0.55f * k));
            }
        }

        float Random01() => (float)_random.NextDouble();
    }
}
