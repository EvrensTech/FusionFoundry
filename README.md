<a id="top"></a>

<div align="center">

[TR](#tr) · [EN](#en)

</div>

<a id="tr"></a>

# FusionFoundry — Türkçe

**Unity 6 ve Photon Fusion ile oda kodu tabanlı multiplayer oturumları için yeniden kullanılabilir bir başlangıç altyapısı ve çalışan demo.**

FusionFoundry, **Evrens Tech** tarafından geliştirilen Unity teknik demo portföyünün bir parçasıdır. Projenin amacı yalnızca örnek bir multiplayer sahnesi sunmak değil; mevcut Unity projelerine multiplayer dönüşümü, ağ mimarisi, oyuncu yaşam döngüsü ve proje bazlı geliştirme desteği sağlayabilecek teknik yetkinliği çalışan bir sistemle göstermektir.

## Demo ne gösteriyor?

`BasicHostClient` örneği şu uçtan uca akışı uygular:

- Çalışma zamanında yeni bir `NetworkRunner` oluşturma
- Altı karakterli, büyük/küçük harfe duyarlı oda koduyla Host oturumu açma
- İkinci istemcinin aynı kodla odaya katılması
- Her bağlanan oyuncu için ağ nesnesi oluşturma ve ayrıldığında kaldırma
- Unity Input System girdisini Fusion ağına taşıma
- Yerel oyuncuyu üçüncü şahıs kamera ile kontrol etme
- Başlangıç hatası, bağlantı kesilmesi ve oturum kapatma sonrasında Runner'ı temizleme
- Aynı uygulama çalışırken temiz bir Runner ile yeniden oturum başlatma

Oturumlar kodla katılmaya açıktır ancak genel lobi listesinde görünmez. İstemcinin yanlışlıkla yeni oturum oluşturmasına izin verilmez.

## Evrens Tech içindeki rolü


Bu proje aşağıdaki hizmet alanları için teknik kanıt ve başlangıç noktası olarak kullanılır:

- Mevcut Unity projesini multiplayer yapıya dönüştürme
- Photon Fusion tabanlı Host/Client ve oturum akışları
- Oyuncu spawn/despawn ve input authority yönetimi
- Multiplayer modül geliştirme ve entegrasyon
- Mevcut ağ kodunun teknik incelemesi ve iyileştirilmesi
- Unity ekiplerine proje bazlı veya dönemsel geliştirme desteği
- Daha kapsamlı lobby, eşleştirme ve oyun akışları için prototipleme

FusionFoundry tek başına son kullanıcıya yönelik tamamlanmış bir oyun veya genel amaçlı matchmaking ürünü değildir. Müşteri projesine uyarlanabilen, kapsamı kontrollü bir teknik temel ve demo niteliğindedir.

## Mimari

```text
BasicHostClientUI
        │  kullanıcının Kur / Katıl / Ayrıl isteği
        ▼
FusionBootstrap
        │  Runner oluşturma, durum ve yaşam döngüsü
        ▼
FusionSessionController
        │  StartGame / Shutdown
        ▼
Photon Fusion NetworkRunner
        ├── NetworkPlayerSpawner
        └── FusionInputProvider
                │
                ▼
        SampleNetworkPlayerMotor
```

Temel tasarım ilkeleri:

- UI, Photon API'sini doğrudan çağırmaz.
- Her oturum için çalışma zamanında yeni bir Runner oluşturulur.
- Oturum yaşam döngüsü `Idle → Starting → Running → Stopping → Idle` olarak yönetilir.
- Multiplayer altyapısı ile örneğe özel sunum/kamera davranışı assembly sınırlarıyla ayrılır.
- Hareket girdisi ağ katmanında taşınır; örnek oyuncu motoru bu girdiyi Fusion tick'i içinde uygular.

## Teknik yapı

| Bileşen | Depoda doğrulanan sürüm / konum |
|---|---|
| Unity Editor | `6000.5.4f1` |
| Photon Fusion | SDK, `Assets/Photon/Fusion` altında projeye gömülü |
| Input System | `1.19.0` |
| Multiplayer Play Mode | `2.0.2` |
| Render Pipeline | Universal RP `17.5.0` |
| Örnek sahne | `Assets/FusionFoundry/Samples/BasicHostClient/Scenes/BasicHostClient.unity` |

> Photon SDK'sı UPM bağımlılığı olarak değil, `Assets/Photon` altında tutulur. Photon'a ait dosyalar ile FusionFoundry kaynakları birbirinden ayrıdır.

## Kurulum

### Gereksinimler

- Unity Hub
- Unity Editor `6000.5.4f1`
- Photon hesabı ve bir **Fusion App ID**
- İki istemciyle test için standalone build veya Multiplayer Play Mode

### Projeyi çalıştırma

1. Depoyu klonlayın ve Unity Hub üzerinden proje klasörünü açın.
2. Unity'nin paketleri içe aktarmasını ve script derlemesini tamamlamasını bekleyin.
3. Photon Dashboard'da bir Fusion uygulaması oluşturun.
4. Fusion App ID'nizi Unity içindeki Photon Fusion ayarlarına girin. Kişisel App ID'nizi repoya commit etmeyin.
5. `BasicHostClient` sahnesini açın. Sahne Build Settings'e eklenmiş durumdadır.
6. Bir instance'ta **Create/Kur** ile oda oluşturun ve ekranda görünen altı karakterli kodu alın.
7. İkinci instance'ta **Join/Katıl** ekranını açın, kodu aynen girin ve odaya bağlanın.

Oda kodları büyük/küçük harfe duyarlıdır. Kod üreticisi okunabilirlik için `0`, `O`, `1`, `l` ve `I` karakterlerini kullanmaz.

## Kontroller

| Girdi | İşlev |
|---|---|
| `W` / `S` veya sol analog dikey eksen | İleri / geri hareket |
| `A` / `D` veya sol analog yatay eksen | Dönüş |
| Fare veya sağ analog | Kamera bakışı |
| `C` | Fare ile kamera bakışını aç / kapat |
| `Esc` veya gamepad Start | Oturumdan ayrıl |

## Proje yapısı

```text
Assets/
├── FusionFoundry/
│   ├── Runtime/
│   │   ├── Bootstrap/       # Runner oluşturma ve oturum koordinasyonu
│   │   ├── Sessions/        # Oturum sözleşmeleri ve Fusion yaşam döngüsü
│   │   ├── Spawning/        # Oyuncu oluşturma/kaldırma
│   │   ├── Input/           # Ağ input verisi ve sağlayıcısı
│   │   └── Players/         # Mevcut örnek ağ hareketi
│   ├── Samples/
│   │   └── BasicHostClient/ # Sahne, UI, prefab, kamera ve input asset'i
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
└── Photon/                  # Photon Fusion SDK
```

## Test kapsamı

Depoda EditMode ve PlayMode testleri bulunur. Mevcut testler başlıca şu davranışları kapsar:

- Host/Client istek sözleşmeleri
- Oda kodu üretimi ve doğrulaması
- Çift oturum başlatma isteğinin engellenmesi
- Başarısız başlangıç ve shutdown temizliği
- Oyuncunun tek kez spawn edilmesi, kaydedilmesi ve despawn edilmesi
- Input verisinin hazırlanması ve örnek oyuncu hareketi
- Yerel kamera sahipliği ve oturum HUD davranışı

Gerçek Photon bağlantısı yine de iki instance ile manuel entegrasyon testi gerektirir. Test Runner'daki izole testlerin geçmesi, bulut bağlantısının veya müşteri ağı koşullarının tek başına doğrulandığı anlamına gelmez.

## Bilinen sınırlar

Bu sürüm bilinçli olarak küçük bir multiplayer dikey dilime odaklanır. Şunlar henüz kapsamda değildir:

- Genel lobi tarayıcısı ve otomatik matchmaking
- Hazır/maç durumu, takım ve yeniden doğma akışları
- Sağlık, silah, envanter ve kalıcılık sistemleri
- Kimlik doğrulama ve backend servisleri
- Relay alternatifi, özel sunucu dağıtımı ve bölge seçimi arayüzü
- Gelişmiş karakter motoru, lag compensation veya mobil optimizasyon paketi

Bu alanlar müşteri ihtiyacına göre ayrı modüller halinde tasarlanabilir; depo mevcut olmayan özellikleri varmış gibi konumlandırmaz.

## Katkıda bulunma

Bu demoyu indirmek, incelemek, yerel ortamda geliştirmek ve projeye katkı önermek serbesttir. Hata düzeltmeleri, dokümantasyon iyileştirmeleri, testler ve projenin mevcut kapsamıyla uyumlu geliştirmeler memnuniyetle karşılanır.

Katkı göndermek için repoya önceden contributor/collaborator olarak eklenmeniz gerekmez:

1. Repoyu kendi GitHub hesabınıza fork edin.
2. Değişikliklerinizi fork içindeki ayrı bir branch üzerinde geliştirin.
3. Değişikliğin amacını ve nasıl doğrulandığını açıklayan bir pull request açın.

Ana repoya veya bu repo altındaki bir branch'e **doğrudan push** etmek için ise GitHub üzerinde yazma yetkisi gerekir. Bu yetki proje sahibi tarafından ayrıca verilir. Yazma yetkisi olmayan kullanıcılar fork ve pull request akışıyla katkı sağlayabilir.

Katkılar incelenir; gönderilen her değişikliğin ana repoya birleştirileceği garanti edilmez. Yeni bir geliştirmeye başlamadan önce mevcut issue ve pull request'leri kontrol etmek, büyük kapsamlı öneriler için önce bir issue açmak tavsiye edilir.

## Sahiplik ve kullanım amacı

FusionFoundry'nin ürün ve uygulama sahibi **Evrens Tech**'tir. Proje, Evrens Tech'in Unity proje desteği ve yazılım ortaklığı hizmetlerini görünür kılan teknik demo portföyünün parçasıdır.

Demo kaynakları indirilebilir, geliştirilebilir ve yukarıdaki katkı süreciyle projeye geri gönderilebilir. Photon Fusion SDK ve depodaki diğer üçüncü taraf içerikler kendi lisans ve kullanım koşullarına tabidir. Kaynak kodun ticari bir üründe kullanımı, yeniden dağıtımı veya lisans kapsamı hakkında ayrıntılı bilgi için Evrens Tech ile iletişime geçin.

Detaylı bilgi, FusionFoundry geliştirme talepleri, mevcut bir Unity projesinin multiplayer yapıya dönüştürülmesi veya yeni multiplayer modül geliştirme çalışmaları için [tech@evrens.net](mailto:tech@evrens.net) adresi üzerinden iletişime geçebilirsiniz.

<div align="right">

[Başa dön / Back to top](#top)

</div>

---

<a id="en"></a>

# FusionFoundry — English

**A reusable foundation and working demo for room-code-based multiplayer sessions built with Unity 6 and Photon Fusion.**

FusionFoundry is part of the Unity technical demo portfolio developed by **Evrens Tech**. Its purpose is not limited to presenting a sample multiplayer scene. It demonstrates, through a working system, the technical capability required to provide multiplayer conversion, network architecture, player lifecycle, and project-based development support for existing Unity projects.


## What does the demo show?

The `BasicHostClient` sample implements the following end-to-end flow:

- Creating a new `NetworkRunner` at runtime
- Starting a Host session with a six-character, case-sensitive room code
- Joining the same room from a second client using that code
- Spawning a network object for every connected player and removing it when the player leaves
- Sending Unity Input System data through the Fusion network
- Controlling the local player with a third-person camera
- Cleaning up the Runner after startup failures, disconnections, and session shutdown
- Starting a new session with a clean Runner within the same application instance

Sessions can be joined by code but are not listed in a public lobby. Clients are not allowed to create a new session accidentally.

## Role within Evrens Tech


The project acts as technical evidence and a starting point for the following services:

- Converting an existing Unity project to multiplayer
- Photon Fusion-based Host/Client and session flows
- Player spawn/despawn and input authority management
- Multiplayer module development and integration
- Technical review and improvement of existing networking code
- Project-based or temporary development support for Unity teams
- Prototyping more comprehensive lobby, matchmaking, and gameplay flows

FusionFoundry is not a finished consumer game or a general-purpose matchmaking product. It is a deliberately scoped technical foundation and demo that can be adapted to a client project.

## Architecture

```text
BasicHostClientUI
        │  Create / Join / Leave intent
        ▼
FusionBootstrap
        │  Runner creation, state, and lifecycle
        ▼
FusionSessionController
        │  StartGame / Shutdown
        ▼
Photon Fusion NetworkRunner
        ├── NetworkPlayerSpawner
        └── FusionInputProvider
                │
                ▼
        SampleNetworkPlayerMotor
```

Core design principles:

- The UI does not call the Photon API directly.
- A fresh Runner is created at runtime for every session.
- The session lifecycle is managed as `Idle → Starting → Running → Stopping → Idle`.
- Assembly boundaries separate the multiplayer foundation from sample-specific presentation and camera behavior.
- Movement input is transported by the networking layer and applied by the sample player motor during a Fusion tick.

## Technical stack

| Component | Version / location verified in the repository |
|---|---|
| Unity Editor | `6000.5.4f1` |
| Photon Fusion | SDK embedded under `Assets/Photon/Fusion` |
| Input System | `1.19.0` |
| Multiplayer Play Mode | `2.0.2` |
| Render Pipeline | Universal RP `17.5.0` |
| Sample scene | `Assets/FusionFoundry/Samples/BasicHostClient/Scenes/BasicHostClient.unity` |

> The Photon SDK is stored under `Assets/Photon` rather than referenced as a UPM dependency. Photon-owned files and FusionFoundry sources are kept separate.

## Setup

### Requirements

- Unity Hub
- Unity Editor `6000.5.4f1`
- A Photon account and a **Fusion App ID**
- A standalone build or Multiplayer Play Mode for two-client testing

### Running the project

1. Clone the repository and open the project directory through Unity Hub.
2. Wait for Unity to import the packages and finish script compilation.
3. Create a Fusion application in the Photon Dashboard.
4. Enter your Fusion App ID in the Photon Fusion settings inside Unity. Do not commit a personal App ID to the repository.
5. Open the `BasicHostClient` scene. It is already included in Build Settings.
6. Select **Create/Kur** in one instance and copy the six-character code displayed on screen.
7. Open **Join/Katıl** in the second instance, enter the code exactly as shown, and connect to the room.

Room codes are case-sensitive. For readability, the generator excludes `0`, `O`, `1`, `l`, and `I`.

## Controls

| Input | Action |
|---|---|
| `W` / `S` or left stick vertical axis | Move forward / backward |
| `A` / `D` or left stick horizontal axis | Turn |
| Mouse or right stick | Look around |
| `C` | Toggle mouse look |
| `Esc` or gamepad Start | Leave the session |

## Project structure

```text
Assets/
├── FusionFoundry/
│   ├── Runtime/
│   │   ├── Bootstrap/       # Runner creation and session coordination
│   │   ├── Sessions/        # Session contracts and Fusion lifecycle
│   │   ├── Spawning/        # Player spawning and removal
│   │   ├── Input/           # Network input data and provider
│   │   └── Players/         # Current sample network movement
│   ├── Samples/
│   │   └── BasicHostClient/ # Scene, UI, prefabs, camera, and input asset
│   └── Tests/
│       ├── EditMode/
│       └── PlayMode/
└── Photon/                  # Photon Fusion SDK
```

## Test coverage

The repository contains EditMode and PlayMode tests. Current tests mainly cover:

- Host/Client request contracts
- Room-code generation and validation
- Prevention of duplicate session-start requests
- Cleanup after startup failures and shutdown
- Spawning, registering, and despawning a player exactly once
- Input preparation and sample player movement
- Local camera ownership and session HUD behavior

Real Photon connectivity still requires a manual integration test with two instances. Passing isolated Test Runner tests does not, by itself, verify cloud connectivity or client-specific network conditions.

## Known limitations

This version intentionally focuses on a small multiplayer vertical slice. The following features are not currently included:

- Public lobby browser and automatic matchmaking
- Ready/match state, team, and respawn flows
- Health, weapons, inventory, and persistence systems
- Authentication and backend services
- Relay alternatives, dedicated server deployment, and a region-selection interface
- Advanced character controller, lag compensation, or a mobile optimization package

These areas can be designed as separate modules according to client needs. The repository does not present unimplemented features as existing capabilities.

## Contributing

You are free to download, inspect, and develop this demo locally, as well as propose contributions to the project. Bug fixes, documentation improvements, tests, and enhancements aligned with the current project scope are welcome.

You do not need to be added to the repository as a contributor or collaborator before proposing a contribution:

1. Fork the repository to your GitHub account.
2. Develop your changes on a separate branch in your fork.
3. Open a pull request describing the purpose of the change and how it was verified.

GitHub write access is required to **push directly** to the main repository or to a branch within it. This permission is granted separately by the project owner. Contributors without write access can use the fork and pull request workflow.

Contributions are reviewed, and submission does not guarantee that a change will be merged. Check existing issues and pull requests before starting work, and consider opening an issue first for large proposals.

## Ownership and intended use

**Evrens Tech** is the product and application owner of FusionFoundry. The project is part of the technical demo portfolio used to present Evrens Tech's Unity project support and software partnership services.

The demo sources may be downloaded, developed, and submitted back to the project through the contribution process described above. The Photon Fusion SDK and other third-party content in the repository remain subject to their respective licenses and terms of use. Contact Evrens Tech for details about commercial product use, redistribution, or licensing of the source code.

For more information, FusionFoundry development requests, conversion of an existing Unity project to multiplayer, or new multiplayer module development, contact [tech@evrens.net](mailto:tech@evrens.net).

<div align="right">

[Başa dön / Back to top](#top)

</div>
