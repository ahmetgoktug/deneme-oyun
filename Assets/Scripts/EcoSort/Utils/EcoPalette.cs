using UnityEngine;

namespace EcoSort.Utils
{
    /// <summary>
    /// Oyunun "cozy / lo-fi" renk kimligi. Sicak krem zemin, yumusak kontrast,
    /// hicbir yerde saf siyah/beyaz yok: ekrana uzun sure bakmak yormasin.
    /// </summary>
    public static class EcoPalette
    {
        public static readonly Color BackgroundTop = Hex(0xFAF4EA);
        public static readonly Color BackgroundBottom = Hex(0xEFE2D0);

        public static readonly Color CardFace = Hex(0xFFFCF7);
        public static readonly Color Ink = Hex(0x3E3A36);
        public static readonly Color InkMuted = Hex(0x9A8F84);

        public static readonly Color Shadow = new Color(0.36f, 0.29f, 0.22f, 0.20f);

        /// <summary>Kartlarin durdugu alt tepsinin zemini: zeminden bir ton koyu.</summary>
        public static readonly Color TrayFill = new Color(0.85f, 0.78f, 0.68f, 0.40f);

        /// <summary>Bos kart yuvasinin soluk izi.</summary>
        public static readonly Color SocketGhost = new Color(0.62f, 0.54f, 0.45f, 0.14f);

        /// <summary>Basari vurgusu (tamamlanan grup rozeti, bitis banneri).</summary>
        public static readonly Color Success = Hex(0x6FBF87);

        public static Color Hex(uint rgb)
        {
            return new Color(
                ((rgb >> 16) & 0xFF) / 255f,
                ((rgb >> 8) & 0xFF) / 255f,
                (rgb & 0xFF) / 255f,
                1f);
        }

        /// <summary>Bir rengin alfasi degistirilmis kopyasi.</summary>
        public static Color WithAlpha(this Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }
    }
}
