using System.Text.Json;
using Wizard.Module.Business;
using Microsoft.AspNetCore.Mvc;

namespace Wizard.Module.Controllers
{
    // Turun tarayıcı tarafıyla konuştuğu uç. Sadece JSON döner, view döndürmez.
    //
    // Hiçbir host sınıfından türemiyor ve host'un giriş kontrolüne bağlı değil:
    // kullanıcıyı IWizardUserProvider'dan, yetkiyi IWizardAccessService'ten sorar.
    // Oturum yoksa kimlik boş gelir, iş katmanı da boş liste döndürür — yani
    // giriş yapmamış birine hiçbir adım gitmez, ayrıca bir filtreye gerek kalmaz.
    //
    // Katman kuralı gereği burada döngü ve koşul yok: her metot kullanıcıyı bulur,
    // Business'a sorar, cevabı geri verir.
    public class WizardController : Controller
    {
        // Alan adları burada sabitleniyor. Host sistemin MVC seri hâle getirici
        // ayarı (camelCase, PascalCase, Newtonsoft) ne olursa olsun tarayıcıya
        // hep aynı isimler gitsin diye JSON'u kendimiz üretip ContentResult
        // olarak döndürüyoruz; host'un formatlayıcısı hiç devreye girmiyor.
        private static readonly JsonSerializerOptions JsonAyarlari = new(JsonSerializerDefaults.Web);

        private readonly IWizardBusinessService _wizardBusinessService;
        private readonly IWizardUserProvider _wizardUserProvider;

        public WizardController(IWizardBusinessService wizardBusinessService, IWizardUserProvider wizardUserProvider)
        {
            _wizardBusinessService = wizardBusinessService;
            _wizardUserProvider = wizardUserProvider;
        }

        // Sayfa açılışında çağrılır: kullanıcının görmediği adımlar.
        [HttpGet]
        public IActionResult GetSteps()
        {
            var userId = _wizardUserProvider.GetCurrentUserId();
            var result = _wizardBusinessService.GetStepsToShow(userId);

            return WizardJson(result);
        }

        // Turu elle yeniden başlatmak için. Adı "All" olsa da yetki filtresi yine
        // uygulanır — atlanan tek şey kullanıcının görme kaydı.
        [HttpGet]
        public IActionResult GetAllSteps()
        {
            var userId = _wizardUserProvider.GetCurrentUserId();
            var result = _wizardBusinessService.GetAllStepsUnfiltered(userId);

            return WizardJson(result);
        }

        // Tur kapanınca bir kez, o turda gösterilen adımların id'leriyle çağrılır.
        //
        // Kullanıcı id'si istekten değil sağlayıcıdan okunuyor; parametre olsaydı
        // başkasının ilerlemesi dışarıdan değiştirilebilirdi. Gelen id listesi ise
        // güvenilmez kabul edilir: var olmayan ya da kullanıcının erişemediği
        // id'ler alt katmanlarda sessizce elenir, istek yine de 200 döner.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkSeen([FromForm] int[] stepIds)
        {
            if (stepIds == null || stepIds.Length == 0)
            {
                return WizardJson(new { success = false, reason = "empty" });
            }

            var userId = _wizardUserProvider.GetCurrentUserId();
            var basarili = _wizardBusinessService.MarkStepsAsSeen(userId, stepIds);

            return WizardJson(new { success = basarili });
        }

        private ContentResult WizardJson(object value)
        {
            return Content(JsonSerializer.Serialize(value, JsonAyarlari), "application/json");
        }
    }
}
