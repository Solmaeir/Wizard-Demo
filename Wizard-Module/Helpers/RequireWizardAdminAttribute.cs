using Wizard.Module.Business;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Wizard.Module.Helpers
{
    /// <summary>
    /// Wizard Yönetimi ekranını korur. Yetki kararı modülün kendisinde değil,
    /// IWizardAccessService uygulamasındadır; burası yalnızca sorar.
    /// </summary>
    // Aranan yetkinin adı da koda gömülü değil, yapılandırmadan geliyor
    // (WizardOptions.ManagePermissionName). Host sistemde bu yetki hangi isimle
    // duruyorsa AddWizard çağrısında o yazılır.
    //
    // Servisler constructor ile değil RequestServices üzerinden alınıyor:
    // attribute'lar .NET'te normal DI ile beslenmiyor, [ServiceFilter] gibi ek bir
    // kayıt gerekirdi. Bu yol sayesinde filtre sınıfın üstüne düz yazılabiliyor.
    //
    // Reddedince 403 dönülüyor ve modülün kendi uyarı sayfası gösteriliyor;
    // host sisteme "şu adrese git" demek, o adresin var olduğunu varsaymak olurdu.
    public class RequireWizardAdminAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var services = context.HttpContext.RequestServices;

            var accessService = services.GetRequiredService<IWizardAccessService>();
            var options = services.GetRequiredService<IOptions<WizardOptions>>().Value;

            if (!accessService.HasPermission(options.ManagePermissionName))
            {
                // Boş bir 403 yerine açıklamalı bir sayfa: kullanıcı bembeyaz
                // ekranı sistemin çökmesi sanıyordu. HTTP durumu yine 403,
                // yalnızca gövdesi anlamlı.
                //
                // Controller'ın ViewData'sı ödünç alınıyor ki sayfa host'un
                // düzeniyle (menü, üst çubuk) render edilebilsin. Controller
                // değilse — beklenmedik bir kullanım — düz 403'e düşüyoruz.
                if (context.Controller is Controller controller)
                {
                    context.Result = new ViewResult
                    {
                        ViewName = "~/Views/WizardSteps/AccessDenied.cshtml",
                        ViewData = controller.ViewData,
                        StatusCode = StatusCodes.Status403Forbidden
                    };

                    return;
                }

                context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden);
                return;
            }

            base.OnActionExecuting(context);
        }
    }
}
