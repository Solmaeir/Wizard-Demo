using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace Wizard.Module.Business
{
    /// <summary>
    /// IWizardPageValidator'ın MVC uygulaması: adresi uygulamanın kayıtlı
    /// controller/action listesiyle karşılaştırır.
    /// </summary>
    // Adrese HTTP isteği atmıyoruz. Kendi kendine istek yapmak yavaş olurdu,
    // kimlik/yetki taşımadığı için yanlış cevap verebilirdi ve tek iş parçacıklı
    // ortamlarda kilitlenme riski taşırdı. Bunun yerine MVC'nin zaten bellekte
    // tuttuğu action listesine bakıyoruz.
    //
    // ÖNEMLİ: karar verilemeyen her durumda "geçerli" diyoruz. Yanlış pozitif
    // (var olan bir adresi reddetmek) yöneticiyi işini yapamaz hâle getirir;
    // yanlış negatif (bozuk adresi kabul etmek) ise yalnızca eski davranışa
    // döner. Bu yüzden şüphede kalırsak engellemiyoruz.
    public class MvcWizardPageValidator : IWizardPageValidator
    {
        private readonly IActionDescriptorCollectionProvider _actionProvider;

        public MvcWizardPageValidator(IActionDescriptorCollectionProvider actionProvider)
        {
            _actionProvider = actionProvider;
        }

        public bool IsKnownPage(string targetUrl)
        {
            // Boş TargetUrl "bulunduğu sayfada göster" demek; kontrol edilecek
            // bir adres yok.
            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                return true;
            }

            var yol = Normalize(targetUrl);

            // Kök adres her uygulamada vardır.
            if (yol.Length == 0)
            {
                return true;
            }

            var parcalar = yol.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var controller = parcalar.Length > 0 ? parcalar[0] : "Home";
            var action = parcalar.Length > 1 ? parcalar[1] : "Index";

            var actionlar = _actionProvider.ActionDescriptors.Items;

            // Liste boşsa karar veremiyoruz demektir; engellemiyoruz.
            if (actionlar.Count == 0)
            {
                return true;
            }

            // MVC dışı uçlar (Razor Pages, minimal API) bu listede farklı türde
            // görünür ve controller/action eşlemesiyle çözülemez. Böyle bir uç
            // varsa, adresi tanıyamamamız "yok" anlamına gelmeyeceği için
            // sonunda engellemiyoruz.
            var cozulemeyenUcVar = false;

            foreach (var descriptor in actionlar)
            {
                if (descriptor is not ControllerActionDescriptor mvcAction)
                {
                    cozulemeyenUcVar = true;
                    continue;
                }

                if (string.Equals(mvcAction.ControllerName, controller, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(mvcAction.ActionName, action, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return cozulemeyenUcVar;
        }

        // Sorgu dizesi ve parça işareti atılır, sondaki eğik çizgi silinir:
        // "/Urunler/", "/Urunler?x=1" ve "/Urunler" aynı sayfadır.
        private static string Normalize(string url)
        {
            var yol = url.Trim();

            var soru = yol.IndexOf('?');
            if (soru >= 0)
            {
                yol = yol.Substring(0, soru);
            }

            var diyez = yol.IndexOf('#');
            if (diyez >= 0)
            {
                yol = yol.Substring(0, diyez);
            }

            return yol.TrimEnd('/');
        }
    }
}
