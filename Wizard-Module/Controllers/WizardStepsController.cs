using Wizard.Module.Business;
using Wizard.Module.Helpers;
using Wizard.Module.Models;
using Wizard.Module.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace Wizard.Module.Controllers
{
    // Adımların yönetildiği ekran: turun içeriği buradan girilir. Adım metinleri
    // koda gömülü olmadığı için yeni adım eklemek derleme gerektirmiyor.
    //
    // Host'un hiçbir taban sınıfından türemiyor ve sayfaları tam sayfa çalışıyor:
    // ne modal penceresine, ne tablo eklentisine, ne de host'un JavaScript
    // dosyalarına bağlı. Formlar düz HTML, silme düz bir POST.
    //
    // Koruma sınıfın üstündeki filtrede: yetkisiz kullanıcı hiçbir action'a
    // giremez. Menüdeki bağlantının gizlenmesi ayrı bir şey, o sadece görsel.
    [RequireWizardAdmin]
    public class WizardStepsController : Controller
    {
        private readonly IWizardBusinessService _wizardBusinessService;
        private readonly IWizardPageValidator _wizardPageValidator;

        public WizardStepsController(
            IWizardBusinessService wizardBusinessService,
            IWizardPageValidator wizardPageValidator)
        {
            _wizardBusinessService = wizardBusinessService;
            _wizardPageValidator = wizardPageValidator;
        }

        [HttpGet]
        public IActionResult Index(string? ara, int sayfa = 1)
        {
            ViewData["Title"] = "Wizard Yönetimi";

            // Sıralama, arama ve sayfalama iş kuralı olduğu için listeyi Business
            // hazır veriyor; burada döngü ya da koşul yok.
            return View(_wizardBusinessService.GetList(ara, sayfa));
        }

        [HttpGet]
        public IActionResult Add()
        {
            ViewData["Title"] = "Yeni Adım";

            return View(new WizardStepViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Add(WizardStepViewModel model)
        {
            ViewData["Title"] = "Yeni Adım";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Sıra çakışmasını kaydetmeden önce soruyoruz ki hata, genel bir
            // "eklenemedi" satırı yerine doğrudan Sıra alanının altında görünsün.
            // Kuralın kendisi Business katmanında; burası yalnızca yöneticiye
            // hangi alanın sorunlu olduğunu söylüyor.
            if (!_wizardBusinessService.IsSortPathAvailable(model.SortPath, model.Id))
            {
                ModelState.AddModelError(nameof(model.SortPath),
                    "Bu sıra zaten başka bir adımda kullanılıyor. Her adımın sırası benzersiz olmalı.");
                return View(model);
            }

            // Adres var olan bir sayfaya işaret etmiyorsa kaydetmiyoruz. Aksi
            // hâlde tur kullanıcıyı 404'e götürüyor ve orada wizard dosyaları
            // yüklenmediği için sessizce duruyordu.
            if (!_wizardPageValidator.IsKnownPage(model.TargetUrl))
            {
                ModelState.AddModelError(nameof(model.TargetUrl),
                    "Geçersiz adres: böyle bir sayfa yok. Örnek: /Urunler");
                return View(model);
            }

            var step = new WizardStep
            {
                ModuleName = model.ModuleName,
                Title = model.Title,
                Description = model.Description,
                TargetSelector = model.TargetSelector,
                TargetUrl = model.TargetUrl,
                SortPath = model.SortPath,
                RequiredPermission = model.RequiredPermission
            };

            if (!_wizardBusinessService.Add(step))
            {
                ModelState.AddModelError(string.Empty, "Adım eklenemedi.");
                return View(model);
            }

            TempData["WizardMesaj"] = "Adım eklendi.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult Edit(int id)
        {
            ViewData["Title"] = "Adımı Düzenle";

            var step = _wizardBusinessService.GetById(id);

            // Olmayan id ile gelinmesi beklenen bir durum: kayıt başkası
            // tarafından silinmiş olabilir. Hata sayfası yerine listeye dönüp
            // sebebini söylüyoruz.
            if (step == null)
            {
                TempData["WizardMesaj"] = "Adım bulunamadı.";
                return RedirectToAction(nameof(Index));
            }

            return View(new WizardStepViewModel
            {
                Id = step.Id,
                ModuleName = step.ModuleName,
                Title = step.Title,
                Description = step.Description,
                TargetSelector = step.TargetSelector,
                TargetUrl = step.TargetUrl,
                SortPath = step.SortPath,
                RequiredPermission = step.RequiredPermission,
                CreatedDate = step.CreatedDate
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(WizardStepViewModel model)
        {
            ViewData["Title"] = "Adımı Düzenle";

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // Sıra çakışmasını kaydetmeden önce soruyoruz ki hata, genel bir
            // "eklenemedi" satırı yerine doğrudan Sıra alanının altında görünsün.
            // Kuralın kendisi Business katmanında; burası yalnızca yöneticiye
            // hangi alanın sorunlu olduğunu söylüyor.
            if (!_wizardBusinessService.IsSortPathAvailable(model.SortPath, model.Id))
            {
                ModelState.AddModelError(nameof(model.SortPath),
                    "Bu sıra zaten başka bir adımda kullanılıyor. Her adımın sırası benzersiz olmalı.");
                return View(model);
            }

            // Adres var olan bir sayfaya işaret etmiyorsa kaydetmiyoruz. Aksi
            // hâlde tur kullanıcıyı 404'e götürüyor ve orada wizard dosyaları
            // yüklenmediği için sessizce duruyordu.
            if (!_wizardPageValidator.IsKnownPage(model.TargetUrl))
            {
                ModelState.AddModelError(nameof(model.TargetUrl),
                    "Geçersiz adres: böyle bir sayfa yok. Örnek: /Urunler");
                return View(model);
            }

            // CreatedDate bilerek aktarılmıyor: güncellemede korunması gereken bir
            // alan ve değerini forma güvenerek değil, veritabanındaki kayıttan
            // okuyarak belirliyoruz (bkz. WizardBusinessService.Update).
            var step = new WizardStep
            {
                Id = model.Id,
                ModuleName = model.ModuleName,
                Title = model.Title,
                Description = model.Description,
                TargetSelector = model.TargetSelector,
                TargetUrl = model.TargetUrl,
                SortPath = model.SortPath,
                RequiredPermission = model.RequiredPermission
            };

            if (!_wizardBusinessService.Update(step))
            {
                ModelState.AddModelError(string.Empty, "Adım güncellenemedi. Kayıt silinmiş olabilir.");
                return View(model);
            }

            TempData["WizardMesaj"] = "Adım güncellendi.";
            return RedirectToAction(nameof(Index));
        }

        // Görülmüş adımlar da silinebilir: görülme kayıtları adımla birlikte
        // gider. Yönetici turun içeriğine müdahale edebilmeli.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(int id)
        {
            TempData["WizardMesaj"] = _wizardBusinessService.Delete(id)
                ? "Adım silindi."
                : "Adım silinemedi. Kayıt bulunamadı.";

            return RedirectToAction(nameof(Index));
        }
    }
}
