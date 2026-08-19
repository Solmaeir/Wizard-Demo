using Wizard.Module.Models;
using Wizard.Module.ViewModels;

namespace Wizard.Module.Business
{
    // Wizard'ın iş kuralları. İki ayrı iş için kullanılıyor: turun kendisi
    // (ilk üç metot) ve adımların yönetildiği ekran (kalanlar).
    public interface IWizardBusinessService
    {
        // Turun normal girişi: kullanıcının daha önce görmediği adımları verir.
        WizardResultViewModel GetStepsToShow(string userId);

        // Kullanıcı turu kendi isteğiyle yeniden başlattığında çağrılır — daha
        // önce görülmüş olsun olmasın bütün adımlar döner. "Unfiltered" sadece
        // görülme kaydını yok sayar; yetki filtresi burada da çalışır.
        WizardResultViewModel GetAllStepsUnfiltered(string userId);

        // Tur kapanırken bir kez çağrılır, o turda gösterilen adımların id'leriyle.
        // Geçersiz id'ler sessizce elenir; çağıranın önceden doğrulaması gerekmez.
        bool MarkStepsAsSeen(string userId, IEnumerable<int> stepIds);

        // Aşağısı yönetim ekranı için. Orada kullanıcıya göre filtreleme yok,
        // zaten o ekrana sadece yetkili kullanıcı girebiliyor.
        // Yönetim ekranının listesi: arama ve sayfalama uygulanmış hâlde döner.
        // Filtreleme ve sayfalama iş katmanında yapılır; view yalnızca gösterir.
        WizardStepListViewModel GetList(string? search, int page);
        WizardStep? GetById(int id);
        bool Add(WizardStep step);
        bool Update(WizardStep step);

        // Adım, kullanıcılar tarafından görülmüş olsa bile silinir; görülme
        // kayıtları onunla birlikte gider. Yönetici kendi turunun içeriğine
        // müdahale edebilmeli, eski bir kayıt buna engel olmamalı.
        bool Delete(int id);

        // Yönetim ekranı, kaydetmeden önce sırayı sorabilsin diye ayrı duruyor:
        // böylece hata mesajı genel bir "eklenemedi" yerine doğrudan Sıra alanının
        // altında görünüyor. Kural ayrıca Add ve Update içinde de uygulanıyor,
        // yani bu kontrol atlanırsa da kayıt geçmez.
        bool IsSortPathAvailable(string sortPath, int excludeStepId);
    }
}
