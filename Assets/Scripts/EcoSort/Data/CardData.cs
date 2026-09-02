using EcoSort.Utils;
using UnityEngine;

namespace EcoSort.Data
{
    /// <summary>
    /// Tek bir kartin verisi. Kart hangi kategoriye ait oldugunu kendisi bilir;
    /// eslesme kontrolu bu referans (ve yedek olarak <see cref="CategoryKind"/>)
    /// uzerinden yapilir.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "Eco-Sort/Card Data", order = 1)]
    public class CardData : ScriptableObject
    {
        [Header("Kimlik")]
        [Tooltip("Bos birakilirsa asset adi kullanilir.")]
        [SerializeField] string _cardId;
        [SerializeField] string _displayName = "Yeni Kart";

        [Header("Gorsel")]
        [Tooltip("Elle cizilmis kart gorseli. Bos birakilirsa asagidaki siluetten " +
                 "calisma zamaninda gecici bir kart yuzu uretilir.")]
        [SerializeField] Sprite _artwork;
        [Tooltip("Artwork bos oldugunda kullanilacak proseduel siluet.")]
        [SerializeField] EcoIcon _iconShape = EcoIcon.Bean;
        [Tooltip("Kart yuzune uygulanacak renk tonu. Beyaz = dokunma.")]
        [SerializeField] Color _tint = Color.white;

        [Header("Kategori")]
        [Tooltip("Kartin ait oldugu tematik grup (asil kaynak).")]
        [SerializeField] CategoryData _category;
        [Tooltip("Kategorinin enum karsiligi. CategoryData atandiginda otomatik doldurulur.")]
        [SerializeField] CategoryKind _categoryKind = CategoryKind.None;

        [Header("Ipucu")]
        [Tooltip("Oyuncu takildiginda gosterilecek kisa ipucu metni.")]
        [TextArea(1, 3)]
        [SerializeField] string _hint;

        public string CardId => string.IsNullOrEmpty(_cardId) ? name : _cardId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Sprite Artwork => _artwork;
        public EcoIcon IconShape => _iconShape;
        public Color Tint => _tint;
        public CategoryData Category => _category;
        public string Hint => _hint;

        /// <summary>
        /// Kartin kategorisi (enum). Referans varsa ondan, yoksa serialize edilmis
        /// yedek degerden okunur.
        /// </summary>
        public CategoryKind Kind => _category != null ? _category.Kind : _categoryKind;

        /// <summary>Kategori renginin kisayolu; kart yuzu ve efektler bunu kullanir.</summary>
        public Color AccentColor => _category != null ? _category.AccentColor : EcoPalette.InkMuted;

        /// <summary>Kart verilen kategoriye ait mi?</summary>
        public bool BelongsTo(CategoryData category)
        {
            if (category == null) return false;
            if (_category != null) return _category == category;

            // Referans kopmussa enum uzerinden karar ver: oyun kirilmasin.
            return _categoryKind != CategoryKind.None && _categoryKind == category.Kind;
        }

        /// <summary>Iki kart ayni tematik gruba mi ait?</summary>
        public bool SharesCategoryWith(CardData other)
        {
            if (other == null) return false;
            if (_category != null && other._category != null) return _category == other._category;

            return Kind != CategoryKind.None && Kind == other.Kind;
        }

#if UNITY_EDITOR
        void OnValidate()
        {
            // Enum'i her zaman referanstan turet: iki kaynak birbirinden ayrilmasin.
            if (_category != null) _categoryKind = _category.Kind;
        }
#endif
    }
}
