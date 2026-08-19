using System.ComponentModel.DataAnnotations;

namespace Wizard.Module.ViewModels
{
    // Hem yönetim ekranındaki adım formunun modeli, hem de tura gönderilen adım.
    //
    // Dil kuralı: tanımlayıcılar (sınıf ve özellik adları) İngilizce, kullanıcının
    // gördüğü her şey Türkçe. Etiketler ve doğrulama mesajları ekranda göründüğü
    // için Türkçe yazılır; alan adları modülün geri kalanıyla tutarlı kalsın diye
    // İngilizce kalır.
    public class WizardStepViewModel
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Modül adı zorunludur")]
        [Display(Name = "Modül")]
        [StringLength(100, ErrorMessage = "Modül adı en fazla 100 karakter olabilir")]
        public string ModuleName { get; set; }

        [Required(ErrorMessage = "Başlık zorunludur")]
        [Display(Name = "Başlık")]
        [StringLength(200, ErrorMessage = "Başlık en fazla 200 karakter olabilir")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Açıklama zorunludur")]
        [Display(Name = "Açıklama")]
        [StringLength(1000, ErrorMessage = "Açıklama en fazla 1000 karakter olabilir")]
        public string Description { get; set; }

        // Seçici biçimi zorunlu tutuluyor. Modülün tek hedefleme sözleşmesi
        // [data-wizard-id="..."] ve bunu doğrulamamak, adım eklerken en sık
        // yapılacak hataya kapı açıyordu:
        //   ".btn"                  -> sayfadaki ilk rastgele butonu vurgular
        //   "[data-wizard-id=..."   -> kapanmamış seçici, querySelector istisna atar
        //   "#btnEkle"              -> her liste sayfasında eşleşir, tur şaşar
        // Üçünde de kayıt kabul ediliyordu, yönetim listesinde görünüyordu ama turda
        // çalışmıyordu; yöneticiye hiçbir geri bildirim gitmiyordu.
        //
        // Değer olarak harf, rakam, tire ve alt çizgi kabul ediliyor; mevcut
        // adımların tamamı bu kalıba uyuyor. EDI'de farklı bir karakter gerekirse
        // genişletilecek yer burası.
        [Required(ErrorMessage = "Hedef seçici zorunludur")]
        [Display(Name = "Hedef Seçici")]
        [StringLength(300, ErrorMessage = "Hedef seçici en fazla 300 karakter olabilir")]
        [RegularExpression(@"^\[data-wizard-id=""[A-Za-z0-9_-]+""\]$",
            ErrorMessage = "Hedef seçici tam olarak [data-wizard-id=\"...\"] biçiminde olmalı - örnek: [data-wizard-id=\"products-menu\"]")]
        public string TargetSelector { get; set; }

        // Regex, SortPath'in noktayla ayrılmış rakamlardan oluşmasını garanti eder.
        // Sıralama bu alanı sayıya çevirerek karşılaştırdığı için, "2.a" gibi bir
        // değer yönetim ekranından girilebilseydi adım sessizce en başa düşerdi.
        [Required(ErrorMessage = "Sıra zorunludur")]
        [Display(Name = "Sıra")]
        [StringLength(50, ErrorMessage = "Sıra en fazla 50 karakter olabilir")]
        // Her segment en fazla 4 basamak. Sınır keyfi değil: sıralama bu alanı
        // int'e çeviriyor ve int'in üst sınırı 2.147.483.647. Basamak sayısı
        // serbest bırakılınca "99999999999.1" gibi bir değer regex'ten geçiyor,
        // sayıya çevrilemiyor ve segment 0 sayılıp adım turun en başına
        // fırlıyordu — form da hata vermediği için sessizce. 9999 seviye başına
        // fazlasıyla yeterli.
        [RegularExpression(@"^\d{1,4}(\.\d{1,4})*$",
            ErrorMessage = "Sıra, noktayla ayrılmış sayılardan oluşmalı ve her bölüm en fazla 4 basamak olmalı (örn. 2.1 veya 2.1.3)")]
        public string SortPath { get; set; } = null!;

        [Display(Name = "Oluşturma Tarihi")]
        [DataType(DataType.Date)]
        public DateTime CreatedDate { get; set; }

        // Yalnizca ayni site icindeki goreli yollara izin verilir. Bu alan dogrudan
        // window.location.href'e verildigi icin serbest birakilirsa iki acik dogar:
        //   "https://sahte-site.com/giris" -> kullanici sessizce dis siteye gider
        //   "javascript:..."               -> tarayicida kod calisir
        // Tek "/" ile baslama sarti "//baska-site.com" bicimini de eler.
        [Display(Name = "Sayfa Adresi")]
        [StringLength(300, ErrorMessage = "Sayfa adresi en fazla 300 karakter olabilir")]
        [RegularExpression(@"^/(?![/\\])\S*$",
            ErrorMessage = "Sayfa adresi tek \"/\" ile başlayan iç adres olmalı (örn. /Urunler)")]
        public string? TargetUrl { get; set; }

        // Adımı görebilmek için gereken yetkinin adı. Boş bırakılırsa adım
        // herkese açık olur.
        //
        // Buraya yazılan değer host sistemdeki yetki adıyla birebir aynı olmalı;
        // modül adın anlamını bilmez, host'a olduğu gibi sorar. EDI'de yetkiler
        // bir tabloda tutuluyor ve User.IsInRole ile kontrol ediliyor.
        [Display(Name = "Gereken Yetki")]
        [StringLength(100, ErrorMessage = "Yetki adı en fazla 100 karakter olabilir")]
        public string? RequiredPermission { get; set; }


    }
}
