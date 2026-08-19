using Microsoft.Extensions.Logging;

namespace Wizard.Module.Business
{
    /// <summary>
    /// Host sistem kendi yetki uygulamasını kaydetmediğinde devreye giren varsayılan.
    /// Güvenli tarafta kalır (her şeye hayır der) ama sessiz kalmaz: neyin eksik
    /// olduğunu log'a yazar, böylece entegrasyonu yapan kişi durumu hemen görür.
    /// </summary>
    // Program.cs'te TryAddScoped ile kaydedilir; host kendi uygulamasını
    // kaydettiyse bu sınıf hiç devreye girmez.
    //
    // Varsayılanın "herkese izin ver" değil "kimseye izin verme" olması bilinçli.
    // Unutulan bir DI kaydı, sessizce herkese açılan bir tur üretmesin diye.
    public class UnconfiguredWizardAccessService : IWizardAccessService
    {
        private const string Uyari =
            "Wizard yetki protokolü tanımlanmamış: IWizardAccessService uygulaması DI'a kaydedilmemiş. " +
            "Güvenlik gereği \"{Yetki}\" yetkisi için erişim reddedildi. " +
            "Kendi yetki kontrolünüzü içeren bir IWizardAccessService uygulaması kaydedin.";

        private readonly ILogger<UnconfiguredWizardAccessService> _logger;

        public UnconfiguredWizardAccessService(ILogger<UnconfiguredWizardAccessService> logger)
        {
            _logger = logger;
        }

        public bool HasPermission(string permissionName)
        {
            _logger.LogWarning(Uyari, permissionName);
            return false;
        }
    }
}
