namespace Wizard.Module.Business
{
    /// <summary>
    /// Wizard modülünün "şu an kim var" sorusunu host sisteme sorduğu nokta.
    /// IWizardAccessService ile aynı mantık: modül sorar, host cevaplar.
    /// </summary>
    // Modülün oturum, çerez, Claims gibi hiçbir kimlik mekanizmasından haberi
    // yoktur. Kimliği metin olarak alır, çünkü host sistemde kullanıcı id'si
    // int de olabilir GUID de, kullanıcı adı da.
    //
    // EDI'ye taşınırken bu arayüzün uygulaması yazılır ve DI'a kaydedilir;
    // modülün içinde değişmesi gereken hiçbir satır yoktur.
    public interface IWizardUserProvider
    {
        /// <summary>
        /// Geçerli kullanıcının kimliği. Oturum yoksa boş metin döner —
        /// null değil, çağıran her yerde null kontrolü yapmasın diye.
        /// </summary>
        string GetCurrentUserId();
    }
}
