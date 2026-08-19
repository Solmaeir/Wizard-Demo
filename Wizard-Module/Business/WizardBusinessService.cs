using Wizard.Module.Data;
using Wizard.Module.Models;
using Wizard.Module.ViewModels;

namespace Wizard.Module.Business
{
    // Turun beyni. Üç iş yapar: adımları doğru sıraya dizer, kullanıcının
    // görmemesi gerekenleri eler, hangilerinin yeni olduğuna karar verir.
    // Adım metinlerinin hiçbiri burada yok, hepsi veritabanından gelir.
    public class WizardBusinessService : IWizardBusinessService
    {
        // Yönetim ekranında bir sayfada gösterilecek adım sayısı.
        private const int SayfaBoyutu = 20;

        private readonly IWizardDataService _wizardDataService;
        private readonly IWizardAccessService _wizardAccessService;

        public WizardBusinessService(IWizardDataService wizardDataService, IWizardAccessService wizardAccessService)
        {
            _wizardDataService = wizardDataService;
            _wizardAccessService = wizardAccessService;
        }

        public WizardResultViewModel GetAllStepsUnfiltered(string userId)
        {
            var allSteps = GetOrderedSteps(userId);

            return new WizardResultViewModel
            {
                Type = "full",
                Steps = MapToViewModel(allSteps)
            };
        }

        public WizardResultViewModel GetStepsToShow(string userId)
        {
            var allSteps = GetOrderedSteps(userId);
            var seenIds = _wizardDataService.GetSeenStepIds(userId);

            // Hiç kaydı olmayan kullanıcı turu baştan görür.
            if (seenIds.Count == 0)
            {
                return new WizardResultViewModel
                {
                    Type = "full",
                    Steps = MapToViewModel(allSteps)
                };
            }

            // Ölçüt "id'si en son görülenden büyük mü" değil, "bu adım görüldü mü".
            // Aradaki fark sonradan bir modüle yetki kazanan kullanıcıda ortaya
            // çıkıyor: o modülün adımları eski ve küçük id'li oldukları hâlde bu
            // kullanıcı için görülmemiş sayılırlar ve gösterilirler.
            var newSteps = allSteps.Where(x => !seenIds.Contains(x.Id)).ToList();

            if (newSteps.Count == 0)
            {
                return new WizardResultViewModel
                {
                    Type = "none",
                    Steps = new List<WizardStepViewModel>()
                };
            }

            return new WizardResultViewModel
            {
                Type = "partial",
                Steps = MapToViewModel(newSteps)
            };
        }

        // Adım adım değil, tur kapanınca bir kez çağrılır. Kullanıcı turu yarıda
        // bıraksa da ilerleme kaydedilir; amaç aynı adımları her girişte tekrar
        // göstermemek.
        public bool MarkStepsAsSeen(string userId, IEnumerable<int> stepIds)
        {
            if (string.IsNullOrWhiteSpace(userId) || stepIds == null)
            {
                return false;
            }

            // Kullanıcının erişemediği bir adım, id'si istekte gönderilmiş olsa
            // bile görülmüş sayılmamalı: yarın yetki kazanırsa onu görmeli.
            var erisilebilir = GetOrderedSteps(userId).Select(x => x.Id).ToHashSet();
            var kabul = stepIds.Where(erisilebilir.Contains).ToList();

            if (kabul.Count == 0)
            {
                return false;
            }

            return _wizardDataService.MarkStepsAsSeen(userId, kabul);
        }

        public WizardStepListViewModel GetList(string? search, int page)
        {
            // Adımlar zaten toplu okunup bellekte sıralanıyor (sıralama kuralı
            // SQL'e çevrilemiyor), o yüzden arama ve sayfalama da burada yapılıyor.
            var steps = GetOrderedSteps();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var aranan = search.Trim();

                steps = steps.Where(x => Iceriyor(x.SortPath, aranan)
                                      || Iceriyor(x.ModuleName, aranan)
                                      || Iceriyor(x.Title, aranan)
                                      || Iceriyor(x.TargetSelector, aranan)
                                      || Iceriyor(x.TargetUrl, aranan)
                                      || Iceriyor(x.RequiredPermission, aranan))
                             .ToList();
            }

            var toplam = steps.Count;
            var sayfaSayisi = Math.Max(1, (int)Math.Ceiling(toplam / (double)SayfaBoyutu));

            // Elle girilen sayfa numarası sınırların dışında olabilir; boş sayfa
            // göstermek yerine en yakın geçerli sayfaya çekiyoruz.
            if (page < 1) { page = 1; }
            if (page > sayfaSayisi) { page = sayfaSayisi; }

            var sayfadakiler = steps.Skip((page - 1) * SayfaBoyutu)
                                    .Take(SayfaBoyutu)
                                    .ToList();

            return new WizardStepListViewModel
            {
                Steps = MapToViewModel(sayfadakiler),
                Search = search,
                Page = page,
                PageCount = sayfaSayisi,
                TotalCount = toplam
            };
        }

        // Aramada büyük/küçük harf ayrımı yok. Ordinal karşılaştırma kullanılıyor:
        // kültüre duyarlı karşılaştırma Türkçe "I/İ" çiftinde beklenmedik sonuçlar
        // verebiliyor ve bu bir arama kutusu, sıralama değil.
        private static bool Iceriyor(string? deger, string aranan)
        {
            return !string.IsNullOrEmpty(deger)
                && deger.Contains(aranan, StringComparison.OrdinalIgnoreCase);
        }

        public WizardStep? GetById(int id)
        {
            return _wizardDataService.GetStepById(id);
        }

        public bool Add(WizardStep step)
        {
            if (step == null)
            {
                return false;
            }

            // Sıra benzersiz olmalı. Kural burada da uygulanıyor, yalnızca
            // yönetim ekranındaki kontrole güvenilmiyor: Business'a başka bir
            // yerden gelen çağrı da bu kuraldan geçmek zorunda.
            if (_wizardDataService.IsSortPathTaken(step.SortPath, 0))
            {
                return false;
            }

            // Tarih formdan gelse kullanıcı geçmişe tarih atabilirdi; burada damgalanıyor.
            step.CreatedDate = DateTime.Now;
            return _wizardDataService.AddStep(step);
        }

        public bool Update(WizardStep step)
        {
            if (step == null || step.Id <= 0)
            {
                return false;
            }

            // Sıra benzersizliği güncellemede de geçerli. Kaydın kendi sırası
            // hariç tutuluyor, yoksa adım kendi sırasıyla kaydedilemezdi.
            if (_wizardDataService.IsSortPathTaken(step.SortPath, step.Id))
            {
                return false;
            }

            // Oluşturulma tarihinin korunması Data katmanının işi: güncelleme
            // mevcut kaydın alanlarını kopyalıyor, CreatedDate'e hiç dokunmuyor.
            // Kaydı burada ayrıca okumuyoruz — okunan nesne EF tarafından takip
            // edilmeye başlıyor ve aynı anahtarla ikinci bir nesne güncellenmek
            // istendiğinde çakışma hatası veriyor.
            return _wizardDataService.UpdateStep(step);
        }

        public bool Delete(int id)
        {
            if (id <= 0)
            {
                return false;
            }

            return _wizardDataService.DeleteStep(id);
        }

        public bool IsSortPathAvailable(string sortPath, int excludeStepId)
        {
            return !_wizardDataService.IsSortPathTaken(sortPath, excludeStepId);
        }

        private List<WizardStepViewModel> MapToViewModel(List<WizardStep> steps)
        {
            return steps.Select(x => new WizardStepViewModel
            {
                Id = x.Id,
                ModuleName = x.ModuleName,
                Title = x.Title,
                Description = x.Description,
                TargetSelector = x.TargetSelector,
                TargetUrl = x.TargetUrl,
                SortPath = x.SortPath,
                RequiredPermission = x.RequiredPermission,
                CreatedDate = x.CreatedDate
            }).ToList();
        }

        // Adımlar SortPath'e göre sıralanır: önce modül (ilk segment), sonra modül
        // içindeki sıra, sonra alt seviyeler. Parametresiz sürüm yönetim ekranı
        // içindir; orada filtreleme yapılmaz.
        private List<WizardStep> GetOrderedSteps()
        {
            var steps = _wizardDataService.GetAllSteps();

            // Eşitlik durumunda Id'ye düşülüyor. Artık aynı sıra iki adıma
            // verilemiyor, ama EDI'ye taşınırken hazır veride çift sıra kalmış
            // olabilir. List.Sort kararsız bir sıralama olduğu için eşit anahtarlı
            // iki adım her istekte farklı sırada gelebilirdi; Id ikinci anahtar
            // olunca sonuç her seferinde aynı.
            steps.Sort((left, right) =>
            {
                var karsilastirma = CompareSortPaths(left.SortPath, right.SortPath);

                return karsilastirma != 0 ? karsilastirma : left.Id.CompareTo(right.Id);
            });

            return steps;
        }

        // Tur için kullanılan sürüm: kullanıcının yetkisi olmayan adımlar burada
        // elenir. Filtreleme sunucuda yapılır; elenen adım tarayıcıya hiç
        // gönderilmez, sadece ekranda gizlenmiş olmaz.
        private List<WizardStep> GetOrderedSteps(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
            {
                return new List<WizardStep>();
            }

            return GetOrderedSteps()
                .Where(CanSee)
                .ToList();
        }

        // Yetki adı boş olan adım herkese açıktır; dolu olanı host'a soruyoruz.
        // Karar modülde değil host'ta: "Ürünler" ya da "WizardYonetimi" gibi bir
        // adın ne anlama geldiğini yalnızca host sistem bilir.
        private bool CanSee(WizardStep step)
        {
            if (string.IsNullOrWhiteSpace(step.RequiredPermission))
            {
                return true;
            }

            return _wizardAccessService.HasPermission(step.RequiredPermission);
        }

        // İki SortPath'i segment segment karşılaştırır. Metin karşılaştırması
        // burada işe yaramaz: "2.10" metin olarak "2.9"dan küçük görünür, yani
        // onuncu adım dokuzuncunun önüne geçerdi.
        private static int CompareSortPaths(string leftPath, string rightPath)
        {
            var left = ParsePathSegments(leftPath);
            var right = ParsePathSegments(rightPath);
            var length = Math.Max(left.Length, right.Length);

            for (var i = 0; i < length; i++)
            {
                // Eksik segment -1 sayılır; böylece "2.2", "2.2.1"den önce gelir.
                var leftSegment = i < left.Length ? left[i] : -1;
                var rightSegment = i < right.Length ? right[i] : -1;

                if (leftSegment != rightSegment)
                {
                    return leftSegment.CompareTo(rightSegment);
                }
            }

            return 0;
        }

        // "2.1.3" -> [2, 1, 3].
        private static int[] ParsePathSegments(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return Array.Empty<int>();
            }

            // Çevrilemeyen segment int.MaxValue sayılıyor, 0 değil. Fark, böyle
            // bir değerin nereye düşeceği: 0 dersek adım turun EN BAŞINA fırlar
            // ve ilk adımın önüne geçer; MaxValue dersek en sona düşer. İkisi de
            // yanlış sıradır ama sona düşmek turun girişini bozmaz.
            //
            // Yeni kayıtlarda buraya böyle bir değer gelmiyor (basamak sınırı
            // formda uygulanıyor); bu satır, hazır veriyle gelen kurulumlar için.
            return path.Split('.', StringSplitOptions.RemoveEmptyEntries)
                       .Select(segment => int.TryParse(segment, out var value) ? value : int.MaxValue)
                       .ToArray();
        }
    }
}
