namespace Wizard.Module.Business
{
    /// <summary>
    /// Modülün host sisteme göre ayarlanan tek yapılandırması.
    /// </summary>
    public class WizardOptions
    {
        /// <summary>
        /// Wizard Yönetimi ekranına girebilmek için gereken yetkinin adı.
        /// </summary>
        // Kod yerine yapılandırma olmasının sebebi: bu ad host sisteme ait.
        // EDI'nin yetki tablosunda hangi isimle duruyorsa o yazılır; modülün
        // içinde sabit bir isim tutmak, her kurulumda kod değiştirmek olurdu.
        public string ManagePermissionName { get; set; } = "WizardYonetimi";
    }
}
