using UnityEngine;

namespace EcoSort.Utils
{
    /// <summary>
    /// Oyunun renk kimligi: derin mor zeminde lila isik, uzerinde krem kartlar.
    /// Hicbir yerde saf siyah/beyaz yok; ekrana uzun sure bakmak yormasin.
    /// </summary>
    public static class EcoPalette
    {
        // --- zemin: yukarida acik lila, asagida derin mor
        public static readonly Color BackgroundTop = Hex(0x8E6FDC);
        public static readonly Color BackgroundBottom = Hex(0x412474);

        // --- kartlar ve metin
        public static readonly Color CardFace = Hex(0xFCF8FF);
        public static readonly Color Ink = Hex(0x33254F);
        public static readonly Color InkMuted = Hex(0x8B7DAA);

        public static readonly Color Shadow = new Color(0.10f, 0.04f, 0.22f, 0.32f);

        // --- kapali kart yuzu (referanstaki baklava desenli sirt)
        /// <summary>Sirtin dis cercevesi: kart formunu koyu zeminden ayirir.</summary>
        public static readonly Color CardBackFrame = Hex(0xF4EEFF);
        /// <summary>Sirtin ic zemini.</summary>
        public static readonly Color CardBackDeep = Hex(0x6A46D2);
        /// <summary>Ic zemin uzerindeki baklava deseni.</summary>
        public static readonly Color CardBackLight = Hex(0x9F82F0);

        // --- kategori basligi (kartin ustunden tasan serit)
        public static readonly Color Tab = Hex(0xF4C560);
        public static readonly Color TabInk = Hex(0x5C3F10);

        // --- ust/alt cubuk
        /// <summary>HUD haplarinin yari saydam koyu zemini.</summary>
        public static readonly Color HudPanel = new Color(0.16f, 0.07f, 0.31f, 0.55f);
        public static readonly Color HudInk = Hex(0xF2ECFF);
        /// <summary>Bos ilerleme kutusu.</summary>
        public static readonly Color HudSlotEmpty = new Color(0.13f, 0.05f, 0.26f, 0.55f);

        /// <summary>Kartlarin durdugu alt alanin zemini: zeminden bir ton koyu.</summary>
        public static readonly Color TrayFill = new Color(0.20f, 0.09f, 0.38f, 0.30f);

        /// <summary>Bos kart yuvasinin soluk izi.</summary>
        public static readonly Color SocketGhost = new Color(1f, 1f, 1f, 0.055f);

        /// <summary>Basari vurgusu (tamamlanan grup rozeti, bitis banneri).</summary>
        public static readonly Color Success = Hex(0x5FD6A0);

        // --- botanik dekor (kenar sarmasiklari, ciceklerve pirilti)
        public static readonly Color VineStem = Hex(0x4C9B6C);
        public static readonly Color BlossomPetal = Hex(0xF2A0C4);
        public static readonly Color BlossomPetalAlt = Hex(0xF4C560);
        public static readonly Color BlossomHeart = Hex(0xFFF1C9);
        public static readonly Color SparkleWarm = Hex(0xFFF3C4);
        public static readonly Color SparkleMint = Hex(0xCFF3E2);

        static readonly Color[] LeafTones =
        {
            Hex(0x62C48D),
            Hex(0x8FE0B0),
            Hex(0x3F9C6B),
            Hex(0xA8ECC4)
        };

        /// <summary>Sarmasik yapraklarinin ton paleti; indeks tasarsa basa doner.</summary>
        public static Color LeafTone(int index)
        {
            if (index < 0) index = -index;
            return LeafTones[index % LeafTones.Length];
        }

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
