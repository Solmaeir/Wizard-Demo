using System;
using System.Collections.Generic;

namespace Wizard.Module.Models;

// Turun tek bir adımı. Adımların metni de hedefi de burada, yani veritabanında
// durur; hiçbiri koda gömülmez. Yeni adım eklemek için kod değişikliği gerekmez.
//
// Alan adlarının İngilizce olması bilinçli bir tercih: bu modül ileride EDI'nin
// kendi sistemine taşınacak. Projenin geri kalanı Türkçe isimlendirmede kalıyor.
public partial class WizardStep
{
    public int Id { get; set; }

    // Yönetim ekranında adımları gruplayıp okumayı kolaylaştırır. Sıralamaya
    // etkisi yoktur, sıralamayı SortPath belirler.
    public string ModuleName { get; set; } = null!;

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    // Her zaman [data-wizard-id="..."] biçiminde. Tek bir hedefleme yöntemine
    // bağlı kalmak, sayfa yapısı veya CSS sınıfları değiştiğinde turun bozulmasını
    // engelliyor; özniteliği taşıyan elemanı kimse tesadüfen silmez.
    public string TargetSelector { get; set; } = null!;

    // Noktalı hiyerarşi: "2.1", "2.2", "2.2.1". İlk segment modülün turdaki yeri,
    // sonrakiler modül içindeki sıra. Ayrı bir modül sırası alanına gerek kalmıyor,
    // gruplama kendiliğinden oluşuyor. Metin gibi sıralanamaz — "2.10" metin
    // olarak "2.9"dan önce gelir. Karşılaştırma WizardBusinessService içinde
    // segment segment sayısal yapılır.
    public string SortPath { get; set; } = null!;

    public DateTime CreatedDate { get; set; }

    // Adımın hangi sayfada gösterileceği. Boşsa adım bulunduğu sayfada gösterilir.
    // Doluysa oraya gidilir — hedef eleman o an açık olan sayfada bulunsa bile.
    // Aksi halde "#btnEkle" gibi her liste sayfasında bulunan seçiciler yanlış
    // sayfada eşleşiyordu.
    //
    // Yalnızca navigasyon içindir; yetkiyle ilgisi yoktur (bkz. RequiredPermission).
    public string? TargetUrl { get; set; }

    // Adımı görebilmek için gereken yetkinin adı. Boşsa adım herkese açıktır.
    //
    // Modül bu adın ne anlama geldiğini bilmez; host sisteme "bu kullanıcıda
    // şu yetki var mı" diye sorar, cevabı IWizardAccessService verir. EDI'de
    // yetkiler bir tabloda tutulup User.IsInRole ile kontrol ediliyor, yani
    // buraya yazılan değer oradaki yetki adıyla birebir aynı olmalı
    // (örn. "Ürünler", "WizardYonetimi").
    //
    // Yetkinin TargetUrl'den ayrılmasının sebebi: adresi olan her adım kısıtlı
    // değildir, kısıtlı her adımın da adresi olmak zorunda değildir. İkisi tek
    // alanda birleştirilince menü tanıtımı gibi adımlar, yalnızca kısıtlanmak
    // için gereksiz yere sayfa değiştirmeye zorlanıyordu.
    public string? RequiredPermission { get; set; }
}
