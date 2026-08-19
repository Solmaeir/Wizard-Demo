namespace Wizard.Module.ViewModels
{
    // Yönetim ekranındaki liste sayfasının modeli: o sayfada gösterilecek adımlar
    // ve sayfalama/arama durumu.
    //
    // Ayrı bir model olmasının sebebi, view'ın sayfa numarasını ya da arama
    // metnini kendi hesaplamaması. Kaçıncı sayfadayız, kaç sayfa var, arama
    // neydi — hepsi hazır gelir.
    public class WizardStepListViewModel
    {
        public List<WizardStepViewModel> Steps { get; set; } = new();

        // Arama kutusunda yazan metin. Sayfa değiştirilirken korunur.
        public string? Search { get; set; }

        public int Page { get; set; } = 1;

        public int PageCount { get; set; } = 1;

        // Aramadan sonra kalan toplam adım sayısı (bu sayfadaki değil, tümü).
        public int TotalCount { get; set; }
    }
}
