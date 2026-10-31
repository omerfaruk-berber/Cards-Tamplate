# Veri seti merkezli kart sistemi

## Dataset düzenleme ve güvenli yükleme

Inspector'da başlık araması, tür filtresi ve hata/uyarı filtresi birlikte kullanılabilir.
Doğrulama mesajındaki **İlgili kart / kurala git** düğmesi kaydı seçer ve ilgili kuralı açar.
Seçim, sürükleyerek sıralama ve Undo/Redo sırasında kart kimliğiyle korunur.
**Gelen bağlantılar**, kayıt silinmeden veya türü değiştirilmeden önce hedefleri ve geçmiş koşullarını gösterir.
Silme ve tür değiştirme referansları otomatik yeniden yazmaz; bozuk bağlantılar ilgili alanlarında bildirilir.

Çelişkili koşullar, mevcut cevapla eşleşemeyen koşullar ve önceki kuralla tamamen kapsanan kurallar uyarılır.
Grafik uyarıları olası bağlantıları gösterir; bütün cevap geçmişlerinin kesin analizini yapmaz.
Sequential modunda kullanılmayan dallanma ayarları yine doğrulanmaz.

Ortak görünümün UI bağlantıları Inspector ve çalışma zamanında aynı kontrollerden geçer.
Başarısız **LoadDataset** mevcut oturumu ve görünümü korur.
Oturumun kart erişimleri bağımsız kopyalar döndürür; bu kopyaları değiştirmek oturum akışını veya asseti değiştirmez.
**GameCompleted**, görüntü değişimi dinleyicisi yeni oturum açsa bile tamamlanan oturum için bir kez yayınlanır.

Migration kaynakları önce doğrular; açık sahneyi açıp kaydetmez ve eski prefabları silmez.
Güncel veri üzerinde tekrar çalıştırma değişiklik üretmez. Önceki sürüm kayıtlarının kimlikleri ve içerikleri korunur.

Kartların içeriği, türü ve sırası bir `CardDataset` içinde tutulur. Bütün kartlar veri setindeki **tek ortak görünümden** oluşturulur.

## Hızlı kullanım

1. `Assets/Scenes/MainScene.unity` sahnesini aç.
2. `CardGameController` üzerinde **Dataset** ve **Spawn Point** alanlarını ata. Kontrolcüde başka kart/prefab alanı yoktur.
3. Veri setini seç. **Ortak görünüm** alanı `Assets/Prefabs/Cards/Card View.prefab` olmalıdır.
4. Listedeki **+** düğmesiyle normal kart ekle; başlık, açıklama, görsel ve seçenek metinlerini doldur.
5. **Sequential** veya **Branching** modunu seç ve oyunu başlat.

Yeni veri seti: **Create > Cards > Kart Veri Seti**. Başlangıç, örnek normal kart, bitiş ve ayarlar kayıtları hazır gelir; ortak görünüm otomatik atanır. Her kart için prefab veya ayrı asset oluşturulmaz.

Örnekler:

- `Assets/Data/Cards/DefaultSequential.asset`: Sıralı örnek.
- `Assets/Data/Cards/ButterflyExample.asset`: İlk cevabın birkaç kart sonra sonucu değiştirdiği örnek.
- `Assets/Scripts/CardData/CardDataset.asset`: Mevcut üç kartlı kullanıcı veri seti; kimlikler, içerikler ve seçenekler korunmuştur.

## Kart türleri

Bütün türler aynı listedeki `CardEntry` kayıtlarıdır ve aynı görünümden üretilir.

| Tür | Davranış |
| --- | --- |
| Normal | Sıralı veya geçmiş seçimlere bağlı oyun akışına katılır. |
| Start | Varsa önce açılır. Sağa kaydırma oyunu başlatır; sola kaydırma yerine döner. |
| End | Oyun tamamlanınca açılır ve ekranda kalır. |
| Settings | Ayarlar düğmesiyle açılır; kapanınca önceki karta dönülür. |
| Cheat | Sayısal kimliğiyle açılır; kapanınca önceki karta dönülür. |

En az bir Normal kayıt gerekir. Start, End ve Settings türlerinden en fazla birer kayıt bulunabilir. Cheat kayıtlarının numaraları benzersiz olmalıdır.

Start yoksa ilk Normal kart doğrudan gösterilir. End yoksa son normal kartın çıkışı tamamlanır ve yeni kart üretilmez; `GameCompleted` olayı yayınlanır. Settings veya istenen Cheat kaydı yoksa açma çağrısı açıklayıcı uyarı verir.

Özel kartların listedeki konumu normal sıraya etki etmez. Örneğin **Normal A → Settings → Normal B** listesinde sıralı oyun **A → B** ilerler; Settings yalnız açma çağrısıyla gösterilir.

## Akış modları ve hedef hataları

**Sequential:** Normal kartların liste sırasını kullanır. Her iki cevap da sonraki normal karta geçer. Dallanma hedefleri ve koşulları okunmaz ve doğrulanmaz. Böylece bu modda boş bir hedef kart alanı oyunu engellemez.

**Branching:** Normal kartların sağ/sol seçeneklerindeki geçişler kullanılır:

- **NextInList:** Sonraki normal kart; son normal kartta oyun tamamlanır.
- **Card:** Açılır listeden seçilen normal kart.
- **EndGame:** Oturumu tamamla ve varsa veri setindeki End kartını göster.

Koşullu geçişler yukarıdan aşağıya değerlendirilir. İlk eşleşen kural uygulanır. Bir kuralın bütün koşulları sağlanmalıdır. Eşleşme yoksa varsayılan geçiş kullanılır.

Geçmiş kart koşulları:

- **Yes:** Sağa / evet cevabı verilmiş.
- **No:** Sola / hayır cevabı verilmiş.
- **Unanswered:** Henüz cevaplanmamış; hayır cevabından farklıdır.

Mevcut cevap koşullardan önce kaydedilir. Tekrar ziyaretlerde son cevap geçerlidir. Dallanma hedefleri ve geçmiş koşulları yalnız Normal kartlara bağlanır; özel kartlar normal akışa veya cevap geçmişine girmez.

Branching modunda **Card** seçip hedef kartı boş bırakırsan ilgili kartın sağ/sol alanında hata gösterilir. Silinmiş hedefler sessizce değiştirilmez. Geçerli bir hedef veya NextInList/EndGame seçilmelidir. Sıralı moda geçmek eski dallanma ayarlarını silmez; yeniden Branching seçildiğinde tekrar kontrol edilirler.

Kimlikler otomatik oluşur, sıralama değişse de korunur. Arama, sürükleyerek sıralama, çoğaltma ve Undo desteklenir. Tekil türdeki kartların kopyaları Normal olarak oluşturulur. Cheat kopyasına kullanılmayan bir numara verilir.

## Görünüm ve animasyon

Veri setindeki ortak prefab yalnız tasarım ve UI kontrollerini taşır. Başlıklar, kart açıklamaları, görseller ve oyun geçişleri prefabın içinde tanımlanmaz.

`CardView` bağlantıları:

- Başlık, açıklama ve görsel alanları.
- Başlangıç süsleri ve başlangıç açıklaması.
- Ayarlar kontrollerinin bulunduğu bölüm ve ayarlar başlığı.

`Bind` kart kaydını bu alanlara uygular; tür değişince ilgili bölümler açılıp kapanır. Boş açıklama ve görseller gizlenir. Ayarlar kontrollerinin sabit etiketleri ortak UI tasarımının parçasıdır.

`DragCard` kart türünü ve veri setini bilmez. Sürükleme, eşik kontrolü, giriş/çıkış/geri dönüş animasyonları ve seçenek metinlerinin saydamlığını yönetir. Mevcut süreler, momentum hesapları, dönüş hareketi ve ses tetiklemeleri korunmuştur. Kart oluşturmaz veya kart nesnesini silmez.

Kontrolcü iki ana görünümü yeniden kullanır. Başlangıç, normal ve bitiş kartları aynı havuzu paylaşır. Ayarlar/hile için aynı prefabdan en fazla bir ek görünüm oluşturulup yeniden kullanılır. Geçişte yeni seçim alınmaz; aynı anda yalnız bir geçici kart açılır.

## Kod API'si

```csharp
[SerializeField] private CardGameController cards;

void Examples(CardDataset nextDataset)
{
    bool loaded = cards.LoadDataset(nextDataset);
    cards.RestartGame();
    cards.SpawnOptionsCard();
    cards.SpawnCheatCard(7);
}

void OnEnable()
{
    cards.CurrentCardChanged += OnCurrentCardChanged;
    cards.GameCompleted += OnGameCompleted;
    OnCurrentCardChanged(cards.CurrentCard);
}

void OnDisable()
{
    cards.CurrentCardChanged -= OnCurrentCardChanged;
    cards.GameCompleted -= OnGameCompleted;
}

void OnCurrentCardChanged(DragCard card) { }
void OnGameCompleted() { }
```

`LoadDataset` geçersiz veride false döner ve mevcut oturumu korur. `CurrentCard` giriş animasyonu sürerken de atanabilir; etkileşim/indirme için `IsReady` kontrol edilir. Hazır olma değişimleri `AvailabilityChanged` ile bildirilir.

`Session.Current` mevcut Normal kaydı verir. `Session.GetAnswer(cardId)` son cevabı, `StartCard`, `EndCard`, `SettingsCard` ve `GetCheatCard(int)` oturumdaki diğer kayıtları verir.

Oturum başlarken veri kopyalanır. Cevaplar assete yazılmaz. Yeniden başlatma veya veri seti değiştirme geçmişi temizler. `GameCompleted` tamamlanan oturum başına bir kez yayımlanır.

## Geçiş, indirme ve doğrulama

Eski Play, End ve Options prefabları ortak görünümde birleştirildi. Önceki sürümden kalmış veri setleri için **Tools > Cards > Migrate to Dataset Cards** kullanılabilir. Geçiş otomatik çalışmaz; sürümü güncel veri setlerine tekrar başlangıç/bitiş eklemez.

Kart indirme etkin kartı olay üzerinden takip eder. Ayrı Canvas ve URP kamerasıyla yalnız kart görüntülenir; diğer UI öğeleri çekime girmez. Hareket sırasında indirme düğmesi pasiftir. PNG `Application.persistentDataPath` altında saklanır; `LastSavedPath` son başarılı yolu verir.

**Window > General > Test Runner**:

- **EditMode:** Sıralı modda boş hedefler, dallanma, geçmiş, tür doğrulaması, kimlikler, Undo, oluşturma ve kontrolcünün iki alanlı arayüzü.
- **PlayMode:** Ana sahne, bütün kart türleri, ayarlar düğmeleri, 200 kaydırmada havuz, eşik altı dönüş, ikinci dokunmanın reddi, indirme ve bitiş kartı olmadan tamamlanma.

Görüntü testleri grafik aygıtı gerektirir; `-nographics` kullanma. Android için ayrıca APK derlemesi ve bağlı cihazda kayıt/galeri kontrolü yapılmalıdır.

