using UnityEngine;

namespace EcoSort.Data
{
    /// <summary>
    /// Tek bir kartin verisi. Kart hangi kategoriye ait oldugunu kendisi bilir;
    /// eslesme kontrolu bu referans uzerinden yapilir.
    /// </summary>
    [CreateAssetMenu(fileName = "Card_", menuName = "Eco-Sort/Card Data", order = 1)]
    public class CardData : ScriptableObject
    {
        [Header("Kimlik")]
        [Tooltip("Bos birakilirsa asset adi kullanilir.")]
        [SerializeField] string _cardId;
        [SerializeField] string _displayName = "Yeni Kart";

        [Header("Gorsel")]
        [SerializeField] Sprite _artwork;
        [Tooltip("Kart yuzune uygulanacak renk tonu. Beyaz = dokunma.")]
        [SerializeField] Color _tint = Color.white;

        [Header("Kategori")]
        [Tooltip("Kartin ait oldugu tematik grup.")]
        [SerializeField] CategoryData _category;

        [Header("Ipucu")]
        [Tooltip("Oyuncu takildiginda gosterilecek kisa ipucu metni.")]
        [TextArea(1, 3)]
        [SerializeField] string _hint;

        public string CardId => string.IsNullOrEmpty(_cardId) ? name : _cardId;
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;
        public Sprite Artwork => _artwork;
        public Color Tint => _tint;
        public CategoryData Category => _category;
        public string Hint => _hint;

        /// <summary>Kart verilen kategoriye ait mi?</summary>
        public bool BelongsTo(CategoryData category)
        {
            return category != null && _category == category;
        }

        /// <summary>Iki kart ayni tematik gruba mi ait?</summary>
        public bool SharesCategoryWith(CardData other)
        {
            return other != null && _category != null && _category == other._category;
        }
    }
}
