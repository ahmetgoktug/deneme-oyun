using System;
using System.Collections;
using System.Collections.Generic;
using EcoSort.Core;
using EcoSort.Data;
using EcoSort.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EcoSort.View
{
    /// <summary>
    /// Bir tematik grubun toplandigi yuva. Uzerine dogru kart birakildikca doldurur,
    /// hedefe ulasinca kutlama efektini oynatip kartlari panodan siler.
    ///
    /// Sahne kurulumu (SlotManager bunu calisma zamaninda da kurabilir):
    ///   Slot (RectTransform + Image[raycast target] + LayoutElement + CategorySlotView)
    ///     +- Glow      : Image (tamamlaninca parlar)
    ///     +- Emblem    : Image (kategori amblemi, filigran)
    ///     +- CardsRoot : RectTransform (kabul edilen kartlar buraya tasinir)
    ///     +- Pips      : HorizontalLayoutGroup (ilerleme noktalari)
    ///     +- Title     : TMP_Text / Text
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class CategorySlotView : MonoBehaviour, IDropHandler, ICardDropTarget
    {
        [Header("Kategori")]
        [SerializeField] CategoryData _category;

        [Header("Gorsel Parcalar")]
        [SerializeField] Image _iconImage;
        [SerializeField] Image _backgroundImage;
        [SerializeField] Image _glowImage;
        [SerializeField] TMP_Text _titleLabel;
        [SerializeField] TMP_Text _progressLabel;
        [Tooltip("Ilerleme sayaci (\"1/3\"). TMP kurulu degilken kullanilan eski Text surumu.")]
        [SerializeField] Text _progressText;

        [Tooltip("Kabul edilen kartlarin tasinacagi kok. Bos ise slotun kendisi kullanilir.")]
        [SerializeField] RectTransform _cardsRoot;

        [Header("Yerlesim")]
        [Tooltip("Ust uste binen kartlar arasindaki kayma.")]
        [SerializeField] Vector2 _cardStackOffset = new Vector2(16f, -14f);
        [Tooltip("Her kart icin uygulanan hafif egim (derece). Elde tutulan deste hissi verir.")]
        [SerializeField] float _cardTiltStep = 2.5f;
        [Tooltip("Slota giren kartin kucultulme orani. 5 slotlu dar duzende kartlar " +
                 "slotun disina tasmasin diye kullanilir.")]
        [SerializeField, Range(0.15f, 1f)] float _acceptedCardScale = 0.34f;

        [Header("Tamamlanma")]
        [Tooltip("Kartlarin tek tek silinmesi arasindaki gecikme. Tatmin edici zincir hissi.")]
        [SerializeField] float _clearStagger = 0.07f;
        [SerializeField] Transform _vfxAnchor;

        readonly List<CardView> _cards = new List<CardView>();
        readonly List<Image> _pips = new List<Image>();

        RectTransform _rect;
        Image _completeBadge;
        bool _cleared;

        public CategoryData Category => _category;
        public int Count => _cards.Count;
        public int Required => _category != null ? _category.RequiredCardCount : 0;
        public bool IsComplete => _category != null && _cards.Count >= _category.RequiredCardCount;
        public IReadOnlyList<CardView> Cards => _cards;

        /// <summary>Kutlama efekti oynatildi mi? (Tamamlanan slot tekrar kart almaz.)</summary>
        public bool IsCleared => _cleared;

        RectTransform CardsRoot => _cardsRoot != null ? _cardsRoot : _rect;

        // ---------------------------------------------------------------- yasam dongusu

        void Awake()
        {
            _rect = (RectTransform)transform;
            Refresh();
        }

        void OnEnable()
        {
            if (CategoryManager.Instance != null) CategoryManager.Instance.RegisterSlot(this);
        }

        void OnDisable()
        {
            if (CategoryManager.Instance != null) CategoryManager.Instance.UnregisterSlot(this);
        }

        // ---------------------------------------------------------------- kurulum

        /// <summary>Slotu calisma zamaninda bir kategoriye baglar (bolum uretimi icin).</summary>
        public void Bind(CategoryData category)
        {
            if (CategoryManager.Instance != null) CategoryManager.Instance.UnregisterSlot(this);

            _category = category;
            _cards.Clear();
            _cleared = false;

            if (CategoryManager.Instance != null) CategoryManager.Instance.RegisterSlot(this);
            Refresh();
        }

        /// <summary>
        /// Gorsel parcalari kod tarafindan baglar (proseduel pano kurucusu icin).
        /// Bind()'dan ONCE cagir; renkler Bind sirasinda uygulanir.
        /// </summary>
        public void ConfigureVisuals(Image background, Image glow = null, RectTransform cardsRoot = null,
            Image emblem = null, Image completeBadge = null)
        {
            _backgroundImage = background;
            _glowImage = glow;
            _cardsRoot = cardsRoot;
            _iconImage = emblem;
            _completeBadge = completeBadge;
        }

        /// <summary>Ilerleme sayacini baglar (proseduel kurucu TMP yerine eski Text kullanir).</summary>
        public void ConfigureProgressLabel(Text label)
        {
            _progressText = label;
            UpdateProgressVisuals();
        }

        /// <summary>Ilerleme noktalarini (pip) baglar. Sayilari Required kadar olmali.</summary>
        public void ConfigurePips(IEnumerable<Image> pips)
        {
            _pips.Clear();
            if (pips != null) _pips.AddRange(pips);
            UpdateProgressVisuals();
        }

        /// <summary>Kart yigininin kayma, egim ve olcek degerlerini ekran olcusune gore ayarlar.</summary>
        public void ConfigureLayout(Vector2 stackOffset, float tiltStep, float acceptedCardScale)
        {
            _cardStackOffset = stackOffset;
            _cardTiltStep = tiltStep;
            _acceptedCardScale = Mathf.Clamp(acceptedCardScale, 0.15f, 1f);
        }

        public void Refresh()
        {
            if (_category == null) return;

            if (_titleLabel != null) _titleLabel.text = _category.DisplayName;

            if (_iconImage != null)
            {
                // Kategoriye ozel sprite yoksa amblem siluetiyle bir filigran ciz.
                _iconImage.sprite = _category.Icon != null
                    ? _category.Icon
                    : IconFactory.GetSprite(_category.Emblem);
                _iconImage.enabled = _iconImage.sprite != null;
            }

            if (_backgroundImage != null)
            {
                // Slot, kategorinin renginin cok soluk bir tonuyla boyanir.
                _backgroundImage.color = _category.AccentColor.WithAlpha(0.18f);
            }

            if (_glowImage != null)
            {
                _glowImage.color = _category.AccentColor;
                // OnValidate icinden SetActive cagirmak Unity uyarisi uretir; sadece
                // oyun calisirken gorunurluge dokun.
                if (Application.isPlaying) _glowImage.gameObject.SetActive(false);
            }

            if (_completeBadge != null)
            {
                _completeBadge.color = EcoPalette.Success;
                if (Application.isPlaying) _completeBadge.gameObject.SetActive(false);
            }

            UpdateProgressVisuals();
        }

        void UpdateProgressVisuals()
        {
            if (_category == null) return;

            // Tamamlanan grupta kartlar silinir; sayac yine de dolu gorunmeli.
            int shown = _cleared ? _category.RequiredCardCount : _cards.Count;

            if (_progressLabel != null)
                _progressLabel.text = $"{shown}/{_category.RequiredCardCount}";

            if (_progressText != null)
                _progressText.text = $"{shown}/{_category.RequiredCardCount}";

            // Pip'ler: dolu olanlar kategori renginde, bos olanlar soluk.
            // Tamamlanan kategoride kartlar silinse de pip'ler dolu KALIR;
            // aksi halde biten grup bos bir slot gibi gorunurdu.
            for (int i = 0; i < _pips.Count; i++)
            {
                if (_pips[i] == null) continue;
                bool filled = _cleared || i < _cards.Count;
                _pips[i].color = filled
                    ? _category.AccentColor
                    : _category.AccentColor.WithAlpha(0.22f);
            }
        }

        // ---------------------------------------------------------------- birakma

        public void OnDrop(PointerEventData eventData)
        {
            var card = eventData.pointerDrag != null ? eventData.pointerDrag.GetComponent<CardView>() : null;
            if (card == null) return;

            if (TryAcceptCard(card)) card.MarkConsumed();
        }

        /// <summary>Karari kural motoruna birakiyoruz; slot yalnizca uygular.</summary>
        public bool TryAcceptCard(CardView card)
        {
            var manager = CategoryManager.Instance;
            if (manager == null)
            {
                Debug.LogError("[EcoSort] Sahnede CategoryManager yok; kart kabul edilemiyor.", this);
                return false;
            }

            return manager.TryPlaceCard(card, this);
        }

        // ---------------------------------------------------------------- yerlestirme

        /// <summary>
        /// CategoryManager dogrulamayi gectikten sonra cagirir.
        /// Karti fiziksel olarak slota tasir ve gostergeleri gunceller.
        /// </summary>
        internal void AttachCard(CardView card)
        {
            if (card == null || _cards.Contains(card)) return;

            _cards.Add(card);

            // Yigini slotun ortasina yasla: kartlar sag alta kaymak yerine dengeli dursun.
            int index = _cards.Count - 1;
            float centered = index - (Required - 1) * 0.5f;
            Vector2 target = _cardStackOffset * centered;

            card.SetInteractable(false);   // gruba giren kart artik suruklenmez
            card.PlaceInto(CardsRoot, target, _acceptedCardScale);
            card.Rect.localRotation = Quaternion.Euler(0f, 0f, -_cardTiltStep * centered);

            UpdateProgressVisuals();
            EcoTween.Punch(_rect, 0.08f, 0.22f);
        }

        /// <summary>
        /// Kutlama: parlama + FX + kartlarin sirayla panodan silinmesi.
        /// Bitince onComplete cagrilir (CategoryManager tahtayi kontrol eder).
        /// </summary>
        internal void PlayCompleteAndClear(Action onComplete = null)
        {
            if (_cleared)
            {
                onComplete?.Invoke();
                return;
            }

            _cleared = true;

            if (!gameObject.activeInHierarchy)
            {
                // Nesne kapaliysa coroutine baslamaz; akis tikanmasin.
                onComplete?.Invoke();
                return;
            }

            StartCoroutine(CompleteRoutine(onComplete));
        }

        IEnumerator CompleteRoutine(Action onComplete)
        {
            EcoTween.Punch(_rect, 0.18f, 0.4f);

            if (_glowImage != null)
            {
                _glowImage.gameObject.SetActive(true);
                var glowGroup = _glowImage.GetComponent<CanvasGroup>();
                if (glowGroup != null)
                {
                    glowGroup.alpha = 0f;
                    EcoTween.Fade(glowGroup, 1f, 0.18f);
                }
            }

            SpawnCompleteVfx();

            // Kartlari sirayla sil: her biri bir onceki bitmeden kisa sure sonra baslar.
            int remaining = _cards.Count;
            for (int i = 0; i < _cards.Count; i++)
            {
                var card = _cards[i];
                if (card == null)
                {
                    remaining--;
                    continue;
                }

                card.PlayClearAndDespawn(i * _clearStagger, () =>
                {
                    if (CategoryManager.Instance != null) CategoryManager.Instance.UnregisterCard(card);
                    // Not: uretimde Destroy yerine havuza (object pool) geri verilmeli.
                    Destroy(card.gameObject);
                    remaining--;
                });
            }

            // Tum kartlar silinene kadar bekle.
            while (remaining > 0) yield return null;

            _cards.Clear();
            UpdateProgressVisuals();

            // Tamamlandi rozetini ac ve slotu dolgun renge cek.
            // Amblem filigrani rozete yer acsin.
            if (_iconImage != null) _iconImage.enabled = false;

            if (_completeBadge != null)
            {
                _completeBadge.gameObject.SetActive(true);
                EcoTween.Scale(_completeBadge.transform, Vector3.one, 0.28f, EcoEase.OutBack);
            }

            if (_backgroundImage != null && _category != null)
                _backgroundImage.color = _category.AccentColor.WithAlpha(0.42f);

            if (_glowImage != null)
            {
                var glowGroup = _glowImage.GetComponent<CanvasGroup>();
                if (glowGroup != null) EcoTween.Fade(glowGroup, 0.45f, 0.35f);
            }

            onComplete?.Invoke();
        }

        void SpawnCompleteVfx()
        {
            if (_category == null) return;

            var anchor = _vfxAnchor != null ? _vfxAnchor : transform;

            // Sanat asseti yoksa proseduel konfeti: kurulum gerektirmez.
            if (_category.CompleteVfxPrefab == null)
            {
                var layer = GetComponentInParent<Canvas>();
                if (layer != null)
                    EcoConfetti.Burst((RectTransform)anchor, (RectTransform)layer.rootCanvas.transform,
                        _category.AccentColor, 22, _rect.rect.width * 0.75f);
                return;
            }

            var fx = Instantiate(_category.CompleteVfxPrefab, anchor.position, Quaternion.identity, anchor);

            // FX kendi omrunu yonetmiyorsa makul bir sure sonra temizle.
            var ps = fx.GetComponent<ParticleSystem>();
            float life = ps != null ? ps.main.duration + ps.main.startLifetime.constantMax : 2f;
            Destroy(fx, life);
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            if (Application.isPlaying) return;
            _rect = (RectTransform)transform;
            Refresh();
        }
#endif
    }
}
