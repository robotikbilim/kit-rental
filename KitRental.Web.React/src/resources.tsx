import { Link } from "react-router-dom";
import { core } from "./api";
import { managers, warehouse, staff, type Row } from "./domain";
import {
  field as f,
  selectEnum,
  TextPreview,
  type Column,
  type Field,
} from "./ui";
export const modelField = f("productModelId", "Eğitim Kiti", {
  source: "product-models",
});
export const componentField = f("componentId", "Komponent", {
  source: "components",
});
export const locationField = f("storageLocationId", "Depo / Raf", {
  source: "storage-locations",
});
export const dates = [
  f("startDate", "Başlangıç Tarihi", { type: "date" }),
  f("endDate", "Bitiş Tarihi", { type: "date" }),
];
export const addressFields = [
  f("title", "Adres Başlığı"),
  f("contactName", "İlgili Kişi"),
  f("phone", "Telefon", { type: "tel" }),
  f("line1", "Açık Adres", { type: "textarea" }),
  f("postalCode", "Posta Kodu", { required: false }),
];
export const studentFields = [
  f("fullName", "Öğrenci Adı Soyadı"),
  f("guardianPhone", "Veli Telefonu", { type: "tel" }),
];
export const quantityField = f("quantity", "Miktar", {
  type: "number",
  min: 1,
  default: 1,
});
export const componentLines = f("lines", "Komponent Listesi", {
  type: "array",
  children: [componentField, quantityField],
});
export const orderLines = f("lines", "Eğitim Kitleri", {
  type: "array",
  children: [modelField, quantityField],
});
export const rentalFields = [
  f("customerName", "Müşteri Adı"),
  f("email", "E-posta", { type: "email" }),
  f("phone", "Telefon", { type: "tel" }),
  f("addressLine", "Açık Adres", { type: "textarea" }),
  f("postalCode", "Posta Kodu", { required: false }),
  ...dates,
];
export const returnFields = [
  f("requesterName", "Ad Soyad"),
  f("requesterPhone", "Telefon", { type: "tel" }),
  f("returnAddress", "İade Adresi", { type: "textarea" }),
  selectEnum("returnReason", "İade Nedeni", "reason"),
];
export const faultFields = [
  f("reporterName", "Ad Soyad"),
  f("reporterPhone", "Telefon", { type: "tel" }),
  f("reporterAddress", "Açık Adres", { type: "textarea" }),
  f("description", "Sorunun Açıklaması", { type: "textarea" }),
];
export const modelFields = [
  f("name", "Kit Adı"),
  f("sku", "Stok Kodu"),
  f("description", "Açıklama", { type: "textarea", required: false }),
  f("imageUrl", "Görsel Adresi", { required: false }),
];
export const componentFields = [
  f("name", "Komponent Adı"),
  f("sku", "Stok Kodu"),
  f("unitOfMeasure", "Birim", { default: "Adet" }),
  f("minimumStock", "Minimum Stok", { type: "number", min: 0, default: 0 }),
  f("imageUrl", "Görsel Adresi", { required: false }),
  f("defaultStorageLocationId", "Varsayılan Raf", {
    source: "storage-locations",
    required: false,
    nullable: true,
  }),
];
export const customerFields = [
  f("name", "Kurum / Müşteri Adı"),
  f("email", "E-posta", { type: "email" }),
  f("allowedProductModelIds", "Kullanıma Açılan Kitler", {
    type: "multi",
    source: "product-models",
    required: false,
    hint: "Seçim yapılmazsa tüm eğitim kitleri kullanılabilir.",
  }),
];
export const userFields = [
  f("displayName", "Ad Soyad"),
  f("email", "E-posta / Kullanıcı Adı"),
  f("password", "Başlangıç Şifresi", { type: "password" }),
  selectEnum("role", "Rol", "role"),
  f("customerId", "Müşteri", {
    source: "customers",
    required: false,
    nullable: true,
  }),
];
export const locationFields = [
  f("code", "Raf Kodu"),
  f("warehouse", "Depo"),
  f("aisle", "Koridor"),
  f("rack", "Bölüm"),
  f("shelf", "Raf"),
  f("isDefaultForNewComponents", "Yeni Komponentler İçin Varsayılan", {
    type: "boolean",
    required: false,
  }),
];
export const guideFields = [
  modelField,
  f("title", "Başlık"),
  f("problem", "Problem", { type: "textarea" }),
  f("solution", "Çözüm", { type: "textarea" }),
  f("displayOrder", "Sıralama", { type: "number", default: 0 }),
  f("isActive", "Yayında", { type: "boolean", default: true, required: false }),
];
const c = (
  key: string,
  label: string,
  extra: Partial<Column> = {},
): Column => ({ key, label, ...extra });
export const kitColumns: Column[] = [
  c("serialNumber", "Seri Numarası"),
  c("kitName", "Eğitim Kiti"),
  c("status", "Durum", { kind: "unit" }),
];
export const studentColumns: Column[] = [
  c("fullName", "Öğrenci"),
  c("guardianPhone", "Veli Telefonu"),
  c("productModelName", "Eğitim Kiti"),
  c("addressLine", "Adres Durumu", {
    render: (r) => (
      <span className={`badge ${r.addressLine ? "green" : "amber"}`}>
        <i />
        {r.addressLine ? "Tamamlandı" : "Adres Bekleniyor"}
      </span>
    ),
  }),
  c("address", "Açık Adres", {
    render: (r) => <TextPreview value={r.addressLine} />,
  }),
  c("serialNumber", "Atanan Kit", {
    render: (r) =>
      r.productUnitId ? (
        <Link to={`/portal/kits/${r.productUnitId}`}>{r.serialNumber}</Link>
      ) : (
        "—"
      ),
  }),
];
export interface Resource {
  title: string;
  description: string;
  path: string;
  roles: string[];
  columns: Column[];
  fields?: Field[];
  createFields?: Field[];
  createPath?: string;
  editPath?: string;
  canDelete?: boolean;
  writeRoles?: string[];
  detail?: string;
  server?: boolean;
  cards?: boolean;
  filters?: Field[];
}
export const resources: Record<string, Resource> = {
  inventory: {
    title: "Envanter",
    description:
      "Her kitin durumunu ve kiralama süresini tek yerden takip edin.",
    path: "inventory",
    roles: staff,
    server: true,
    columns: [
      c("serialNumber", "Seri Numarası", {
        render: (r) => <Link to={`/kits/${r.id}`}>{r.serialNumber}</Link>,
      }),
      c("productModelName", "Eğitim Kiti"),
      c("status", "Durum", { kind: "unit" }),
      c("customerName", "Müşteri"),
      c("orderNumber", "Sipariş"),
      c("rentalEndDate", "Kiralama Bitişi", { format: "date" }),
      c("daysRemaining", "Kalan Gün", {
        render: (r) =>
          r.daysRemaining == null ? (
            "—"
          ) : (
            <span className={r.daysRemaining < 0 ? "text-danger" : ""}>
              {r.daysRemaining < 0
                ? `${Math.abs(r.daysRemaining)} gün gecikti`
                : `${r.daysRemaining} gün`}
            </span>
          ),
      }),
    ],
    filters: [
      { ...modelField, required: false },
      { ...selectEnum("status", "Kit Durumu", "unit"), required: false },
      f("rentalExpiry", "Kiralama Süresi", {
        required: false,
        options: [
          { value: "expired", label: "Süresi Dolan" },
          { value: "upcoming", label: "Yaklaşan" },
        ],
      }),
      f("createdFrom", "Oluşturulma Başlangıcı", {
        type: "date",
        required: false,
      }),
      f("createdTo", "Oluşturulma Bitişi", { type: "date", required: false }),
    ],
  },
  kits: {
    title: "Fiziksel Kitler",
    description:
      "Kit filonuzun tamamı. Seri numaraları, QR etiketleri ve yaşam döngüsü.",
    path: "physical-kits",
    roles: warehouse,
    columns: kitColumns,
    detail: "/kits/",
    createPath: "product-unit-batches",
    createFields: [
      modelField,
      f("quantity", "Oluşturulacak Kit Sayısı", {
        type: "number",
        min: 1,
        step: 1,
        default: 1,
      }),
    ],
    writeRoles: warehouse,
  },
  models: {
    title: "Eğitim Kitleri",
    description: "Öğrenmeye ilham veren kitler ve üretim reçeteleri.",
    path: "product-models",
    roles: warehouse,
    columns: [
      c("name", "Eğitim Kiti"),
      c("sku", "Stok Kodu"),
      c("description", "Açıklama"),
    ],
    fields: modelFields,
    createFields: [
      ...modelFields,
      f("bomVersion", "Reçete Sürümü", { type: "number", default: 1, min: 1 }),
      componentLines,
    ],
    createPath: "kit-models",
    canDelete: true,
    detail: "/models/",
    cards: true,
    writeRoles: managers,
  },
  components: {
    title: "Komponentler",
    description:
      "Stok seviyelerini izleyin, kritik eksikleri zamanında tamamlayın.",
    path: "components",
    roles: warehouse,
    columns: [
      c("name", "Komponent"),
      c("sku", "Stok Kodu"),
      c("totalStock", "Stok", { format: "number" }),
      c("unitOfMeasure", "Birim"),
      c("minimumStock", "Minimum", { format: "number" }),
      c("isLowStock", "Stok Durumu", {
        render: (r) => (
          <span className={`badge ${r.isLowStock ? "amber" : "green"}`}>
            <i />
            {r.isLowStock ? "Kritik Stok" : "Yeterli"}
          </span>
        ),
      }),
    ],
    fields: componentFields,
    createFields: [
      ...componentFields,
      f("initialStock", "Başlangıç Stoğu", {
        type: "number",
        min: 0,
        default: 0,
      }),
    ],
    canDelete: true,
    detail: "/components/",
    writeRoles: warehouse,
    filters: [
      f("lowStockOnly", "Stok Filtresi", {
        required: false,
        options: [{ value: "true", label: "Yalnızca Kritik Stok" }],
      }),
    ],
  },
  locations: {
    title: "Raf Düzeni",
    description: "Depodan rafa, her parçanın yeri belli.",
    path: "storage-locations",
    roles: warehouse,
    columns: [
      c("code", "Raf Kodu"),
      c("warehouse", "Depo"),
      c("aisle", "Koridor"),
      c("rack", "Bölüm"),
      c("shelf", "Raf"),
      c("isDefaultForNewComponents", "Varsayılan"),
    ],
    fields: locationFields,
    canDelete: true,
    writeRoles: warehouse,
  },
  manufacturing: {
    title: "Üretilebilirlik",
    description: "Mevcut komponent stoğuyla neler üretebileceğinizi görün.",
    path: "manufacturing/buildable-kits",
    roles: warehouse,
    columns: [
      c("productName", "Eğitim Kiti"),
      c("productSku", "Stok Kodu"),
      c("bomVersion", "Reçete Sürümü"),
      c("buildableQuantity", "Üretilebilir Adet", { format: "number" }),
    ],
    detail: "/manufacturing/",
    cards: true,
  },
  orders: {
    title: "Siparişler",
    description:
      "İlk talepten kit teslimine, tüm sipariş akışı elinizin altında.",
    path: "order-summaries",
    roles: staff,
    columns: [
      c("orderNumber", "Sipariş No"),
      c("customerName", "Müşteri"),
      c("status", "Durum", { kind: "order" }),
      c("createdAt", "Oluşturulma", { format: "date" }),
      c("startDate", "Başlangıç", { format: "date" }),
      c("endDate", "Bitiş", { format: "date" }),
      c("assignedKitCount", "Atanan Kit", { format: "number" }),
    ],
    detail: "/orders/",
    createPath: "orders",
    createFields: [
      f("customerId", "Müşteri", { source: "customers" }),
      modelField,
      ...dates,
      f("students", "Öğrenciler", {
        type: "array",
        children: studentFields,
        hint: "Adresler sipariş oluşturulduktan sonra kişiye özel bağlantılarla toplanır.",
      }),
    ],
    writeRoles: managers,
  },
  supply: {
    title: "İhtiyaç Listeleri",
    description:
      "Stok önerilerini değerlendirin ve tedarik sürecini tamamlayın.",
    path: "supply-needs",
    roles: warehouse,
    columns: [
      c("createdAt", "Oluşturulma", { format: "date" }),
      c("status", "Durum", { kind: "supply" }),
      c("lines", "Komponent Sayısı", { render: (r) => r.lines.length }),
      c("updatedAt", "Son Güncelleme", { format: "date" }),
    ],
    createFields: [componentLines],
    detail: "/supply/",
    writeRoles: warehouse,
  },
  faults: {
    title: "Arıza Merkezi",
    description:
      "Bildirilen sorunları takip edin, kitleri yeniden öğrenmeye kazandırın.",
    path: "faults",
    roles: staff,
    server: true,
    columns: [
      c("number", "Arıza No"),
      c("customerName", "Müşteri"),
      c("reporterName", "Bildiren"),
      c("description", "Açıklama", {
        render: (r) => <TextPreview value={r.description} />,
      }),
      c("status", "Durum", { kind: "fault" }),
      c("origin", "Kaynak", { kind: "origin" }),
      c("openedAt", "Bildirim Tarihi", { format: "date" }),
    ],
    filters: [
      { ...selectEnum("status", "Arıza Durumu", "fault"), required: false },
      {
        ...selectEnum("severity", "Önem Derecesi", "severity"),
        required: false,
      },
      f("openedFrom", "Başlangıç", { type: "date", required: false }),
      f("openedTo", "Bitiş", { type: "date", required: false }),
    ],
  },
  guides: {
    title: "Problem Rehberi",
    description:
      "Sık karşılaşılan sorunlar için anlaşılır, adım adım çözümler.",
    path: "fault-guides",
    roles: managers,
    columns: [
      c("title", "Başlık"),
      c("productModelName", "Eğitim Kiti"),
      c("problem", "Problem", {
        render: (r) => <TextPreview value={r.problem} />,
      }),
      c("solution", "Çözüm", {
        render: (r) => <TextPreview value={r.solution} />,
      }),
      c("isActive", "Yayında"),
      c("displayOrder", "Sıralama"),
    ],
    fields: guideFields,
    canDelete: true,
    writeRoles: managers,
  },
  customers: {
    title: "Müşteriler",
    description:
      "Kurumlar, iletişim bilgileri ve müşteriye özel kit erişimleri.",
    path: "customers",
    roles: managers,
    columns: [
      c("name", "Kurum / Müşteri"),
      c("email", "E-posta"),
      c("isActive", "Durum", {
        render: (r) => (
          <span className={`badge ${r.isActive ? "green" : "amber"}`}>
            <i />
            {r.isActive ? "Aktif" : "Pasif"}
          </span>
        ),
      }),
      c("addresses", "Adresler", {
        render: (r) => `${r.addresses?.length ?? 0} adres`,
      }),
    ],
    fields: [
      ...customerFields,
      f("isActive", "Müşteri Aktif", {
        type: "boolean",
        default: true,
        required: false,
      }),
    ],
    createFields: [
      ...customerFields,
      f("address", "İletişim Adresi", {
        type: "object",
        children: addressFields,
      }),
    ],
    detail: "/customers/",
    canDelete: true,
    writeRoles: managers,
  },
  users: {
    title: "Kullanıcı Yönetimi",
    description: "Ekibinizin rollerini ve müşteri hesabı erişimlerini yönetin.",
    path: "/identity/api/users",
    roles: ["SystemAdmin"],
    columns: [
      c("displayName", "Ad Soyad"),
      c("email", "E-posta"),
      c("role", "Rol", { kind: "role" }),
      c("isActive", "Aktif"),
    ],
    createFields: userFields,
    writeRoles: ["SystemAdmin"],
  },
  emails: {
    title: "E-posta Geçmişi",
    description: "Gönderilen operasyon bildirimlerinin teslimat durumları.",
    path: "email-deliveries",
    roles: managers,
    columns: [
      c("recipient", "Alıcı"),
      c("subject", "Konu"),
      c("status", "Durum", {
        render: (r) => (
          <span className={`badge ${r.status === 1 ? "green" : "red"}`}>
            {r.status === 1 ? "Gönderildi" : "Başarısız"}
          </span>
        ),
      }),
      c("occurredAt", "Tarih", { format: "date" }),
      c("error", "Hata", { render: (r) => <TextPreview value={r.error} /> }),
    ],
  },
  audit: {
    title: "İşlem Geçmişi",
    description:
      "Sistemdeki değişiklikleri ve sorumlu kullanıcıları takip edin.",
    path: "audit-entries",
    roles: ["SystemAdmin", "Auditor"],
    server: true,
    columns: [
      c("action", "İşlem"),
      c("entityType", "Kayıt Türü"),
      c("entityId", "Kayıt"),
      c("actorId", "Kullanıcı"),
      c("occurredAt", "Tarih", { format: "date" }),
      c("previousValue", "Önceki Değer", {
        render: (r) => <TextPreview value={r.previousValue} />,
      }),
      c("newValue", "Yeni Değer", {
        render: (r) => <TextPreview value={r.newValue} />,
      }),
    ],
    filters: [
      f("action", "İşlem Türü", { required: false }),
      f("actorId", "Kullanıcı Kimliği", { required: false }),
      f("occurredFrom", "Başlangıç", { type: "date", required: false }),
      f("occurredTo", "Bitiş", { type: "date", required: false }),
    ],
  },
};
export const endpoint = (path: string) =>
  path.startsWith("/") ? path : core(path);
