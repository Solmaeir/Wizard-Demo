namespace Wizard.Module.ViewModels
{
    // Tur isteğine verilen cevap. Tarayıcıya "kaç adım var" değil, "ne tür bir
    // tur bu" bilgisi de gidiyor; wizard-module.js karşılama metnini buna göre
    // seçiyor.
    public class WizardResultViewModel
    {
        // Sözleşme üç değerden ibarettir, dördüncüsü yok:
        //   "full"    - kullanıcı turu ilk kez görüyor, adımların tamamı gönderildi
        //   "partial" - daha önce görmüş, sadece sonradan eklenen adımlar var
        //   "none"    - gösterilecek yeni adım kalmadı, Steps boş gelir
        public string Type { get; set; }

        // Yetkisi olmayan kullanıcının erişemediği adımlar bu listeye hiç girmez.
        // Eleme sunucuda, Business katmanında yapılır; gizlenmiş hâlde tarayıcıya
        // gönderilseydi DevTools'tan okunabilirdi.
        public List<WizardStepViewModel> Steps { get; set; }
    }
}
