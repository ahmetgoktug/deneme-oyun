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
    ///   Canvas
    ///     +- Background   : mor gradyan + lila sahne isigi + soluk dekor daireler
    ///     |    +- Botanical_Back : kenar sarmasiklari ve pirilti (oyunun arkasinda)
    ///     +- SafeArea     : SafeAreaFitter
    ///     |    +- TopBar    : baslik, tamamlanan kategori tikleri, ilerleme hapi
    ///     |    +- Status    : tek satirlik yonlendirme metni
    ///     |    +- SlotRow   : ANA KARTLAR  -> SlotManager, kategori basina bir kart
    ///     |    +- CardTray  : KAPALI DESTELER -> ust uste yigilmis kart sutunlari
    ///     |    +- BottomBar : hamle sayaci, ipucu dugmesi, kombo gostergesi
    ///     +- Botanical_Front : kose yapraklari (oyunun onunde, soluk)
    ///     +- DragLayer    : suruklenen kart, konfeti ve bitis banner'i
    ///
    /// Ust seritteki her "ana kart" bir kategoriyi temsil eder. Alttaki desteler
    /// kapali kartlardan olusur ve yalnizca en alttaki kart aciktir. Deste sayisi
    /// <c>_cardColumns</c> ile ayarlanir: ana kart sayisina esitlenirse desteler
    /// slotlarla birebir hizalanir, azaltilirsa desteler derinlesir ve ayni anda
    /// daha az kart oynanabildigi icin oyun zorlasir.
    ///
    /// Tum olculer guvenli alanin genisligine oranla hesaplanir; 9:16'dan 9:21'e
    /// kadar farkli telefon oranlarinda ayni duzen korunur. Artan dikey bosluk
    /// bloklar arasina esit dagitilir, boylece uzun ekranlarda pano yukari
    /// yapismaz.
    ///
    /// Kurulum: sahnede Canvas + EventSystem olsun; bu bileseni CategoryManager ile
    /// ayni GameObject'e ekleyip _categories listesini doldur.
    /// </summary>
    [RequireComponent(typeof(CategoryManager))]
    public class SandboxBoard : MonoBehaviour
    {
        [Header("Icerik")]
        [Tooltip("Ust seritteki kategoriler (soldan saga). Her biri bir ana kart olur.")]
        [SerializeField] List<CategoryData> _categories = new List<CategoryData>();

        [Header("Sahne")]
        [Tooltip("Bos birakilirsa sahnedeki ilk Canvas kullanilir.")]
        [SerializeField] Canvas _canvas;

        [Header("Duzen (guvenli alan genisligine orani)")]
        [Tooltip("Kenar bosluklari.")]
        [SerializeField, Range(0.02f, 0.10f)] float _paddingRatio = 0.040f;
        [Tooltip("Ogeler arasi bosluk.")]
        [SerializeField, Range(0.01f, 0.06f)] float _gapRatio = 0.020f;

        [Header("Kapali Desteler")]
        [Tooltip("Deste sayisi. 0 = ana kart sayisiyla ayni (desteler slotlarla birebir hizali). " +
                 "Azaltmak desteleri derinlestirir: ayni anda daha az kart oynanabilir, oyun zorlasir.")]
        [SerializeField, Range(0, 6)] int _cardColumns = 4;
        [Tooltip("Ust uste binen kartlarda gorunen en kucuk serit: kart yuksekliginin orani. " +
                 "Deste bu deger kadar sikisir; kucuk deger = daha cok gizlenen kart.")]
        [SerializeField, Range(0.14f, 0.70f)] float _revealRatio = 0.22f;
        [Tooltip("Ekranda bos dikey alan kaldiginda deste bu orana kadar acilir. " +
                 "Kart sayisi arttikca kendiliginden _revealRatio degerine dogru sikisir.")]
        [SerializeField, Range(0.14f, 0.70f)] float _maxRevealRatio = 0.30f;
        [Tooltip("Her destede kac kart acik dursun.")]
        [SerializeField, Range(1, 3)] int _faceUpPerColumn = 1;

        [Header("Kart Gorunumu")]
        [Tooltip("Ikonun altinda kart adini da goster. Ikonlar tek basina okunmuyorsa ac.")]
        [SerializeField] bool _showCardNames = true;

        [Tooltip("Ikon kendi cercevesini/zeminini tasiyorsa ac: arkadaki kategori renkli " +
                 "altlik cizilmez ve ikon karti neredeyse tamamen doldurur.")]
        [SerializeField] bool _iconHasOwnBackdrop;

        [Header("Girdi")]
        [Tooltip("Acikken karta tek dokunus onu kendi ana kartina ucurur. " +
                 "Kapali: kart yalnizca surukleyerek oynanir.")]
        [SerializeField] bool _tapToPlay;

        [Header("Dekor")]
        [Tooltip("Ekranin kenarlarinda sallanan sarmasiklar ve zeminde pirilti.")]
        [SerializeField] bool _botanicalFrame = true;

        [Header("Yardim")]
        [Tooltip("Oyuncuya verilen ipucu hakki.")]
        [SerializeField, Range(0, 9)] int _hintCount = 3;

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
        Text _movesLabel;
        Text _comboLabel;
        Text _hintLabel;
        Button _hintButton;
        CanvasGroup _bannerGroup;
        Text _bannerLabel;

        readonly List<Image> _tickBoxes = new List<Image>();
        readonly List<Image> _tickMarks = new List<Image>();

        int _moves;
        int _hintsLeft;

        // Olculer (piksel, canvas referans birimi)
        float _canvasWidth;
        float _canvasHeight;
        float _safeWidth;
        float _safeHeight;
        float _padding;
        float _gap;

        RectTransform _backgroundRoot;

        // ---------------------------------------------------------------- yasam dongusu

        void Awake()
        {
            _manager = GetComponent<CategoryManager>();
            _hintsLeft = _hintCount;
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
            BuildBotanicalFrame();
            BuildLayout();
            BuildBanner();

            Subscribe();
            SetStatus("Açık kartı doğru ana karta sürükle");
        }

        void OnDestroy()
        {
            if (_manager != null)
            {
                _manager.MatchRejected -= HandleRejected;
                _manager.ComboChanged -= HandleCombo;
                _manager.CardAccepted -= HandleAccepted;
            }

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

            _canvasWidth = canvasWidth;
            _canvasHeight = canvasHeight;

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
            _backgroundRoot = root;

            // Zemin iki katman: altta duz koyu mor, ustunde yukaridan asagi
            // saydamlasan lila. Iki rengi de Image.color tasidigi icin proje
            // Linear renk uzayinda olsa bile tonlar dogru cikar.
            var baseFill = root.gameObject.AddComponent<Image>();
            baseFill.color = EcoPalette.BackgroundBottom;
            baseFill.raycastTarget = false;

            var top = EcoUi.FullRect("TopTint", root);
            var topFill = top.gameObject.AddComponent<Image>();
            topFill.sprite = UiSpriteFactory.VerticalFade();
            topFill.type = Image.Type.Simple;
            topFill.color = EcoPalette.BackgroundTop;
            topFill.raycastTarget = false;

            // Ust seridin arkasinda yumusak bir sahne isigi.
            var glow = EcoUi.Rect("TopGlow", root, new Vector2(_safeWidth * 1.35f, _safeWidth * 1.35f));
            glow.anchoredPosition = new Vector2(0f, _safeHeight * 0.22f);
            var glowImage = glow.gameObject.AddComponent<Image>();
            glowImage.sprite = UiSpriteFactory.RadialGlow();
            // Cok hafif: gradyani yikamadan sadece ust seride odak versin.
            glowImage.color = new Color(1f, 0.95f, 1f, 0.22f);
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
                var dot = EcoUi.Disc("Decor_" + i, root, diameter, new Color(1f, 1f, 1f, 0.05f));

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

        // ---------------------------------------------------------------- botanik dekor

        /// <summary>
        /// Kenar sarmasiklarini iki katmanda kurar:
        ///   arka  -> Background'in cocugu, oyunun tamamen arkasinda
        ///   on    -> SafeArea ile DragLayer arasinda, kose yapraklari icin
        ///
        /// Iki katman da raycast gecirmez; dekor hicbir dokunusu yutmaz.
        /// Olculer guvenli alandan degil CANVAS'tan alinir: sarmasiklar centigin
        /// altindan da gecip ekranin gercek kenarina yaslansin.
        /// </summary>
        void BuildBotanicalFrame()
        {
            if (!_botanicalFrame || _backgroundRoot == null) return;

            var canvasRect = (RectTransform)_canvas.transform;

            var back = EcoUi.FullRect("Botanical_Back", _backgroundRoot);
            back.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;

            var front = EcoUi.FullRect("Botanical_Front", canvasRect);
            front.gameObject.AddComponent<CanvasGroup>().blocksRaycasts = false;
            front.SetSiblingIndex(_dragLayer.GetSiblingIndex());

            var frame = back.gameObject.AddComponent<BotanicalFrame>();
            frame.Build(back, front, _canvasWidth, _canvasHeight);
        }

        // ---------------------------------------------------------------- ana duzen

        /// <summary>
        /// Bloklari once OLCER, sonra yerlestirir. Ikisini ayirmamizin sebebi:
        /// artan dikey bosluk bloklarin arasina esit dagitilsin, pano hem 16:9
        /// hem 21:9 ekranda dengeli dursun.
        /// </summary>
        void BuildLayout()
        {
            float rowWidth = _safeWidth - _padding * 2f;

            var pool = BuildCardPool();
            if (pool.Count == 0)
                Debug.LogWarning("[EcoSort] Kategorilerde hic kart yok; desteler bos kalacak.", this);

            // --- olcum
            float topBarHeight = _safeWidth * 0.115f;
            float statusHeight = _safeWidth * 0.055f;
            float bottomBarHeight = _safeWidth * 0.155f;

            _slotManager = GetComponent<SlotManager>();
            if (_slotManager == null) _slotManager = gameObject.AddComponent<SlotManager>();
            _slotManager.SetCategories(_categories);

            float slotHeight = _slotManager.Build(_safeArea, rowWidth, _gap);

            // Deste genisligi sutun sayisindan turer. Sutun sayisi ana kart
            // sayisina esitse bu deger slot genisligiyle birebir ayni cikar,
            // yani desteler slotlarin tam altina hizalanir.
            float cardWidth = (rowWidth - _gap * (ColumnCount - 1)) / ColumnCount;

            var trayRect = EcoUi.Rect("CardTray", _safeArea);
            _tray = trayRect.gameObject.AddComponent<CardTray>();
            _tray.Configure(ColumnCount, CardAspect, FitRevealRatio(
                cardWidth, slotHeight, pool.Count,
                topBarHeight + statusHeight + bottomBarHeight));

            float trayHeight = _tray.MeasureHeight(cardWidth, pool.Count);

            // --- artan bosluk: dort aralik arasinda paylastirilir
            float content = topBarHeight + statusHeight + slotHeight + trayHeight + bottomBarHeight;
            float used = _padding * 2f + content + _gap * 4f;
            float extra = Mathf.Clamp((_safeHeight - used) / 4f, 0f, _gap * 9f);

            // --- yerlestirme
            float cursorY = _padding;

            BuildTopBar(cursorY, rowWidth, topBarHeight);
            cursorY += topBarHeight + _gap * 0.6f + extra * 0.4f;

            BuildStatus(cursorY, rowWidth, statusHeight);
            cursorY += statusHeight + _gap + extra;

            PlaceFromTop(_slotManager.SlotRow, cursorY);
            cursorY += slotHeight + _gap + extra;

            BuildTray(trayRect, pool, cardWidth, trayHeight, cursorY);
            cursorY += trayHeight + _gap + extra;

            // Alt cubuk kalan boslugun altina yaslanir: ekran ne kadar uzun olursa
            // olsun dugmeler basparmak menzilinde kalir.
            float bottomY = Mathf.Max(cursorY, _safeHeight - _padding - bottomBarHeight);
            BuildBottomBar(bottomY, rowWidth, bottomBarHeight);
        }

        /// <summary>
        /// Destelerin ne kadar acilacagini ekranda kalan bosluga gore secer.
        ///
        /// Az kart varsa deste acilir (kartlar daha okunur olur ve ekranin alti
        /// bos kalmaz); kart sayisi arttikca kendiliginden _revealRatio degerine
        /// kadar sikisir. Boylece ayni duzen 15 kartla da 40 kartla da calisir.
        /// </summary>
        float FitRevealRatio(float cardWidth, float slotHeight, int cardCount, float fixedBarsHeight)
        {
            int deepest = Mathf.Max(0, Mathf.CeilToInt(cardCount / (float)ColumnCount) - 1);
            if (deepest == 0) return _revealRatio;

            float cardHeight = cardWidth * CardAspect;
            float available = _safeHeight - _padding * 2f - fixedBarsHeight - slotHeight - _gap * 4f;
            float fitted = (available - cardHeight) / (deepest * cardHeight);

            return Mathf.Clamp(fitted, _revealRatio, _maxRevealRatio);
        }

        // ---------------------------------------------------------------- ust cubuk

        void BuildTopBar(float cursorY, float rowWidth, float height)
        {
            var bar = EcoUi.Rect("TopBar", _safeArea, new Vector2(rowWidth, height));
            PlaceFromTop(bar, cursorY);

            // --- sol: oyun adi
            var title = EcoUi.Label("Title", bar, new Vector2(rowWidth * 0.34f, height),
                Mathf.RoundToInt(_safeWidth * 0.058f), EcoPalette.HudInk, FontStyle.Bold,
                TextAnchor.MiddleLeft);
            title.text = "Eco-Sort";
            title.rectTransform.anchoredPosition = new Vector2(-rowWidth * 0.33f, 0f);

            // --- orta: her kategori icin bir tik kutusu
            BuildTickRow(bar, height);

            // --- sag: tamamlanan grup sayaci
            float pillWidth = rowWidth * 0.20f;
            var pill = EcoUi.Panel("CounterPill", bar, new Vector2(pillWidth, height * 0.74f),
                Mathf.RoundToInt(height * 0.37f), EcoPalette.HudPanel);
            pill.rectTransform.anchoredPosition = new Vector2((rowWidth - pillWidth) * 0.5f, 0f);

            _counterLabel = EcoUi.Label("Counter", pill.rectTransform,
                new Vector2(pillWidth, height * 0.74f),
                Mathf.RoundToInt(_safeWidth * 0.040f), EcoPalette.HudInk, FontStyle.Bold);
            _counterLabel.text = "0/" + _categories.Count;
        }

        /// <summary>
        /// Referanstaki tik siras: her kategori icin bir kutu; grup tamamlaninca
        /// kutu yesile doner ve icinde bir isaret belirir.
        /// </summary>
        void BuildTickRow(RectTransform bar, float height)
        {
            float box = height * 0.52f;
            float spacing = box * 0.34f;
            int count = _categories.Count;

            var row = EcoUi.Rect("Ticks", bar,
                new Vector2(box * count + spacing * (count - 1), box));

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            for (int i = 0; i < count; i++)
            {
                var tick = EcoUi.Panel("Tick_" + i, row, Vector2.one * box,
                    Mathf.RoundToInt(box * 0.26f), EcoPalette.HudSlotEmpty);
                EcoUi.FixedSize(tick.rectTransform, box, box);

                var mark = EcoUi.Icon("Mark", tick.rectTransform, Vector2.one * (box * 0.62f),
                    IconFactory.GetSprite(EcoIcon.Sparkle), EcoPalette.CardFace);
                mark.gameObject.SetActive(false);

                _tickBoxes.Add(tick);
                _tickMarks.Add(mark);
            }
        }

        void BuildStatus(float cursorY, float rowWidth, float height)
        {
            _statusLabel = EcoUi.Label("Status", _safeArea, new Vector2(rowWidth, height),
                Mathf.RoundToInt(_safeWidth * 0.036f), EcoPalette.HudInk.WithAlpha(0.80f),
                FontStyle.Normal, TextAnchor.MiddleCenter);
            PlaceFromTop(_statusLabel.rectTransform, cursorY);
        }

        // ---------------------------------------------------------------- kapali desteler

        void BuildTray(RectTransform trayRect, List<CardData> pool, float cardWidth,
            float trayHeight, float cursorY)
        {
            _tray.SetFaceUpCount(_faceUpPerColumn);

            // Deste genisligi ve araligi ust seritle AYNI: her deste kendi ana
            // kartinin tam altinda durur.
            var cellSize = _tray.Build(pool.Count, cardWidth, _gap);
            PlaceFromTop(trayRect, cursorY);

            for (int i = 0; i < pool.Count; i++)
            {
                var card = CreateCard(pool[i], cellSize, trayRect);
                _tray.AddCard(card, i);
                _manager.RegisterCard(card);
            }

            _tray.PlayDealIn();
        }

        /// <summary>Kart yuksekliginin genisligine orani.</summary>
        float CardAspect => _showCardNames ? 1.36f : (_iconHasOwnBackdrop ? 1.20f : 1.32f);

        /// <summary>Alt bolgedeki deste sayisi. 0 ayarlanirsa ana kart sayisi kullanilir.</summary>
        int ColumnCount => _cardColumns > 0 ? _cardColumns : Mathf.Max(1, _categories.Count);

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
        ///
        ///   Card (golge + CanvasGroup + CardView)
        ///     +- Body  : kart yuzu (ikon, ad)   <- dokunmayi bu yakalar
        ///     +- Back  : kapali yuz             <- govdeden SONRA, yani ustunde
        /// </summary>
        CardView CreateCard(CardData data, Vector2 size, RectTransform temporaryParent)
        {
            int cornerRadius = Mathf.RoundToInt(size.x * 0.15f);
            var accent = data.AccentColor;

            // Kok: yumusak golge. Govde biraz yukari kaydirilinca golge altta gorunur.
            var rect = EcoUi.Rect("Card_" + data.CardId, temporaryParent, size);

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
            float contentY = _showCardNames ? size.y * 0.10f : 0f;

            // Kategori renginde yumusak altlik: seffaf zeminli ikonun arkasindaki
            // renk kodu. Ikon kendi zeminini tasiyorsa gereksiz, cizilmez.
            if (!_iconHasOwnBackdrop)
            {
                float padSize = size.x * (_showCardNames ? 0.80f : 0.88f);
                var pad = EcoUi.Panel("IconPad", body.rectTransform, Vector2.one * padSize,
                    Mathf.RoundToInt(padSize * 0.22f), accent.WithAlpha(0.28f));
                pad.rectTransform.anchoredPosition = new Vector2(0f, contentY);
            }

            float iconScale = _iconHasOwnBackdrop
                ? (_showCardNames ? 0.72f : 0.95f)
                : (_showCardNames ? 0.68f : 0.80f);

            var icon = EcoUi.Icon("Icon", body.rectTransform, Vector2.one * (size.x * iconScale),
                null, Color.white);
            icon.rectTransform.anchoredPosition = new Vector2(0f, contentY);

            if (_showCardNames)
            {
                var label = EcoUi.Label("Name", body.rectTransform,
                    new Vector2(size.x * 0.92f, size.y * 0.20f),
                    Mathf.RoundToInt(size.x * 0.115f), EcoPalette.Ink, FontStyle.Bold);
                label.text = data.DisplayName;
                label.rectTransform.anchoredPosition = new Vector2(0f, -size.y * 0.36f);
            }

            // Kapali yuz govdeden SONRA eklenir; boylece govdenin uzerini orter.
            var back = EcoUi.CardBack("Back", rect, size);
            back.anchoredPosition = body.rectTransform.anchoredPosition;

            var card = rect.gameObject.AddComponent<CardView>();
            card.ConfigureVisuals(body, icon, back.gameObject);
            card.SetTapToPlay(_tapToPlay);
            card.Bind(data);   // sprite burada cozulur (Artwork ya da proseduel kart yuzu)

            return card;
        }

        // ---------------------------------------------------------------- alt cubuk

        void BuildBottomBar(float cursorY, float rowWidth, float height)
        {
            var bar = EcoUi.Rect("BottomBar", _safeArea, new Vector2(rowWidth, height));
            PlaceFromTop(bar, cursorY);

            float tileWidth = (rowWidth - _gap * 2f) / 3f;

            // --- sol: hamle sayaci
            _movesLabel = BuildInfoTile(bar, "Moves", "HAMLE", "0",
                new Vector2(-(tileWidth + _gap), 0f), tileWidth, height);

            // --- orta: ipucu dugmesi
            BuildHintButton(bar, tileWidth, height);

            // --- sag: kombo
            _comboLabel = BuildInfoTile(bar, "Combo", "KOMBO", "x0",
                new Vector2(tileWidth + _gap, 0f), tileWidth, height);
        }

        /// <summary>Ust satirda kucuk bir baslik, altinda buyuk bir deger tasiyan kutucuk.</summary>
        Text BuildInfoTile(RectTransform bar, string name, string caption, string value,
            Vector2 position, float width, float height)
        {
            var tile = EcoUi.Panel(name, bar, new Vector2(width, height),
                Mathf.RoundToInt(height * 0.28f), EcoPalette.HudPanel);
            tile.rectTransform.anchoredPosition = position;

            var captionLabel = EcoUi.Label("Caption", tile.rectTransform,
                new Vector2(width, height * 0.34f),
                Mathf.RoundToInt(_safeWidth * 0.028f), EcoPalette.HudInk.WithAlpha(0.60f),
                FontStyle.Bold);
            captionLabel.text = caption;
            captionLabel.rectTransform.anchoredPosition = new Vector2(0f, height * 0.24f);

            var valueLabel = EcoUi.Label("Value", tile.rectTransform,
                new Vector2(width, height * 0.46f),
                Mathf.RoundToInt(_safeWidth * 0.052f), EcoPalette.HudInk, FontStyle.Bold);
            valueLabel.text = value;
            valueLabel.rectTransform.anchoredPosition = new Vector2(0f, -height * 0.16f);

            return valueLabel;
        }

        void BuildHintButton(RectTransform bar, float width, float height)
        {
            var panel = EcoUi.Panel("HintButton", bar, new Vector2(width, height),
                Mathf.RoundToInt(height * 0.28f), EcoPalette.Tab, true);

            var icon = EcoUi.Icon("HintIcon", panel.rectTransform, Vector2.one * (height * 0.46f),
                IconFactory.GetSprite(EcoIcon.Sparkle), EcoPalette.TabInk);
            icon.rectTransform.anchoredPosition = new Vector2(0f, height * 0.18f);

            _hintLabel = EcoUi.Label("HintCount", panel.rectTransform,
                new Vector2(width, height * 0.38f),
                Mathf.RoundToInt(_safeWidth * 0.034f), EcoPalette.TabInk, FontStyle.Bold);
            _hintLabel.rectTransform.anchoredPosition = new Vector2(0f, -height * 0.24f);

            _hintButton = panel.gameObject.AddComponent<Button>();
            _hintButton.targetGraphic = panel;
            _hintButton.onClick.AddListener(UseHint);

            RefreshHintLabel();
        }

        /// <summary>
        /// Ipucu: oynanabilir bir karti nefes aldirir ve gitmesi gereken ana karti
        /// zipatir. Kural motoruna dokunmaz, sadece dikkat ceker.
        /// </summary>
        void UseHint()
        {
            if (_hintsLeft <= 0 || _tray == null) return;

            var card = _tray.PulseNextPlayableCard();
            if (card == null)
            {
                SetStatus("Şu an gösterebileceğim bir kart yok");
                return;
            }

            var slot = _manager.ResolveSlot(card.Data);
            if (slot != null) EcoTween.Punch(slot.transform, 0.14f, 0.35f);

            _hintsLeft--;
            RefreshHintLabel();
            SetStatus(card.Data.DisplayName + " nereye gidiyor?");
        }

        void RefreshHintLabel()
        {
            if (_hintLabel != null) _hintLabel.text = "İPUCU " + _hintsLeft;

            if (_hintButton != null)
            {
                bool usable = _hintsLeft > 0;
                _hintButton.interactable = usable;

                var image = _hintButton.targetGraphic as Image;
                if (image != null)
                    image.color = usable ? EcoPalette.Tab : EcoPalette.Tab.WithAlpha(0.35f);
            }
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
            _manager.CardAccepted += HandleAccepted;

            // Kategori olaylari SlotManager uzerinden dinlenir: istenen mimarideki
            // OnCategoryCompleted akisi burasi.
            _slotManager.OnCategoryProgress += HandleProgress;
            _slotManager.OnCategoryCompleted += HandleCompleted;
            _slotManager.OnAllCategoriesCompleted += HandleAllCompleted;
        }

        void HandleAccepted(CardView card, CategorySlotView slot)
        {
            CountMove();
        }

        void HandleProgress(CategorySlotView slot, int current, int required)
        {
            if (slot == null || slot.Category == null) return;
            SetStatus(slot.Category.DisplayName + "  " + current + "/" + required);
        }

        void HandleCombo(int combo)
        {
            if (_comboLabel != null) _comboLabel.text = "x" + combo;
            if (combo > 1) SetStatus("Harika!   kombo x" + combo);
        }

        void HandleCompleted(CategoryData category, CategorySlotView slot)
        {
            SetStatus(category.CompleteMessage + " tamamlandı!");

            int done = _slotManager.CompletedCount;
            _counterLabel.text = done + "/" + _slotManager.TotalCount;
            MarkTicks(done);
        }

        /// <summary>Ust cubuktaki tik kutularini tamamlanan grup sayisina gore doldurur.</summary>
        void MarkTicks(int completed)
        {
            for (int i = 0; i < _tickBoxes.Count; i++)
            {
                bool filled = i < completed;
                if (_tickBoxes[i] != null)
                    _tickBoxes[i].color = filled ? EcoPalette.Success : EcoPalette.HudSlotEmpty;

                if (_tickMarks[i] != null && _tickMarks[i].gameObject.activeSelf != filled)
                {
                    _tickMarks[i].gameObject.SetActive(filled);
                    if (filled) EcoTween.Punch(_tickBoxes[i].transform, 0.20f, 0.30f);
                }
            }
        }

        void HandleAllCompleted()
        {
            SetStatus("Bütün gruplar tamam!");
            ShowBanner("Pano temiz!\n" + _moves + " hamlede bitirdin.");
        }

        void HandleRejected(CardView card)
        {
            CountMove();

            if (card != null && card.Data != null && !string.IsNullOrEmpty(card.Data.Hint))
            {
                SetStatus(card.Data.Hint);
                return;
            }

            SetStatus("Bu kart oraya ait değil");
        }

        void CountMove()
        {
            _moves++;
            if (_movesLabel != null) _movesLabel.text = _moves.ToString();
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
