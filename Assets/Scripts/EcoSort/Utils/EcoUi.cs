using UnityEngine;
using UnityEngine.UI;

namespace EcoSort.Utils
{
    /// <summary>
    /// Calisma zamaninda UI kurmak icin kucuk yardimcilar.
    ///
    /// Pano proseduel kuruldugu icin (prefab yok) ayni "GameObject olustur,
    /// RectTransform ayarla, Image ekle" kalibi her yerde tekrarlaniyordu.
    /// Kurucular (SlotManager / CardTray / SandboxBoard) artik bu tek kaynagi kullanir.
    /// </summary>
    public static class EcoUi
    {
        static Font s_font;

        /// <summary>Projeye font eklemeden calisan gomulu Unity fontu.</summary>
        public static Font DefaultFont
        {
            get
            {
                if (s_font == null) s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return s_font;
            }
        }

        /// <summary>Bos bir RectTransform olusturur (merkez ankraj).</summary>
        public static RectTransform Rect(string name, Transform parent, Vector2 size = default)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;
            return rect;
        }

        /// <summary>Ebeveynini tamamen kaplayan bir RectTransform olusturur.</summary>
        public static RectTransform FullRect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }

        /// <summary>Yuvarlak koseli panel (9-slice).</summary>
        public static Image Panel(string name, Transform parent, Vector2 size, int cornerRadius,
            Color color, bool raycastTarget = false)
        {
            var rect = Rect(name, parent, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSpriteFactory.Rounded(cornerRadius);
            image.type = Image.Type.Sliced;
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>Daire seklinde gorsel (pip, disk, rozet zemini).</summary>
        public static Image Disc(string name, Transform parent, float diameter, Color color,
            bool raycastTarget = false)
        {
            var rect = Rect(name, parent, Vector2.one * diameter);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = UiSpriteFactory.Circle();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>Sprite tasiyan sade gorsel (ikon, amblem).</summary>
        public static Image Icon(string name, Transform parent, Vector2 size, Sprite sprite, Color color)
        {
            var rect = Rect(name, parent, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Gomulu fontla metin. TMP bagimliligi olmadan calisir.</summary>
        public static Text Label(string name, Transform parent, Vector2 size, int fontSize,
            Color color, FontStyle style = FontStyle.Normal,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var rect = Rect(name, parent, size);

            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = Mathf.Max(8, fontSize);
            text.fontStyle = style;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.color = color;
            // Dar slot basliklarinda uzun kategori adlari sigsin.
            text.resizeTextForBestFit = false;
            return text;
        }

        /// <summary>RectTransform'u ebeveynine tamamen yayar.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Bu RectTransform'un bir Layout Group tarafindan olculendirilmesini engeller
        /// ve sabit bir boyut verir.
        /// </summary>
        public static LayoutElement FixedSize(RectTransform rect, float width, float height)
        {
            var element = rect.gameObject.GetComponent<LayoutElement>();
            if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();

            element.preferredWidth = width;
            element.preferredHeight = height;
            element.minWidth = width;
            element.minHeight = height;
            element.flexibleWidth = 0f;
            element.flexibleHeight = 0f;
            return element;
        }
    }
}
