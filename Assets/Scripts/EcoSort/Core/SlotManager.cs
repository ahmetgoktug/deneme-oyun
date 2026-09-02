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
    /// Ekranin ust seridindeki kategori slotlarini yoneten sinif.
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
    /// Her iki durumda da serit bir <see cref="HorizontalLayoutGroup"/> ile hizalanir,
    /// boylece 3 kategoriden 5 kategoriye cikildiginda hicbir olcu elle duzeltilmez.
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
        [SerializeField, Range(0.8f, 2f)] float _slotAspect = 1.72f;
        [Tooltip("Slota giren kartin kucultulme orani.")]
        [SerializeField, Range(0.15f, 1f)] float _acceptedCardScale = 0.38f;

        // ---------------------------------------------------------------- olaylar

        /// <summary>
        /// Bir kategorinin 3/3 kartı toplandi. Istenen mimarideki
        /// "OnCategoryCompleted" olayi budur.
        /// </summary>
        public event Action<CategoryData, CategorySlotView> OnCategoryCompleted;

        /// <summary>Ilerleme degisti. (slot, mevcut, gereken)</summary>
        public event Action<CategorySlotView, int, int> OnCategoryProgress;

        /// <summary>Bes kategorinin tamami tamamlandi: bolum bitti.</summary>
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
        /// <param name="parent">Seridin ekleneceği kok (genelde SafeArea).</param>
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

            int count = Mathf.Max(1, _categories.Count);

            // Slot genisligi seride sigacak sekilde hesaplanir; Layout Group
            // hizalamayi yapar, biz sadece yuksekligi bildiririz.
            float slotWidth = (rowWidth - spacing * (count - 1)) / count;
            float slotHeight = slotWidth * _slotAspect;

            if (_slots.Count == 0) BuildSlots(slotWidth, slotHeight, spacing);
            else ApplyLayoutToExistingSlots(slotWidth, slotHeight);

            _slotRow.sizeDelta = new Vector2(rowWidth, slotHeight);

            Subscribe();
            return slotHeight;
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
            return new Vector2(slotWidth * 0.20f, -slotWidth * 0.05f);
        }

        // ---------------------------------------------------------------- slot uretimi

        void BuildSlots(float slotWidth, float slotHeight, float spacing)
        {
            int cornerRadius = Mathf.RoundToInt(slotWidth * 0.20f);

            foreach (var category in _categories)
            {
                if (category == null) continue;

                // --- slot govdesi (Layout Group'un dogrudan cocugu)
                var slotRect = EcoUi.Rect($"Slot_{category.CategoryId}", _slotRow,
                    new Vector2(slotWidth, slotHeight));

                var background = slotRect.gameObject.AddComponent<Image>();
                background.sprite = UiSpriteFactory.Rounded(cornerRadius);
                background.type = Image.Type.Sliced;
                background.color = category.AccentColor.WithAlpha(0.18f);
                background.raycastTarget = true;   // birakma hedefi

                EcoUi.FixedSize(slotRect, slotWidth, slotHeight);

                // --- tamamlanma parlamasi (govdenin hemen ustunde)
                var glow = EcoUi.Panel("Glow", slotRect, new Vector2(slotWidth, slotHeight),
                    cornerRadius, category.AccentColor);
                glow.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
                glow.gameObject.SetActive(false);

                // --- kategori amblemi: soluk bir filigran
                var emblemSprite = category.Icon != null
                    ? category.Icon
                    : IconFactory.GetSprite(category.Emblem);

                var emblem = EcoUi.Icon("Emblem", slotRect, Vector2.one * (slotWidth * 0.52f),
                    emblemSprite, category.AccentColor.WithAlpha(0.30f));
                emblem.rectTransform.anchoredPosition = new Vector2(0f, slotHeight * 0.06f);

                // --- baslik
                var title = EcoUi.Label($"Title_{category.CategoryId}", slotRect,
                    new Vector2(slotWidth * 0.94f, slotHeight * 0.22f),
                    Mathf.RoundToInt(slotWidth * 0.155f), EcoPalette.Ink, FontStyle.Bold);
                title.text = category.DisplayName;
                title.rectTransform.anchoredPosition = new Vector2(0f, slotHeight * 0.36f);
                _titles[category] = title;

                // --- ilerleme noktalari (ikinci bir Horizontal Layout Group)
                var pips = BuildPips(slotRect, category, slotWidth, slotHeight);

                // --- kabul edilen kartlarin kutusu (en ustte cizilsin diye en son)
                var cardsRoot = EcoUi.Rect("CardsRoot", slotRect, new Vector2(slotWidth, slotHeight));
                cardsRoot.anchoredPosition = new Vector2(0f, slotHeight * 0.02f);

                // --- tamamlandi rozeti
                // Rozet slotun ORTASINDA durur: kartlar temizlenince bosalan alani doldurur
                // ve baslikla cakismaz.
                var badge = EcoUi.Icon("CompleteBadge", slotRect, Vector2.one * (slotWidth * 0.46f),
                    IconFactory.GetSprite(EcoIcon.Sparkle), EcoPalette.Success);
                badge.rectTransform.anchoredPosition = new Vector2(0f, slotHeight * 0.04f);
                badge.rectTransform.localScale = Vector3.zero;
                badge.gameObject.SetActive(false);

                // --- davranis
                var slot = slotRect.gameObject.AddComponent<CategorySlotView>();
                slot.ConfigureVisuals(background, glow, cardsRoot, emblem, badge);
                slot.ConfigureLayout(StackOffset(slotWidth), 3.5f, _acceptedCardScale);
                slot.Bind(category);
                slot.ConfigurePips(pips);

                _slots.Add(slot);
            }
        }

        List<Image> BuildPips(RectTransform slotRect, CategoryData category, float slotWidth, float slotHeight)
        {
            float pipSize = slotWidth * 0.11f;
            float pipSpacing = slotWidth * 0.06f;
            int required = category.RequiredCardCount;

            var row = EcoUi.Rect("Pips", slotRect,
                new Vector2(pipSize * required + pipSpacing * (required - 1), pipSize));
            row.anchoredPosition = new Vector2(0f, -slotHeight * 0.36f);

            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = pipSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            var pips = new List<Image>(required);
            for (int i = 0; i < required; i++)
            {
                var pip = EcoUi.Disc($"Pip_{i}", row, pipSize, category.AccentColor.WithAlpha(0.22f));
                EcoUi.FixedSize(pip.rectTransform, pipSize, pipSize);
                pips.Add(pip);
            }

            return pips;
        }

        // ---------------------------------------------------------------- olay kopruSU

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
