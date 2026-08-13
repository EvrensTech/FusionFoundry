# Duel Protocol Cloud Code

Bu klasör WP-06 ve WP-15–18 için sunucu tarafı alan kurallarını içerir. `duel-domain.js` istemciden bağımsız, deterministik bilet/settlement/rating mantığıdır; `duel-domain.test.js` Node ile çalıştırılabilir. `Deploy/` altındaki dört bağımsız script UGS CLI ile doğrudan dağıtılabilecek uçlardır.

Canlı uçlar şu adlarla yayımlanmalıdır:

- `CreateMatchTicket`: authenticated `context.playerId`, Cloud Save profili ve istemcinin `mode/buildId/platform` alanlarından 90 saniyelik, tek kullanımlık bilet üretir ve korumalı aktif bilet kaydı oluşturur. Rating istemciden alınmaz.
- `SubmitMatchResult`: authenticated çağıranın katılımcı olduğunu; skor/süre/sonuç kurallarını; bilet kimliği, nonce, mod, sahip ve son kullanma bağını doğrular. Yalnız çağıranın korumalı profilini Cloud Save write-lock ile günceller ve rating’i leaderboard’a yazar. `matchId` idempotency anahtarıdır; çelişkili ikinci katılımcı raporu `Disputed` olur. İstemciden rating/XP kabul edilmez.
- `GetDuelProfile`: çağıranın profilini veri kaybetmeyen güncel şemaya yükselterek döndürür.
- `UpdateDuelPreferences`: yalnız kontrol binding override ve erişilebilirlik tercihlerini doğrulanmış sınırlar içinde günceller; rating, XP veya geçmişe yazamaz.

`duel-domain.js` storage bağımlılığı taşımadığı için Cloud Code JavaScript script veya C# module adaptöründen çağrılabilir. Production dağıtımında Cloud Save protected write, Leaderboards `duel-rating` skoru ve audit kaydı aynı settlement transaction/lock sınırında güncellenmelidir. Servis kimlikleri ve anahtarlar repoya eklenmez; Unity Dashboard environment secret store kullanılır.

Yerel doğrulama:

```powershell
node Backend/CloudCode/duel-domain.test.js
```

Dağıtım öncesi sözdizimi ve dry-run:

```powershell
Get-ChildItem Backend/CloudCode/Deploy/*.js | ForEach-Object { node --check $_.FullName }
./Tools/Deploy-DuelUgs.ps1 -ProjectId '<uuid>' -Environment development -DryRun
```

Gerçek environment dağıtımı için `-DryRun` kaldırılır. Yetkili service-account kimlik bilgileri yalnız CI secret store/UGS CLI kimlik deposundan sağlanır; repoya eklenmez. `Backend/Leaderboards/duel-rating.lb` aynı komutta dağıtılır.
