using System;
using System.Collections.Generic;
using EcoSort.Data;
using EcoSort.Utils;
using EcoSort.View;
using UnityEngine;

namespace EcoSort.Core
{
    /// <summary>
    /// Oyunun kural motoru. Bir kartin bir slota (veya baska bir kartin uzerine)
    /// birakilmasi gecerli mi, buna tek basina bu sinif karar verir.
    ///
    /// Gorseller (CardView / CategorySlotView) kural bilmez; sadece
    /// "su kart su hedefe birakildi" der ve donen sonuca gore animasyon oynatir.
    /// </summary>
    [DisallowMultipleComponent]
    public class CategoryManager : MonoBehaviour
    {
        public static CategoryManager Instance { get; private set; }

        [Header("Kural Ayarlari")]
        [Tooltip("Kart, iliskili baska bir kartin uzerine birakildiginda otomatik olarak " +
                 "o kategorinin slotuna yonlendirilsin mi?")]
        [SerializeField] bool _routeCardOnCardDrops = true;

        [Tooltip("Kombo zinciri bu sure boyunca eslesme olmazsa sifirlanir.")]
        [SerializeField] float _comboResetSeconds = 4f;

        // ---------------------------------------------------------------- olaylar

        /// <summary>Kart dogru slota yerlesti. (kart, slot)</summary>
        public event Action<CardView, CategorySlotView> CardAccepted;

        /// <summary>Bir kategorinin ilerlemesi degisti. (slot, mevcut, gereken)</summary>
        public event Action<CategorySlotView, int, int> CategoryProgressChanged;

        /// <summary>Kategori tamamlandi; kutlama animasyonu baslamak uzere.</summary>
        public event Action<CategoryData> CategoryCompleted;

        /// <summary>Gecersiz birakma. UI burada nazik bir uyari gosterebilir.</summary>
        public event Action<CardView> MatchRejected;

        /// <summary>Ust uste dogru eslesme sayisi degisti (ses/puan carpani icin).</summary>
        public event Action<int> ComboChanged;

        /// <summary>Panodaki tum kartlar temizlendi: bolum bitti.</summary>
        public event Action BoardCleared;

        // ---------------------------------------------------------------- ic durum

        readonly Dictionary<CategoryData, CategorySlotView> _slots = new Dictionary<CategoryData, CategorySlotView>();
        readonly HashSet<CardView> _boardCards = new HashSet<CardView>();

        int _combo;
        float _lastMatchTime = -999f;

        public int Combo => _combo;
        public int RemainingCards => _boardCards.Count;
        public IReadOnlyCollection<CardView> BoardCards => _boardCards;

        // ---------------------------------------------------------------- yasam dongusu

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[EcoSort] Sahnede birden fazla CategoryManager var. Fazlasi kapatildi.", this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (_combo > 0 && Time.unscaledTime - _lastMatchTime > _comboResetSeconds)
                SetCombo(0);
        }

        // ---------------------------------------------------------------- kayit

        public void RegisterSlot(CategorySlotView slot)
        {
            if (slot == null || slot.Category == null) return;

            if (_slots.TryGetValue(slot.Category, out var existing) && existing != slot)
            {
                Debug.LogWarning($"[EcoSort] '{slot.Category.DisplayName}' kategorisi icin birden fazla slot var. " +
                                 "Kart-uzerine-kart yonlendirmesi ilk slota gider.", slot);
                return;
            }

            _slots[slot.Category] = slot;
        }

        public void UnregisterSlot(CategorySlotView slot)
        {
            if (slot == null || slot.Category == null) return;
            if (_slots.TryGetValue(slot.Category, out var existing) && existing == slot)
                _slots.Remove(slot.Category);
        }

        /// <summary>Tahta kurulurken her kart buraya kaydedilir; bitis kosulu bununla olculur.</summary>
        public void RegisterCard(CardView card)
        {
            if (card != null) _boardCards.Add(card);
        }

        public void UnregisterCard(CardView card)
        {
            if (card == null) return;
            if (_boardCards.Remove(card) && _boardCards.Count == 0)
                BoardCleared?.Invoke();
        }

        /// <summary>Verilen kategoriye hizmet eden slotu dondurur (yoksa null).</summary>
        public CategorySlotView ResolveSlot(CategoryData category)
        {
            if (category == null) return null;
            return _slots.TryGetValue(category, out var slot) ? slot : null;
        }

        // ---------------------------------------------------------------- kural motoru

        /// <summary>Kart bu slota konabilir mi? (animasyon oynatmaz, sadece sorar)</summary>
        public bool CanPlace(CardView card, CategorySlotView slot)
        {
            if (card == null || slot == null || card.Data == null) return false;
            if (slot.IsComplete) return false;
            return card.Data.BelongsTo(slot.Category);
        }

        /// <summary>
        /// Kartin bir kategori slotuna birakilmasi. Slot'un IDropHandler'i burayi cagirir.
        /// Kabul edilirse true doner ve kart slota oturur.
        /// </summary>
        public bool TryPlaceCard(CardView card, CategorySlotView slot)
        {
            if (!CanPlace(card, slot))
            {
                Reject(card);
                return false;
            }

            Accept(card, slot);
            return true;
        }

        /// <summary>
        /// Kartin baska bir kartin uzerine birakilmasi. Iki kart ayni tematik gruptaysa
        /// suruklenen kart o grubun slotuna yonlendirilir; boylece "kartlar birlesir".
        /// </summary>
        public bool TryMatchCards(CardView dragged, CardView target)
        {
            if (dragged == null || target == null || dragged == target) return false;

            if (!_routeCardOnCardDrops ||
                dragged.Data == null || !dragged.Data.SharesCategoryWith(target.Data))
            {
                Reject(dragged);
                return false;
            }

            var slot = ResolveSlot(dragged.Data.Category);
            if (slot == null || slot.IsComplete)
            {
                // Grubu toplayacak bir slot yoksa eslesmeyi tamamlayamayiz.
                Reject(dragged);
                return false;
            }

            Accept(dragged, slot);
            return true;
        }

        /// <summary>
        /// Tek dokunusla oynama (mobil konfor): kart kendi kategorisinin slotuna ucar.
        /// </summary>
        public bool TryAutoPlace(CardView card)
        {
            if (card == null || card.Data == null) return false;

            var slot = ResolveSlot(card.Data.Category);
            if (slot == null || slot.IsComplete)
            {
                Reject(card);
                return false;
            }

            Accept(card, slot);
            return true;
        }

        // ---------------------------------------------------------------- sonuc uygulama

        void Accept(CardView card, CategorySlotView slot)
        {
            _boardCards.Remove(card);   // artik slotun sorumlulugunda

            slot.AttachCard(card);
            card.PlayAccepted();

            SetCombo(_combo + 1);
            _lastMatchTime = Time.unscaledTime;

            var category = slot.Category;
            EcoAudio.PlayStep(category.CardAcceptedSfx, Mathf.Min(_combo - 1, 8), category.SfxVolume);

            CardAccepted?.Invoke(card, slot);
            CategoryProgressChanged?.Invoke(slot, slot.Count, category.RequiredCardCount);

            if (slot.IsComplete) CompleteCategory(slot);
        }

        void CompleteCategory(CategorySlotView slot)
        {
            var category = slot.Category;
            CategoryCompleted?.Invoke(category);

            EcoAudio.Play(category.CompleteSfx, category.SfxVolume);

            // Slot kutlamayi oynatir ve kartlari yok eder; bitince tahtayi kontrol ederiz.
            slot.PlayCompleteAndClear(() =>
            {
                if (_boardCards.Count == 0) BoardCleared?.Invoke();
            });
        }

        void Reject(CardView card)
        {
            SetCombo(0);
            if (card != null) card.PlayRejected();
            MatchRejected?.Invoke(card);
        }

        void SetCombo(int value)
        {
            if (_combo == value) return;
            _combo = Mathf.Max(0, value);
            ComboChanged?.Invoke(_combo);
        }
    }
}
