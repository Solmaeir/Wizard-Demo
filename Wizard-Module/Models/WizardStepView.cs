namespace Wizard.Module.Models;

// Bir kullanıcının bir adımı gördüğü kaydı. Her kullanıcı-adım ikilisi için
// en fazla bir satır olur.
//
// Önceki sürümde kullanıcı başına tek bir sayı tutuluyordu (LastSeenStepId) ve
// "id'si bundan küçük olan her adım görülmüştür" varsayılıyordu. O varsayım,
// sonradan bir modüle yetki kazanan kullanıcının o modülün adımlarını bir daha
// hiç görememesine yol açıyordu: adımların id'si zaten o sayıdan küçüktü.
// Artık soru "id'si büyük mü" değil, "bu adımı gördü mü".
public class WizardStepView
{
    public int Id { get; set; }

    // Kullanıcı kimliği metin olarak tutulur. Host sistem int, GUID ya da
    // kullanıcı adı kullanıyor olabilir; modül hiçbirini varsaymaz.
    public string UserId { get; set; } = null!;

    // Veritabanında WizardSteps'e yabancı anahtarla bağlı ve ON DELETE CASCADE
    // tanımlı: yönetici, daha önce görülmüş bir adımı da silebilsin diye.
    public int StepId { get; set; }

    public DateTime SeenDate { get; set; }
}
