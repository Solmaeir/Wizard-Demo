namespace Wizard.Module.Business
{
    /// <summary>
    /// Adıma girilen TargetUrl'in gerçekten var olan bir sayfaya işaret edip
    /// etmediğini söyler. Amaç, turun kullanıcıyı 404'e götürmesini kayıt
    /// anında engellemek.
    /// </summary>
    // Ayrı bir arayüz olmasının sebebi taşınabilirlik: bu soruyu cevaplamak
    // host sistemin yönlendirme yapısını bilmeyi gerektiriyor. Modül varsayılan
    // bir uygulama getiriyor (MVC controller/action eşlemesi), EDI'de rotalar
    // farklı kuruluysa yalnızca bu arayüzün uygulaması değiştirilir.
    public interface IWizardPageValidator
    {
        /// <summary>
        /// Adres bilinen bir sayfaya karşılık geliyorsa true.
        /// Karar verilemeyen durumlarda true döner — bkz. uygulamadaki not.
        /// </summary>
        bool IsKnownPage(string targetUrl);
    }
}
