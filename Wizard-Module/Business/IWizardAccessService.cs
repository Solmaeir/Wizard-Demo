namespace Wizard.Module.Business
{
    /// <summary>
    /// Wizard modülünün yetki konusunda host sisteme sorduğu tek nokta.
    /// Modülün kendi rol/yetki kavramı yoktur; yalnızca "bu kullanıcıda şu
    /// yetki var mı" diye sorar.
    /// </summary>
    // Kalıp şu: modül soruyu sorar, host cevabı verir. Yetki kuralını modülün
    // içine yazmak, taşındığı her sistemde o kuralı yeniden yazmak demekti.
    //
    // Soru geçerli kullanıcı üzerinedir, kimlik parametresi almaz. Sebebi host
    // tarafının çalışma biçimi: EDI yetkileri User.IsInRole ile, yani o anki
    // istek sahibi üzerinden kontrol ediyor. Kimlik parametresi isteseydik,
    // uygulayan taraf elindeki hazır mekanizmayı kullanamayıp kullanıcıyı
    // id'den yeniden bulmak zorunda kalırdı.
    public interface IWizardAccessService
    {
        /// <summary>
        /// Geçerli kullanıcıda verilen yetki var mı?
        /// Yetki adı boşsa çağrılmaz; boş ad "herkese açık" anlamına gelir.
        /// </summary>
        /// <param name="permissionName">
        /// Host sistemdeki yetkinin adı — adımın RequiredPermission alanından gelir.
        /// </param>
        bool HasPermission(string permissionName);
    }
}
