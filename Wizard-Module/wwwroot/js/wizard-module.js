// Tanıtım turunun tarayıcı tarafı. Adımların içeriği burada değil, sunucudan
// geliyor; bu dosyanın işi gelen adımları sırayla göstermek, hedef elemanı bulmak,
// gerekiyorsa sayfa değiştirmek ve tur bitince ilerlemeyi kaydetmek.
//
// Turun kendisini driver.js çiziyor. Aşağıdaki kodun önemli bir kısmı o
// kütüphanenin davranışını istediğimiz hâle getirmek için; hangi satırın neden
// öyle yazıldığı ilgili yerde ayrıca açıklandı.

// Sayfa değişirken kalan adımların saklandığı anahtar. sessionStorage kullanılıyor:
// sekme kapanınca kendiliğinden siliniyor, yarım kalmış bir tur haftalarca
// bekleyip beklenmedik bir anda açılmıyor.
var WIZARD_RESUME_KEY = 'wizardResumeState';

// wizard-theme.css'teki .wizard-animate-in giriş animasyonu süresiyle aynı olmalı.
var WIZARD_ENTRANCE_MS = 420;

// Bilgi kutusu ile hedef arasındaki görsel boşluk (px). Büyütmek istersen tek
// değiştirilecek yer burası.
//
// Bu boşluk için driver.js'in kendi popoverOffset ayarı kullanılmadı: o değer
// kütüphanenin "kutu bu tarafa sığar mı" hesabına giriyor (genişlik + stagePadding
// + popoverOffset) ve dar alanlarda kutuyu yanlış tarafa atıyordu. Boşluk bunun
// yerine aşağıda transform ile veriliyor, yani sığdırma hesabına hiç karışmıyor.
var WIZARD_POPOVER_GAP = 14;

// Modülün tek giriş noktası. jQuery kullanılmıyor: modül taşındığı sistemde
// jQuery'nin bulunacağını varsayamaz. Kalan tek dış bağımlılık driver.js.
document.addEventListener('DOMContentLoaded', function () {
    checkWizardOnLoad();

    // Buton her sayfada olmayabilir; host isterse hiç koymayabilir de.
    var startButton = document.getElementById('btnWizardBaslat');

    if (startButton) {
        startButton.addEventListener('click', startFullWizard);
    }
});

// Sunucudan adım listesi ister. Hata hâlinde tur açılmaz ama sayfa da bozulmaz:
// istek başarısız olursa, cevap JSON değilse ya da beklenen alanlar yoksa
// konsola tek satır uyarı düşer ve iş sessizce biter.
function fetchSteps(url, onSuccess) {
    fetch(url, {
        method: 'GET',
        headers: { 'Accept': 'application/json' },
        credentials: 'same-origin'
    })
        .then(function (response) {
            if (!response.ok) {
                throw new Error('HTTP ' + response.status);
            }

            return response.json();
        })
        .then(function (result) {
            // Sunucu cevabının biçimi değişmiş olabilir; içeriğini kontrol
            // etmeden kullanmak "undefined.length" gibi hatalar üretirdi.
            if (!result || !Array.isArray(result.steps)) {
                console.warn('Wizard: unexpected response shape from', url, result);
                return;
            }

            onSuccess(result);
        })
        .catch(function (hata) {
            console.warn('Wizard: step request failed:', url, hata.message);
        });
}

function getWizardConfig() {
    if (!window.wizardConfig) {
        console.warn('Wizard: window.wizardConfig is not defined. Add the wizard config block to the layout before wizard-module.js.');
        return null;
    }

    return window.wizardConfig;
}

// Her sayfa açılışında çalışır. Önce "yarıda kalmış tur var mı" diye bakılıyor;
// varsa sunucuya hiç sorulmadan kaldığı yerden devam ediliyor.
function checkWizardOnLoad() {
    var resumeState = readResumeState();

    if (resumeState) {
        runDriverWithSteps(resumeState.steps, resumeState.stepIds);
        return;
    }

    var config = getWizardConfig();

    if (config === null) {
        return;
    }

    fetchSteps(config.getStepsUrl, function (result) {
        // "none": kullanıcının görmediği adım kalmamış, tur hiç başlatılmıyor.
        if (result.type === 'none') {
            return;
        }

        runDriverWithSteps(result.steps);
    });
}

// Kullanıcı turu kendisi başlattığında: görülmüş olsun olmasın bütün adımlar.
function startFullWizard() {
    var config = getWizardConfig();

    if (config === null) {
        return;
    }

    fetchSteps(config.getAllStepsUrl, function (result) {
        runDriverWithSteps(result.steps);
    });
}

// Hedef eleman sayfada hemen olmayabilir: tablo satırları AJAX ile geliyor,
// bazı kontroller de eklentiler tarafından sonradan üretiliyor. Bu yüzden eleman
// tek seferde aranmıyor, kısa aralıklarla belirli sayıda deneniyor. Bulunamazsa
// adım sessizce atlanıyor — eksik bir adım yüzünden turun tamamı durmasın.
function waitForElement(selector, callback, maxRetries, intervalMs) {
    var attempts = 0;

    var interval = setInterval(function () {
        attempts++;

        var el;

        // Seçici yönetim ekranından serbest metin olarak giriliyor; hatalı yazılmış
        // bir seçici querySelector'ı hata fırlatmaya iter. Yakalamazsak interval hiç
        // temizlenmez ve her tık hata üretmeye devam eder.
        try {
            el = document.querySelector(selector);
        } catch (hata) {
            clearInterval(interval);
            console.warn('Wizard: invalid selector, step skipped:', selector, hata.message);
            callback(null);
            return;
        }

        if (el) {
            clearInterval(interval);
            callback(el);
        } else if (attempts >= maxRetries) {
            clearInterval(interval);
            console.warn('Wizard: element not found after retries:', selector);
            callback(null);
        }
    }, intervalMs);
}

// Adımın sayfası açık olan sayfa mı? Karşılaştırma yalnızca yol üzerinden,
// büyük/küçük harf ve sondaki eğik çizgi yok sayılarak yapılıyor: "/Urunler",
// "/urunler/" ve "/Urunler?x=1" aynı sayfadır, boşuna yönlendirme olmasın.
// TargetUrl veritabanindan geliyor ve dogrudan window.location.href'e veriliyor.
// Sunucu tarafinda dogrulaniyor ama burada da kontrol ediyoruz: eski kayitlar ya
// da baska bir yoldan yazilmis bir deger, kullaniciyi dis siteye goturmesin veya
// "javascript:" ile kod calistirmasin.
function isSafeInternalUrl(url) {
    if (!url) {
        return false;
    }

    try {
        var target = new URL(url, window.location.origin);

        if (target.origin !== window.location.origin) {
            return false;
        }

        return target.protocol === 'http:' || target.protocol === 'https:';
    } catch (e) {
        return false;
    }
}

function isSameAsCurrentPage(url) {
    try {
        var target = new URL(url, window.location.origin);

        // Origin de karsilastirilmali: yalnizca pathname'e bakmak,
        // "https://baska-site.com/aynı/yol" adresini "ayni sayfa" sayardi.
        if (target.origin !== window.location.origin) {
            return false;
        }

        var currentPath = window.location.pathname.replace(/\/+$/, '').toLowerCase();
        var targetPath = target.pathname.replace(/\/+$/, '').toLowerCase();
        return currentPath === targetPath;
    } catch (e) {
        return false;
    }
}

// Turun sonunda kaydedilecek adım id'leri. Kapanışta tek istek gönderilir ve o
// turun bütün adımları görülmüş sayılır — adım adım işaretleme yapılmıyor.
//
// Liste, sayfa değişmeden önce toplanıyor: yönlendirmeden sonra elimizde yalnızca
// kalan adımlar olacağı için baştaki adımlar kayda geçmezdi.
//
// Önceden burada tek bir sayı (en yüksek id) tutuluyordu ve sunucu "id'si bundan
// küçük olan her adım görülmüştür" varsayıyordu. O varsayım, sonradan bir modüle
// yetki kazanan kullanıcının o modülü hiç görememesine yol açıyordu.
function collectStepIds(steps) {
    var ids = [];

    for (var i = 0; i < steps.length; i++) {
        // Sunucudan beklenmedik bir kayıt gelirse listeye girmesin.
        if (steps[i] && typeof steps[i].id === 'number') {
            ids.push(steps[i].id);
        }
    }

    return ids;
}

function saveResumeState(pendingSteps, allStepIds) {
    try {
        sessionStorage.setItem(WIZARD_RESUME_KEY, JSON.stringify({
            steps: pendingSteps,
            stepIds: allStepIds
        }));
    } catch (hata) {
        // sessionStorage kapalı ya da dolu olabilir (gizli sekme, kota). Tur
        // yönlendirmeden sonra baştan başlar ama sayfa bozulmaz.
        console.warn('Wizard: resume state could not be saved:', hata.message);
    }
}

function readResumeState() {
    var raw = null;

    try {
        raw = sessionStorage.getItem(WIZARD_RESUME_KEY);
    } catch (hata) {
        return null;
    }

    if (!raw) {
        return null;
    }

    // Okur okumaz siliniyor. Kalsaydı, tur yarıda kesildiğinde kullanıcı o sayfayı
    // her yenilediğinde aynı adımlar tekrar açılırdı.
    sessionStorage.removeItem(WIZARD_RESUME_KEY);

    try {
        var state = JSON.parse(raw);

        // Kayıt elle değiştirilmiş ya da eski sürümden kalmış olabilir.
        if (!state || !Array.isArray(state.steps) || !Array.isArray(state.stepIds)) {
            return null;
        }

        return state;
    } catch (e) {
        return null;
    }
}

function runDriverWithSteps(steps, overrideStepIds) {
    if (!Array.isArray(steps) || steps.length === 0) {
        return;
    }

    // Yönlendirmeden sonra devam ederken liste baştaki adımları da içersin diye
    // id'ler ilk turda toplanıp sessionStorage üzerinden taşınıyor.
    var allStepIds = Array.isArray(overrideStepIds) ? overrideStepIds : collectStepIds(steps);

    resolveSteps(steps, 0, [], function (resolvedSteps, pendingSteps) {
        if (resolvedSteps.length === 0) {
            if (pendingSteps && pendingSteps.length > 0) {
                redirectToPendingStep(pendingSteps, allStepIds);
                return;
            }
            console.warn('Wizard: no target element was found on this page, wizard is not starting.');
            return;
        }

        startDriver(resolvedSteps, pendingSteps, allStepIds);
    });
}

// Adımları sırayla gezip bu sayfada gösterilebilecek olanları ayırır. Döngü değil
// özyineleme kullanılıyor, çünkü eleman arama beklemeli bir iş; düz döngü sıradaki
// adıma bir öncekinin sonucunu beklemeden geçerdi.
//
// İki liste çıkıyor: burada gösterilecekler ve başka sayfaya ait olduğu için
// bekleyenler.
function resolveSteps(steps, index, resolvedSteps, callback) {
    if (index >= steps.length) {
        callback(resolvedSteps, null);
        return;
    }

    var step = steps[index];

    // Güvenli olmayan bir TargetUrl (dış site ya da "javascript:") varsa adım
    // tamamen atlanır; yönlendirme yapılmaz.
    if (step.targetUrl && !isSafeInternalUrl(step.targetUrl)) {
        console.warn('Wizard: unsafe TargetUrl, step skipped:', step.targetUrl);
        resolveSteps(steps, index + 1, resolvedSteps, callback);
        return;
    }

    // Adıma elle bir TargetUrl girilmişse öncelik oraya gitmektir. Aynı seçici
    // başka bir sayfada da eşleşebileceği için (örn. #btnEkle her liste sayfasında
    // var) elemanı aramadan önce doğru sayfada olup olmadığımıza bakıyoruz.
    if (step.targetUrl && !isSameAsCurrentPage(step.targetUrl)) {
        callback(resolvedSteps, steps.slice(index));
        return;
    }

    waitForElement(step.targetSelector, function (element) {
        if (element) {
            resolvedSteps.push(step);
        }

        resolveSteps(steps, index + 1, resolvedSteps, callback);
    }, 10, 100);
}

// Sonraki adım başka bir sayfadaysa kullanıcıya sorulmadan oraya geçilir;
// kalan adımlar sessionStorage'a yazılıp yeni sayfada kaldığı yerden devam eder.
// Onay penceresi bilerek yok: tur zaten kullanıcıyı gezdirmek için var, her
// sayfada "geçilsin mi?" diye sormak akışı bölerdi.
function redirectToPendingStep(pendingSteps, allStepIds) {
    var nextStep = pendingSteps[0];

    // Son savunma: buraya güvenli olmayan bir adres gelmemeli, gelirse yönlendirme
    // yapılmaz ve tur olduğu yerde tamamlanmış sayılır.
    if (!isSafeInternalUrl(nextStep.targetUrl)) {
        console.warn('Wizard: unsafe TargetUrl, redirect cancelled:', nextStep.targetUrl);
        markWizardCompleted(allStepIds);
        return;
    }

    saveResumeState(pendingSteps, allStepIds);
    window.location.href = nextStep.targetUrl;
}


// Bilgi kutusunu ekranın içine çeker.
//
// driver.js kutuyu hedefe göre konumlandırırken kutunun boyunu hesaba katıyor
// ama uzun bir kutu için konumu yeniden değerlendirmiyor: sayfanın altındaki bir
// hedefte 1000 karakterlik açıklama, kutuyu ekranın altından taşırıyordu. Metin
// kendi içinde kaydırılabilir (CSS), fakat kutunun alt kenarı görünmüyorsa
// butonlara da ulaşılamıyor.
//
// Burada yalnızca dikey kaydırma yapıyoruz; yatayda driver.js zaten sığdırıyor.
function clampPopoverToViewport(popover) {
    var KENAR_BOSLUGU = 8;

    var kutu = popover.getBoundingClientRect();
    var tasma = kutu.bottom - (window.innerHeight - KENAR_BOSLUGU);

    if (tasma <= 0) {
        return;
    }

    var mevcutUst = parseFloat(window.getComputedStyle(popover).top);

    if (isNaN(mevcutUst)) {
        return;
    }

    // Yukarı çekerken üst kenarı da ekranın dışına itmeyelim.
    popover.style.top = Math.max(KENAR_BOSLUGU, mevcutUst - tasma) + 'px';
}
function startPopoverEntrance() {
    var popover = document.querySelector('.driver-popover');
    var arrow = document.querySelector('.driver-popover-arrow');

    if (!popover) {
        return;
    }

    // Popover, okun bulunduğu noktadan büyüyerek açılsın. Ok'un gerçek konumu
    // ölçülüyor; sınıf adlarına göre tahmin yürütülmüyor.
    var popoverRect = popover.getBoundingClientRect();
    var originX = popoverRect.width / 2;
    var originY = popoverRect.height / 2;

    if (arrow && !arrow.classList.contains('driver-popover-arrow-none')) {
        var arrowRect = arrow.getBoundingClientRect();

        if (arrowRect.width > 0 || arrowRect.height > 0) {
            originX = arrowRect.left + arrowRect.width / 2 - popoverRect.left;
            originY = arrowRect.top + arrowRect.height / 2 - popoverRect.top;
        }
    }

    popover.style.transformOrigin = originX + 'px ' + originY + 'px';
    applyPopoverGap(popover, arrow);
    popover.classList.add('wizard-animate-in');

    // Uzun metinli kutu ekranın altından taşabiliyor; konumlandırma bittikten
    // sonra içeri çekiyoruz.
    clampPopoverToViewport(popover);

    return popover;
}

// Bilgi kutusunu hedeften biraz uzaklaştırır; aksi hâlde hedefin parlaması
// kutunun kenarına değiyor. Ok sınıfı popover'ın hedefin HANGİ TARAFINDA
// olduğunu söyler (örn. arrow-side-right => popover hedefin sağındadır),
// biz de o yönde dışarı doğru kaydırıyoruz.
//
// Sınıf adları ilk bakışta ters okunuyor: "side-right" okun sağa baktığını değil,
// kutunun sağda durduğunu anlatıyor. İsimden çıkarım yapmadan önce buraya bak.
function applyPopoverGap(popover, arrow) {
    var gapX = 0;
    var gapY = 0;

    if (arrow) {
        if (arrow.classList.contains('driver-popover-arrow-side-right')) {
            gapX = WIZARD_POPOVER_GAP;
        } else if (arrow.classList.contains('driver-popover-arrow-side-left')) {
            gapX = -WIZARD_POPOVER_GAP;
        } else if (arrow.classList.contains('driver-popover-arrow-side-bottom')) {
            gapY = WIZARD_POPOVER_GAP;
        } else if (arrow.classList.contains('driver-popover-arrow-side-top')) {
            gapY = -WIZARD_POPOVER_GAP;
        }
    }

    popover.style.setProperty('--wizard-gap-x', gapX + 'px');
    popover.style.setProperty('--wizard-gap-y', gapY + 'px');
}

// allowClose:false yapıldığında driver.js kendi kapatma butonunu hiç basmıyor
// (showButtons ile de geri getirilemiyor), bu yüzden kendi butonumuzu ekliyoruz.
function addWizardCloseButton(popover, onCloseRequested) {
    if (popover.querySelector('.wizard-close-btn')) {
        return;
    }

    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'wizard-close-btn';
    button.setAttribute('aria-label', 'Tanıtım turunu kapat');
    button.innerHTML = '&times;';
    button.addEventListener('click', onCloseRequested);

    popover.appendChild(button);
}

// "Bölümü Atla": bu sayfadaki kalan adımları geçip sonraki sayfaya geçer.
// X butonundan farkı: X turdan tamamen çıkar, bu ise tura devam eder.
// Buton yalnızca başka sayfada bekleyen adım varken eklenir; yoksa "atlanacak
// bölüm" de yok demektir.
function addWizardSkipButton(popover, onSkipRequested) {
    var navigation = popover.querySelector('.driver-popover-navigation-btns');

    if (!navigation || navigation.querySelector('.wizard-skip-btn')) {
        return;
    }

    var button = document.createElement('button');
    button.type = 'button';
    button.className = 'wizard-skip-btn';
    button.textContent = 'Bölümü Atla';
    button.addEventListener('click', onSkipRequested);

    // Geri/İleri'nin soluna: birincil eylem (İleri) en sağda kalsın.
    navigation.insertBefore(button, navigation.firstChild);
}

// Yalnızca o anki hedef vurgulanmalı. driver.js bazı geçişlerde önceki elemandan
// sınıfı kaldırmayabiliyor; bu da birden fazla elemanın aynı anda parlamasına yol açar.
function keepOnlyActiveHighlight(currentElement) {
    var highlighted = document.querySelectorAll('.driver-active-element');

    for (var i = 0; i < highlighted.length; i++) {
        if (highlighted[i] !== currentElement) {
            highlighted[i].classList.remove('driver-active-element');
        }
    }
}

// --- Süslemelerin hedefi takip etmesi ---
//
// Parlama katmanı ile bilgi kutusunun boşluğu, hedefin O ANKİ ölçülerine göre
// hesaplanıyor. Kullanıcı pencereyi büyütüp küçülttüğünde ya da sayfayı
// kaydırdığında bu ölçüler değişiyor: katman position:fixed olduğu için yerinde
// kalıp hedeften kopuyor, kutu da yön değiştirdiğinde boşluk ters tarafa itiyor.
//
// Çözüm sürekli çalışan bir döngü değil — o, tur boyunca boşuna CPU yakardı.
// Bunun yerine yalnızca gerçekten değişiklik olan anlarda haber alıyoruz:
//   resize / scroll  -> pencere ve kaydırma değişimleri
//   ResizeObserver   -> hedefin kendi boyutu değişirse (menü katlanması gibi,
//                       pencere hiç değişmeden de olabiliyor)
// Üç kaynak da tek bir requestAnimationFrame'e bağlanıyor: aynı karede kaç olay
// gelirse gelsin hesap bir kez yapılıyor ve tarayıcının çizim adımına denk geldiği
// için ara kare titremesi olmuyor.
var wizardTakip = null;

function ensureShimmerLayer() {
    var layer = document.getElementById('wizardShimmerLayer');

    if (!layer) {
        layer = document.createElement('div');
        layer.id = 'wizardShimmerLayer';
        layer.className = 'wizard-shimmer-layer';
        document.body.appendChild(layer);
    }

    return layer;
}

// Katmanı hedefin güncel konumuna oturtur. Yeniden yaratmak yerine var olanı
// güncellemek, animasyonun baştan başlamamasını sağlıyor.
function positionShimmer(element) {
    if (!element || !element.getBoundingClientRect) {
        return;
    }

    var rect = element.getBoundingClientRect();
    var layer = ensureShimmerLayer();

    // Hedef ekrandan çıkmış ya da gizlenmişse katman da görünmesin; aksi hâlde
    // sıfır boyutlu bir kutu ekranın köşesinde parlar.
    if (rect.width === 0 || rect.height === 0) {
        layer.style.display = 'none';
        return;
    }

    layer.style.display = '';
    layer.style.left = rect.left + 'px';
    layer.style.top = rect.top + 'px';
    layer.style.width = rect.width + 'px';
    layer.style.height = rect.height + 'px';
    layer.style.borderRadius = window.getComputedStyle(element).borderRadius;
}

function spawnShimmer(element, yerlesinceCagir) {
    stopFollowingTarget();

    if (!element || !element.getBoundingClientRect) {
        clearShimmer();
        return;
    }

    positionShimmer(element);
    startFollowingTarget(element, yerlesinceCagir);
}

// Hedefin konumu yalnızca olay anında değil, olaydan SONRA da değişebiliyor:
// AdminLTE kenar menüyü CSS geçişiyle kaydırıyor, yani resize olayı bittikten
// sonra menü ~0,3 saniye daha yol alıyor. Tek karelik bir hesap menüyü yolun
// ortasında yakalayıp parlamayı orada bırakıyordu.
//
// Bu yüzden tetiklenince tek kare değil, "yerleşene kadar" takip ediyoruz:
// her karede hedefin kutusuna bakıp değiştiyse güncelliyoruz, üst üste birkaç
// kare hiç değişmediyse duruyoruz. Hareket yokken hiçbir şey çalışmıyor,
// hareket varken de ne kadar sürerse sürsün yetişiyor.
function startFollowingTarget(element, yerlesinceCagir) {
    // Kaç kare üst üste sabit kalırsa "yerleşti" sayılacağı ve en fazla ne kadar
    // takip edileceği. 10 kare ~160 ms; üst sınır, biri sonsuz süren bir animasyon
    // koyarsa döngünün sonsuza kadar dönmemesi için.
    var SABIT_KARE_ESIGI = 10;
    var EN_FAZLA_KARE = 90;

    var calisiyor = false;
    var sonKutu = null;
    var baslangicKutusu = null;

    function kutuMetni(el) {
        var r = el.getBoundingClientRect();
        return r.left + ',' + r.top + ',' + r.width + ',' + r.height;
    }

    function dongu(sabitKare, toplamKare) {
        var simdiki = kutuMetni(element);

        if (simdiki === sonKutu) {
            sabitKare++;
        } else {
            sabitKare = 0;
            sonKutu = simdiki;
            positionShimmer(element);

            // Kutunun boşluğu da yeniden hesaplanmalı: driver.js pencere daraldığında
            // kutuyu hedefin diğer tarafına alabiliyor, o zaman ok sınıfı değişiyor ve
            // eski yöne verilen boşluk kutuyu hedefin üstüne bindiriyor.
            var popover = document.querySelector('.driver-popover');

            if (popover) {
                applyPopoverGap(popover, popover.querySelector('.driver-popover-arrow'));
                clampPopoverToViewport(popover);
            }
        }

        if (sabitKare < SABIT_KARE_ESIGI && toplamKare < EN_FAZLA_KARE) {
            window.requestAnimationFrame(function () { dongu(sabitKare, toplamKare + 1); });
            return;
        }

        calisiyor = false;

        // Hedef gerçekten yer değiştirdiyse bilgi kutusunu da bir kez yerine
        // oturtuyoruz. Her karede yapılsaydı kutu sürekli yeniden çizilip
        // titreyecekti; yerleştikten sonra tek sefer yapmak yeterli.
        if (yerlesinceCagir && baslangicKutusu !== sonKutu) {
            yerlesinceCagir();
        }
    }

    function planla() {
        if (calisiyor) {
            return;
        }

        calisiyor = true;
        baslangicKutusu = kutuMetni(element);
        window.requestAnimationFrame(function () { dongu(0, 0); });
    }

    // passive: dinleyici kaydırmayı geciktirmiyor. capture: iç içe kaydırılabilir
    // alanlarda da haber alabilmek için.
    window.addEventListener('resize', planla, { passive: true });
    window.addEventListener('scroll', planla, { passive: true, capture: true });

    // Menü açma/kapama gibi, pencere hiç değişmeden yapılan geçişler için.
    document.addEventListener('transitionend', planla, { passive: true, capture: true });

    var gozlemci = null;

    if (window.ResizeObserver) {
        gozlemci = new ResizeObserver(planla);
        gozlemci.observe(element);
    }

    wizardTakip = { planla: planla, gozlemci: gozlemci };
}

function stopFollowingTarget() {
    if (!wizardTakip) {
        return;
    }

    window.removeEventListener('resize', wizardTakip.planla);
    window.removeEventListener('scroll', wizardTakip.planla, { capture: true });
    document.removeEventListener('transitionend', wizardTakip.planla, { capture: true });

    if (wizardTakip.gozlemci) {
        wizardTakip.gozlemci.disconnect();
    }

    wizardTakip = null;
}
// sayfa ömrü boyunca boşuna iş yapmak olurdu.
function clearShimmer() {
    stopFollowingTarget();

    var existing = document.getElementById('wizardShimmerLayer');
    if (existing) {
        existing.remove();
    }
}

// driver.js adım başlığını ve açıklamasını innerHTML ile basıyor. Bu metinler
// yönetim ekranından serbestçe girildiği için, HTML olarak yorumlanmaları kod
// çalıştırılmasına yol açar. Metni driver.js'e vermeden ÖNCE kaçışlıyoruz;
// sonradan temizlemek işe yaramıyor, çünkü innerHTML atandığı anda çalışıyor.
//
// Kaçışlama elle değil tarayıcının kendi kodlayıcısıyla yapılıyor: metin bir
// elemana textContent olarak yazılıp innerHTML'i geri okunuyor. Yan faydası,
// "Stok < 10" gibi metinlerin artık doğru görünmesi.
function escapeHtml(deger) {
    var kutu = document.createElement('div');
    kutu.textContent = deger === null || deger === undefined ? '' : String(deger);
    return kutu.innerHTML;
}

function startDriver(resolvedSteps, pendingSteps, allStepIds) {
    if (!window.driver || !window.driver.js) {
        console.warn('Wizard: driver.js is not loaded.');
        return;
    }

    var driverFactory = window.driver.js.driver;
    var hasPending = !!(pendingSteps && pendingSteps.length > 0);
    var finished = false;

    // devamEt = true  : bu sayfanın adımları bitti, varsa sonraki sayfaya geçilsin
    //                   ("Devam Et" ve "Bölümü Atla" bu yolu kullanır).
    // devamEt = false : kullanıcı turdan tamamen çıkıyor ("X" butonu). Sonraki
    //                   sayfaya ATLANMAZ; ilerleme kaydedilip tur kapanır.
    //
    // Üç yolun da ortak yanı ilerlemenin kaydedilmesi: kullanıcı turu nasıl
    // bitirirse bitirsin aynı adımlar bir daha karşısına çıkmasın.
    function finish(devamEt) {
        // finished bayrağı çift çağrıya karşı. Kapatma butonu hem kendi olayını
        // tetikliyor hem driver.js'in onCloseClick'ini; korumasız kalsaydı kayıt
        // iki kez gönderilirdi.
        if (finished) {
            return;
        }
        finished = true;
        clearShimmer();

        if (devamEt && hasPending) {
            redirectToPendingStep(pendingSteps, allStepIds);
            return;
        }
        markWizardCompleted(allStepIds);
    }

    var driverSteps = resolvedSteps.map(function (step) {
        return {
            element: step.targetSelector,
            popover: {
                title: escapeHtml(step.title),
                description: escapeHtml(step.description)
            }
        };
    });

    // Bitirme mantığı son adımın "İleri" tıklamasına bağlanıyor. Beklenen yol
    // driver.js'in onDestroyed'ı olurdu ama o, tur normal şekilde tamamlandığında
    // tetiklenmiyor — tur bitiyor, hiçbir şey kaydedilmiyordu.
    driverSteps[driverSteps.length - 1].popover.onNextClick = function () {
        finish(true);
        driverObj.destroy();
    };

    var driverObj = driverFactory({
        steps: driverSteps,
        showProgress: true,
        // Boşluğa tıklayınca tur kapanmasın; kapatma yalnızca kendi butonumuzla olsun.
        allowClose: false,
        // Vurgulanan eleman tur sırasında tıklanamasın; aksi halde link/buton çalışıp
        // sayfa değiştiği için tur kaybolur.
        disableActiveInteraction: true,
        // Aydınlatılan alan tam olarak hedef elemanın kendi alanı kadar olsun.
        stagePadding: 0,
        stageRadius: 6,
        nextBtnText: 'İleri',
        prevBtnText: 'Geri',
        doneBtnText: hasPending ? 'Devam Et' : 'Bitir',
        onHighlightStarted: function () {
            clearShimmer();
        },
        onPopoverRender: function () {
            // Giriş animasyonu neden burada ve neden requestAnimationFrame ile:
            // animasyon transform: scale() kullanıyor, driver.js ise kutunun yerini
            // getBoundingClientRect ile ölçüyor. Animasyon konumlandırmadan önce
            // başlarsa küçülmüş kutu ölçülüyor ve popover yanlış yere oturuyor.
            // Bu yüzden animasyon, konumlandırma bittikten sonraki karede ekleniyor.
            window.requestAnimationFrame(function () {
                var popover = startPopoverEntrance();

                if (!popover) {
                    return;
                }

                addWizardCloseButton(popover, function () {
                    finish(false);
                    driverObj.destroy();
                });

                // Sonraki sayfada adım varsa "Bölümü Atla" butonu gösterilir.
                if (hasPending) {
                    addWizardSkipButton(popover, function () {
                        finish(true);
                        driverObj.destroy();
                    });
                }

                // Animasyon bitince konumu yeniden hesaplat: animasyon sürerken pencere
                // boyutu/kaydırma değişirse driver.js küçülmüş kutuyu ölçmüş olur.
                window.setTimeout(function () {
                    if (driverObj.isActive()) {
                        driverObj.refresh();
                    }
                }, WIZARD_ENTRANCE_MS + 30);
            });
        },
        onHighlighted: function (element) {
            keepOnlyActiveHighlight(element);

            // İkinci parametre, hedef hareketini bitirdiğinde bir kez çalışır.
            // Menü kayarken driver.js kutuyu yolun ortasındaki konuma göre
            // yerleştirmiş oluyor; hareket bitince yeniden hesaplatıyoruz.
            spawnShimmer(element, function () {
                if (driverObj.isActive()) {
                    driverObj.refresh();
                }
            });
        },
        onCloseClick: function () {
            finish(false);
            driverObj.destroy();
        }
    });

    driverObj.drive();
}

// Tur kapanışında tek istek: o turun bütün adımları görülmüş olarak kaydedilir.
//
// Kullanıcı kimliği gönderilmiyor, sunucu onu kendi tarafında buluyor — parametre
// olsaydı başkasının ilerlemesi dışarıdan değiştirilebilirdi.
//
// İstek POST olduğu için sahtecilik jetonu (antiforgery) taşıması gerekiyor.
// Jeton gövdeye, sunucunun her koşulda okuduğu alan adıyla konuyor; başlıkla
// göndermek, host'un jeton başlığı ayarını değiştirmemiş olmasına bel bağlamak
// olurdu. Jeton yoksa istek yine gönderilir ve 400 döner — sessizce yutmak
// yerine aşağıdaki uyarıyla görünür hâle geliyor.
function markWizardCompleted(stepIds) {
    var config = getWizardConfig();

    if (config === null || !Array.isArray(stepIds) || stepIds.length === 0) {
        return;
    }

    var govde = new URLSearchParams();

    for (var i = 0; i < stepIds.length; i++) {
        govde.append('stepIds', stepIds[i]);
    }

    if (config.antiforgeryToken) {
        govde.append(config.antiforgeryFieldName || '__RequestVerificationToken', config.antiforgeryToken);
    } else {
        console.warn('Wizard: antiforgery token is missing from wizardConfig; progress will not be saved.');
    }

    fetch(config.markSeenUrl, {
        method: 'POST',
        headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
        credentials: 'same-origin',
        body: govde.toString()
    })
        .then(function (response) {
            // Sessiz başarısızlık en kötüsü olurdu: ilerleme kaydedilmezse tur
            // her sayfa açılışında yeniden başlar ve kimse sebebini anlamaz.
            if (!response.ok) {
                console.warn('Wizard: progress could not be saved, HTTP ' + response.status +
                             (response.status === 400 ? ' (antiforgery token rejected)' : ''));
            }
        })
        .catch(function (hata) {
            console.warn('Wizard: progress request failed:', hata.message);
        });
}
