using System.Collections;
using System.Collections.Generic;
using EcoSort.Data;
using EcoSort.Utils;
using EcoSort.View;
using UnityEngine;
using UnityEngine.UI;

namespace EcoSort.Core
{
    /// <summary>
    /// Panoyu calisma zamaninda kuran mobil (portrait) duzen.
    ///
    /// Yapisi:
    ///   Canvas
    ///     +- Background   : gradyan + slot seridinin arkasinda yumusak isik
    ///     +- SafeArea     : SafeAreaFitter
    ///         +- Header   : baslik, durum metni, tamamlanan grup sayaci
    ///         +- SlotRow  : SlotManager -> HorizontalLayoutGroup + 5 kategori slotu
    ///         +- CardTray : CardTray    -> GridLayoutGroup + 15 kart yuvasi
    ///     +- DragLayer    : suruklenen kart ve konfeti burada cizilir
    ///
    /// Tum olculer guvenli alanin genisligine oranla hesaplanir; 9:16'dan 9:21'e
    /// kadar farkli telefon oranlarinda ayni duzen korunur.
    ///
    /// Kurulum: sahnede Canvas + EventSystem olsun; bu bileseni CategoryManager ile
    /// ayni GameObject'e ekleyip _categories listesini doldur.
    /// </summary>
    [RequireComponent(typeof(CategoryManager))]
    public class SandboxBoard : MonoBehaviour
    {
        [Header("Icerik")]
        [Tooltip("Ust seritteki kategoriler (soldan saga). Bes kategori beklenir.")]
        [SerializeField] List<CategoryData> _categories = new List<CategoryData>();

        [Header("Sahne")]
        [Tooltip("Bos birakilirsa sahnedeki ilk Canvas kullanilir.")]
        [SerializeField] Canvas _canvas;

        [Header("Duzen (guvenli alan genisligine orani)")]
        [Tooltip("Kenar bosluklari.")]
        [SerializeField, Range(0.02f, 0.10f)] float _paddingRatio = 0.040f;
        [Tooltip("Ogeler arasi bosluk.")]
        [SerializeField, Range(0.01f, 0.06f)] float _gapRatio = 0.020f;
        [Tooltip("Alt tepside satirdaki kart sayisi.")]
        [SerializeField, Range(2, 6)] int _cardColumns = 4;

        [Header("Kart Gorunumu")]
        [Tooltip("Ikonun altinda kart adini da goster. Ikonlar tek basina okunmuyorsa ac.")]
        [SerializeField] bool _showCardNames;

        [Tooltip("Ikon kendi cercevesini/zeminini tasiyorsa ac: arkadaki kategori renkli disk " +
                 "cizilmez ve ikon karti neredeyse tamamen doldurur. Siluet ikonlarda kapali birak.")]
        [SerializeField] bool _iconHasOwnBackdrop = true;

        [Header("Test")]
        [SerializeField] bool _shuffle = true;
        [SerializeField] int _randomSeed;

        CategoryManager _manager;
        SlotManager _slotManager;
        CardTray _tray;

        RectTransform _safeArea;
        RectTransform _dragLayer;

        Text _statusLabel;
        Text _counterLabel;
        CanvasGroup _bannerGroup;
        Text _bannerLabel;

        // Olculer (piksel, canvas referans birimi)
        float _safeWidth;
        float _safeHeight;
        float _padding;
        float _gap;

        // ---------------------------------------------------------------- yasam dongusu

        void Awake()
        {
            _manager = GetComponent<CategoryManager>();
        }

        IEnumerator Start()
        {
            if (_canvas == null) _canvas = FindAnyObjectByType<Canvas>();
            if (_canvas == null)
            {
                Debug.LogError("[EcoSort] Sahnede Canvas yok; pano kurulamiyor.", this);
                yield break;
            }

            if (_categories == null || _categories.Count == 0)
            {
                Debug.LogError("[EcoSort] SandboxBoard'a en az bir CategoryData atanmali.", this);
                yield break;
            }

            // CanvasScaler referans cozunurlugu ilk karede uygulanir. Start icinde
            // canvasRect.rect okunursa tasarim zamanindaki (cogu zaman 0) olcu gelir
            // ve tum duzen yanlis olculenir. Bir kare bekleyip kesin olcuyu aliyoruz.
            yield return null;
            Canvas.ForceUpdateCanvases();

            Measure();
            BuildBackground();
            BuildRoots();

            float cursorY = BuildHeader();
            cursorY = BuildSlotRow(cursorY);
            BuildTray(cursorY);
            BuildBanner();

            Subscribe();
            SetStatus("Kartı doğru kategoriye sürükle");
        }

        void OnDestroy()
        {
            if (_manager == null) return;
            _manager.MatchRejected -= HandleRejected;
            _manager.ComboChanged -= HandleCombo;

            if (_slotManager == null) return;
            _slotManager.OnCategoryProgress -= HandleProgress;
            _slotManager.OnCategoryCompleted -= HandleCompleted;
            _slotManager.OnAllCategoriesCompleted -= HandleAllCompleted;
        }

        // ---------------------------------------------------------------- olculendirme

        void Measure()
        {
            var canvasRect = (RectTransform)_canvas.transform;
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            // Emniyet: Canvas henuz olculenmediyse ekran olcusune dus.
            if (canvasWidth <= 1f) canvasWidth = Screen.width;
            if (canvasHeight <= 1f) canvasHeight = Screen.height;

            // Guvenli alani canvas birimine cevir: centik/gesture cubugu duzeni bozmasin.
            float safeRatioX = Screen.width > 0 ? Screen.safeArea.width / Screen.width : 1f;
            float safeRatioY = Screen.height > 0 ? Screen.safeArea.height / Screen.height : 1f;

            _safeWidth = canvasWidth * safeRatioX;
            _safeHeight = canvasHeight * safeRatioY;

            _padding = _safeWidth * _paddingRatio;
            _gap = _safeWidth * _gapRatio;
        }

        // ---------------------------------------------------------------- zemin

        void BuildBackground()
        {
            var canvasRect = (RectTransform)_canvas.transform;

            var root = EcoUi.FullRect("Background", canvasRect);
            root.SetAsFirstSibling();

            var gradient = root.gameObject.AddComponent<Image>();
            gradient.sprite = UiSpriteFactory.VerticalGradient(EcoPalette.BackgroundBottom, EcoPalette.BackgroundTop);
            gradient.type = Image.Type.Simple;
            gradient.raycastTarget = false;

            // Ust seridin arkasinda yumusak bir sahne isigi.
            var glow = EcoUi.Rect("TopGlow", root, new Vector2(_safeWidth * 1.25f, _safeWidth * 1.25f));
            glow.anchoredPosition = new Vector2(0f, _safeHeight * 0.28f);
            var glowImage = glow.gameObject.AddComponent<Image>();
            glowImage.sprite = UiSpriteFactory.RadialGlow();
            // Cok hafif: gradyani yikamadan sadece ust seride odak versin.
            glowImage.color = new Color(1f, 1f, 1f, 0.30f);
            glowImage.raycastTarget = false;

            BuildFloatingDecor(root);
        }

        /// <summary>
        /// Zemine dagilmis, cok soluk daireler. Duz gradyani kirar ve
        /// ekrana derinlik katar; hicbir etkilesime karismaz.
        /// </summary>
        void BuildFloatingDecor(RectTransform root)
        {
            // Sabit tohum: her acilista ayni kompozisyon, yani "rastgele ama tasarlanmis".
            var random = new System.Random(20260902);

            for (int i = 0; i < 9; i++)
            {
                float diameter = _safeWidth * (float)(0.08f + random.NextDouble() * 0.22f);
                var dot = EcoUi.Disc($"Decor_{i}", root, diameter,
                    EcoPalette.InkMuted.WithAlpha(0.055f));

                dot.rectTransform.anchoredPosition = new Vector2(
                    (float)(random.NextDouble() - 0.5) * _safeWidth * 1.05f,
                    (float)(random.NextDouble() - 0.5) * _safeHeight * 1.05f);
            }
        }

        void BuildRoots()
        {
            var canvasRect = (RectTransform)_canvas.transform;

            _safeArea = EcoUi.FullRect("SafeArea", canvasRect);
            _safeArea.gameObject.AddComponent<SafeAreaFitter>();

            // DragLayer guvenli alanin disinda ve en sonda: suruklenen kart her seyin uzerinde.
            _dragLayer = EcoUi.FullRect("DragLayer", canvasRect);
            _dragLayer.SetAsLastSibling();

            // Katmanin kendisi dokunuslari yutmasin.
            var group = _dragLayer.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;

            CardView.SetDragLayer(_dragLayer);
        }

        // ---------------------------------------------------------------- baslik

        /// <summary>Baslik blogunu kurar ve bir sonraki ogenin ust sinirini dondurur.</summary>
        float BuildHeader()
        {
            float cursorY = _padding;

            float titleSize = _safeWidth * 0.062f;
            float statusSize = _safeWidth * 0.033f;
            float counterSize = _safeWidth * 0.036f;

            float titleHeight = titleSize * 1.4f;
            float statusHeight = statusSize * 1.7f;

            // --- baslik ve sayac ayni satirda
            var title = EcoUi.Label("Title", _safeArea,
                new Vector2(_safeWidth - _padding * 2f, titleHeight),
                Mathf.RoundToInt(titleSize), EcoPalette.Ink, FontStyle.Bold, TextAnchor.MiddleLeft);
            title.text = "Eco-Sort";
            PlaceFromTop(title.rectTransform, cursorY);

            var counterPill = EcoUi.Panel("CounterPill", _safeArea,
                new Vector2(_safeWidth * 0.26f, titleHeight * 0.78f),
                Mathf.RoundToInt(titleHeight * 0.39f), EcoPalette.CardFace.WithAlpha(0.85f));
            PlaceFromTop(counterPill.rectTransform, cursorY + titleHeight * 0.11f,
                (_safeWidth - _padding * 2f - _safeWidth * 0.26f) * 0.5f);

            _counterLabel = EcoUi.Label("Counter", counterPill.rectTransform,
                new Vector2(_safeWidth * 0.26f, titleHeight * 0.78f),
                Mathf.RoundToInt(counterSize), EcoPalette.Ink, FontStyle.Bold);
            _counterLabel.text = $"0 / {_categories.Count} grup";

            cursorY += titleHeight + _gap * 0.3f;

            // --- durum satiri
            _statusLabel = EcoUi.Label("Status", _safeArea,
                new Vector2(_safeWidth - _padding * 2f, statusHeight),
                Mathf.RoundToInt(statusSize), EcoPalette.InkMuted, FontStyle.Normal, TextAnchor.MiddleLeft);
            PlaceFromTop(_statusLabel.rectTransform, cursorY);

            cursorY += statusHeight + _gap * 1.4f;
            return cursorY;
        }

        // ---------------------------------------------------------------- slot seridi

        float BuildSlotRow(float cursorY)
        {
            _slotManager = gameObject.GetComponent<SlotManager>();
            if (_slotManager == null) _slotManager = gameObject.AddComponent<SlotManager>();

            _slotManager.SetCategories(_categories);

            float rowWidth = _safeWidth - _padding * 2f;
            float rowHeight = _slotManager.Build(_safeArea, rowWidth, _gap);

            PlaceFromTop(_slotManager.SlotRow, cursorY);

            return cursorY + rowHeight + _gap * 1.6f;
        }

        // ---------------------------------------------------------------- kart tepsisi

        void BuildTray(float cursorY)
        {
            var pool = BuildCardPool();
            if (pool.Count == 0)
            {
                Debug.LogWarning("[EcoSort] Kategorilerde hic kart yok; tepsi bos kalacak.", this);
                return;
            }

            float trayWidth = _safeWidth - _padding * 2f;
            float trayPadding = _gap;
            // Ust serit beklenenden buyuk gelirse tepsi negatif yukseklige dusmesin.
            float available = Mathf.Max(_safeHeight * 0.25f, _safeHeight - _padding - cursorY);

            var trayRect = EcoUi.Rect("CardTray", _safeArea, new Vector2(trayWidth, available));
            _tray = trayRect.gameObject.AddComponent<CardTray>();

            // Kart orani tek yerde tanimli olsun diye tepsiye soruyoruz: adi gosterilen
            // kartlar biraz uzun, sadece ikonlu kartlar kare durur.
            _tray.Configure(_cardColumns, CardAspect);

            // Gereken yukseklik kalan bosluktan buyukse tepsi tamamini kullanir ve
            // kartlari kendisi kucultur.
            float wanted = _tray.MeasureHeight(trayWidth, pool.Count, _gap, trayPadding);
            float trayHeight = Mathf.Min(wanted, available);
            trayRect.sizeDelta = new Vector2(trayWidth, trayHeight);

            // Tepsiyi kalan bosluga ortala: ust serit ile ekran alti arasinda nefes olsun.
            PlaceFromTop(trayRect, cursorY + Mathf.Max(0f, (available - trayHeight) * 0.5f));

            var cellSize = _tray.Build(new Vector2(trayWidth, trayHeight), pool.Count, _gap, trayPadding);

            for (int i = 0; i < pool.Count; i++)
            {
                var card = CreateCard(pool[i], cellSize, trayRect);
                _tray.AddCard(card, i);
                _manager.RegisterCard(card);
            }

            _tray.PlayDealIn();
        }

        /// <summary>Kart yuksekliginin genisligine orani.</summary>
        float CardAspect => _showCardNames ? 1.28f : (_iconHasOwnBackdrop ? 1f : 1.34f);

        List<CardData> BuildCardPool()
        {
            var pool = new List<CardData>();

            foreach (var category in _categories)
            {
                if (category == null) continue;
                foreach (var card in category.Cards)
                    if (card != null) pool.Add(card);
            }

            if (_shuffle) Shuffle(pool);
            return pool;
        }

        /// <summary>
        /// Tek bir kart nesnesi uretir. Kart once tepsinin altinda dogar,
        /// sonra CardTray.AddCard ile kendi yuvasina oturur.
        /// </summary>
        CardView CreateCard(CardData data, Vector2 size, RectTransform temporaryParent)
        {
            int cornerRadius = Mathf.RoundToInt(size.x * 0.15f);
            var accent = data.AccentColor;

            // Kok: yumusak golge. Govde biraz yukari kaydirilinca golge altta gorunur.
            var rect = EcoUi.Rect($"Card_{data.CardId}", temporaryParent, size);

            var shadow = rect.gameObject.AddComponent<Image>();
            shadow.sprite = UiSpriteFactory.Shadow(cornerRadius, Mathf.RoundToInt(size.x * 0.09f));
            shadow.type = Image.Type.Sliced;
            shadow.color = EcoPalette.Shadow;
            shadow.raycastTarget = false;

            rect.gameObject.AddComponent<CanvasGroup>();

            // Govde: dokunmayi bu yakalar, olaylar koke cikar.
            var body = EcoUi.Panel("Body", rect, size, cornerRadius, EcoPalette.CardFace, true);
            body.rectTransform.anchoredPosition = new Vector2(0f, size.y * 0.02f);

            // Ad gosterilmiyorsa ikon kartin tam ortasinda dursun.
            float contentY = _showCardNames ? size.y * 0.12f : 0f;

            // Kategori renginde yumusak altlik: seffaf zeminli ikonun arkasindaki
            // renk kodu. Ikon kendi zeminini tasiyorsa gereksiz, cizilmez.
            //
            // Daire yerine yuvarlak kare: ikonlar (klavye, atki) yatay uzandigi icin
            // dairenin disina tasiyordu; kare altlik kartin formuyla da uyumlu.
            if (!_iconHasOwnBackdrop)
            {
                float padSize = size.x * (_showCardNames ? 0.84f : 0.90f);
                var pad = EcoUi.Panel("IconPad", body.rectTransform, Vector2.one * padSize,
                    Mathf.RoundToInt(padSize * 0.22f), accent.WithAlpha(0.28f));
                pad.rectTransform.anchoredPosition = new Vector2(0f, contentY);
            }

            float iconScale = _iconHasOwnBackdrop
                ? (_showCardNames ? 0.74f : 0.95f)
                : (_showCardNames ? 0.72f : 0.80f);

            var icon = EcoUi.Icon("Icon", body.rectTransform, Vector2.one * (size.x * iconScale),
                null, Color.white);
            icon.rectTransform.anchoredPosition = new Vector2(0f, contentY);

            if (_showCardNames)
            {
                var label = EcoUi.Label("Name", body.rectTransform,
                    new Vector2(size.x * 0.90f, size.y * 0.22f),
                    Mathf.RoundToInt(size.x * 0.125f), EcoPalette.Ink, FontStyle.Bold);
                label.text = data.DisplayName;
                label.rectTransform.anchoredPosition = new Vector2(0f, -size.y * 0.335f);
            }

            var card = rect.gameObject.AddComponent<CardView>();
            card.ConfigureVisuals(body, icon);
            card.Bind(data);   // sprite burada cozulur (Artwork ya da proseduel kart yuzu)

            return card;
        }

        // ---------------------------------------------------------------- kutlama banneri

        void BuildBanner()
        {
            var rect = EcoUi.Rect("Banner", _dragLayer,
                new Vector2(_safeWidth * 0.86f, _safeWidth * 0.30f));

            var panel = rect.gameObject.AddComponent<Image>();
            panel.sprite = UiSpriteFactory.Rounded(Mathf.RoundToInt(_safeWidth * 0.07f));
            panel.type = Image.Type.Sliced;
            panel.color = EcoPalette.CardFace.WithAlpha(0.96f);
            panel.raycastTarget = false;

            _bannerLabel = EcoUi.Label("BannerText", rect,
                new Vector2(_safeWidth * 0.78f, _safeWidth * 0.26f),
                Mathf.RoundToInt(_safeWidth * 0.055f), EcoPalette.Ink, FontStyle.Bold);

            _bannerGroup = rect.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.alpha = 0f;
            _bannerGroup.blocksRaycasts = false;
        }

        void ShowBanner(string message)
        {
            if (_bannerGroup == null) return;

            _bannerLabel.text = message;

            var rect = (RectTransform)_bannerGroup.transform;
            rect.localScale = Vector3.one * 0.8f;

            EcoTween.Fade(_bannerGroup, 1f, 0.2f);
            EcoTween.Scale(rect, Vector3.one, 0.4f, EcoEase.OutBack);

            EcoConfetti.Burst(rect, _dragLayer, EcoPalette.Success, 40, _safeWidth * 0.5f);
        }

        // ---------------------------------------------------------------- olaylar

        void Subscribe()
        {
            _manager.MatchRejected += HandleRejected;
            _manager.ComboChanged += HandleCombo;

            // Kategori olaylari SlotManager uzerinden dinlenir: istenen mimarideki
            // OnCategoryCompleted akisi burasi.
            _slotManager.OnCategoryProgress += HandleProgress;
            _slotManager.OnCategoryCompleted += HandleCompleted;
            _slotManager.OnAllCategoriesCompleted += HandleAllCompleted;
        }

        void HandleProgress(CategorySlotView slot, int current, int required)
        {
            if (slot == null || slot.Category == null) return;
            SetStatus($"{slot.Category.DisplayName}  {current}/{required}");
        }

        void HandleCombo(int combo)
        {
            if (combo > 1) SetStatus($"Harika!   kombo x{combo}");
        }

        void HandleCompleted(CategoryData category, CategorySlotView slot)
        {
            SetStatus($"{category.CompleteMessage} tamamlandı!");
            _counterLabel.text = $"{_slotManager.CompletedCount} / {_slotManager.TotalCount} grup";
        }

        void HandleAllCompleted()
        {
            SetStatus("Bütün gruplar tamam!");
            ShowBanner("Pano temiz!\nBeş grubun da yerini buldu.");
        }

        void HandleRejected(CardView card)
        {
            if (card != null && card.Data != null && !string.IsNullOrEmpty(card.Data.Hint))
            {
                SetStatus(card.Data.Hint);
                return;
            }

            SetStatus("Bu kart oraya ait değil");
        }

        void SetStatus(string message)
        {
            if (_statusLabel != null) _statusLabel.text = message;
        }

        // ---------------------------------------------------------------- yardimcilar

        /// <summary>Ogeyi guvenli alanin ustunden olculen mesafeye yerlestirir.</summary>
        void PlaceFromTop(RectTransform rect, float yFromTop, float x = 0f)
        {
            float height = rect.sizeDelta.y;
            rect.anchoredPosition = new Vector2(x, _safeHeight * 0.5f - yFromTop - height * 0.5f);
        }

        void Shuffle(List<CardData> list)
        {
            var random = _randomSeed != 0 ? new System.Random(_randomSeed) : new System.Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
