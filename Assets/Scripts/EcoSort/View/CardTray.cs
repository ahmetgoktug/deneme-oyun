using System.Collections.Generic;
using EcoSort.Core;
using EcoSort.Utils;
using UnityEngine;
using UnityEngine.UI;

namespace EcoSort.View
{
    /// <summary>
    /// Ekranin alt yarisi: her "ana kart" slotunun altinda duran, ust uste binmis
    /// KAPALI kart destesi.
    ///
    ///   Slot_0   Slot_1   Slot_2   Slot_3   Slot_4      <- ust serit (SlotManager)
    ///   -------  -------  -------  -------  -------
    ///   [kapali] [kapali] [kapali] [kapali] [kapali]    <- sadece ust seridi gorunur
    ///   [kapali] [kapali] [kapali] [kapali] [kapali]
    ///   [ ACIK ] [ ACIK ] [ ACIK ] [ ACIK ] [ ACIK ]    <- oynanabilir kart
    ///
    /// Her destede yalnizca en alttaki kart aciktir ve suruklenebilir. O kart bir
    /// kategoriye gidince ustundeki kart cevrilir. Deste bu yuzden asagidan yukari
    /// dogru erir; sutunlarin ust hizasi hic degismez.
    ///
    /// Onemli tasarim karari: kartlar dogrudan konumlandirilmaz, bos "yuva" (socket)
    /// nesnelerinin cocugu olur. Boylece surukleme/geri donus tween'leri yerlesim
    /// hesabiyla yarismaz ve bir kart panodan cikinca yerinde soluk bir iz kalir.
    ///
    /// Destelerin icerigi kategoriye gore DEGIL, karisik dagitilir: bir sutun tek
    /// basina bir kategoriyi cozmez.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class CardTray : MonoBehaviour
    {
        [Header("Duzen")]
        [Tooltip("Deste sayisi. Ust seritteki slot sayisiyla ayni olmali.")]
        [SerializeField, Range(2, 6)] int _columns = 5;
        [Tooltip("Kart yuksekliginin genisligine orani. 1 = kare kart.")]
        [SerializeField, Range(0.8f, 1.6f)] float _cardAspect = 1.38f;
        [Tooltip("Ust uste binen kartlarda gorunen serit: kart yuksekliginin orani.")]
        [SerializeField, Range(0.18f, 0.70f)] float _revealRatio = 0.40f;

        [Header("Kapali Yuz")]
        [Tooltip("Her destede kac kart acik dursun. 1 = klasik solitaire hissi.")]
        [SerializeField, Range(1, 3)] int _faceUpPerColumn = 1;

        [Header("Dagitim")]
        [Tooltip("Kartlarin sirayla yerine oturmasi arasindaki gecikme.")]
        [SerializeField] float _dealStagger = 0.045f;

        [Header("Ipucu")]
        [Tooltip("Bu kadar saniye hicbir sey yapilmazsa bir kart nefes alarak dikkat ceker. 0 = kapali.")]
        [SerializeField] float _idleHintSeconds = 6f;

        readonly List<CardView> _cards = new List<CardView>();
        readonly List<RectTransform> _sockets = new List<RectTransform>();

        /// <summary>Sutun basina kartlar; son eleman en altta duran (acik) karttir.</summary>
        readonly List<List<CardView>> _columnCards = new List<List<CardView>>();

        RectTransform _rect;
        CategoryManager _rules;

        float _lastActivityTime;
        int _hintIndex;
        bool _dealt;

        public IReadOnlyList<CardView> Cards => _cards;
        public RectTransform Root => _rect;

        // ---------------------------------------------------------------- kurulum

        /// <summary>
        /// Sutun sayisi, kart orani ve ust uste binme miktarini kod tarafindan verir.
        /// Build() ve MeasureHeight()'ten ONCE cagrilmalidir.
        /// </summary>
        public void Configure(int columns, float cardAspect, float revealRatio)
        {
            _columns = Mathf.Clamp(columns, 2, 6);
            _cardAspect = Mathf.Clamp(cardAspect, 0.8f, 1.6f);
            _revealRatio = Mathf.Clamp(revealRatio, 0.18f, 0.70f);
        }

        /// <summary>Her destede kac kartin acik duracagini belirler. Build() oncesi cagir.</summary>
        public void SetFaceUpCount(int count)
        {
            _faceUpPerColumn = Mathf.Clamp(count, 1, 3);
        }

        /// <summary>
        /// Verilen kart genisliginde bu kadar kartin sigmasi icin gereken toplam yukseklik.
        /// Yerlesimi yapan kod once bunu sorar, sonra Build() cagirir.
        /// </summary>
        public float MeasureHeight(float cardWidth, int cardCount)
        {
            float cardHeight = cardWidth * _cardAspect;
            int deepest = DeepestIndex(cardCount);
            return cardHeight + deepest * cardHeight * _revealRatio;
        }

        /// <summary>En kalabalik sutunda kac kart var (0 tabanli en derin indeks).</summary>
        int DeepestIndex(int cardCount)
        {
            int tallest = Mathf.CeilToInt(cardCount / (float)_columns);
            return Mathf.Max(0, tallest - 1);
        }

        /// <summary>
        /// Desteleri kurar: her sutun icin bos yuvalar acar ve kart olcusunu dondurur.
        /// </summary>
        /// <param name="cardCount">Kac yuva acilacak.</param>
        /// <param name="cardWidth">Bir kartin genisligi (ust seritteki slotla ayni).</param>
        /// <param name="columnSpacing">Sutunlar arasi bosluk (ust seritle ayni).</param>
        public Vector2 Build(int cardCount, float cardWidth, float columnSpacing)
        {
            _rect = (RectTransform)transform;

            float cardHeight = cardWidth * _cardAspect;
            float step = cardHeight * _revealRatio;

            float width = _columns * cardWidth + (_columns - 1) * columnSpacing;
            float height = cardHeight + DeepestIndex(cardCount) * step;
            _rect.sizeDelta = new Vector2(width, height);

            var cellSize = new Vector2(cardWidth, cardHeight);

            _columnCards.Clear();
            for (int c = 0; c < _columns; c++) _columnCards.Add(new List<CardView>());

            // Kartlar sutunlara SIRAYLA dagitilir (0,1,2,3,4,0,1,...): boylece ayni
            // kategorinin kartlari tek bir destede toplanmaz.
            //
            // Yuvalar da bu sirayla uretildigi icin bir sutunda derinlesen kart
            // her zaman bir oncekinden SONRA gelir; ust uste binmede alttaki kart
            // ustte cizilir, tipki elde tutulan deste gibi.
            for (int i = 0; i < cardCount; i++)
            {
                int column = i % _columns;
                int depth = i / _columns;

                var socket = EcoUi.Rect("Socket_" + i, _rect, cellSize);
                socket.anchoredPosition = new Vector2(
                    (column - (_columns - 1) * 0.5f) * (cardWidth + columnSpacing),
                    height * 0.5f - cardHeight * 0.5f - depth * step);

                // Bos yuvanin soluk izi: kart cikinca destenin ayak izi okunur kalir.
                var ghost = EcoUi.Panel("Ghost", socket, cellSize,
                    Mathf.RoundToInt(cardWidth * 0.15f), EcoPalette.SocketGhost);
                EcoUi.Stretch(ghost.rectTransform);

                _sockets.Add(socket);
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
            _columnCards[socketIndex % _columns].Add(card);

            // Oyuncu etkilesimi ipucu sayacini sifirlasin.
            card.DragStarted += HandleCardTouched;
            card.Tapped += HandleCardTouched;
            card.DragEnded += HandleCardReleased;
        }

        /// <summary>
        /// Tum kartlar eklendikten sonra cagrilir: destelerin yuzunu kapatir ve
        /// kartlari sirayla asagidan sekerek yerine oturtur.
        /// </summary>
        public void PlayDealIn()
        {
            // Yuz durumunu dagitimdan ONCE, animasyonsuz uygula: kartlar zaten
            // kapali olarak ucup gelsin, yerine oturduktan sonra cevrilmesin.
            for (int c = 0; c < _columnCards.Count; c++) RefreshColumn(c, false);

            for (int i = 0; i < _cards.Count; i++)
                if (_cards[i] != null) _cards[i].PlayDealIn(i * _dealStagger);

            _dealt = true;
            _lastActivityTime = Time.unscaledTime + _cards.Count * _dealStagger;

            Subscribe();
        }

        /// <summary>Yuva sayisi (kurulumda acilan kart sayisi).</summary>
        public int SocketCount => _sockets.Count;

        void OnDestroy()
        {
            Unsubscribe();

            foreach (var card in _cards)
            {
                if (card == null) continue;
                card.DragStarted -= HandleCardTouched;
                card.Tapped -= HandleCardTouched;
                card.DragEnded -= HandleCardReleased;
            }
        }

        // ---------------------------------------------------------------- kapali yuz akisi

        void Subscribe()
        {
            _rules = _rules != null ? _rules : CategoryManager.Instance;
            if (_rules == null) return;

            _rules.CardAccepted += HandleCardAccepted;
        }

        void Unsubscribe()
        {
            if (_rules == null) return;
            _rules.CardAccepted -= HandleCardAccepted;
        }

        /// <summary>
        /// Kart bir kategoriye gitti: destesinden dus, altta kalan yeni kart cevrilsin.
        /// Bu olayi dinliyoruz cunku kart hem surukleyerek hem tek dokunusla
        /// oynanabiliyor; iki yolun da ortak noktasi kural motorunun kabulu.
        /// </summary>
        void HandleCardAccepted(CardView card, CategorySlotView slot)
        {
            for (int c = 0; c < _columnCards.Count; c++)
            {
                if (!_columnCards[c].Remove(card)) continue;
                RefreshColumn(c, true);
                break;
            }

            _lastActivityTime = Time.unscaledTime;
        }

        /// <summary>
        /// Bir destenin yuz durumunu tazeler: en alttaki _faceUpPerColumn kart acik
        /// ve oynanabilir, digerleri kapali ve dokunulamaz.
        /// </summary>
        void RefreshColumn(int column, bool animate)
        {
            if (column < 0 || column >= _columnCards.Count) return;

            var cards = _columnCards[column];
            int firstFaceUp = Mathf.Max(0, cards.Count - _faceUpPerColumn);

            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                if (card == null) continue;

                bool faceUp = i >= firstFaceUp;

                // Once oynanabilirlik: kapanan kart ayni karede dokunus almasin.
                card.SetInteractable(faceUp);
                card.SetFaceUp(faceUp, animate && faceUp);
            }
        }

        // ---------------------------------------------------------------- ipucu

        void Update()
        {
            if (!_dealt || _idleHintSeconds <= 0f) return;
            if (Time.unscaledTime - _lastActivityTime < _idleHintSeconds) return;

            _lastActivityTime = Time.unscaledTime;
            PulseNextPlayableCard();
        }

        /// <summary>
        /// Su an oynanabilir bir karti nefes aldirir ve o karti dondurur.
        /// Ipucu dugmesi de burayi cagirir; hicbir kart bulunamazsa null doner.
        /// </summary>
        public CardView PulseNextPlayableCard()
        {
            if (_cards.Count == 0) return null;

            _rules = _rules != null ? _rules : CategoryManager.Instance;

            for (int step = 0; step < _cards.Count; step++)
            {
                _hintIndex = (_hintIndex + 1) % _cards.Count;
                var card = _cards[_hintIndex];

                if (card == null || !card.CanDrag) continue;

                // Gidebilecegi bir slot kalmadiysa oyuncuyu yanlis yone itmeyelim.
                if (_rules != null && _rules.ResolveSlot(card.Data) == null) continue;

                card.PlayHintPulse();
                _lastActivityTime = Time.unscaledTime;
                return card;
            }

            return null;
        }

        void HandleCardTouched(CardView card) => _lastActivityTime = Time.unscaledTime;

        void HandleCardReleased(CardView card, bool accepted) => _lastActivityTime = Time.unscaledTime;
    }
}
