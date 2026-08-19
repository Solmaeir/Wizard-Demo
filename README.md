# Wizard Modülü — Entegrasyon Rehberi

Bu klasördeki `Wizard-Module`, uygulama içi tanıtım turu (onboarding wizard) sağlayan, **host sistemin hiçbir sınıfından türemeyen, bağımsız** bir ASP.NET Core MVC modülüdür. Aşağıdaki adımlar EDI'nin kendi sistemine entegrasyon içindir.

Modülün namespace kökü: **`Wizard.Module`** (`Wizard.Module.Business`, `.Controllers`, `.Data`, `.Helpers`, `.Models`, `.ViewModels`).

---

## 1. Genel mimari

Modül host sistemle iki noktadan konuşur, host sistem hakkında başka hiçbir varsayımda bulunmaz:

- **`IWizardAccessService`** — "bu kullanıcıda şu yetki var mı?" sorusunun cevabı.
- **`IWizardUserProvider`** — "şu anki kullanıcının kimliği ne?" sorusunun cevabı.

Modül **sorar, host cevaplar**. Bu iki arayüzün EDI'ye özel implementasyonunu yazıp DI'a kaydetmek entegrasyonun asıl işidir — modül içinde değişmesi gereken tek satır yoktur.

Bu iki kayıt yapılmazsa modül çökmez; `UnconfiguredWizardAccessService` / `UnconfiguredWizardUserProvider` devreye girer, **her şeye "hayır" der ve log'a uyarı yazar**. Yani yanlışlıkla herkese açık bir tur oluşmaz, sessizce de çalışmaz — eksik entegrasyon hemen fark edilir.

---

## 2. Kurulum adımları

### 2.1. Dosyaları kopyala

`Wizard-Module` klasörünün tamamını EDI solution'ı içine kopyalayın (proje referansı ya da aynı projeye dahil etme — tercihe bağlı). Klasör yapısı:

```
Wizard-Module/
  Business/     -> arayüzler, iş kuralları, DI kayıt uzantısı
  Controllers/  -> WizardController (JSON API), WizardStepsController (yönetim ekranı)
  Data/         -> IWizardDataService / WizardDataService (EF Core)
  Helpers/      -> RequireWizardAdminAttribute
  Models/       -> WizardStep, WizardStepView (DB entity'leri)
  ViewModels/   -> formlar ve liste ekranı için
  Views/        -> yönetim ekranı + AccessDenied + _WizardScripts partial
  wwwroot/      -> driver.js (yerel, CDN yok), tema CSS, JS
```

### 2.2. Veritabanı tablolarını oluştur

DB-first bir sistem olduğu için tabloları migration ile değil, doğrudan SQL ile oluşturun (bkz. **Bölüm 3**). Oluşturduktan sonra scaffold/DB-first akışınızla `WizardStep` ve `WizardStepView` entity'lerinin karşılığını (veya bu iki DbSet'i) kendi `DbContext`'inize ekleyin — **`WizardDataService`, constructor'ında doğrudan bir `DbContext` bekler** ve `context.WizardSteps` / `context.WizardStepViews` üzerinden çalışır.

### 2.3. İki seam'i implemente edin

```csharp
public class EdiWizardAccessService : IWizardAccessService
{
    public bool HasPermission(string permissionName)
        => // EDI'nin kendi yetki sorgusu, örn:
           _http.HttpContext.User.IsInRole(permissionName);
}

public class EdiWizardUserProvider : IWizardUserProvider
{
    public string GetCurrentUserId()
        => // geçerli kullanıcının kimliğini METİN olarak döndürün
           _http.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
}
```

`HasPermission` kimlik parametresi almaz — geçerli istek sahibi üzerinden cevap verir (EDI'de yetkiler `User.IsInRole` ile kontrol ediliyor, bu yüzden seam bilerek bu şekilde tasarlandı).

`GetCurrentUserId` **metin** döner; EDI'nin kullanıcı kimliği int, GUID ya da başka bir tip olsa da modül hiçbir varsayımda bulunmaz.

### 2.4. `Program.cs`'e (veya `Startup.cs`) kaydedin

```csharp
builder.Services.AddWizard("WizardYonetimi"); // yönetim ekranına girmek için gereken yetki adı

builder.Services.AddScoped<IWizardAccessService, EdiWizardAccessService>();
builder.Services.AddScoped<IWizardUserProvider, EdiWizardUserProvider>();
```

`AddWizard(...)` parametresi, Wizard Yönetimi ekranını (`/WizardSteps`) korumak için EDI'nin yetki tablosundaki yetkinin **adı**dır. Verilmezse `"WizardYonetimi"` varsayılır. Bu tek satır değişince hem menüdeki bağlantının görünürlüğü hem de ekranın korunması otomatik güncellenir — kod içinde ikinci bir yerde bu ismi tekrar yazmanıza gerek yoktur.

### 2.5. Layout'a tek satır ekleyin

`</body>` kapanmadan hemen önce:

```cshtml
@await Html.PartialAsync("_WizardScripts")
```

Bu partial; antiforgery jetonunu, API adreslerini (`Url.Action` ile üretilir, yönlendirme değişse de kendiliğinden düzelir), `driver.js` kütüphanesini ve modülün kendi JS/CSS dosyalarını tek yerden yükler. Host layout'una Wizard'a ait başka hiçbir satır eklenmez.

### 2.6. Tur hedeflerini işaretleyin

Turda durdurulacak her elemana `data-wizard-id="..."` özniteliği ekleyin (bu değer, yönetim ekranında adımın `TargetSelector` alanına yazılan değerle eşleşir):

```html
<a data-wizard-id="products-menu" asp-controller="Urunler" asp-action="Index">Ürünler</a>
```

Menüde "Wizard Yönetimi" bağlantısını göstermek isterseniz, EDI'nin kendi `IWizardAccessService.HasPermission("WizardYonetimi")` sonucuna göre görünürlüğü kendi layout/menü kodunuzda kontrol edin (modül bunu dayatmaz, yalnızca ekranın kendisini korur).

### 2.7. Turu başlatın (opsiyonel elle tetikleme)

Sayfa açılışında görülmemiş adımlar otomatik gelir (`GET /Wizard/GetSteps`). Kullanıcı turu elle yeniden başlatmak isterse `GET /Wizard/GetAllSteps` çağrılır — yetki filtresi yine uygulanır, atlanan tek şey "daha önce görüldü" kaydıdır.

---

## 3. Veritabanı — oluşturulacak tablolar

Modül yalnızca bu iki tabloyu kullanır, başka hiçbir tabloya (EDI'nin kullanıcı tablosu dahil) yabancı anahtar vermez. Kullanıcı kimliği her iki sistemde de **metin** (`NVARCHAR`) tutulur; EDI'nin kullanıcı id tipi (int/GUID/kullanıcı adı) ne olursa olsun şema değişmez.

```sql
/* =====================================================================
   Wizard modulu - veritabani semasi (SQL Server)
   ===================================================================== */

IF OBJECT_ID('dbo.WizardSteps', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WizardSteps
    (
        Id                 INT            IDENTITY(1,1) NOT NULL,
        ModuleName         NVARCHAR(100)  NOT NULL,
        Title              NVARCHAR(200)  NOT NULL,
        Description        NVARCHAR(1000) NOT NULL,
        TargetSelector     NVARCHAR(300)  NOT NULL,
        TargetUrl          NVARCHAR(300)  NULL,
        SortPath           NVARCHAR(50)   NOT NULL,
        RequiredPermission NVARCHAR(100)  NULL,
        CreatedDate        DATETIME2(7)   NOT NULL CONSTRAINT DF_WizardSteps_CreatedDate DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_WizardSteps PRIMARY KEY CLUSTERED (Id)
    );

    -- Aynı sıra iki adıma verilemez: sıra hem turun akışını hem modül
    -- gruplamasını belirliyor. Uygulama kuralı da aynısını uyguluyor;
    -- bu indeks eşzamanlı iki kayda karşı son savunma.
    CREATE UNIQUE INDEX UX_WizardSteps_SortPath
        ON dbo.WizardSteps (SortPath);
END
GO

-- Görülen adım kaydı. Her kullanıcı-adım ikilisi için en fazla bir satır.
-- Adım silindiğinde bu satırlar da silinir (CASCADE): yönetici, daha önce
-- görülmüş bir adımı da silebilmeli.
IF OBJECT_ID('dbo.WizardStepViews', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WizardStepViews
    (
        Id       INT           IDENTITY(1,1) NOT NULL,
        UserId   NVARCHAR(100) NOT NULL,
        StepId   INT           NOT NULL,
        SeenDate DATETIME2(7)  NOT NULL CONSTRAINT DF_WizardStepViews_SeenDate DEFAULT (SYSDATETIME()),

        CONSTRAINT PK_WizardStepViews PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_WizardStepViews_WizardSteps FOREIGN KEY (StepId)
            REFERENCES dbo.WizardSteps (Id) ON DELETE CASCADE
    );

    -- Aynı adım aynı kullanıcıya iki kez kaydedilmesin. Kod da kontrol
    -- ediyor; bu, eşzamanlı iki isteğe karşı son savunma.
    CREATE UNIQUE INDEX UX_WizardStepViews_User_Step
        ON dbo.WizardStepViews (UserId, StepId);

    -- Tur her sayfa açılışında "bu kullanıcı neleri gördü" diye soruyor.
    CREATE INDEX IX_WizardStepViews_UserId
        ON dbo.WizardStepViews (UserId);
END
GO
```

### Sütun sözlüğü

| Tablo.Sütun | Tip | Açıklama |
|---|---|---|
| `WizardSteps.ModuleName` | `NVARCHAR(100)` | Yönetim ekranında gruplama için; sıralamaya etkisi yok. |
| `WizardSteps.TargetSelector` | `NVARCHAR(300)` | Sayfadaki `[data-wizard-id="..."]` değeri. |
| `WizardSteps.TargetUrl` | `NVARCHAR(300)` NULL | Doluysa adım o adrese gider (yalnızca `/` ile başlayan iç adresler kabul edilir). Boşsa adım bulunduğu sayfada gösterilir. **Yalnızca navigasyon içindir, yetkiyle ilgisi yoktur.** |
| `WizardSteps.SortPath` | `NVARCHAR(50)` | Noktalı hiyerarşi (`"2.1"`, `"2.10"`). Metin değil **sayısal** segment bazlı sıralanır — uygulama katmanında. |
| `WizardSteps.RequiredPermission` | `NVARCHAR(100)` NULL | Boşsa adım herkese açık. Doluysa EDI'nin yetki tablosundaki yetki adıyla **birebir aynı** olmalı. |
| `WizardStepViews.UserId` | `NVARCHAR(100)` | Host sistemin kullanıcı kimliği, metne çevrilmiş hâliyle. |

### Mevcut (eski sürüm) kurulumdan yükseltme

Temiz kurulumda gerek yok. Yalnızca modülün önceki bir sürümünü (`UserWizardStatuses` tablolu, tek `LastSeenStepId` alanlı sürüm) çalıştırmış bir kurulumdan geliyorsanız migration script'leri gerekir — proje geçmişinde `WizardDelivery/02-migrate-from-userwizardstatus.sql`, `03-sortpath-benzersiz.sql`, `04-required-permission.sql` dosyalarına bakın.

---

## 4. Önemli talimatlar — değiştirmeden önce oku

Aşağıdakiler koda bakınca gerekçesi görünmeyen, **bilinçli** kararlardır. "Sadeleştirme" ya da "modernleştirme" adına geri alınmamalı:

- **İki seam'in dışında hiçbir yerde host'a bağımlılık yoktur.** `WizardController` ve `WizardStepsController` `Controller`'dan doğrudan türer, host'un taban sınıflarından türemez.
- **JSON alan adları controller'da elle sabitlenir** (`System.Text.Json`, `ContentResult`). EDI'nin genel MVC serileştirme ayarı (Newtonsoft, camelCase/PascalCase vb.) ne olursa olsun tarayıcıya giden JSON etkilenmez.
- **`SortPath` sayısal karşılaştırılır**, metin olarak değil — aksi hâlde `"2.10" < "2.9"` gibi yanlış sıralama üretir.
- **Hedefleme tek biçimdedir:** `[data-wizard-id="..."]`. Başka bir seçici yöntemi (CSS class, id) eklemeyin; sayfa yapısı değiştiğinde turun bozulmaması bu tekliğe dayanıyor.
- **Görülen adımlar tek tek tutulur** (`WizardStepViews`, kullanıcı+adım). Tek bir "son görülen adım" sayacına geri dönmeyin — sonradan yetki kazanan kullanıcı geçmiş adımları hiç göremez hâle gelir.
- **Adım silme cascade'lidir** — görülmüş bir adım da silinebilir; yönetici içeriğe müdahale edebilmeli.
- **CSRF jetonu gövdede gönderilir** (header'da değil), çünkü host sistemde header adı değişmiş olabilir. Wizard'ın POST uçlarında `[ValidateAntiForgeryToken]` zorunludur, kaldırılmamalı.
- **`driver.js` yerelden yüklenir, CDN'den değil** (`wwwroot/lib/driver.js/`). Kapalı kurum ağlarında CDN'e erişilemiyor ve dışarıdan script yüklemek güven sınırını genişletiyor.
- **Modülün tarayıcı tarafı jQuery kullanmaz**, `fetch` + vanilla DOM. Host'un jQuery'si varsa bile modül ona bağımlı olmamalı.
- **XSS:** driver.js `innerHTML` kullandığı için adım metinleri `escapeHtml`'den geçirilir; yönetim ekranı ise sunucuda Razor ile basılır. Bu iki farklı kaçışlama yolunu birleştirmeyin.
- **`RequiredPermission` ile `TargetUrl` birbirinden bağımsızdır.** Biri "nereye gidilir", diğeri "kim görebilir" sorusuna cevap verir — tek alana indirgemeyin.

---

## 5. Kurulumu doğrulama

1. Tabloları oluşturduktan sonra en az bir `WizardStep` kaydı ekleyin (yönetim ekranından ya da elle `INSERT`).
2. Seam'leri ve `AddWizard(...)`'ı kaydedip uygulamayı başlatın; log'da `UnconfiguredWizard*` uyarısı **görünmemeli**.
3. Sayfayı açtığınızda tur otomatik başlamalı; adım hedefine tıklanan `data-wizard-id` bulunamıyorsa popover görünmez (sessizce atlanır, hata fırlatmaz).
4. `/WizardSteps` adresine yetkisiz bir kullanıcıyla girip 403 + `AccessDenied.cshtml` sayfasını görün.
