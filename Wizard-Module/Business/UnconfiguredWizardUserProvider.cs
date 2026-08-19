using Microsoft.Extensions.Logging;

namespace Wizard.Module.Business
{
    /// <summary>
    /// Host sistem kendi kullanıcı sağlayıcısını kaydetmediğinde devreye giren
    /// varsayılan. Kimseyi tanımaz ve eksiği log'a yazar.
    /// </summary>
    // UnconfiguredWizardAccessService ile aynı desen: sessizce çalışıyormuş gibi
    // yapmak yerine, entegrasyonu yapan kişiye neyin eksik olduğunu söyler.
    // Boş kimlik döndüğünde yetki servisi de zaten hiçbir şeye izin vermez.
    public class UnconfiguredWizardUserProvider : IWizardUserProvider
    {
        private const string Uyari =
            "Wizard kullanıcı sağlayıcısı tanımlanmamış: IWizardUserProvider uygulaması DI'a kaydedilmemiş. " +
            "Tur çalışmayacak. Host sistemdeki geçerli kullanıcının kimliğini döndüren bir uygulama kaydedin.";

        private readonly ILogger<UnconfiguredWizardUserProvider> _logger;

        public UnconfiguredWizardUserProvider(ILogger<UnconfiguredWizardUserProvider> logger)
        {
            _logger = logger;
        }

        public string GetCurrentUserId()
        {
            _logger.LogWarning(Uyari);
            return string.Empty;
        }
    }
}
