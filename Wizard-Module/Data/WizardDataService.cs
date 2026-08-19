using Wizard.Module.Models;
using Microsoft.EntityFrameworkCore;

namespace Wizard.Module.Data
{
    // Wizard'ın veritabanıyla konuşan tek sınıfı. Aşağıda iki bölüm var:
    // önce adım tablosu (WizardSteps), sonra görülme kaydı (WizardStepViews).
    public class WizardDataService : IWizardDataService
    {
        // Tek SQL sorgusuna konacak en fazla id sayısı. SQL Server bir sorguda
        // 2100 parametre kabul ediyor ve her id bir parametre; 1000 hem altında
        // kalıyor hem de sorgu sayısını makul tutuyor.
        private const int SorguParcaBoyutu = 1000;

        private readonly AppDbContext _context;

        public WizardDataService(AppDbContext context)
        {
            _context = context;
        }

        // --- Adım tablosu ---

        public List<WizardStep> GetAllSteps()
        {
            // Id'ye göre sıralamak turdaki sırayı vermez, sadece her çağrıda aynı
            // sonucu almayı garanti eder. Asıl sıralama SortPath'e göre yapılır ve
            // o bir iş kuralı olduğu için Business katmanına bırakıldı.
            return _context.WizardSteps.OrderBy(x => x.Id).ToList();
        }

        public WizardStep? GetStepById(int id)
        {
            return _context.WizardSteps.FirstOrDefault(x => x.Id == id);
        }

        public bool AddStep(WizardStep step)
        {
            if (step == null)
            {
                return false;
            }

            _context.WizardSteps.Add(step);
            return _context.SaveChanges() > 0;
        }

        // Güncelleme, gelen nesneyi olduğu gibi iliştirmek yerine mevcut kaydı
        // çekip alanlarını kopyalıyor. İki sebebi var:
        //
        // 1) CreatedDate'e hiç dokunulmuyor, yani oluşturulma tarihi güncellemeyle
        //    değişmiyor. Eskiden gelen nesne doğrudan yazıldığı ve formda bu alan
        //    taşınmadığı için tarih her düzenlemede varsayılan değere düşüyordu.
        // 2) Aynı kaydı önce okuyup sonra ikinci bir nesneyle güncellemeye
        //    çalışmak EF'i "aynı anahtar iki kez takip ediliyor" hatasına
        //    düşürüyor. Tek nesne üzerinden gidince o çakışma da kalkıyor.
        public bool UpdateStep(WizardStep step)
        {
            if (step == null)
            {
                return false;
            }

            var mevcut = _context.WizardSteps.FirstOrDefault(x => x.Id == step.Id);

            if (mevcut == null)
            {
                return false;
            }

            mevcut.ModuleName = step.ModuleName;
            mevcut.Title = step.Title;
            mevcut.Description = step.Description;
            mevcut.TargetSelector = step.TargetSelector;
            mevcut.TargetUrl = step.TargetUrl;
            mevcut.SortPath = step.SortPath;

            // Hiçbir alan değişmemişse SaveChanges 0 döner; bu bir hata değil.
            _context.SaveChanges();
            return true;
        }

        public bool DeleteStep(int id)
        {
            var step = _context.WizardSteps.FirstOrDefault(x => x.Id == id);
            if (step == null) return false;

            // Adımın görülme kayıtları burada elle siliniyor. Veritabanında
            // ON DELETE CASCADE tanımlı olsa da EF, takip ettiği satırları
            // kendi silmek ister; ikisini birlikte yapmak, tablonun cascade'siz
            // kurulduğu ortamlarda da silmenin çalışmasını sağlıyor.
            var views = _context.WizardStepViews.Where(x => x.StepId == id).ToList();

            if (views.Count > 0)
            {
                _context.WizardStepViews.RemoveRange(views);
            }

            _context.WizardSteps.Remove(step);
            return _context.SaveChanges() > 0;
        }

        public bool IsSortPathTaken(string sortPath, int excludeStepId)
        {
            if (string.IsNullOrWhiteSpace(sortPath))
            {
                return false;
            }

            return _context.WizardSteps.Any(x => x.SortPath == sortPath && x.Id != excludeStepId);
        }

        // --- Görülme kaydı ---

        public HashSet<int> GetSeenStepIds(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new HashSet<int>();
            }

            return _context.WizardStepViews
                           .Where(x => x.UserId == userId)
                           .Select(x => x.StepId)
                           .ToHashSet();
        }

        public bool MarkStepsAsSeen(string userId, IEnumerable<int> stepIds)
        {
            if (string.IsNullOrWhiteSpace(userId) || stepIds == null)
            {
                return false;
            }

            var istenen = stepIds.Distinct().ToList();

            if (istenen.Count == 0)
            {
                return false;
            }

            // Gelen id'ler tarayıcıdan geliyor; var olmayan bir id doğrudan
            // yazılsaydı yabancı anahtar hatası 500'e dönüşürdü. Önce gerçekten
            // var olanları süzüyoruz.
            //
            // Sorgu parçalara bölünüyor çünkü SQL Server tek sorguda sınırlı
            // sayıda parametre kabul ediyor (2100) ve her id bir parametre.
            // Önceden liste doğrudan kesiliyordu; bu, çok adımlı kurulumlarda
            // kuyruktaki adımların sessizce hiç işaretlenmemesine ve turun her
            // girişte yeniden açılmasına yol açıyordu. Kesmek yerine bölüyoruz,
            // böylece adım sayısının üst sınırı kalmıyor.
            var gecerli = new List<int>();

            foreach (var parca in Parcala(istenen, SorguParcaBoyutu))
            {
                gecerli.AddRange(_context.WizardSteps
                                         .Where(x => parca.Contains(x.Id))
                                         .Select(x => x.Id)
                                         .ToList());
            }

            if (gecerli.Count == 0)
            {
                return false;
            }

            // Bu sorgu da aynı sebeple parçalanıyor.
            var mevcut = new HashSet<int>();

            foreach (var parca in Parcala(gecerli, SorguParcaBoyutu))
            {
                foreach (var stepId in _context.WizardStepViews
                                               .Where(x => x.UserId == userId && parca.Contains(x.StepId))
                                               .Select(x => x.StepId)
                                               .ToList())
                {
                    mevcut.Add(stepId);
                }
            }

            var yeni = gecerli.Where(id => !mevcut.Contains(id))
                              .Select(id => new WizardStepView
                              {
                                  UserId = userId,
                                  StepId = id,
                                  SeenDate = DateTime.Now
                              })
                              .ToList();

            // Hepsi zaten kayıtlıysa yazacak bir şey yok; bu bir hata değil,
            // turu ikinci kez kapatmış olmak da normal.
            if (yeni.Count == 0)
            {
                return true;
            }

            _context.WizardStepViews.AddRange(yeni);

            try
            {
                return _context.SaveChanges() > 0;
            }
            catch (DbUpdateException)
            {
                // Aynı kullanıcı turu iki sekmede birden bitirirse, iki istek de
                // "bu kayıt yok" deyip aynı satırı yazmaya kalkabilir; benzersiz
                // indeks ikincisini reddeder. Kayıt zaten yazılmış olduğu için bu
                // bir hata değil, yarıştan kaybedilen tarafın normal sonucu.
                _context.ChangeTracker.Clear();
                return true;
            }
        }

        // Listeyi belirli boyutta parçalara böler. Yerleşik bir Chunk metodu
        // .NET 6 ile geldi ama burada elle yazıldı ki modül daha eski hedef
        // sürümlere de sorunsuz taşınabilsin.
        private static IEnumerable<List<int>> Parcala(List<int> kaynak, int boyut)
        {
            for (var i = 0; i < kaynak.Count; i += boyut)
            {
                yield return kaynak.GetRange(i, Math.Min(boyut, kaynak.Count - i));
            }
        }
    }
}
