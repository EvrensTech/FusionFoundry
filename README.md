<div align="center">

![Duel Protocol cover](DuelProtocol_Cover.png)

# Duel Protocol

**A polished Unity 6 integration sample for single-player and two-player online duels, Photon Fusion networking, Unity Gaming Services progression, a cosmetic shop, and LevelPlay rewarded ads.**

[Türkçe](#türkçe) · [English](#english)

[▶ Tanıtım videosu / Watch the trailer](DuelProtocol_Trailer.mp4)

</div>

## Türkçe

Duel Protocol, tek oyunculu yapay zekâ düellosu ile iki oyunculu çevrimiçi düelloyu aynı oyun kuralları üzerinde birleştiren çalışır bir Unity örneğidir. Proje yalnız bağlantı kurmayı değil; ürün seviyesinde menüleri, mobil kontrolleri, authoritative maç akışını, kalıcı oyuncu profilini, kozmetik mağazayı ve isteğe bağlı ödüllü reklamı tek mimaride gösterir.

### Öne çıkanlar

- Yapay zekâya karşı singleplayer ve iki oyunculu online multiplayer
- Photon Fusion Host Mode; özel oda kodu, ranked/unranked matchmaking, ready kapısı, rematch ve reconnect
- Host-authoritative enerji çekirdeği, skor, süre, saldırı, cooldown, stun ve sonuç akışı
- Klavye/fare, gamepad ve mobil joystick + saldırı düğmesi
- Türkçe, İngilizce ve Almanca yerelleştirme
- Responsive ana menü, mod/arena seçimi, ayarlar, profil, lobi, HUD, sonuç, mağaza ve skin koleksiyonu
- Altı kozmetik skin ve yerel CORE para birimi vitrini
- UGS Authentication, Cloud Code, Cloud Save ve Leaderboards örnek entegrasyonu
- Unity LevelPlay üzerinden ironSource Ads, Unity Ads ve Google AdMob rewarded mediation örneği
- Windows x64 ve Android ARM64 hedefleri

### Oyun döngüsü

İki oyuncu arenanın merkezindeki enerji çekirdeğini ele geçirmek için yarışır. Çekirdeği taşıyan oyuncu süre boyunca hayatta kalıp puan toplamaya çalışırken rakip, hareket yönüne doğru saldırarak taşıyıcıyı stun edip çekirdeği düşürmeye çalışır. Maç sonunda sonuç UGS progression katmanına gönderilebilir; mükerrer `matchId` işlemleri rating veya ödülü ikinci kez artırmaz.

### Mimari

```text
Input / AI
    → ortak DuelCommand
    → authoritative match simulation
    → Fusion network state
    → presentation, HUD and VFX
    → validated match report
    → UGS Cloud Code settlement
    → Cloud Save profile + Leaderboard

Store UI
    → provider-independent rewarded-ad contract
    → Unity LevelPlay
    → ironSource Ads / Unity Ads / Google AdMob
```

Fusion’a bağlı yeniden kullanılabilir ağ bileşenleri `Assets/FusionFoundry` altında tutulur. Duel Protocol’e özel oyun, sunum ve servis adaptörleri `Assets/DuelProtocol` altında modüler kalır.

### Teknoloji

| Katman | Teknoloji |
|---|---|
| Oyun motoru | Unity `6000.5.4f1`, C#, URP |
| Multiplayer | Photon Fusion 2 |
| Backend | Unity Authentication, Cloud Code, Cloud Save, Leaderboards |
| Reklam | Unity LevelPlay rewarded mediation |
| Reklam kaynakları | ironSource Ads, Unity Ads, Google AdMob |
| Girdi | Unity Input System; klavye, gamepad ve dokunmatik |
| Platform | Windows x64, Android ARM64 |

### Kurulum

1. Projeyi Unity Hub ile Unity `6000.5.4f1` üzerinde açın.
2. Photon Dashboard’dan oluşturduğunuz Fusion App ID’yi yerel `PhotonAppSettings` asset’ine girin.
3. Unity projesini kendi Cloud Project’inize bağlayın ve Editor environment değerini `development` seçin.
4. UGS Cloud Code ve Leaderboard tanımlarını kendi development ortamınıza dağıtın.
5. LevelPlay uygulamanızı ve rewarded ad unit’inizi oluşturun; App Key, ad unit ve placement değerlerini yerel `DuelAdsSettings` asset’ine girin.
6. LevelPlay Network Manager’dan kullanacağınız ağ adapter’larını kurun ve her ağın kendi Game/App/Placement/Ad Unit değerlerini LevelPlay Dashboard’da tanımlayın.
7. `Assets/DuelProtocol/Scenes/DuelFrontEnd.unity` sahnesini açıp Play’e basın.

> Servis anahtarları ve sağlayıcı kimlikleri public örnekten çıkarılmalıdır. `.gitignore` yeni yerel ayar dosyalarını engeller; daha önce Git tarafından izlenen kimlik dosyaları ise yayından önce ayrıca index’ten çıkarılmalıdır.

### Reklam örneği

Mağazadaki `REKLAM İZLE // +100 CORE` düğmesi tamamen isteğe bağlı rewarded akıştır. Ödül yalnız LevelPlay’in reward callback’i geldikten sonra verilir. Reklam kapatılırsa, yüklenemezse veya gösterim başarısız olursa bakiye artmaz. LevelPlay, aktif ağlar arasında açık artırma yapar; oyun kodu ironSource, Unity Ads veya AdMob’u doğrudan seçmez.

### Kapsam

Bu repository, iki kişilik düello ve servis entegrasyonu için profesyonel bir örnektir. Dedicated server, tam anti-cheat, gerçek para IAP, klan, turnuva ve canlı sezon sistemi bu demonun mevcut yayın kapsamına dahil değildir.

---

## English

Duel Protocol is a working Unity sample that runs a single-player AI duel and a two-player online duel on the same gameplay rules. It demonstrates more than connectivity: a product-grade front end, mobile controls, authoritative match flow, persistent player progression, a cosmetic shop, and opt-in rewarded advertising in one modular architecture.

### Highlights

- Single-player AI and two-player online multiplayer
- Photon Fusion Host Mode with private room codes, ranked/unranked matchmaking, ready, rematch, and reconnect flows
- Host-authoritative energy core, score, timer, attack, cooldown, stun, and match results
- Keyboard/mouse, gamepad, and mobile joystick plus attack button
- Turkish, English, and German localization
- Responsive front end, mode/arena selection, settings, profile, lobby, HUD, results, shop, and skin collection
- Six cosmetic skins and a local CORE currency showcase
- Sample integration for UGS Authentication, Cloud Code, Cloud Save, and Leaderboards
- Rewarded mediation through Unity LevelPlay with ironSource Ads, Unity Ads, and Google AdMob
- Windows x64 and Android ARM64 targets

### Core architecture

```text
Input / AI → DuelCommand → authoritative simulation → Fusion state
           → presentation → validated result → UGS settlement
           → Cloud Save profile + Leaderboard

Store UI → rewarded-ad abstraction → Unity LevelPlay
         → ironSource Ads / Unity Ads / Google AdMob
```

Reusable Fusion networking components live under `Assets/FusionFoundry`; game-specific gameplay, presentation, and service adapters remain modular under `Assets/DuelProtocol`.

### Getting started

1. Open the project with Unity `6000.5.4f1`.
2. Configure your own Photon Fusion App ID locally.
3. Link the project to your own Unity Cloud Project and select the `development` environment.
4. Deploy the Cloud Code and Leaderboard definitions to your development environment.
5. Configure your own LevelPlay App Key, rewarded ad unit, and placement locally.
6. Install the required mediation adapters and map each network’s own IDs in the LevelPlay Dashboard.
7. Open `Assets/DuelProtocol/Scenes/DuelFrontEnd.unity` and enter Play Mode.

> Provider IDs and credentials must be excluded from the public sample. `.gitignore` blocks new local settings files; any identifier file already tracked by Git must also be removed from the index before publication.

### Positioning

This repository is a professional two-player duel and service-integration sample. Dedicated servers, complete anti-cheat, real-money IAP, clans, tournaments, and live seasons are outside the current published demo scope.

---

Developed by **Evrens Tech** as a Unity multiplayer and live-services integration reference.
