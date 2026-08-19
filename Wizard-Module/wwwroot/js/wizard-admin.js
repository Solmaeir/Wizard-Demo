// Yönetim formundaki uzun metin alanları için karakter sayacı.
//
// Yönetim ekranı bilerek JavaScript'siz kuruldu (tablo, arama, sayfalama, silme
// — hepsi sunucuda). Bu dosya o kararı bozmuyor: hiçbir kütüphaneye bağlı değil,
// yüklenmezse form aynen çalışmaya devam eder. Tek yaptığı, sınıra ne kadar
// kaldığını yazmak.
//
// Asıl sınır sunucuda (StringLength). Buradaki sayaç yalnızca kullanıcıya
// önceden haber vermek için; kaydı reddeden taraf hep sunucu.
(function () {
    'use strict';

    function sayacKur(alan) {
        // Sınır view'da tekrar yazılmıyor: doğrulama özniteliği zaten ViewModel'deki
        // StringLength değerini taşıyor, sayaç onu okuyor. Böylece sınır tek yerde
        // tanımlı kalıyor.
        var sinir = parseInt(alan.getAttribute('data-val-length-max') || alan.getAttribute('maxlength') || '0', 10);

        if (!sinir) {
            return;
        }

        // Tarayıcı da sınırı uygulasın: kullanıcı 1000'i geçen bir metin yazıp
        // kaydettikten sonra hata görmektense hiç yazamasın.
        alan.setAttribute('maxlength', String(sinir));

        var sayac = document.createElement('small');
        sayac.className = 'form-text';
        alan.parentNode.insertBefore(sayac, alan.nextSibling);

        function guncelle() {
            var uzunluk = alan.value.length;
            var doldu = uzunluk >= sinir;

            sayac.textContent = uzunluk + ' / ' + sinir + (doldu ? ' — karakter sınırına ulaştınız' : '');
            sayac.classList.toggle('text-danger', doldu);
            sayac.classList.toggle('text-muted', !doldu);
        }

        alan.addEventListener('input', guncelle);
        guncelle();
    }

    document.addEventListener('DOMContentLoaded', function () {
        var alanlar = document.querySelectorAll('[data-wizard-counter]');

        for (var i = 0; i < alanlar.length; i++) {
            sayacKur(alanlar[i]);
        }
    });
})();
