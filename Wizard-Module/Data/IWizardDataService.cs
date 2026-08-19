using Wizard.Module.Models;

namespace Wizard.Module.Data
{
    // Wizard'ın tek veritabanı erişim noktası. İki ayrı tabloya baksa da servis
    // bilerek bölünmedi: modül taşınırken tek dosya gidiyor, adım ile görülme
    // kaydı da zaten hep birlikte kullanılıyor.
    public interface IWizardDataService
    {
        List<WizardStep> GetAllSteps();

        // Kayıt yoksa null döner; "bulunamadı" beklenen bir durum, hata değil.
        WizardStep? GetStepById(int id);

        bool AddStep(WizardStep step);
        bool UpdateStep(WizardStep step);

        // Adıma ait görülme kayıtları veritabanında ON DELETE CASCADE ile
        // birlikte silinir; yönetici görülmüş bir adımı da silebilir.
        bool DeleteStep(int id);

        // Aynı sıra iki adıma verilemez. Sıra hem turun akışını hem de modül
        // gruplamasını belirlediği için, iki adımın aynı yeri iddia etmesi
        // sıralamayı belirsiz bırakır.
        //
        // excludeStepId, düzenleme sırasında kaydın kendi sırasını "dolu" saymamak
        // için: adım kendi SortPath'iyle kaydedilebilmeli.
        bool IsSortPathTaken(string sortPath, int excludeStepId);

        // --- Görülme kaydı ---

        // Kullanıcının gördüğü adımların id'leri. Kayıt yoksa boş küme döner.
        HashSet<int> GetSeenStepIds(string userId);

        // Verilen adımları görülmüş olarak işaretler. Zaten kayıtlı olanlar
        // atlanır, var olmayan adım id'leri yok sayılır: dışarıdan gelen bir
        // istek yabancı anahtar hatasıyla uygulamayı düşüremesin.
        bool MarkStepsAsSeen(string userId, IEnumerable<int> stepIds);
    }
}
