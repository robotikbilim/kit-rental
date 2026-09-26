namespace KitRental.Web.Mvc.Services;

public static class OperationsDisplay
{
    public static string FaultStatus(int status) => status switch
    {
        1 => "Kayıt geldi", 2 => "İnceleniyor", 3 => "Müşteri bilgisi bekleniyor",
        4 => "Kit bekleniyor", 5 => "Onarım aşamasında", 6 => "Eski kargo süreci",
        7 => "Çözüldü", 8 => "Tamamlandı", 9 => "Kabul edildi", 10 => "Reddedildi",
        11 => "Uzaktan destekle çözüldü", 12 => "Kargo bekliyor", 13 => "Atölyeye kargolandı",
        14 => "Atölyeye geldi", 15 => "Arıza giderildi", 16 => "Müşteriye kargolandı", _ => "İşlemde"
    };
    public static string ReturnState(string state) => state switch
    {
        "completed" => "İade Tamamlandı", "in-transit" => "Kargoda",
        "missing-form" => "İade Formu Eksik", _ => "İade Bekleniyor"
    };
    public static readonly IReadOnlyDictionary<int, string> OrderStatuses = new Dictionary<int, string>
    {
        [1] = "Taslak", [2] = "Onay bekliyor", [3] = "Onaylandı", [4] = "Hazırlanıyor",
        [5] = "Kargoya hazır", [6] = "Kargoya verildi", [7] = "Teslim edildi",
        [8] = "Aktif kiralama", [9] = "İade bekleniyor", [10] = "İade yolunda",
        [11] = "Kontrolde", [12] = "Hasar incelemesi", [13] = "Sipariş tamamlandı",
        [14] = "Reddedildi", [15] = "İptal", [16] = "Teslimat sorunu", [17] = "Gecikmiş"
    };
    public static readonly IReadOnlyDictionary<string, string> FocusOptions = new Dictionary<string, string>
    {
        ["approval"] = "Onay bekleyen", ["address"] = "Adres eksiği olan",
        ["preparation"] = "Kit hazırlığı eksik", ["shipment"] = "Gönderim bekleyen",
        ["ready"] = "Kargo kabulü bekleyen", ["in-transit"] = "Kargoda olan",
        ["delivered"] = "Teslim edilen gönderisi olan", ["shipment-failed"] = "Kargo hatası olan",
        ["active"] = "Aktif kiralaması olan", ["faults"] = "Açık arızası olan",
        ["returns"] = "İadesi devam eden", ["overdue"] = "Süresi geçmiş",
        ["ending-soon"] = "7 gün içinde bitecek"
    };
    public static readonly IReadOnlyDictionary<string, string> FaultStages = new Dictionary<string, string>
    {
        ["open"] = "Tüm açık arızalar", ["review"] = "İnceleme / bilgi bekleyen",
        ["repair"] = "Onarım sürecinde", ["shipment"] = "Kargo bekleyen", ["completed"] = "Çözümlenen"
    };
    public static string OrderStatus(int status) => OrderStatuses.GetValueOrDefault(status, "Bilinmeyen durum");
}
