namespace EcoSort.Data
{
    /// <summary>
    /// Oyundaki tematik gruplar. Kart ve slot eslesmesinin ASIL kaynagi
    /// <see cref="CategoryData"/> referansidir; bu enum ise:
    ///
    ///   - Inspector'da hangi kartin nereye ait oldugunu tek bakista gostermek,
    ///   - Editor araclarinin (ikon uretici, icerik kurucu) kartlari gruplamasi,
    ///   - Referans kopsa bile (asset silindi/tasindi) eslesmenin calismaya devam etmesi
    ///
    /// icin kullanilir. Ikisi CardData.OnValidate() ile otomatik senkron tutulur.
    /// </summary>
    public enum CategoryKind
    {
        None = 0,
        Deniz = 1,
        Kahve = 2,
        Sonbahar = 3,
        Taki = 4,
        Oyun = 5
    }
}
