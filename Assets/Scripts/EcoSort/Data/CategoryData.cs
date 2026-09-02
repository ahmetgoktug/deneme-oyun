using System.Collections.Generic;
using UnityEngine;

namespace EcoSort.Data
{
    /// <summary>
    /// Bir tematik grubu tanimlar (ornek: "Kahve Keyfi", "Sonbahar").
    /// Kac kart toplandiginda grubun tamamlanacagini ve tamamlandiginda
    /// calacak gorsel/isitsel geri bildirimi tutar.
    /// </summary>
    [CreateAssetMenu(fileName = "Category_", menuName = "Eco-Sort/Category Data", order = 0)]
    public class CategoryData : ScriptableObject
    {
        [Header("Kimlik")]
        [Tooltip("Bos birakilirsa asset adi kullanilir. Kayit/analitik icin sabit tutun.")]
        [SerializeField] string _categoryId;
        [SerializeField] string _displayName = "Yeni Kategori";
        [Tooltip("Kartlarin enum uzerinden eslesebilmesi icin grup turu.")]
        [SerializeField] CategoryKind _kind = CategoryKind.None;

        [Header("Gorsel Kimlik")]
        [SerializeField] Sprite _icon;
        [SerializeField] Color _accentColor = new Color(0.42f, 0.72f, 0.55f);
        [Tooltip("Slot rozeti bos oldugunda cizilecek proseduel amblem.")]
        [SerializeField] Utils.EcoIcon _emblem = Utils.EcoIcon.Leaf;

        [Header("Tamamlanma Kurali")]
        [Tooltip("Grubun temizlenmesi icin gereken kart sayisi (3/3, 4/4 gibi).")]
        [SerializeField, Min(1)] int _requiredCardCount = 3;

        [Tooltip("Bu kategoriye ait kartlar. Level uretimi ve dogrulama icin kullanilir.")]
        [SerializeField] List<CardData> _cards = new List<CardData>();

        [Header("Geri Bildirim (Juice)")]
        [Tooltip("Grup tamamlandiginda slot uzerinde spawn edilecek partikul/FX prefabi.")]
        [SerializeField] GameObject _completeVfxPrefab;
        [SerializeField] AudioClip _cardAcceptedSfx;
        [SerializeField] AudioClip _completeSfx;
        [SerializeField, Range(0f, 1f)] float _sfxVolume = 0.85f;

        [Tooltip("Tamamlaninca ekranda beliren kutlama metni. Ornek: \"Kahve Keyfi!\"")]
        [SerializeField] string _completeMessage;

        public string CategoryId => string.IsNullOrEmpty(_categoryId) ? name : _categoryId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public CategoryKind Kind => _kind;
        public Sprite Icon => _icon;
        public Color AccentColor => _accentColor;
        public Utils.EcoIcon Emblem => _emblem;
        public int RequiredCardCount => Mathf.Max(1, _requiredCardCount);
        public IReadOnlyList<CardData> Cards => _cards;

        public GameObject CompleteVfxPrefab => _completeVfxPrefab;
        public AudioClip CardAcceptedSfx => _cardAcceptedSfx;
        public AudioClip CompleteSfx => _completeSfx;
        public float SfxVolume => _sfxVolume;
        public string CompleteMessage => string.IsNullOrEmpty(_completeMessage) ? DisplayName : _completeMessage;

#if UNITY_EDITOR
        void OnValidate()
        {
            // Gereken kart sayisi, tanimli kart havuzunu asamaz.
            if (_cards.Count > 0 && _requiredCardCount > _cards.Count)
                _requiredCardCount = _cards.Count;
        }
#endif
    }
}
