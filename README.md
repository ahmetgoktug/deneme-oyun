# deneme-oyun

Unity 2D (URP) projesi — iki kişilik ortak geliştirme.

| | |
|---|---|
| **Unity sürümü** | `6000.5.9f1` (birebir aynı olmalı) |
| **Şablon** | 2D Cross-Platform (URP) |
| **Render pipeline** | Universal RP — 2D Renderer |

---

## İlk kurulum (her yeni makinede bir kez)

### 1. Unity 6000.5.9f1'i kur

Unity Hub → *Installs* → *Install Editor* → **tam olarak `6000.5.9f1`**.

> Farklı bir sürümle açarsanız Unity tüm proje dosyalarını kendi sürümüne göre
> yeniden yazar. Bu, karşı tarafta devasa ve çözülmesi zor çakışmalar üretir.
> Sürüm eşleşmesi bu projedeki en kritik kuraldır.

### 2. Git LFS'i etkinleştir

Görseller, sesler ve modeller normal Git yerine LFS üzerinden saklanıyor.
LFS kurulu değilse bu dosyalar bilgisayarınıza küçük metin dosyaları olarak
iner ve Unity'de bozuk görünür.

```bash
git lfs install
```

### 3. Repoyu klonla

GitHub Desktop → *Clone repository* → `ahmetgoktug/deneme-oyun`

Daha önce boşken klonladıysan klonlamana gerek yok, sadece **Fetch origin → Pull** yeterli.

### 4. Sahne birleştirme aracını ayarla

Unity sahne (`.unity`) ve prefab (`.prefab`) dosyaları YAML formatında. Git bunları
tek başına birleştiremez; Unity'nin kendi aracı gerekir. Repo klasöründe **bir kez** çalıştır:

```bash
git config merge.unityyamlmerge.name "Unity SmartMerge"
```

```bash
git config merge.unityyamlmerge.driver '"C:\Program Files\Unity\Hub\Editor\6000.5.9f1\Editor\Data\Tools\UnityYAMLMerge.exe" merge -p --force --fallback none %O %B %A %A'
```

> Unity'yi farklı bir konuma kurduysan yoldaki `C:\Program Files\Unity\Hub\Editor` kısmını kendi kurulumuna göre düzelt.
> macOS'ta yol: `/Applications/Unity/Hub/Editor/6000.5.9f1/Unity.app/Contents/Tools/UnityYAMLMerge`

### 5. Projeyi aç

Unity Hub → *Add* → *Add project from disk* → klonladığın klasörü seç.

İlk açılış birkaç dakika sürer (Unity `Library/` klasörünü sıfırdan üretir). Bu normaldir.

---

## Günlük çalışma akışı

**Çalışmaya başlamadan önce mutlaka `Pull` yap.** Unity'yi açmadan önce çekmek en temizi —
proje dosyaları Unity açıkken değişirse Editor kafası karışabilir.

**İşin bitince `Commit` + `Push` yap.** Günlerce lokalde biriktirme; ne kadar beklerse
çakışma ihtimali o kadar büyür.

### Altın kural: aynı anda aynı sahnede çalışmayın

`.unity` ve `.prefab` dosyalarındaki çakışmalar otomatik çözülmez ve çoğu zaman
birinin işini çöpe atmakla sonuçlanır. İkiniz de sahne düzenleyecekse:

- Önceden konuşup **kim hangi sahnede/prefab'de çalışacak** belirleyin, veya
- Herkes kendi sahnesinde çalışsın, sonra birleştirin, veya
- İşi **prefab**'lere bölün — ayrı prefab'ler ayrı dosyadır, çakışmaz

Kod dosyaları (`.cs`) için böyle bir kısıt yok; Git bunları sorunsuz birleştirir.

### Branch kullanımı

Küçük değişiklikler için doğrudan `main` yeterli. Daha büyük bir özellik için:

```bash
git checkout -b ozellik-adi
```

İş bitince GitHub'da Pull Request açıp birleştirin.

---

## Sık karşılaşılan durumlar

**"Unity açılışta sürüm uyarısı verdi"** → Yanlış Unity sürümü kurulu.
*Upgrade* deme, Editor'ü kapat ve `6000.5.9f1`'i kur.

**"Sahnede her şey pembe / görseller bozuk"** → Git LFS kurulu değil.
`git lfs install` çalıştır, sonra `git lfs pull`.

**"Çok fazla dosya değişmiş görünüyor"** → Muhtemelen `Library/` commit edilmiş.
`Library/`, `Temp/`, `obj/`, `Build/` klasörleri asla commit edilmez — `.gitignore` bunları zaten dışlıyor.

**Sahne çakışması oldu** → Panikleyip rastgele çözme. Karşı tarafla konuş,
kimin versiyonunun temel alınacağına karar verin, sonra diğerinin değişikliğini elle tekrar uygulayın.

---

## Eco-Sort oyun mimarisi

Oyun `Assets/Scripts/EcoSort/` altında, dört katmana ayrılmış durumda. Katmanlar
tek yönlü konuşur: **View kural bilmez, Core görsel bilmez.**

| Katman | Dosyalar | Sorumluluk |
|---|---|---|
| `Data/` | `CardData`, `CategoryData`, `CategoryKind` | ScriptableObject içerik. Kod değişmeden kart/kategori eklenir. |
| `Core/` | `CategoryManager`, `SlotManager`, `SandboxBoard` | Kural motoru, slot şeridi, ekran kurulumu. |
| `View/` | `CardView`, `CategorySlotView`, `CardTray`, `BotanicalFrame`, `SafeAreaFitter` | Sürükle-bırak, kapalı/açık yüz, animasyon, dekor, dokunsal geri bildirim. |
| `Utils/` | `EcoUi`, `EcoTween`, `EcoPalette`, `IconFactory`, `UiSpriteFactory`, `EcoConfetti`, `EcoAudio` | Asset gerektirmeyen görsel/ses altyapısı. |

### Ekran düzeni

Pano `SandboxBoard` tarafından çalışma zamanında kurulur (prefab yok):

```
Canvas (Screen Space - Overlay, CanvasScaler 1080x1920, Match = Width)
 ├─ Background   mor/lila gradyan + radyal ışık + soluk dekor daireler
 │   └─ Botanical_Back    kenar sarmaşıkları, çiçekler, pırıltı (oyunun arkasında)
 ├─ SafeArea     SafeAreaFitter (çentik/gesture çubuğu)
 │   ├─ TopBar      başlık + kategori tikleri + "0/5" hapı
 │   ├─ Status      tek satırlık yönlendirme metni
 │   ├─ SlotRow     ANA KARTLAR → HorizontalLayoutGroup, kategori başına bir kart
 │   ├─ CardTray    KAPALI DESTELER → üst üste yığılmış kart sütunları
 │   └─ BottomBar   hamle sayacı · ipucu düğmesi · kombo göstergesi
 ├─ Botanical_Front  köşe yaprakları (oyunun önünde, soluk)
 └─ DragLayer    sürüklenen kart, konfeti ve bitiş banner'ı
```

Üstteki her **ana kart** bir kategoriyi temsil eder: krem gövde, tepesinde amber
başlık şeridi, ortada amblem, altta `1/3` sayacı.

**Kapalı yüz akışı:** Her destede yalnızca en alttaki kart açıktır ve
sürüklenebilir. O kart bir kategoriye gidince `CardTray` bunu
`CategoryManager.CardAccepted` üzerinden duyar, kartı destesinden düşürür ve
üstünde kalan kartı çevirir. Deste aşağıdan yukarı erir; sütunların üst hizası
hiç değişmez. Kartlar kategoriye göre değil karışık dağıtılır, yani bir sütun
tek başına bir kategoriyi çözmez.

**Zorluk ayarı:** Deste sayısı `_cardColumns` ile verilir. Ana kart sayısına
eşitlenirse (veya 0 bırakılırsa) desteler slotlarla birebir hizalanır; azaltmak
desteleri derinleştirir, aynı anda daha az kart açık kalır ve oyun zorlaşır.
`_revealRatio` kartların ne kadar üst üste bineceğini belirler — küçük değer
daha çok kartı gizler.

**Girdi:** Kart yalnızca **sürükleyerek** oynanır. Tek dokunuşla otomatik
yerleştirme `CardView._tapToPlay` ile kapalıdır (`SandboxBoard._tapToPlay`
üzerinden verilir); dokunmak sadece `Tapped` olayını yayınlar.

**Botanik dekor:** `BotanicalFrame` iki kenardan yükselen sarmaşıkları, üstlerindeki
yaprak ve çiçekleri, artı zemine dağılmış pırıltıyı prosedürel üretir — sanat
asseti gerekmez. Salınım tek bir `Update` döngüsünde, her parça kendi faziyle
yürür. Dekorun tamamı raycast geçirmez; hiçbir dokunuşu yutmaz.

**Neden kartlar Layout Group'un doğrudan çocuğu değil:** Layout Group her karede
`anchoredPosition`'ı geri yazar ve sürükleme/geri dönüş animasyonlarını bozar.
Bu yüzden kart, konumu sabit bir *yuvanın* (socket) çocuğudur.

**Dikey boşluk:** Bloklar önce ölçülür, sonra yerleştirilir; artan boşluk
aralara eşit dağıtılır. Destelerin ne kadar açılacağı da (`_revealRatio` ile
`_maxRevealRatio` arasında) kalan alana göre seçilir — az kartla deste açılır,
kart sayısı artınca kendiliğinden sıkışır.

**Renk uyarısı:** Proje Linear renk uzayında. Renkleri doku içine *pişirmeyin*
(ham sRGB baytları ekranda soluk çıkar); `Image.color` üzerinden verin. Zemin
gradyanı bu yüzden "düz koyu renk + alfası azalan açık katman" olarak kurulur
(`UiSpriteFactory.VerticalFade`).

### Eşleşme akışı

```
CardView.OnDrop / OnPointerClick
        ↓
CategoryManager.TryPlaceCard / TryAutoPlace / TryMatchCards   ← tek karar noktası
        ↓ kabul
CategorySlotView.AttachCard  →  3/3 olunca  PlayCompleteAndClear
        ↓
SlotManager.OnCategoryCompleted  →  OnAllCategoriesCompleted
```

### Yeni kategori eklemek

1. `Assets/EcoSort/Data/` içinde sağ tık → *Create > Eco-Sort > Category Data*.
2. Aynı menüden 3 adet *Card Data* üret, `_category` alanını yeni kategoriye bağla.
3. Kategorinin `_cards` listesine kartları ekle.
4. Sahnedeki `EcoSortGameManager` → `SandboxBoard` → *Icerik* listesine kategoriyi ekle.

Ölçüler ekran genişliğine oranlı hesaplandığı için slot ve kart boyutlarını elle
düzeltmeye gerek yoktur.

### Kart görselleri

- Elle çizilmiş görseller `Assets/EcoSort/Art/CardIcons/` klasörüne konur;
  **Eco-Sort > Kart Ikonlarini Bagla** menüsü dosya adına bakarak kartlara bağlar.
- Görseli olmayan kartlar için `IconFactory` çalışma zamanında kategori renginde
  bir kart yüzü üretir. **Eco-Sort > Eksik Kart Yuzlerini Uret (PNG)** menüsü
  aynı yüzü PNG asset'i olarak diske yazar; sanatçı o dosyanın üzerine çizer.
