// Boundary records mirror the existing Core API JSON contracts; enum values are numeric on the wire.
export type Row = Record<string, any>;
export interface Page<T = Row> {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  items: T[];
}
export interface Session {
  accessToken: string;
  expiresAt: string;
  user: {
    id: string;
    email: string;
    displayName: string;
    role: number | string;
    customerId?: string;
  };
}
export const roles = [
  "",
  "SystemAdmin",
  "OperationsManager",
  "WarehouseStaff",
  "ServiceTechnician",
  "CustomerAccountManager",
  "CustomerUser",
  "Auditor",
];
export const roleName = (role: number | string) =>
  typeof role === "number" ? roles[role] : role;
export const managers = ["SystemAdmin", "OperationsManager"];
export const warehouse = [...managers, "WarehouseStaff"];
export const staff = [...warehouse, "ServiceTechnician", "Auditor"];
export const customers = ["CustomerAccountManager", "CustomerUser"];
export const enums: Record<string, Record<number, string>> = {
  unit: {
    1: "Kullanıma Hazır",
    2: "Rezerve",
    3: "Hazırlanıyor",
    4: "Sevkiyatta",
    5: "Müşteride",
    6: "İade Yolunda",
    7: "İncelemede",
    8: "Bakımda",
    9: "Karantinada",
    10: "Kayıp",
    11: "Kullanım Dışı",
    12: "Satıldı",
  },
  order: {
    1: "Taslak",
    2: "Onay Bekliyor",
    3: "Onaylandı",
    4: "Hazırlanıyor",
    5: "Sevke Hazır",
    6: "Sevkiyatta",
    7: "Teslim Edildi",
    8: "Kiralama Aktif",
    9: "İade Bekliyor",
    10: "İade Yolunda",
    11: "İncelemede",
    12: "Hasar İncelemesi",
    13: "Tamamlandı",
    14: "Reddedildi",
    15: "İptal Edildi",
    16: "Teslimat Sorunu",
    17: "Gecikmiş",
  },
  fault: {
    1: "Açık",
    2: "Araştırılıyor",
    3: "Müşteri Bekleniyor",
    4: "İade Bekleniyor",
    5: "Serviste",
    6: "Değişim Yolunda",
    7: "Çözüldü",
    8: "Kapandı",
  },
  severity: { 1: "Düşük", 2: "Orta", 3: "Yüksek", 4: "Kritik" },
  return: { 1: "Talep Alındı", 2: "Yolda", 3: "Teslim Alındı" },
  supply: { 0: "Öneri", 1: "Tedarik Bekliyor", 2: "Tedarik Edildi" },
  origin: { 1: "İç Kayıt", 2: "QR Formu", 3: "Müşteri Portalı" },
  role: {
    1: "Sistem Yöneticisi",
    2: "Operasyon Yöneticisi",
    3: "Depo Görevlisi",
    4: "Servis Teknisyeni",
    5: "Müşteri Yöneticisi",
    6: "Müşteri Kullanıcısı",
    7: "Denetçi",
  },
  reason: { 1: "Eğitim Tamamlandı", 2: "Kayıt Silindi" },
  delivery: { 1: "Adresimden Alınsın", 2: "Kendim Bırakacağım" },
};
export const date = (v: unknown) =>
  v
    ? new Intl.DateTimeFormat("tr-TR", {
        day: "2-digit",
        month: "short",
        year: "numeric",
      }).format(new Date(String(v)))
    : "—";
export const number = (v: unknown) =>
  new Intl.NumberFormat("tr-TR").format(Number(v ?? 0));
export const addressUrl = (token: string) =>
  token ? `${location.origin}/adres/${encodeURIComponent(token)}` : "";
export const qrUrl = (qr: string) =>
  `${location.origin}/ariza/${encodeURIComponent(qr)}`;
export const items = (value: any): Row[] =>
  Array.isArray(value) ? value : (value?.items ?? []);
