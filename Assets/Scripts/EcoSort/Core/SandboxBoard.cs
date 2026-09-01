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
    /// Tum olculer ekran genisligine oranla hesaplanir; 9:16'dan 9:21'e kadar
    /// farkli telefon oranlarinda ayni duzen korunur.
    ///
    /// Gecici bir kurucudur: gercek bolum akisi ileride LevelData + BoardBuilder
    /// ile gelecek, ama olculendirme mantigi oraya tasinabilir.
    ///
    /// Kurulum: sahnede Canvas + EventSystem + CategoryManager olsun, bu bileseni de
    /// CategoryManager ile ayni GameObject'e ekleyip _categories listesini doldur.
    /// </summary>
    [RequireComponent(typeof(CategoryManager))]
    public class SandboxBoard : MonoBehaviour
    {
        [Header("Icerik")]
        [SerializeField] List<CategoryData> _categories = new List<CategoryData>();

        [Header("Sahne")]
        [Tooltip("Bos birakilirsa sahnedeki ilk Canvas kullanilir.")]
        [SerializeField] Canvas _canvas;

        [Header("Duzen (ekran genisligine orani)")]
        [Tooltip("Kenar bosluklari.")]
        [SerializeField, Range(0.02f, 0.10f)] float _paddingRatio = 0.045f;
        [Tooltip("Ogeler arasi bosluk.")]
        [SerializeField, Range(0.01f, 0.06f)] float _gapRatio = 0.022f;
        [SerializeField, Range(2, 5)] int _columns = 3;

        [Header("Kart Gorunumu")]
        [Tooltip("Ikonun altinda kart adini da goster. Ikonlar tek basina okunmuyorsa ac.")]
        [SerializeField] bool _showCardNames;

        [Tooltip("Ikon kendi cercevesini/zeminini tasiyorsa ac: arkadaki kategori renkli disk " +
                 "cizilmez ve ikon karti neredeyse tamamen doldurur. Siluet ikonlarda kapali birak.")]
        [SerializeField] bool _iconHasOwnBackdrop;

        [Header("Test")]
        [SerializeField] bool _shuffle = true;
        [SerializeField] int _randomSeed = 0;

        CategoryManager _manager;
        RectTransform _safeArea;
        RectTransform _slotsRoot;
        RectTransform _boardRoot;
        RectTransform _dragLayer;
        Text _statusLabel;

        readonly Dictionary<CategoryData, Text> _progressLabels = new Dictionary<CategoryData, Text>();

        // Olculer (piksel, canvas referans birimi)
        float _safeWidth;
        float _safeHeight;
        float _padding;
        float _gap;
        float _columnWidth;

        static Font s_font;

        static Font UiFont
        {
            get
            {
                if (s_font == null) s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return s_font;
            }
        }

        // ---------------------------------------------------------------- yasam dongusu

        void Awake()
        {
            _manager = GetComponent<CategoryManager>();
        }

        void Start()
        {
            if (_canvas == null) _canvas = FindFirstObjectByType<Canvas>();
            if (_canvas == null)
            {
                Debug.LogError("[EcoSort] Sahnede Canvas yok; pano kurulamiyor.", this);
                return;
            }

            if (_categories == null || _categories.Count == 0)
            {
                Debug.LogError("[EcoSort] SandboxBoard'a en az bir CategoryData atanmali.", this);
                return;
            }

            Measure();
            BuildBackground();
            BuildRoots();

            float slotsBottom = BuildHeaderAndSlots();
            BuildCards(slotsBottom);

            Subscribe();
            SetStatus("Kartlari dogru kategoriye surukle");
        }

        void OnDestroy()
        {
            if (_manager == null) return;
            _manager.CategoryProgressChanged -= HandleProgress;
            _manager.CategoryCompleted -= HandleCompleted;
            _manager.MatchRejected -= HandleRejected;
            _manager.BoardCleared -= HandleBoardCleared;
        }

        // ---------------------------------------------------------------- olculendirme

        void Measure()
        {
            var canvasRect = (RectTransform)_canvas.transform;
            float canvasWidth = canvasRect.rect.width;
            float canvasHeight = canvasRect.rect.height;

            // Guvenli alani canvas birimine cevir: centik/gesture cubugu duzeni bozmasin.
            float safeRatioX = Screen.width > 0 ? Screen.safeArea.width / Screen.width : 1f;
            float safeRatioY = Screen.height > 0 ? Screen.safeArea.height / Screen.height : 1f;

            _safeWidth = canvasWidth * safeRatioX;
            _safeHeight = canvasHeight * safeRatioY;

            _padding = _safeWidth * _paddingRatio;
            _gap = _safeWidth * _gapRatio;
            _columnWidth = (_safeWidth - _padding * 2f - _gap * (_columns - 1)) / _columns;
        }

        // ---------------------------------------------------------------- kurulum

        void BuildBackground()
        {
            var canvasRect = (RectTransform)_canvas.transform;

            var go = new GameObject("Background", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(canvasRect, false);
            Stretch(rect);
            rect.SetAsFirstSibling();

            var image = go.AddComponent<Image>();
            image.sprite = UiSpriteFactory.VerticalGradient(EcoPalette.BackgroundBottom, EcoPalette.BackgroundTop);
            image.type = Image.Type.Simple;
            image.raycastTarget = false;
        }

        void BuildRoots()
        {
            var canvasRect = (RectTransform)_canvas.transform;

            var safeGo = new GameObject("SafeArea", typeof(RectTransform));
            _safeArea = (RectTransform)safeGo.transform;
            _safeArea.SetParent(canvasRect, false);
            Stretch(_safeArea);
            safeGo.AddComponent<SafeAreaFitter>();

            _slotsRoot = CreateRect("Slots", _safeArea);
            _boardRoot = CreateRect("Board", _safeArea);

            // DragLayer guvenli alanin disinda ve en sonda: suruklenen kart her seyin uzerinde.
            var dragGo = new GameObject("DragLayer", typeof(RectTransform));
            _dragLayer = (RectTransform)dragGo.transform;
            _dragLayer.SetParent(canvasRect, false);
            Stretch(_dragLayer);
            _dragLayer.SetAsLastSibling();
            CardView.SetDragLayer(_dragLayer);
        }

        /// <summary>Baslik ve kategori slotlarini kurar; kart alaninin ust sinirini dondurur.</summary>
        float BuildHeaderAndSlots()
        {
            float titleSize = _safeWidth * 0.058f;
            float statusSize = _safeWidth * 0.032f;

            // ---- baslik
            float cursorY = _padding;

            var title = CreateLabel("Title", _safeArea, new Vector2(_safeWidth - _padding * 2f, titleSize * 1.4f),
                Mathf.RoundToInt(titleSize), FontStyle.Bold);
            title.color = EcoPalette.Ink;
            title.text = "Eco-Sort";
            PlaceFromTop(title.rectTransform, cursorY);
            cursorY += titleSize * 1.4f + _gap * 0.4f;

            _statusLabel = CreateLabel("Status", _safeArea, new Vector2(_safeWidth - _padding * 2f, statusSize * 1.6f),
                Mathf.RoundToInt(statusSize));
            _statusLabel.color = EcoPalette.InkMuted;
            PlaceFromTop(_statusLabel.rectTransform, cursorY);
            cursorY += statusSize * 1.6f + _gap * 1.6f;

            // ---- slotlar
            // Slot, icine girecek kartla ayni oranda dursun.
            float slotHeight = _columnWidth * (_iconHasOwnBackdrop ? 1.06f : 1.28f);
            float slotTitleSize = _columnWidth * 0.105f;
            float progressSize = _columnWidth * 0.125f;

            float slotTitleH = slotTitleSize * 1.5f;
            float progressH = progressSize * 1.5f;

            float startX = -(_columnWidth + _gap) * (_categories.Count - 1) * 0.5f;
            float slotTop = cursorY;

            for (int i = 0; i < _categories.Count; i++)
            {
                var category = _categories[i];
                if (category == null) continue;

                float groupX = startX + (_columnWidth + _gap) * i;

                // Baslik (slotun ustunde)
                var slotTitle = CreateLabel($"Title_{category.CategoryId}", _slotsRoot,
                    new Vector2(_columnWidth, slotTitleH), Mathf.RoundToInt(slotTitleSize), FontStyle.Bold);
                slotTitle.color = EcoPalette.Ink;
                slotTitle.text = category.DisplayName;
                PlaceFromTop(slotTitle.rectTransform, slotTop, groupX);

                // Slot govdesi
                var slotGo = new GameObject($"Slot_{category.CategoryId}", typeof(RectTransform));
                var slotRect = (RectTransform)slotGo.transform;
                slotRect.SetParent(_slotsRoot, false);
                slotRect.sizeDelta = new Vector2(_columnWidth, slotHeight);
                PlaceFromTop(slotRect, slotTop + slotTitleH, groupX);

                var background = slotGo.AddComponent<Image>();
                background.sprite = UiSpriteFactory.Rounded(Mathf.RoundToInt(_columnWidth * 0.11f));
                background.type = Image.Type.Sliced;

                // Bos slot isareti: ilk kart gelince kartlarin altinda kalir.
                var hint = new GameObject("Hint", typeof(RectTransform));
                var hintRect = (RectTransform)hint.transform;
                hintRect.SetParent(slotRect, false);
                hintRect.sizeDelta = Vector2.one * (_columnWidth * 0.38f);
                var hintImage = hint.AddComponent<Image>();
                hintImage.sprite = UiSpriteFactory.Circle();
                hintImage.color = category.AccentColor.WithAlpha(0.18f);
                hintImage.raycastTarget = false;

                // Tamamlanma parlamasi
                var glow = new GameObject("Glow", typeof(RectTransform));
                var glowRect = (RectTransform)glow.transform;
                glowRect.SetParent(slotRect, false);
                glowRect.sizeDelta = new Vector2(_columnWidth, slotHeight);
                var glowImage = glow.AddComponent<Image>();
                glowImage.sprite = background.sprite;
                glowImage.type = Image.Type.Sliced;
                glowImage.raycastTarget = false;
                glow.AddComponent<CanvasGroup>().alpha = 0f;
                glow.SetActive(false);

                var slot = slotGo.AddComponent<CategorySlotView>();
                slot.ConfigureVisuals(background, glowImage);
                slot.ConfigureLayout(new Vector2(_columnWidth * 0.05f, -_columnWidth * 0.045f), 3f);
                slot.Bind(category);

                // Ilerleme (slotun altinda)
                var progress = CreateLabel($"Progress_{category.CategoryId}", _slotsRoot,
                    new Vector2(_columnWidth, progressH), Mathf.RoundToInt(progressSize), FontStyle.Bold);
                progress.color = category.AccentColor;
                PlaceFromTop(progress.rectTransform, slotTop + slotTitleH + slotHeight, groupX);
                _progressLabels[category] = progress;

                SetProgressLabel(category, 0);
            }

            return slotTop + slotTitleH + slotHeight + progressH + _gap * 1.6f;
        }

        void BuildCards(float boardTop)
        {
            var pool = new List<CardData>();
            foreach (var category in _categories)
            {
                if (category == null) continue;
                foreach (var card in category.Cards)
                    if (card != null) pool.Add(card);
            }

            if (pool.Count == 0) return;
            if (_shuffle) Shuffle(pool);

            int rows = Mathf.CeilToInt(pool.Count / (float)_columns);
            float boardHeight = _safeHeight - _padding - boardTop;

            // Kendi cercevesini tasiyan ikonlar kare cizilmistir: kart da kare olsun ki
            // ikonun altinda ustunde bos beyaz kalmasin.
            float aspect = _iconHasOwnBackdrop ? 1f : 1.34f;
            float cardWidth = _columnWidth * 0.92f;
            float cardHeight = cardWidth * aspect;
            float maxCardHeight = (boardHeight - _gap * (rows - 1)) / rows;

            if (cardHeight > maxCardHeight)
            {
                cardHeight = maxCardHeight;
                cardWidth = cardHeight / aspect;
            }

            float gridHeight = cardHeight * rows + _gap * (rows - 1);
            float gridTop = boardTop + Mathf.Max(0f, (boardHeight - gridHeight) * 0.5f);
            float startX = -(cardWidth + _gap) * (_columns - 1) * 0.5f;

            for (int i = 0; i < pool.Count; i++)
            {
                int row = i / _columns;
                int col = i % _columns;

                // Son satir eksikse ortala.
                int itemsInRow = Mathf.Min(_columns, pool.Count - row * _columns);
                float rowStartX = itemsInRow == _columns
                    ? startX
                    : -(cardWidth + _gap) * (itemsInRow - 1) * 0.5f;

                float x = rowStartX + (cardWidth + _gap) * col;
                float y = gridTop + (cardHeight + _gap) * row;

                CreateCard(pool[i], new Vector2(cardWidth, cardHeight), x, y);
            }
        }

        void CreateCard(CardData data, Vector2 size, float x, float yFromTop)
        {
            var accent = data.Category != null ? data.Category.AccentColor : EcoPalette.InkMuted;
            int cornerRadius = Mathf.RoundToInt(size.x * 0.13f);

            // Kok: yumusak golge. Govde biraz yukari kaydirilinca golge altta gorunur.
            var go = new GameObject($"Card_{data.CardId}", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_boardRoot, false);
            rect.sizeDelta = size;
            PlaceFromTop(rect, yFromTop, x);

            var shadow = go.AddComponent<Image>();
            shadow.sprite = UiSpriteFactory.Shadow(cornerRadius, Mathf.RoundToInt(size.x * 0.08f));
            shadow.type = Image.Type.Sliced;
            shadow.color = EcoPalette.Shadow;
            shadow.raycastTarget = false;

            go.AddComponent<CanvasGroup>();

            // Govde
            var body = new GameObject("Body", typeof(RectTransform));
            var bodyRect = (RectTransform)body.transform;
            bodyRect.SetParent(rect, false);
            bodyRect.sizeDelta = size;
            bodyRect.anchoredPosition = new Vector2(0f, size.y * 0.02f);
            var bodyImage = body.AddComponent<Image>();
            bodyImage.sprite = UiSpriteFactory.Rounded(cornerRadius);
            bodyImage.type = Image.Type.Sliced;
            bodyImage.raycastTarget = true;   // dokunmayi bu yakalar, olaylar koke cikar

            // Ad gosterilmiyorsa ikon kartin tam ortasinda dursun.
            float contentY = _showCardNames ? size.y * 0.14f : 0f;

            // Kategori renginde disk: siluet ikonun arkasindaki renk kodu.
            // Ikon kendi zeminini tasiyorsa gereksiz, cizilmez.
            if (!_iconHasOwnBackdrop)
            {
                var disc = new GameObject("Disc", typeof(RectTransform));
                var discRect = (RectTransform)disc.transform;
                discRect.SetParent(bodyRect, false);
                discRect.sizeDelta = Vector2.one * (size.x * (_showCardNames ? 0.52f : 0.64f));
                discRect.anchoredPosition = new Vector2(0f, contentY);
                var discImage = disc.AddComponent<Image>();
                discImage.sprite = UiSpriteFactory.Circle();
                discImage.color = accent;
                discImage.raycastTarget = false;
            }

            // Ikon. Sprite'i CardData.Artwork saglar.
            float iconScale = _iconHasOwnBackdrop
                ? (_showCardNames ? 0.78f : 0.94f)
                : (_showCardNames ? 0.34f : 0.42f);

            var art = new GameObject("Icon", typeof(RectTransform));
            var artRect = (RectTransform)art.transform;
            artRect.SetParent(bodyRect, false);
            artRect.sizeDelta = Vector2.one * (size.x * iconScale);
            artRect.anchoredPosition = new Vector2(0f, contentY);
            var artImage = art.AddComponent<Image>();
            artImage.preserveAspect = true;
            artImage.raycastTarget = false;

            if (_showCardNames)
            {
                var label = CreateLabel("Name", bodyRect, new Vector2(size.x * 0.86f, size.y * 0.26f),
                    Mathf.RoundToInt(size.x * 0.115f), FontStyle.Bold);
                label.color = EcoPalette.Ink;
                label.text = data.DisplayName;
                label.rectTransform.anchoredPosition = new Vector2(0f, -size.y * 0.30f);
            }

            var card = go.AddComponent<CardView>();
            card.ConfigureVisuals(bodyImage, artImage);
            card.Bind(data);
            card.CaptureHome();   // konum atandiktan SONRA: evi burasi

            _manager.RegisterCard(card);
        }

        // ---------------------------------------------------------------- olaylar

        void Subscribe()
        {
            _manager.CategoryProgressChanged += HandleProgress;
            _manager.CategoryCompleted += HandleCompleted;
            _manager.MatchRejected += HandleRejected;
            _manager.BoardCleared += HandleBoardCleared;
        }

        void HandleProgress(CategorySlotView slot, int current, int required)
        {
            SetProgressLabel(slot.Category, current);
            SetStatus(_manager.Combo > 1
                ? $"Harika!  kombo x{_manager.Combo}"
                : slot.Category.DisplayName);
        }

        void HandleCompleted(CategoryData category)
        {
            SetStatus($"{category.CompleteMessage} tamamlandi!");
        }

        void HandleRejected(CardView card)
        {
            SetStatus("Bu kart oraya ait degil");
        }

        void HandleBoardCleared()
        {
            SetStatus("Pano temizlendi!");
        }

        void SetProgressLabel(CategoryData category, int current)
        {
            if (category == null || !_progressLabels.TryGetValue(category, out var label)) return;
            label.text = $"{current}/{category.RequiredCardCount}";
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

        static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        RectTransform CreateRect(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            return rect;
        }

        Text CreateLabel(string name, RectTransform parent, Vector2 size, int fontSize,
            FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            var text = go.AddComponent<Text>();
            text.font = UiFont;
            text.fontSize = Mathf.Max(8, fontSize);
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = EcoPalette.Ink;
            return text;
        }

        void Shuffle(List<CardData> list)
        {
            var random = _randomSeed != 0 ? new System.Random(_randomSeed) : new System.Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }
    }
}
