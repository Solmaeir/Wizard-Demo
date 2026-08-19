using Wizard.Module.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wizard.Module.Business
{
    /// <summary>
    /// Wizard modülünün DI kaydı. Host sistemin tek yapması gereken bunu çağırıp
    /// kendi IWizardAccessService ve IWizardUserProvider uygulamasını kaydetmek.
    /// </summary>
    public static class WizardServiceCollectionExtensions
    {
        /// <param name="managePermissionName">
        /// Wizard Yönetimi ekranına girebilmek için gereken yetkinin, host
        /// sistemdeki adı. Verilmezse "WizardYonetimi" varsayılır.
        /// </param>
        public static IServiceCollection AddWizard(this IServiceCollection services, string? managePermissionName = null)
        {
            services.AddScoped<IWizardDataService, WizardDataService>();
            services.AddScoped<IWizardBusinessService, WizardBusinessService>();

            services.Configure<WizardOptions>(options =>
            {
                if (!string.IsNullOrWhiteSpace(managePermissionName))
                {
                    options.ManagePermissionName = managePermissionName;
                }
            });

            // İki seam için güvenli varsayılanlar. TryAdd olduğu için host kendi
            // uygulamasını kaydettiyse bunlar hiç devreye girmez; kaydetmediyse
            // modül sessizce çalışmaya devam etmek yerine her şeye hayır der ve
            // eksiği log'a yazar.
            services.TryAddScoped<IWizardAccessService, UnconfiguredWizardAccessService>();
            services.TryAddScoped<IWizardUserProvider, UnconfiguredWizardUserProvider>();

            // TargetUrl'in var olan bir sayfaya işaret edip etmediğini kontrol eder.
            // TryAdd: EDI'nin yönlendirme yapısı farklıysa kendi uygulamasını
            // kaydedip bunu devre dışı bırakabilir.
            services.TryAddScoped<IWizardPageValidator, MvcWizardPageValidator>();

            return services;
        }
    }
}
