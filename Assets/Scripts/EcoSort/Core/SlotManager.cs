using System;
using System.Collections.Generic;
using EcoSort.Data;
using EcoSort.Utils;
using EcoSort.View;
using UnityEngine;
using UnityEngine.UI;

namespace EcoSort.Core
{
    /// <summary>
    /// Ekranin ust seridindeki "ana kart" slotlarini yoneten sinif.
    ///
    /// Sorumluluk ayrimi:
    ///   SlotManager      -> slot seridinin KURULUMU ve dis dunyaya acilan olaylar
    ///   CategoryManager  -> "bu kart bu slota gider mi" KURALI
    ///   CategorySlotView -> tek bir slotun gorsel davranisi
    ///
    /// Iki calisma bicimi vardir:
    ///   1) Sahnede elle kurulmus slotlar: _slotRow altindaki CategorySlotView'lari toplar.
    ///   2) Proseduel: Build() cagrilirsa serit + slotlar calisma zamaninda uretilir.
    ///
    /// Slotun gorunumu bir oyun kartidir: krem govde, tepesinde amber kategori
    /// seridi, ortada soluk amblem, altta "1/3" sayaci.
    /// </summary>
    [DisallowMultipleComponent]
    public class SlotManager : MonoBehaviour
    {
        [Header("Icerik")]
        [Tooltip("Ust seritte gosterilecek kategoriler (soldan saga).")]
        [SerializeField] List<CategoryData> _categories = new List<CategoryData>();

        [Header("Sahne")]
        [Tooltip("Slotlarin yerlestigi serit. Bos birakilirsa Build() ile uretilir.")]
        [SerializeField] RectTransform _slotRow;

        [Header("Gorunum")]
        [Tooltip("Slotun yuksekliginin genisligine orani.")]
        [SerializeField, Range(0.8f, 2f)] float _slotAspect = 1.50f;
        [Tooltip("Slota giren kartin kucultulme orani.")]
        [SerializeField, Range(0.15f, 1f)] float _acceptedCardScale = 0.52f;

        // ---------------------------------------------------------------- olaylar

        /// <summary>
        /// Bir kategorinin tum kartlari toplandi. Istenen mimarideki
        /// "OnCategoryCompleted" olayi budur.
        /// </summary>
        public event Action<CategoryData, CategorySlotView> OnCategoryCompleted;

        /// <summary>Ilerleme degisti. (slot, mevcut, gereken)</summary>
        public event Action<CategorySlotView, int, int> OnCategoryProgress;

        /// <summary>Tum kategoriler tamamlandi: bolum bitti.</summary>
        public event Action OnAllCategoriesCompleted;

        // ---------------------------------------------------------------- ic durum

        readonly List<CategorySlotView> _slots = new List<CategorySlotView>();
        readonly Dictionary<CategoryData, Text> _titles = new Dictionary<CategoryData, Text>();

        CategoryManager _rules;
        int _completedCount;

        public IReadOnlyList<CategorySlotView> Slots => _slots;
        public IReadOnlyList<CategoryData> Categories => _categories;
        public int CompletedCount => _completedCount;
        public int TotalCount => _slots.Count;
        public RectTransform SlotRow => _slotRow;

        // ---------------------------------------------------------------- yasam dongusu

        void OnDestroy() => Unsubscribe();

        /// <summary>Kategori listesini kod tarafindan verir (bolum uretimi icin).</summary>
        public void SetCategories(IEnumerable<CategoryData> categories)
        {
            _categories.Clear();
            if (categories == null) return;

            foreach (var category in categories)
                if (category != null) _categories.Add(category);
        }

        // ---------------------------------------------------------------- kurulum

        /// <summary>
        /// Slot seridini kurar. _slotRow atanmissa oradaki mevcut slotlar toplanir,
        /// degilse serit ve slotlar proseduel olarak uretilir.
        /// </summary>
        /// <param name="parent">Seridin eklenecegi kok (genelde SafeArea).</param>
        /// <param name="rowWidth">Seridin toplam genisligi (piksel).</param>
        /// <param name="spacing">Slotlar arasi bosluk (piksel).</param>
        /// <returns>Kurulan seridin yuksekligi.</returns>
        public float Build(RectTransform parent, float rowWidth, float spacing)
        {
            // Build birden fazla kez cagrilirsa olaylara iki kez baglanmayalim.
            Unsubscribe();

            _rules = CategoryManager.Instance;
            _slots.Clear();
            _titles.Clear();
            _completedCount = 0;

            if (_slotRow == null) _slotRow = CreateRow(parent, spacing);
            else CollectExistingSlots();

            float slotWidth = MeasureSlotWidth(rowWidth, spacing);
            float slotHeight = slotWidth * _slotAspect;

            if (_slots.Count == 0) BuildSlots(slotWidth, slotHeight);
            else ApplyLayoutToExistingSlots(slotWidth, slotHeight);

            _slotRow.sizeDelta = new Vector2(rowWidth, slotHeight);

            Subscribe();
            return slotHeight;
        }

        /// <summary>
        /// Bir slotun genisligi. Alt sutunlar bunu sorup kendilerini ayni
        /// genislige ayarlar; slot ile altindaki deste birebir hizali kalir.
        /// </summary>
        public float MeasureSlotWidth(float rowWidth, float spacing)
        {
            int count = Mathf.Max(1, _categories.Count);
            return (rowWidth - spacing * (count - 1)) / count;
        }

        RectTransform CreateRow(RectTransform parent, float spacing)
        {
            var row = EcoUi.Rect("SlotRow", parent);

            // Istenen mimari: ust seritte Horizontal Layout Group.
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;
            layout.childScaleWidth = false;
            layout.childScaleHeight = false;

            return row;
        }

        void CollectExistingSlots()
        {
            _slots.Clear();

            for (int i = 0; i < _slotRow.childCount; i++)
            {
                var slot = _slotRow.GetChild(i).GetComponent<CategorySlotView>();
                if (slot != null) _slots.Add(slot);
            }

            // Sahnede kurulmus slotlarin kategorileri listeyi belirlesin.
            if (_slots.Count > 0 && _categories.Count == 0)
                foreach (var slot in _slots)
                    if (slot.Category != null) _categories.Add(slot.Category);
        }

        void ApplyLayoutToExistingSlots(float slotWidth, float slotHeight)
        {
            foreach (var slot in _slots)
            {
                slot.ConfigureLayout(StackOffset(slotWidth), 3.5f, _acceptedCardScale);
                EcoUi.FixedSize((RectTransform)slot.transform, slotWidth, slotHeight);
            }
        }

        static Vector2 StackOffset(float slotWidth)
        {
            // Kartlar slotun icinde hafif bir yelpaze olusturur.
            return new Vector2(slotWidth * 0.15f, -slotWidth * 0.02f);
        }

        // ---------------------------------------------------------------- slot uretimi

        /// <summary>
        /// Tek bir "ana kart" uretir:
        ///
        ///   Slot            yumusak golge (birakma hedefi)
        ///     +- Body       krem kart govdesi
        ///     |    +- Tint      kategori renginde cok soluk katman
        ///     |    +- Glow      tamamlanma parlamasi
        ///     |    +- Tab       amber baslik seridi
        ///     |    +- Emblem    filigran amblem
        ///     |    +- CountPill "0/3" sayaci
        ///     +- CardsRoot  kabul edilen kartlar (govdenin ustunde)
        ///     +- CompleteBadge
        ///
        /// Govdenin kendisi krem kalir; kategori rengi ayri bir katmanda durur.
        /// Boylece CategorySlotView ilerlemeye gore o katmanin alfasini degistirse
        /// bile kartin krem kimligi bozulmaz.
        /// </summary>
        void BuildSlots(float slotWidth, float slotHeight)
        {
            int cornerRadius = Mathf.RoundToInt(slotWidth * 0.15f);

            foreach (var category in _categories)
            {
                if (category == null) continue;

                // --- kok: yumusak golge. Kart mor zeminden kalkmis gorunsun diye
                // golge koke, krem govde ise onun cocuguna cizilir.
                var slotRect = EcoUi.Rect("Slot_" + category.CategoryId, _slotRow,
                    new Vector2(slotWidth, slotHeight));

                var shadow = slotRect.gameObject.AddComponent<Image>();
                shadow.sprite = UiSpriteFactory.Shadow(cornerRadius, Mathf.RoundToInt(slotWidth * 0.08f));
                shadow.type = Image.Type.Sliced;
                shadow.color = EcoPalette.Shadow;
                shadow.raycastTarget = true;   // birakma hedefi

                EcoUi.FixedSize(slotRect, slotWidth, slotHeight);

                var slotSize = new Vector2(slotWidth, slotHeight);
                var body = EcoUi.Panel("Body", slotRect, slotSize, cornerRadius, EcoPalette.CardFace);
                body.rectTransform.anchoredPosition = new Vector2(0f, slotHeight * 0.015f);

                var bodyRect = body.rectTransform;

                // --- kategori tonu
                var tint = EcoUi.Panel("Tint", bodyRect, slotSize,
                    cornerRadius, category.AccentColor.WithAlpha(0.18f));

                // --- tamamlanma parlamasi
                var glow = EcoUi.Panel("Glow", bodyRect, slotSize, cornerRadius, category.AccentColor);
                glow.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
                glow.gameObject.SetActive(false);

                // --- amber baslik seridi (referanstaki sari sekme)
                float tabHeight = slotHeight * 0.16f;
                var tab = EcoUi.Panel("Tab", bodyRect,
                    new Vector2(slotWidth * 0.88f, tabHeight),
                    Mathf.RoundToInt(tabHeight * 0.42f), EcoPalette.Tab);
                tab.rectTransform.anchoredPosition =
                    new Vector2(0f, slotHeight * 0.5f - tabHeight * 0.80f);

                var title = EcoUi.Label("Title_" + category.CategoryId, tab.rectTransform,
                    new Vector2(slotWidth * 0.80f, tabHeight),
                    Mathf.RoundToInt(slotWidth * 0.115f), EcoPalette.TabInk, FontStyle.Bold);
                title.text = category.DisplayName;
                _titles[category] = title;

                // --- amblem: kartin ortasinda filigran
                var emblemSprite = category.Icon != null
                    ? category.Icon
                    : IconFactory.GetSprite(category.Emblem);

                var emblem = EcoUi.Icon("Emblem", bodyRect, Vector2.one * (slotWidth * 0.62f),
                    emblemSprite, category.AccentColor.WithAlpha(0.48f));
                emblem.rectTransform.anchoredPosition = new Vector2(0f, -slotHeight * 0.02f);

                // --- sayac: "0/3"
                float countHeight = slotHeight * 0.145f;
                var countPill = EcoUi.Panel("CountPill", bodyRect,
                    new Vector2(slotWidth * 0.46f, countHeight),
                    Mathf.RoundToInt(countHeight * 0.5f), category.AccentColor.WithAlpha(0.24f));
                countPill.rectTransform.anchoredPosition =
                    new Vector2(0f, -slotHeight * 0.5f + countHeight * 0.85f);

                var count = EcoUi.Label("Count", countPill.rectTransform,
                    new Vector2(slotWidth * 0.46f, countHeight),
                    Mathf.RoundToInt(slotWidth * 0.115f), EcoPalette.Ink, FontStyle.Bold);
                count.text = "0/" + category.RequiredCardCount;

                // --- kabul edilen kartlarin kutusu (en ustte cizilsin diye en son)
                var cardsRoot = EcoUi.Rect("CardsRoot", slotRect, new Vector2(slotWidth, slotHeight));
                cardsRoot.anchoredPosition = new Vector2(0f, -slotHeight * 0.02f);

                // --- tamamlandi rozeti
                var badge = EcoUi.Icon("CompleteBadge", slotRect, Vector2.one * (slotWidth * 0.44f),
                    IconFactory.GetSprite(EcoIcon.Sparkle), EcoPalette.Success);
                badge.rectTransform.anchoredPosition = new Vector2(0f, -slotHeight * 0.02f);
                badge.rectTransform.localScale = Vector3.zero;
                badge.gameObject.SetActive(false);

                // --- davranis
                var slot = slotRect.gameObject.AddComponent<CategorySlotView>();
                slot.ConfigureVisuals(tint, glow, cardsRoot, emblem, badge);
                slot.ConfigureLayout(StackOffset(slotWidth), 3.5f, _acceptedCardScale);
                slot.Bind(category);
                slot.ConfigureProgressLabel(count);

                _slots.Add(slot);
            }
        }

        // ---------------------------------------------------------------- olay koprusu

        void Subscribe()
        {
            if (_rules == null) _rules = CategoryManager.Instance;
            if (_rules == null) return;

            _rules.CategoryCompleted += HandleCategoryCompleted;
            _rules.CategoryProgressChanged += HandleProgress;
        }

        void Unsubscribe()
        {
            if (_rules == null) return;

            _rules.CategoryCompleted -= HandleCategoryCompleted;
            _rules.CategoryProgressChanged -= HandleProgress;
        }

        void HandleProgress(CategorySlotView slot, int current, int required)
        {
            OnCategoryProgress?.Invoke(slot, current, required);
        }

        void HandleCategoryCompleted(CategoryData category)
        {
            var slot = FindSlot(category);
            _completedCount++;

            OnCategoryCompleted?.Invoke(category, slot);

            if (_completedCount >= _slots.Count)
                OnAllCategoriesCompleted?.Invoke();
        }

        public CategorySlotView FindSlot(CategoryData category)
        {
            if (category == null) return null;

            foreach (var slot in _slots)
                if (slot != null && slot.Category == category) return slot;

            return null;
        }

        /// <summary>Kategorinin ust seritteki basligini gunceller (dil degisimi vb.).</summary>
        public void SetTitle(CategoryData category, string text)
        {
            if (category != null && _titles.TryGetValue(category, out var label) && label != null)
                label.text = text;
        }
    }
}
