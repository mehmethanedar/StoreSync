# StoreSync — İstanbul / Ukrayna aktarım demosu

İki proje vardır: **UpdateApi** (.NET) ve **client** (Angular). Ayrı agent veya Electron uygulaması gerekmez.

## Çalıştırma

Gereksinimler: .NET SDK 10, Node.js 22.12 veya uyumlu üst sürüm, npm. İlk bağımlılık kurulumu için internet gerekir.

1. Visual Studio'da `UpdateApi/UpdateApi.slnx` dosyasını açın ve UpdateApi profiliyle F5'e basın. Swagger: <http://127.0.0.1:5080/swagger>.
2. İkinci terminalde `client` klasörüne gidin, ilk seferinde `npm ci`, ardından `npm start` çalıştırın.
3. Web ekranı: <http://localhost:4200/home>.

Visual Studio yerine proje kökünden `dotnet run --project UpdateApi` kullanılabilir. Aynı API'yi iki kez başlatmayın; 5080 portunu ve demo verisini tek süreç kullanmalıdır.

## Render'da ücretsiz yayın

[Render'da yayınla](https://render.com/deploy?repo=https://github.com/mehmethanedar/StoreSync)

1. Render hesabınıza giriş yapın ve yukarıdaki bağlantıyı açın.
2. Blueprint için bir ad yazın. Servis planının **Free** olduğunu kontrol edin ve **Deploy Blueprint** düğmesine basın.
3. İlk derleme bitince Render'ın verdiği `https://...onrender.com` adresini açın. Ana sayfa `/home`, Swagger `/swagger` yolundadır.

`render.yaml` tek bir ücretsiz web servisi tanımlar. Dockerfile, Angular'ı derler, .NET API'yi yayınlar ve arayüz dosyalarını API'nin `wwwroot` klasörüne koyar. Böylece arayüz ve API aynı adresten çalışır. Kaynak kodunda yine yalnızca `UpdateApi` ve `client` projeleri vardır.

Render'da uygulama 10000 portunda çalışır; dosya aktarımları aynı servisin yerel HTTP adresini kullanır. Bilgisayarınızın açık kalması gerekmez. Demo dosyaları yayın paketine dahil edilmez; servis ilk açıldığında oluşturulur. Tüm ziyaretçiler aynı demo klasörlerini ve aktarım durumunu kullanır.

Free planda 15 dakika ziyaret olmazsa servis uyur. İlk açılışta yaklaşık bir dakika beklenebilir. Uyuma, yeniden başlatma veya yeni yayın sırasında demo dosyaları ve kayıtlı ilerleme silinir; uygulama başlangıç senaryosunu yeniden oluşturur. Ekrandaki Duraklat/Devam et işlemleri servis çalıştığı sürece kullanılabilir. Sunucu yeniden başlatıldıktan sonra da ilerlemeyi korumak için kalıcı diskli bir plan gerekir. [Render Free planının sınırları](https://render.com/docs/free).

## Demo klasörleri

İlk API açılışında aşağıdaki gerçek klasörler oluşturulur:

```text
StoreSync/
  UpdateApi/                         # Tek ASP.NET Core projesi
    Controllers/                     # Swagger üzerinden kullanılan HTTP uçları
    Models/                          # Aktarım planı ve durum bilgileri
    Services/                        # Karşılaştırma, indirme ve kalıcılık
    DemoData/
      IstanbulSunucu/                # 100 adet 1 MiB örnek dosya
      UkraynaMagaza/                 # İlk durumda 70 dosya
      .storesync/                   # İlerleme ve kısmi dosyalar
  client/                            # Angular web projesi
    src/app/core/                    # API servisi ve modeller
    src/app/layouts/                 # Ortak ekran çerçevesi
    src/app/pages/home/              # Ana sayfa component'i, HTML ve SCSS
    src/app/shared/                  # Ortak ilerleme component'i
```

Kaynakta 100 dosya vardır. Hedefte 60 dosya aynıdır, 10 dosyanın adı ve boyutu aynı fakat içeriği farklıdır, 30 dosya eksiktir. Böylece yalnızca dosya adı/boyutu kontrolünün yetersiz olduğu gösterilir.

## Ekrandan gösterim

1. **Dosyaları analiz et**: içerikler SHA-256 ile karşılaştırılır. 60 aynı dosya atlanır; 10 farklı ve 30 eksik dosya, toplam 40 aktarım planlanır.
2. **Sistemi aktar / Devam et**: bu dosyalar sırayla aktarılır. Dosya sayıları, tamamlanan/kalan sayaçları ve MB ilerlemesi görünür.
3. **Aktarım hızı**: Hızlı veya Yavaş seçilir; aktarım sırasında da değiştirilebilir. Hızlı'da ek bekleme yoktur, Yavaş'ta her 64 KiB bloktan sonra 150 ms beklenir.
4. **Duraklat** ve **Devam et**: tamamlanan dosyalar ve yarım dosyanın byte konumu korunur.
5. Tarayıcıyı kapatıp açın: API çalıştığı sürece aktarım sürer. API yeniden başlatılırsa kayıtlı durumdan devam edilir.
6. Bitince hedefte 100 dosya olur. Tekrar analiz edildiğinde 100 dosya atlanır.
7. **Demoyu sıfırla**: 100 örnek dosya yeniden oluşturulur; başlangıçtaki 100 kaynak / 70 hedef durumuna dönülür ve aktarım planı temizlenir. Hedefte farklı isimli dosyalar varsa korunur. Aktarım sürerken sıfırlama engellenir.

Dosya tablosu aynı, bekleyen veya tamamlanan dosyalara göre filtrelenebilir.

## API uçları

| İstek | Görev |
|---|---|
| GET /api/transfers/status | Güncel sayaçları ve dosya durumlarını getirir |
| POST /api/transfers/analyze | Kaynak ile hedefi karşılaştırır |
| POST /api/transfers/start | Aktarımı başlatır veya devam ettirir |
| POST /api/transfers/pause | Aktarımı duraklatır |
| POST /api/transfers/speed | Hızı değiştirir: isSlow true (yavaş) veya false (hızlı) |
| POST /api/transfers/reset | Örnek senaryoyu ve planı sıfırlar |
| GET /api/source/files/{fileName} | Kaynak dosyayı Range ve ETag desteğiyle sunar |

Swagger'da toplam 7 işlem vardır. API 2 controller, 2 servis ve aynı süreçte çalışan 1 arka plan sınıfından oluşur.

Başlatma ve hız değiştirme isteklerinde yalnızca seçim gönderilir: `{ "isSlow": true }` yavaş, `{ "isSlow": false }` hızlı. Bekleme süresi servis içinde belirlenir. Durum yanıtındaki `delayMilliseconds` mevcut bekleme süresini gösterir; JSON ilerleme dosyasının önceki kayıtları da okunmaya devam eder.

## Kodun kısa haritası

| Sınıf | Görevi |
|---|---|
| Program | Controller, servis ve arka plan görevi kayıtları |
| TransfersController | Analiz, başlat/devam, duraklat, hız, durum ve sıfırlama uçları |
| SourceFilesController | İstanbul dosyalarını ETag ve HTTP Range ile sunma |
| DemoFolderService | Örnek dosyalar, güvenli dosya yolları, hash ve dosya karşılaştırma |
| TransferService | Aktarım planını oluşturma, dosyaları indirme, doğrulama ve ilerlemeyi kaydetme |
| TransferWorker | Aynı API süreci içinde bekleyen aktarımı çalıştırma |
| HomeComponent | Sayaçlar, liste ve butonlar |
| Angular UpdateService | Controller'a HTTP istekleri gönderme |

Akış: **Angular → Controller → TransferService → HTTP kaynak dosyası → doğrulama → Ukrayna klasörü**.

İndirme `File.Copy` ile yapılmaz: aynı API'nin kaynak dosya uçlarına HTTP isteği gönderilir. Hash'i aynı olan hedef dosyaları hiç indirilmez. `File.Copy` yalnızca örnek senaryoyu oluştururken aynı hedef dosyaları hazırlamak için kullanılır.

## Kalıcılık ve kurallar

- Plan ve ilerleme `.storesync/state.json` içine geçici dosya + rename ile kaydedilir.
- Kısmi dosya, adı ve kaynak hash'iyle `.storesync/partial` altında tutulur. Devam konumu kısmi dosyanın gerçek boyutudur.
- Devam isteğinde Range ve If-Range gönderilir. HTTP 206 Content-Range doğrulanır. Sunucu 200 dönerse kısmi dosyayı baştan yazar.
- Her blok diske flush edilir. Dosyanın boyutu ve SHA-256 değeri doğrulanmadan hedefteki mevcut dosya değiştirilmez.
- Bozuk kısmi dosya silinir ve yeniden indirilir. Doğru dosya hedefe rename ile alınır.
- Hedefte olup kaynakta olmayan dosyalar korunur. Alt klasörler bu sürüme dahil değildir.
- API aynı veri klasörünü ikinci bir sürecin kullanmasını dosya kilidiyle engeller.
- Aktarım gecikmesi dışında yeniden deneme aralığı bir saniyedir. API kapalıyken aktarım yapılamaz; yeniden başlatıldığında devam eder.

## Derleme ve ayarlar

API derlemesi: proje kökünde `dotnet build UpdateApi`. Angular derlemesi: `client` klasöründe `npm run build`. Yerel web geliştirme sunucusu `/api` isteklerini 5080'e iletir. Render yayınında Angular dosyaları doğrudan API'den sunulur.

İzole veri klasörü için `STORESYNC_DATA`, izole API adresi için `--urls` ve `--Demo:SourceBaseUrl` kullanılabilir. `Demo:SourceBaseUrl`, demo içindeki HTTP dosya kaynağının adresidir; ayrıca uzaktaki dosya kataloğunu otomatik keşfeden bir özellik değildir.

## Demo kapsamı

Bu sürüm, aynı bilgisayardaki iki klasörle İstanbul/Ukrayna sunucularını canlandırır. HTTP ve Range gerçekten kullanılır; gerçek WAN/ülkeler arası bağlantı testi değildir. Dosyalar tarayıcının bilgisayarına değil **API'nin çalıştığı bilgisayardaki** Ukrayna klasörüne yazılır. API'nin tümden kapanması sırasında aktarım durur; yeniden başlatıldığında devam eder.

Üretim için uzak kaynak kataloğu/manifest, kimlik doğrulama, HTTPS, imzalı metadata, kaynak değişimi yönetimi ve güç kesintisine dayanıklı toplu rollback ayrıca tasarlanmalıdır. SHA-256 burada içerik eşitliğini ve bütünlüğünü kontrol eder; kaynağın kimliğini doğrulamaz. Delta patch uygulanmaz: yalnızca değişen dosyalar seçilir, seçilen dosyanın tamamı indirilir; yarım kalan dosyanın eksik kısmı Range ile alınır.
