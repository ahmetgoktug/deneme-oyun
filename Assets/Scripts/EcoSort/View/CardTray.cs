using System;
using System.Collections.Generic;
using EcoSort.Core;
using EcoSort.Data;
using EcoSort.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace EcoSort.View
{
    /// <summary>
    /// Ekranin alt yarisindaki cekilebilir kart alani.
    ///
    /// Duzen: bir <see cref="VerticalLayoutGroup"/> icinde satirlar, her satir da
    /// kendi <see cref="HorizontalLayoutGroup"/>'u ile ORTALANMIS kart yuvalari.
    ///
    /// Onemli tasarim karari: kartlar Layout Group'un DOGRUDAN cocugu DEGILDIR.
    /// Layout, bos "yuva" (socket) nesnelerini hizalar; kart ise yuvanin cocugudur.
    /// Boylece:
    ///
    ///   - Kart suruklenirken/geri donerken tween'i Layout Group ile yarismaz
    ///     (Layout Group her karede anchoredPosition'i geri yazardi),
    ///   - Bir kart panodan cikinca duzen ziplamaz, yerinde soluk bir iz kalir.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class CardTray : MonoBehaviour
    {
        [Header("Duzen")]
        [Tooltip("Satirdaki kart sayisi.")]
        [SerializeField, Range(2, 6)] int _columns = 4;
        [Tooltip("Kart yuksekliginin genisligine orani. 1 = kare kart.")]
        [SerializeField, Range(0.8f, 1.6f)] float _cardAspect = 1.28f;

        [Header("Dagitim")]
        [Tooltip("Kartlarin sirayla yerine oturmasi arasindaki gecikme.")]
        [SerializeField] float _dealStagger = 0.045f;

        [Header("Ipucu")]
        [Tooltip("Bu kadar saniye hicbir sey yapilmazsa bir kart nefes alarak dikkat ceker. 0 = kapali.")]
        [SerializeField] float _idleHintSeconds = 6f;

        readonly List<CardView> _cards = new List<CardView>();
        readonly List<RectTransform> _sockets = new List<RectTransform>();

        RectTransform _rect;
        RectTransform _rowsRoot;
        CategoryManager _rules;

        float _lastActivityTime;
        int _hintIndex;

        public IReadOnlyList<CardView> Cards => _cards;
        public RectTransform Root => _rect;

        // ---------------------------------------------------------------- kurulum

        /// <summary>
        /// Sutun sayisi ve kart oranini kod tarafindan verir.
        /// Build()'dan ONCE cagrilmalidir; olculer buna gore hesaplanir.
        /// </summary>
        public void Configure(int columns, float cardAspect)
        {
            _columns = Mathf.Clamp(columns, 2, 6);
            _cardAspect = Mathf.Clamp(cardAspect, 0.8f, 1.6f);
        }

        /// <summary>
        /// Verilen genislikte bu kadar kartin sigmasi icin gereken tepsi yuksekligi.
        /// Tepsiyi yerlestiren kod once bunu sorar, sonra Build() cagirir; boylece
        /// kart orani tek bir yerde (burada) tanimli kalir.
        /// </summary>
        public float MeasureHeight(float width, int cardCount, float spacing, float padding)
        {
            int rows = RowCount(cardCount);
            float cardWidth = (width - padding * 2f - spacing * (_columns - 1)) / _columns;
            float cardHeight = cardWidth * _cardAspect;
            return padding * 2f + cardHeight * rows + spacing * (rows - 1);
        }

        int RowCount(int cardCount) => Mathf.Max(1, Mathf.CeilToInt(cardCount / (float)_columns));

        /// <summary>
        /// Tepsiyi kurar: zemin paneli + grid + bos yuvalar.
        /// </summary>
        /// <param name="size">Tepsinin toplam olcusu (piksel).</param>
        /// <param name="cardCount">Kac yuva acilacak.</param>
        /// <param name="spacing">Kartlar arasi bosluk.</param>
        /// <param name="padding">Tepsi ic bosluğu.</param>
        public Vector2 Build(Vector2 size, int cardCount, float spacing, float padding)
        {
            // Bilesen zaten tepsinin RectTransform'u uzerinde durur.
            _rect = (RectTransform)transform;
            _rect.sizeDelta = size;

            // --- zemin: kartlarin uzerinde durdugu yumusak tepsi
            var backdrop = EcoUi.Panel("TrayBackdrop", _rect, size,
                Mathf.RoundToInt(size.x * 0.06f), EcoPalette.TrayFill);
            EcoUi.Stretch(backdrop.rectTransform);

            // --- olcu: kart genisligi tepsiye sigacak sekilde hesaplanir
            int rows = RowCount(cardCount);

            float usableWidth = size.x - padding * 2f - spacing * (_columns - 1);
            float usableHeight = size.y - padding * 2f - spacing * (rows - 1);

            float cardWidth = usableWidth / _columns;
            float cardHeight = cardWidth * _cardAspect;

            if (cardHeight * rows > usableHeight)
            {
                cardHeight = usableHeight / rows;
                cardWidth = cardHeight / _cardAspect;
            }

            var cellSize = new Vector2(cardWidth, cardHeight);

            // --- duzen: dikey satir listesi, her satir kendi icinde ORTALANMIS yatay dizi.
            //
            // GridLayoutGroup yerine bunu tercih ediyoruz: 15 kart 4'luk satirlara
            // bolununce son satirda 3 kart kalir ve Grid onlari sola yaslar.
            // Satir basina bir HorizontalLayoutGroup her satiri ortalar.
            _rowsRoot = EcoUi.Rect("Rows", _rect, size);
            EcoUi.Stretch(_rowsRoot);

            var column = _rowsRoot.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = spacing;
            column.padding = new RectOffset(
                Mathf.RoundToInt(padding), Mathf.RoundToInt(padding),
                Mathf.RoundToInt(padding), Mathf.RoundToInt(padding));
            column.childAlignment = TextAnchor.MiddleCenter;
            column.childControlWidth = false;
            column.childControlHeight = false;
            column.childForceExpandWidth = false;
            column.childForceExpandHeight = false;

            for (int row = 0; row < rows; row++)
            {
                int firstIndex = row * _columns;
                int countInRow = Mathf.Min(_columns, cardCount - firstIndex);
                float rowWidth = countInRow * cardWidth + (countInRow - 1) * spacing;

                var rowRect = EcoUi.Rect($"Row_{row}", _rowsRoot, new Vector2(rowWidth, cardHeight));
                EcoUi.FixedSize(rowRect, rowWidth, cardHeight);

                var line = rowRect.gameObject.AddComponent<HorizontalLayoutGroup>();
                line.spacing = spacing;
                line.childAlignment = TextAnchor.MiddleCenter;
                line.childControlWidth = false;
                line.childControlHeight = false;
                line.childForceExpandWidth = false;
                line.childForceExpandHeight = false;

                for (int i = 0; i < countInRow; i++)
                {
                    var socket = EcoUi.Rect($"Socket_{firstIndex + i}", rowRect, cellSize);
                    EcoUi.FixedSize(socket, cardWidth, cardHeight);

                    // Bos yuvanin soluk izi: kart cikinca duzen bosluklu ama okunur kalir.
                    var ghost = EcoUi.Panel("Ghost", socket, cellSize,
                        Mathf.RoundToInt(cardWidth * 0.16f), EcoPalette.SocketGhost);
                    EcoUi.Stretch(ghost.rectTransform);

                    _sockets.Add(socket);
                }
            }

            _lastActivityTime = Time.unscaledTime;
            return cellSize;
        }

        /// <summary>Yuvaya bir kart yerlestirir (kurulum sirasinda, animasyonsuz).</summary>
        public void AddCard(CardView card, int socketIndex)
        {
            if (card == null || socketIndex < 0 || socketIndex >= _sockets.Count) return;

            card.SnapInto(_sockets[socketIndex], Vector2.zero);
            _cards.Add(card);

            // Oyuncu etkilesimi ipucu sayacini sifirlasin.
            card.DragStarted += HandleCardTouched;
            card.Tapped += HandleCardTouched;
            card.DragEnded += HandleCardReleased;
        }

        /// <summary>Kartlari sirayla asagidan sekerek yerine oturtur.</summary>
        public void PlayDealIn()
        {
            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i] != null) _cards[i].PlayDealIn(i * _dealStagger);

            _lastActivityTime = Time.unscaledTime + _cards.Count * _dealStagger;
        }

        /// <summary>Yuva sayisi (kurulumda acilan kart sayisi).</summary>
        public int SocketCount => _sockets.Count;

        // ---------------------------------------------------------------- ipucu

        void Update()
        {
            if (_idleHintSeconds <= 0f) return;
            if (Time.unscaledTime - _lastActivityTime < _idleHintSeconds) return;

            _lastActivityTime = Time.unscaledTime;
            PulseNextPlayableCard();
        }

        /// <summary>Hala oynanabilir bir karti sirayla nefes aldirir.</summary>
        void PulseNextPlayableCard()
        {
            if (_cards.Count == 0) return;

            _rules = _rules != null ? _rules : CategoryManager.Instance;

            for (int step = 0; step < _cards.Count; step++)
            {
                _hintIndex = (_hintIndex + 1) % _cards.Count;
                var card = _cards[_hintIndex];

                if (card == null || !card.CanDrag) continue;

                // Gidebilecegi bir slot kalmadiysa oyuncuyu yanlis yone itmeyelim.
                if (_rules != null && _rules.ResolveSlot(card.Data) == null) continue;

                card.PlayHintPulse();
                return;
            }
        }

        void HandleCardTouched(CardView card) => _lastActivityTime = Time.unscaledTime;

        void HandleCardReleased(CardView card, bool accepted) => _lastActivityTime = Time.unscaledTime;

        void OnDestroy()
        {
            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.DragStarted -= HandleCardTouched;
                card.Tapped -= HandleCardTouched;
                card.DragEnded -= HandleCardReleased;
            }
        }
    }
}
