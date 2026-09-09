import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  Check,
  Copy,
  Package,
  Pencil,
  Plus,
  Printer,
  Trash2,
} from "lucide-react";
import { QRCodeSVG } from "qrcode.react";
import { all, core, imageUrl, mutate, request } from "../api";
import { useAuth } from "../auth";
import {
  addressUrl,
  date,
  managers,
  qrUrl,
  warehouse,
  type Row,
} from "../domain";
import {
  addressFields,
  componentField,
  componentLines,
  customerFields,
  dates,
  endpoint,
  locationField,
  modelField,
  orderLines,
  quantityField,
  rentalFields,
  resources,
} from "../resources";
import {
  Badge,
  ConfirmAction,
  Empty,
  Form,
  Heading,
  Modal,
  Panel,
  State,
  Table,
  TextPreview,
  exportSheet,
  field,
  selectEnum,
  useApi,
  useLoad,
  useNotice,
  type Field,
} from "../ui";
export function Back({
  to,
  label = "Listeye Dön",
}: {
  to: string;
  label?: string;
}) {
  return (
    <Link className="back-link" to={to}>
      <ArrowLeft size={15} />
      {label}
    </Link>
  );
}
export function ActionForm({
  label,
  fields,
  initial = {},
  path,
  method = "POST",
  onSaved,
}: {
  label: string;
  fields: Field[];
  initial?: Row;
  path: string;
  method?: string;
  onSaved: () => void;
}) {
  const [open, setOpen] = useState(false);
  const notice = useNotice();
  return (
    <>
      <button className="secondary small" onClick={() => setOpen(true)}>
        {label}
      </button>
      {open && (
        <Modal title={label} onClose={() => setOpen(false)}>
          <Form
            fields={fields}
            initial={initial}
            onCancel={() => setOpen(false)}
            onSubmit={async (value) => {
              await mutate(path, value, method);
              setOpen(false);
              onSaved();
              notice("İşlem tamamlandı.");
            }}
          />
        </Modal>
      )}
    </>
  );
}
export function StudentAddresses({
  students,
  admin = false,
}: {
  students: Row[];
  admin?: boolean;
}) {
  const notice = useNotice();
  const columns = [
    { key: "fullName", label: "Öğrenci" },
    { key: "guardianPhone", label: "Veli Telefonu" },
    { key: admin ? "productName" : "productModelName", label: "Eğitim Kiti" },
    {
      key: "addressLine",
      label: "Adres Durumu",
      render: (r: Row) => (
        <span className={`badge ${r.addressLine ? "green" : "amber"}`}>
          <i />
          {r.addressLine ? "Tamamlandı" : "Adres Bekleniyor"}
        </span>
      ),
    },
    {
      key: "address",
      label: "Açık Adres",
      render: (r: Row) => <TextPreview value={r.addressLine} />,
    },
    {
      key: "publicLink",
      label: "Adres Bağlantısı",
      render: (r: Row) => (
        <TextPreview
          value={addressUrl(r.publicAddressToken)}
          label="Bağlantıyı Göster"
        />
      ),
    },
  ];
  return (
    <Panel
      title="Öğrenci Adresleri"
      subtitle={`${students.filter((s) => !!s.addressLine).length} / ${students.length} öğrencinin adresi tamamlandı.`}
      actions={
        <button
          className="secondary small"
          onClick={() =>
            exportSheet(
              "ogrenci-adresleri",
              students.map((s) => ({
                ...s,
                publicLink: addressUrl(s.publicAddressToken),
              })),
              columns.map((c) =>
                c.key === "address" ? { ...c, key: "addressLine" } : c,
              ),
            ).catch((e) => notice(e.message, true))
          }
        >
          Excel İndir
        </button>
      }
    >
      <Table rows={students} columns={columns} />
    </Panel>
  );
}
export function OrderDetail() {
  const { id } = useParams();
  const state = useApi(core(`orders/${id}`));
  const auth = useAuth();
  const d = state.data;
  const notice = useNotice();
  return (
    <>
      <Back to="/orders" />
      <State {...state} retry={state.reload}>
        {d && (
          <>
            <Heading
              title={d.orderNumber}
              description={`${d.customerName} · ${d.type === 2 ? "Satın Alma" : "Kiralama"} Siparişi`}
              actions={
                <>
                  <Badge value={d.status} kind="order" />
                  {auth.can(managers) && d.status === 2 && (
                    <ConfirmAction
                      label="Siparişi Onayla"
                      description="Onay sonrasında müşteri öğrenci listesini değiştiremez."
                      onConfirm={async () => {
                        await mutate(core(`orders/${id}/status-transitions`), {
                          target: 3,
                        });
                        state.reload();
                      }}
                    />
                  )}
                  {auth.can(managers) && [3, 4, 5, 6].includes(d.status) && (
                    <>
                      {d.students.some((s: Row) => !s.hasAddress) ? (
                        <button
                          onClick={() =>
                            notice(
                              "Eksik adres bilgisi olan kayıtlar var, önce adresleri doldurun.",
                              true,
                            )
                          }
                        >
                          Siparişi Tamamla
                        </button>
                      ) : (
                        <ConfirmAction
                          label="Siparişi Tamamla"
                          description="Atanan kitler teslim edilmiş olarak işaretlenecek ve öğrenci adresleri konum geçmişine yazılacak."
                          onConfirm={async () => {
                            await mutate(
                              core(`orders/${id}/status-transitions`),
                              { target: 13 },
                            );
                            state.reload();
                          }}
                        />
                      )}
                    </>
                  )}
                  {d.kits.length > 0 && (
                    <Link
                      className="button secondary"
                      to={`/labels/order/${id}`}
                    >
                      <Printer size={16} />
                      Etiket Yazdır
                    </Link>
                  )}
                </>
              }
            />
            <div className="order-progress">
              {[
                "Sipariş Alındı",
                "Onaylandı",
                "Kitler Atandı",
                "Tamamlandı",
              ].map((text, i) => (
                <div
                  key={text}
                  className={
                    i === 0 ||
                    (i === 1 && d.status >= 3 && d.status < 14) ||
                    (i === 2 && d.kits.length > 0) ||
                    (i === 3 && [7, 8, 13].includes(d.status))
                      ? "complete"
                      : ""
                  }
                >
                  <span>{i + 1}</span>
                  {text}
                </div>
              ))}
            </div>
            <div className="detail-facts">
              <div>
                <small>Müşteri</small>
                <strong>{d.customerName}</strong>
              </div>
              <div>
                <small>Başlangıç</small>
                <strong>{date(d.startDate)}</strong>
              </div>
              <div>
                <small>Bitiş</small>
                <strong>{date(d.endDate)}</strong>
              </div>
              <div>
                <small>Atanan Kit</small>
                <strong>{d.kits.length}</strong>
              </div>
            </div>
            <Panel
              title="Sipariş Kalemleri"
              actions={
                auth.can(managers) && [3, 4].includes(d.status) ? (
                  <ActionForm
                    label="Kitleri Hazırla"
                    fields={[
                      orderLines,
                      field("useAvailableKits", "Önce Hazır Kitleri Kullan", {
                        type: "boolean",
                        required: false,
                        default: true,
                      }),
                      field("rentalCohortId", "Sipariş Dönemi", {
                        source: `customers/${d.customerId}/rental-periods`,
                        required: false,
                        nullable: true,
                      }),
                    ]}
                    initial={{
                      lines: d.lines
                        .map((l: Row) => ({
                          productModelId: l.productModelId,
                          quantity: Math.max(0, l.quantity - l.createdKitCount),
                        }))
                        .filter((l: Row) => l.quantity > 0),
                      rentalCohortId: d.rentalCohortId,
                    }}
                    path={core(`orders/${id}/kits`)}
                    onSaved={state.reload}
                  />
                ) : undefined
              }
            >
              <Table
                rows={d.lines}
                search={false}
                columns={[
                  { key: "productName", label: "Eğitim Kiti" },
                  { key: "productSku", label: "Stok Kodu" },
                  { key: "quantity", label: "Sipariş Adedi" },
                  { key: "createdKitCount", label: "Hazırlanan Kit" },
                ]}
              />
            </Panel>
            {d.students.length > 0 && (
              <StudentAddresses students={d.students} admin />
            )}
            <Panel title="Siparişe Bağlı Kitler">
              <Table
                rows={d.kits}
                columns={[
                  { key: "serialNumber", label: "Seri Numarası" },
                  { key: "productName", label: "Eğitim Kiti" },
                  { key: "qrCode", label: "QR Kodu" },
                  { key: "status", label: "Durum", kind: "unit" },
                ]}
                actions={(r) => (
                  <Link className="row-link" to={`/kits/${r.id}`}>
                    Kit Geçmişi
                    <ArrowRight size={13} />
                  </Link>
                )}
              />
            </Panel>
          </>
        )}
      </State>
    </>
  );
}
export function KitDetail({ lookup = false }: { lookup?: boolean }) {
  const { id } = useParams();
  const [identifier, setIdentifier] = useState("");
  const [query, setQuery] = useState("");
  const auth = useAuth();
  const state = useLoad(
    (signal) =>
      lookup && !query
        ? Promise.resolve(null)
        : request(
            core(
              lookup
                ? `physical-kits/lookup?identifier=${encodeURIComponent(query)}`
                : `physical-kits/${id}`,
            ),
            { signal },
          ),
    [id, query, lookup],
  );
  const d = state.data;
  return (
    <>
      <Back to="/kits" />
      {lookup && (
        <>
          <Heading
            title="Kit Geçmişi"
            description="Seri numarası veya QR koduyla kitin tüm yolculuğuna ulaşın."
          />
          <form
            className="lookup-form"
            onSubmit={(e) => {
              e.preventDefault();
              setQuery(identifier);
            }}
          >
            <input
              aria-label="Seri Numarası veya QR Kodu"
              placeholder="Seri numarası veya QR kodu girin…"
              required
              value={identifier}
              onChange={(e) => setIdentifier(e.target.value)}
            />
            <button>
              Kit Bul
              <ArrowRight size={17} />
            </button>
          </form>
        </>
      )}
      <State {...state} retry={state.reload}>
        {d && (
          <>
            <Heading
              title={d.kit.serialNumber}
              description={d.kit.kitName}
              actions={
                <>
                  <Badge value={d.kit.status} />
                  <Link
                    className="button secondary"
                    to={`/labels/kit/${d.kit.id}`}
                  >
                    <Printer size={16} />
                    QR Etiketi
                  </Link>
                  {auth.can(warehouse) && (
                    <ActionForm
                      label="Düzenle"
                      fields={[
                        field("serialNumber", "Seri Numarası"),
                        field("qrCode", "QR Kodu"),
                      ]}
                      initial={d.kit}
                      path={core(`product-units/${d.kit.id}`)}
                      method="PUT"
                      onSaved={state.reload}
                    />
                  )}
                  {auth.can(managers) && d.kit.status === 1 && (
                    <ActionForm
                      label="Kirala"
                      fields={rentalFields}
                      path={core(`physical-kits/${d.kit.id}/rentals`)}
                      onSaved={state.reload}
                    />
                  )}
                </>
              }
            />
            <div className="kit-detail-top">
              <Panel className="kit-identity">
                <div className="kit-mini-art">
                  {imageUrl(d.kit.imageUrl) ? (
                    <img src={imageUrl(d.kit.imageUrl)} alt={d.kit.kitName} />
                  ) : (
                    <Package size={58} />
                  )}
                </div>
                <div>
                  <h2>{d.kit.kitName}</h2>
                  <p>{d.kit.kitSku}</p>
                  <TextPreview value={d.kit.qrCode} />
                </div>
                <QRCodeSVG value={qrUrl(d.kit.qrCode)} size={90} />
              </Panel>
              <Panel title="Güncel Konum">
                {d.currentLocation ? (
                  <div className="detail-summary">
                    <strong>{d.currentLocation.recipientName}</strong>
                    <p>{d.currentLocation.phone}</p>
                    <p>{d.currentLocation.addressLine}</p>
                    <small>{date(d.currentLocation.deliveredAt)}</small>
                  </div>
                ) : (
                  <Empty title="Konum henüz kaydedilmedi" />
                )}
              </Panel>
            </div>
            <Panel title="İşlem Zaman Çizelgesi">
              <div className="timeline">
                {d.activityHistory.length ? (
                  d.activityHistory.map((r: Row, i: number) => (
                    <div key={i}>
                      <span className="timeline-dot">
                        <Check size={13} />
                      </span>
                      <div>
                        <strong>{r.description}</strong>
                        <p>
                          {r.actorDisplayName} · {date(r.occurredAt)}
                        </p>
                      </div>
                    </div>
                  ))
                ) : (
                  <Empty title="İşlem geçmişi bulunmuyor" />
                )}
              </div>
            </Panel>
            <Panel title="Teslimat ve Kiralama Geçmişi">
              <Table
                rows={d.deliveryHistory}
                columns={[
                  { key: "orderNumber", label: "Sipariş" },
                  { key: "customerName", label: "Müşteri" },
                  { key: "recipientName", label: "Teslim Alan" },
                  { key: "phone", label: "Telefon" },
                  {
                    key: "addressLine",
                    label: "Adres",
                    render: (r) => <TextPreview value={r.addressLine} />,
                  },
                  { key: "startDate", label: "Başlangıç", format: "date" },
                  { key: "endDate", label: "Bitiş", format: "date" },
                ]}
              />
            </Panel>
            <Panel title="Arıza Geçmişi">
              <Table
                rows={d.faultHistory}
                columns={[
                  { key: "number", label: "Arıza" },
                  { key: "status", label: "Durum", kind: "fault" },
                  {
                    key: "description",
                    label: "Açıklama",
                    render: (r) => <TextPreview value={r.description} />,
                  },
                  { key: "openedAt", label: "Tarih", format: "date" },
                ]}
              />
            </Panel>
            <Panel title="İade Geçmişi">
              <Table
                rows={d.returnHistory}
                columns={[
                  { key: "returnNumber", label: "İade" },
                  { key: "status", label: "Durum", kind: "return" },
                  { key: "requesterName", label: "Talep Eden" },
                  { key: "createdAt", label: "Talep Tarihi", format: "date" },
                  { key: "receivedAt", label: "Teslim Alınma", format: "date" },
                ]}
              />
            </Panel>
            <Panel title="Durum Geçmişi">
              <Table
                rows={d.statusHistory}
                columns={[
                  { key: "newStatus", label: "Yeni Durum", kind: "unit" },
                  { key: "reason", label: "Açıklama" },
                  { key: "occurredAt", label: "Tarih", format: "date" },
                ]}
              />
            </Panel>
          </>
        )}
      </State>
    </>
  );
}
export function ModelDetail({
  manufacturing = false,
}: {
  manufacturing?: boolean;
}) {
  const { id } = useParams();
  const auth = useAuth();
  const state = useLoad(
    async (signal) => {
      const [model, bom] = await Promise.all([
        request(
          core(
            manufacturing
              ? `manufacturing/buildable-kits/${id}`
              : `product-models/${id}`,
          ),
          { signal },
        ),
        request(core(`product-models/${id}/bom`), { signal }),
      ]);
      return { model, bom };
    },
    [id, manufacturing],
  );
  const d = state.data;
  return (
    <>
      <Back to={manufacturing ? "/manufacturing" : "/models"} />
      <State {...state} retry={state.reload}>
        {d && (
          <>
            <Heading
              title={d.model.name ?? d.model.productName}
              description={
                d.model.description ??
                "Üretim reçetesi ve komponent gereksinimleri."
              }
              actions={
                auth.can(warehouse) ? (
                  <ActionForm
                    label="Reçeteyi Düzenle"
                    fields={[
                      field("version", "Reçete Sürümü", {
                        type: "number",
                        min: 1,
                        default: (d.bom?.version ?? 0) + 1,
                      }),
                      componentLines,
                    ]}
                    initial={{ lines: d.bom?.lines ?? [] }}
                    path={core(`product-models/${id}/bom`)}
                    onSaved={state.reload}
                  />
                ) : undefined
              }
            />
            {manufacturing && (
              <div className="hero-number">
                <strong>{d.model.buildableQuantity}</strong>
                <span>kit mevcut stokla üretilebilir</span>
              </div>
            )}
            <Panel
              title={
                manufacturing
                  ? "Komponent Yeterliliği"
                  : `Kit Reçetesi · Sürüm ${d.bom?.version ?? "—"}`
              }
            >
              <Table
                rows={manufacturing ? d.model.components : (d.bom?.lines ?? [])}
                columns={
                  manufacturing
                    ? [
                        { key: "componentName", label: "Komponent" },
                        { key: "requiredPerKit", label: "Kit Başına" },
                        { key: "availableStock", label: "Mevcut Stok" },
                        { key: "supportsKitCount", label: "Desteklenen Kit" },
                        {
                          key: "missingForNextKit",
                          label: "Sonraki Kit İçin Eksik",
                        },
                        { key: "isBottleneck", label: "Darboğaz" },
                      ]
                    : [
                        { key: "componentName", label: "Komponent" },
                        { key: "componentSku", label: "Stok Kodu" },
                        { key: "quantity", label: "Miktar" },
                        { key: "unitOfMeasure", label: "Birim" },
                      ]
                }
                actions={(r) => (
                  <Link
                    className="row-link"
                    to={`/components/${r.componentId}`}
                  >
                    Konumu Gör
                  </Link>
                )}
              />
            </Panel>
            <Link className="button secondary" to={`/labels/model/${id}`}>
              <Printer size={16} />
              Bu Modelin Etiketleri
            </Link>
          </>
        )}
      </State>
    </>
  );
}
export function ComponentDetail() {
  const { id } = useParams();
  const state = useLoad(
    async (signal) => {
      const [locator, movements] = await Promise.all([
        request(core(`components/${id}/locator`), { signal }),
        all(core(`component-stock/movements?componentId=${id}`), signal),
      ]);
      return { locator, movements };
    },
    [id],
  );
  const d = state.data;
  return (
    <>
      <Back to="/components" />
      <State {...state} retry={state.reload}>
        {d && (
          <>
            <Heading
              title={d.locator.name}
              description={`${d.locator.sku} · ${d.locator.totalStock} ${d.locator.unitOfMeasure}`}
              actions={
                <ActionForm
                  label="Stok Düzelt"
                  fields={[
                    field("change", "Stok Değişimi", {
                      type: "number",
                      hint: "Giriş için pozitif, düşüm için negatif değer girin.",
                    }),
                  ]}
                  path={core(`components/${id}/stock-adjustments`)}
                  onSaved={state.reload}
                />
              }
            />
            <div className="actions stock-actions">
              {[
                ["receipts", "Stok Girişi"],
                ["consumptions", "Stok Tüketimi"],
                ["transfers", "Raflar Arası Transfer"],
              ].map(([action, label]) => (
                <ActionForm
                  key={action}
                  label={label}
                  fields={[
                    { ...componentField, default: id },
                    ...(action === "transfers"
                      ? [
                          {
                            ...locationField,
                            name: "fromStorageLocationId",
                            label: "Kaynak Raf",
                          },
                          {
                            ...locationField,
                            name: "toStorageLocationId",
                            label: "Hedef Raf",
                          },
                        ]
                      : [locationField]),
                    quantityField,
                    field("reference", "Referans / Açıklama"),
                  ]}
                  path={core(`component-stock/${action}`)}
                  onSaved={state.reload}
                />
              ))}
            </div>
            <Panel title="Raflardaki Dağılım">
              <Table
                rows={d.locator.locations}
                columns={[
                  { key: "locationCode", label: "Raf Kodu" },
                  { key: "warehouse", label: "Depo" },
                  { key: "aisle", label: "Koridor" },
                  { key: "rack", label: "Bölüm" },
                  { key: "shelf", label: "Raf" },
                  { key: "quantity", label: "Miktar" },
                ]}
              />
            </Panel>
            <Panel title="Stok Hareketleri">
              <Table
                rows={d.movements}
                columns={[
                  { key: "occurredAt", label: "Tarih", format: "date" },
                  { key: "quantity", label: "Miktar" },
                  { key: "reference", label: "Referans" },
                  { key: "storageLocationId", label: "Raf Kimliği" },
                ]}
              />
            </Panel>
          </>
        )}
      </State>
    </>
  );
}
export function SupplyDetail() {
  const { id } = useParams();
  const state = useApi(core(`supply-needs/${id}`));
  const d = state.data;
  const navigate = useNavigate();
  return (
    <>
      <Back to="/supply" />
      <State {...state} retry={state.reload}>
        {d && (
          <>
            <Heading
              title="Tedarik Listesi"
              description={date(d.createdAt)}
              actions={
                <>
                  <Badge value={d.status} kind="supply" />
                  {d.status === 0 && (
                    <ConfirmAction
                      label="Öneriyi Onayla"
                      onConfirm={async () => {
                        await mutate(core(`supply-needs/${id}/approvals`));
                        state.reload();
                      }}
                    />
                  )}
                  {d.status !== 2 && (
                    <>
                      <ActionForm
                        label="Listeyi Düzenle"
                        fields={[componentLines]}
                        initial={d}
                        path={core(`supply-needs/${id}`)}
                        method="PUT"
                        onSaved={state.reload}
                      />
                      <ActionForm
                        label="Tedariki Tamamla"
                        fields={[locationField, componentLines]}
                        initial={d}
                        path={core(`supply-needs/${id}/completions`)}
                        onSaved={state.reload}
                      />
                      <ConfirmAction
                        label="Sil"
                        danger
                        onConfirm={async () => {
                          await mutate(
                            core(`supply-needs/${id}`),
                            null,
                            "DELETE",
                          );
                          navigate("/supply");
                        }}
                      />
                    </>
                  )}
                </>
              }
            />
            <Panel>
              <Table
                rows={d.lines}
                columns={[
                  { key: "componentName", label: "Komponent" },
                  { key: "componentSku", label: "Stok Kodu" },
                  { key: "quantity", label: "İstenen Miktar" },
                  { key: "suppliedQuantity", label: "Tedarik Edilen" },
                  { key: "unitOfMeasure", label: "Birim" },
                ]}
              />
            </Panel>
          </>
        )}
      </State>
    </>
  );
}
export function CustomerDetail() {
  const { id } = useParams();
  const state = useApi(core(`customers/${id}`));
  const d = state.data;
  const auth = useAuth();
  return (
    <>
      <Back to="/customers" />
      <State {...state} retry={state.reload}>
        {d && (
          <>
            <Heading
              title={d.name}
              description={d.email}
              actions={
                <>
                  {auth.can(["SystemAdmin"]) && (
                    <ActionForm
                      label="Müşteri Hesabı Oluştur"
                      fields={[
                        field("displayName", "Ad Soyad"),
                        field("email", "Kullanıcı Adı"),
                        field("password", "Şifre", { type: "password" }),
                        {
                          ...selectEnum("role", "Rol", "role"),
                          options: [
                            { value: 5, label: "Müşteri Yöneticisi" },
                            { value: 6, label: "Müşteri Kullanıcısı" },
                          ],
                          default: 5,
                        },
                        field("customerId", "Müşteri", {
                          source: "customers",
                          default: id,
                        }),
                      ]}
                      path="/identity/api/users"
                      onSaved={state.reload}
                    />
                  )}
                  <ActionForm
                    label="Müşteriyi Düzenle"
                    fields={resources.customers.fields!}
                    initial={d}
                    path={core(`customers/${id}`)}
                    method="PUT"
                    onSaved={state.reload}
                  />
                </>
              }
            />
            <Panel
              title="Adresler"
              actions={
                <ActionForm
                  label="Yeni Adres"
                  fields={addressFields}
                  path={core(`customers/${id}/addresses`)}
                  onSaved={state.reload}
                />
              }
            >
              <Table
                rows={d.addresses}
                columns={[
                  { key: "title", label: "Adres Başlığı" },
                  { key: "contactName", label: "İlgili Kişi" },
                  { key: "phone", label: "Telefon" },
                  {
                    key: "line1",
                    label: "Açık Adres",
                    render: (r) => <TextPreview value={r.line1} />,
                  },
                ]}
                actions={(r) => (
                  <>
                    <ActionForm
                      label="Düzenle"
                      fields={addressFields}
                      initial={r}
                      path={core(`customers/${id}/addresses/${r.id}`)}
                      method="PUT"
                      onSaved={state.reload}
                    />
                    <ConfirmAction
                      label="Sil"
                      danger
                      onConfirm={async () => {
                        await mutate(
                          core(`customers/${id}/addresses/${r.id}`),
                          null,
                          "DELETE",
                        );
                        state.reload();
                      }}
                    />
                  </>
                )}
              />
            </Panel>
          </>
        )}
      </State>
    </>
  );
}
export function PurchaseOrder() {
  const [customer, setCustomer] = useState("");
  const navigate = useNavigate();
  const customers = useLoad((signal) => all(core("customers"), signal), []);
  const selected = customers.data?.find((c) => c.id === customer);
  return (
    <>
      <Back to="/orders" />
      <Heading
        title="Satın Alma Siparişi"
        description="Müşteri, teslimat adresi ve satın alınacak kitleri seçin."
      />
      <Panel className="form-panel">
        <State {...customers} retry={customers.reload}>
          <label className="standalone-field">
            Müşteri
            <select
              value={customer}
              onChange={(e) => setCustomer(e.target.value)}
            >
              <option value="">Müşteri Seçin</option>
              {customers.data?.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </label>
          {selected && (
            <Form
              key={customer}
              fields={[
                field("addressId", "Teslimat Adresi", {
                  options: selected.addresses.map((a: Row) => ({
                    value: a.id,
                    label: `${a.title} · ${a.line1}`,
                  })),
                }),
                orderLines,
              ]}
              onSubmit={async (value) => {
                const result = await mutate(core("purchase-orders"), {
                  ...value,
                  customerId: customer,
                });
                navigate(`/orders/${result.id}`);
              }}
              submitLabel="Siparişi Oluştur"
            />
          )}
        </State>
      </Panel>
    </>
  );
}
export function BulkRental() {
  const state = useLoad((signal) => all(core("physical-kits"), signal), []);
  const navigate = useNavigate();
  return (
    <>
      <Back to="/kits" />
      <Heading
        title="Toplu Kit Kiralama"
        description="Hazır durumdaki kitleri seçip aynı müşteri için kiralayın."
      />
      <State {...state} retry={state.reload}>
        <Panel className="form-panel">
          {state.data && (
            <Form
              fields={[
                field("productUnitIds", "Kiralanacak Kitler", {
                  type: "multi",
                  options: state.data
                    .filter((k) => k.status === 1)
                    .map((k) => ({
                      value: k.id,
                      label: `${k.serialNumber} · ${k.kitName}`,
                    })),
                }),
                ...rentalFields,
              ]}
              submitLabel="Kitleri Kirala"
              onSubmit={async (value) => {
                if (!value.productUnitIds.length)
                  throw new Error("En az bir kit seçin.");
                const result = await mutate(
                  core("physical-kit-rental-batches"),
                  value,
                );
                navigate(`/orders/${result.orderId}`);
              }}
            />
          )}
        </Panel>
      </State>
    </>
  );
}
export function Returns() {
  const state = useApi(core("dashboard"));
  return (
    <>
      <Heading
        title="Gelen Teslimatlar"
        description="İade taleplerini kontrol edin ve gelen kitleri teslim alın."
      />
      <State {...state} retry={state.reload}>
        <Panel>
          <Table
            rows={state.data?.returnsInProgress ?? []}
            columns={[
              { key: "customerName", label: "Müşteri" },
              { key: "requesterName", label: "Talep Eden" },
              { key: "requesterPhone", label: "Telefon" },
              { key: "kitCount", label: "Kit Adedi" },
              { key: "status", label: "Durum", kind: "return" },
              {
                key: "returnAddress",
                label: "Adres",
                render: (r) => <TextPreview value={r.returnAddress} />,
              },
              { key: "trackingNumber", label: "Takip No" },
              { key: "createdAt", label: "Tarih", format: "date" },
            ]}
            actions={(r) => (
              <ConfirmAction
                label="Teslim Al"
                description="İade edilen kitlerin teslim alındığını onaylıyorsunuz."
                onConfirm={async () => {
                  await mutate(core(`kit-returns/${r.id}/receipts`));
                  state.reload();
                }}
              />
            )}
          />
        </Panel>
      </State>
    </>
  );
}
export function Labels() {
  const { kind, id } = useParams();
  const state = useLoad(
    async (signal) => {
      if (kind === "order") {
        const order = await request(core(`orders/${id}`), { signal });
        let students: Row[] = [];
        if (order.rentalCohortId)
          students =
            (
              await all(
                core(`customers/${order.customerId}/rental-periods`),
                signal,
              )
            ).find((c) => c.id === order.rentalCohortId)?.students ?? [];
        return order.kits.map((kit: Row) => ({
          ...kit,
          heading: order.customerName,
          student: students.find((s) => s.productUnitId === kit.id),
        }));
      }
      if (kind === "kit") {
        const detail = await request(core(`physical-kits/${id}`), { signal });
        return [detail.kit];
      }
      return all(core(`physical-kits/models/${id}/labels`), signal);
    },
    [kind, id],
  );
  return (
    <>
      <Heading
        title="QR Etiketleri"
        description="A4 kâğıt üzerinde üç sütun halinde yazdırılır."
        actions={
          <button onClick={() => window.print()}>
            <Printer size={17} />
            Yazdır
          </button>
        }
      />
      <State {...state} retry={state.reload}>
        <div className="labels-grid">
          {state.data?.map((r: Row) => (
            <div className="qr-label" key={r.id}>
              <h3>{r.heading || "Robotik Bilim"}</h3>
              {r.student && (
                <div className="label-student">
                  <strong>{r.student.fullName}</strong>
                  <span>{r.student.guardianPhone}</span>
                </div>
              )}
              <QRCodeSVG value={qrUrl(r.qrCode)} size={130} marginSize={2} />
              <strong>{r.serialNumber}</strong>
              <span>{r.kitName ?? r.productName}</span>
              <small>Arıza bildirimi veya iade için okutun</small>
            </div>
          ))}
        </div>
      </State>
    </>
  );
}
