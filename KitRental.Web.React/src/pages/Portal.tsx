import { useState } from "react";
import { Link, useParams, useSearchParams } from "react-router-dom";
import {
  ArrowRight,
  Download,
  FileSpreadsheet,
  LockKeyhole,
  Plus,
  Search,
  Upload,
} from "lucide-react";
import { core, mutate } from "../api";
import { addressUrl, date, type Row } from "../domain";
import { dates, faultFields, returnFields, studentFields } from "../resources";
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
  useApi,
  useNotice,
} from "../ui";
import { ActionForm, Back } from "./Details";

export function PortalList({
  kind,
}: {
  kind: "orders" | "kits" | "faults" | "returns";
}) {
  const state = useApi(core("customer-portal"));
  const d = state.data;
  const [params, setParams] = useSearchParams();
  const [create, setCreate] = useState(false);
  const notice = useNotice();
  const [fault, setFault] = useState<Row>();
  const titles = {
    orders: "Siparişlerim",
    kits: "Kitlerim",
    faults: "Arıza Kayıtları",
    returns: "İadelerim",
  };
  const rows: Row[] = d
    ? (kind === "orders" ? d.rentalCohorts : d[kind])
        .filter((r: Row) => {
          if (kind === "kits") {
            const assignment = params.get("assignmentState");
            if (
              assignment === "assigned" &&
              (!r.assignedStudentName || r.isReturned)
            )
              return false;
            if (
              assignment === "unassigned" &&
              (r.assignedStudentName || r.isReturned)
            )
              return false;
            if (params.get("returned") === "true" && !r.isReturned)
              return false;
            if (
              params.get("expiry") === "expired" &&
              (r.isReturned ||
                r.endDate >= new Date().toISOString().slice(0, 10))
            )
              return false;
          }
          if (kind === "faults" && params.get("state"))
            return params.get("state") === "closed"
              ? r.status >= 7
              : r.status < 7;
          if (kind === "orders") {
            if (params.get("approval") === "approved" && !r.isApproved)
              return false;
            if (params.get("approval") === "pending" && r.isApproved)
              return false;
            if (params.get("period") && r.name !== params.get("period"))
              return false;
          }
          return true;
        })
        .sort((a: Row, b: Row) =>
          String(b.createdAt ?? b.openedAt ?? "").localeCompare(
            String(a.createdAt ?? a.openedAt ?? ""),
          ),
        )
    : [];
  const columns =
    kind === "orders"
      ? [
          { key: "name", label: "Sipariş Dönemi" },
          { key: "orderNumber", label: "Sipariş No" },
          {
            key: "isApproved",
            label: "Onay Durumu",
            render: (r: Row) => (
              <span className={`badge ${r.isApproved ? "green" : "amber"}`}>
                <i />
                {r.isApproved ? "Onaylandı" : "Onay Bekliyor"}
              </span>
            ),
          },
          { key: "studentCount", label: "Öğrenci" },
          { key: "startDate", label: "Başlangıç", format: "date" as const },
          { key: "endDate", label: "Bitiş", format: "date" as const },
          { key: "createdAt", label: "Oluşturulma", format: "date" as const },
        ]
      : kind === "kits"
        ? [
            { key: "serialNumber", label: "Seri Numarası" },
            { key: "kitName", label: "Eğitim Kiti" },
            { key: "assignedStudentName", label: "Öğrenci" },
            { key: "assignedStudentGuardianPhone", label: "Veli Telefonu" },
            { key: "assignedStudentPeriodName", label: "Dönem" },
            { key: "unitStatus", label: "Durum", kind: "unit" },
            {
              key: "endDate",
              label: "Kiralama Bitişi",
              format: "date" as const,
            },
          ]
        : kind === "faults"
          ? [
              { key: "number", label: "Arıza No" },
              { key: "kitName", label: "Eğitim Kiti" },
              { key: "serialNumber", label: "Seri No" },
              { key: "status", label: "Durum", kind: "fault" },
              {
                key: "description",
                label: "Açıklama",
                render: (r: Row) => <TextPreview value={r.description} />,
              },
              { key: "openedAt", label: "Tarih", format: "date" as const },
            ]
          : [
              {
                key: "createdAt",
                label: "Talep Tarihi",
                format: "date" as const,
              },
              { key: "status", label: "Durum", kind: "return" },
              { key: "requesterName", label: "Talep Eden" },
              {
                key: "items",
                label: "Kit Sayısı",
                render: (r: Row) => r.items.length,
              },
              { key: "carrier", label: "Kargo" },
              { key: "trackingNumber", label: "Takip No" },
            ];
  return (
    <>
      <Heading
        eyebrow="MÜŞTERİ PORTALI"
        title={titles[kind]}
        description="Kurumunuza ait kayıtları görüntüleyin ve işlemlerinizi kolayca tamamlayın."
        actions={
          kind === "orders" ? (
            <button onClick={() => setCreate(true)}>
              <Plus size={17} />
              Yeni Sipariş Oluştur
            </button>
          ) : undefined
        }
      />
      {kind === "orders" && d && (
        <div className="filter-bar">
          <label>
            <span>Sipariş Dönemi</span>
            <select
              value={params.get("period") || ""}
              onChange={(e) => {
                const next = new URLSearchParams(params);
                e.target.value
                  ? next.set("period", e.target.value)
                  : next.delete("period");
                setParams(next);
              }}
            >
              <option value="">Tüm Dönemler</option>
              {[
                ...new Set<string>(d?.rentalCohorts.map((r: Row) => r.name)),
              ].map((name) => (
                <option key={name}>{name}</option>
              ))}
            </select>
          </label>
          <label>
            <span>Onay Durumu</span>
            <select
              value={params.get("approval") || ""}
              onChange={(e) => {
                const next = new URLSearchParams(params);
                e.target.value
                  ? next.set("approval", e.target.value)
                  : next.delete("approval");
                setParams(next);
              }}
            >
              <option value="">Tümü</option>
              <option value="pending">Onay Bekliyor</option>
              <option value="approved">Onaylandı</option>
            </select>
          </label>
        </div>
      )}
      {kind === "kits" && (
        <div className="tabs">
          {[
            ["", "Tüm Kitler"],
            ["assigned", "Öğrenciye Atanan"],
            ["unassigned", "Atanmayan"],
          ].map(([value, label]) => (
            <button
              className={
                (params.get("assignmentState") || "") === value ? "active" : ""
              }
              key={label}
              onClick={() => setParams(value ? { assignmentState: value } : {})}
            >
              {label}
            </button>
          ))}
        </div>
      )}
      {kind === "faults" && (
        <div className="tabs">
          {[
            ["", "Tümü"],
            ["open", "Açık"],
            ["closed", "Çözülen"],
          ].map(([value, label]) => (
            <button
              className={(params.get("state") || "") === value ? "active" : ""}
              key={label}
              onClick={() => setParams(value ? { state: value } : {})}
            >
              {label}
            </button>
          ))}
        </div>
      )}
      <State {...state} retry={state.reload}>
        <Panel>
          <Table
            rows={rows}
            columns={columns}
            exportName={`musteri-${kind}`}
            actions={(r) =>
              kind === "orders" ? (
                <>
                  <Link className="row-link" to={`/portal/orders/${r.id}`}>
                    {[7, 8, 13].includes(r.orderStatus)
                      ? "Tamamlandı"
                      : "Öğrenciler"}
                    <ArrowRight size={14} />
                  </Link>
                  {!r.isApproved && (
                    <>
                      <ActionForm
                        label="Düzenle"
                        fields={[field("name", "Dönem Adı"), ...dates]}
                        initial={r}
                        path={core(`customer-portal/rental-periods/${r.id}`)}
                        method="PUT"
                        onSaved={state.reload}
                      />
                      <ConfirmAction
                        label="Sil"
                        danger
                        onConfirm={async () => {
                          await mutate(
                            core(`customer-portal/rental-periods/${r.id}`),
                            null,
                            "DELETE",
                          );
                          state.reload();
                        }}
                      />
                    </>
                  )}
                </>
              ) : kind === "kits" ? (
                <Link
                  className="row-link"
                  to={`/portal/kits/${r.productUnitId}`}
                >
                  Detay
                  <ArrowRight size={14} />
                </Link>
              ) : kind === "faults" ? (
                <button className="secondary small" onClick={() => setFault(r)}>
                  Detay
                </button>
              ) : r.status === 1 ? (
                <ActionForm
                  label="Kargo Bilgisi Ekle"
                  fields={[
                    field("carrier", "Kargo Firması"),
                    field("trackingNumber", "Takip Numarası"),
                  ]}
                  path={core(`customer-portal/returns/${r.id}/shipments`)}
                  onSaved={state.reload}
                />
              ) : null
            }
          />
        </Panel>
      </State>
      {create && (
        <Modal
          title="Yeni Sipariş Dönemi"
          description="Döneminizi oluşturun, ardından öğrencilerinizi ekleyin."
          onClose={() => setCreate(false)}
        >
          {d?.rentalCohorts.length > 0 && (
            <p className="hint">
              Önceki dönemler:{" "}
              {[
                ...new Set<string>(d?.rentalCohorts.map((r: Row) => r.name)),
              ].join(", ")}
            </p>
          )}
          <Form
            fields={[field("name", "Dönem Adı"), ...dates]}
            onSubmit={async (value) => {
              await mutate(core("customer-portal/rental-periods"), value);
              setCreate(false);
              state.reload();
              notice("Sipariş dönemi oluşturuldu.");
            }}
          />
        </Modal>
      )}
      {fault && (
        <Modal
          title={fault.number}
          description={fault.description}
          onClose={() => setFault(undefined)}
        >
          <Badge value={fault.status} kind="fault" />
          <Table
            rows={fault.history}
            columns={[
              { key: "current", label: "Durum", kind: "fault" },
              { key: "note", label: "Not" },
              { key: "occurredAt", label: "Tarih", format: "date" },
            ]}
          />
          {fault.shipments.length > 0 && (
            <Table
              rows={fault.shipments}
              columns={[
                { key: "carrier", label: "Kargo" },
                { key: "trackingNumber", label: "Takip No" },
              ]}
            />
          )}
        </Modal>
      )}
    </>
  );
}
export function PortalOrder() {
  const { id } = useParams();
  const state = useApi(core("customer-portal"));
  const d = state.data;
  const cohort = d?.rentalCohorts.find((r: Row) => r.id === id);
  const [student, setStudent] = useState<Row | null>();
  const [importing, setImporting] = useState(false);
  const [fault, setFault] = useState<Row>();
  const [returning, setReturning] = useState<Row>();
  const [addressFilter, setAddressFilter] = useState("");
  const [modelFilter, setModelFilter] = useState("");
  const [assignment, setAssignment] = useState("");
  const notice = useNotice();
  const students: Row[] = (cohort?.students ?? [])
    .filter((r: Row) => !r.isDeleted)
    .filter(
      (r: Row) =>
        (!addressFilter ||
          (addressFilter === "filled" ? !!r.addressLine : !r.addressLine)) &&
        (!modelFilter || r.productModelId === modelFilter) &&
        (!assignment ||
          (assignment === "assigned" ? !!r.assignmentId : !r.assignmentId)),
    );
  const models: Row[] = d?.productModels ?? [];
  return (
    <>
      <Back to="/portal/orders" />
      <State {...state} retry={state.reload}>
        {d && !cohort ? (
          <Empty title="Sipariş bulunamadı" />
        ) : (
          cohort && (
            <>
              <Heading
                eyebrow="SİPARİŞ / ÖĞRENCİLER"
                title={cohort.name}
                description={`${cohort.orderNumber || "Yeni Sipariş"} · ${date(cohort.startDate)} — ${date(cohort.endDate)}`}
                actions={
                  <>
                    {!cohort.isApproved && (
                      <>
                        <button
                          className="secondary"
                          onClick={() => setImporting(true)}
                        >
                          <Upload size={17} />
                          Excel İle Ekle
                        </button>
                        <button onClick={() => setStudent(null)}>
                          <Plus size={17} />
                          Öğrenci Ekle
                        </button>
                      </>
                    )}
                    <button
                      className="secondary"
                      onClick={() =>
                        exportSheet(
                          "ogrenci-adres-listesi",
                          cohort.students
                            .filter((r: Row) => !r.isDeleted)
                            .map((r: Row) => ({
                              ...r,
                              publicLink: addressUrl(r.publicAddressToken),
                              addressState: r.addressLine
                                ? "Tamamlandı"
                                : "Adres Bekleniyor",
                            })),
                          [
                            { key: "fullName", label: "Öğrenci" },
                            { key: "guardianPhone", label: "Telefon" },
                            { key: "productModelName", label: "Eğitim Kiti" },
                            { key: "addressState", label: "Adres Durumu" },
                            { key: "addressLine", label: "Açık Adres" },
                            { key: "publicLink", label: "Adres Bağlantısı" },
                          ],
                        ).catch((e) => notice(e.message, true))
                      }
                    >
                      <Download size={16} />
                      Excel İndir
                    </button>
                  </>
                }
              />
              {cohort.isApproved ? (
                <div className="info-strip">
                  <LockKeyhole size={18} />
                  <span>
                    Siparişiniz onaylandı. Öğrenci listesi kilitli; aktif kitler
                    için arıza ve iade işlemlerine devam edebilirsiniz.
                  </span>
                </div>
              ) : (
                <div className="info-strip">
                  <CheckIcon />
                  <span>
                    İlk öğrenci eklendiğinde siparişiniz otomatik olarak onaya
                    gönderilir. Onaya kadar listeyi düzenleyebilirsiniz.
                  </span>
                </div>
              )}
              <div className="detail-facts">
                <div>
                  <small>Öğrenci</small>
                  <strong>{cohort.studentCount}</strong>
                </div>
                <div>
                  <small>Atanan Kit</small>
                  <strong>{cohort.assignedKitCount}</strong>
                </div>
                <div>
                  <small>Adresi Tamamlanan</small>
                  <strong>
                    {
                      cohort.students.filter(
                        (r: Row) => !r.isDeleted && r.addressLine,
                      ).length
                    }
                  </strong>
                </div>
                <div>
                  <small>Durum</small>
                  <Badge value={cohort.orderStatus ?? 2} kind="order" />
                </div>
              </div>
              <div className="filter-bar">
                <label>
                  <span>Adres Durumu</span>
                  <select
                    value={addressFilter}
                    onChange={(e) => setAddressFilter(e.target.value)}
                  >
                    <option value="">Tümü</option>
                    <option value="filled">Tamamlandı</option>
                    <option value="missing">Adres Bekleniyor</option>
                  </select>
                </label>
                <label>
                  <span>Eğitim Kiti</span>
                  <select
                    value={modelFilter}
                    onChange={(e) => setModelFilter(e.target.value)}
                  >
                    <option value="">Tüm Kitler</option>
                    {models.map((r) => (
                      <option key={r.id} value={r.id}>
                        {r.name}
                      </option>
                    ))}
                  </select>
                </label>
                <label>
                  <span>Kit Ataması</span>
                  <select
                    value={assignment}
                    onChange={(e) => setAssignment(e.target.value)}
                  >
                    <option value="">Tümü</option>
                    <option value="assigned">Atanan</option>
                    <option value="unassigned">Atanmayan</option>
                  </select>
                </label>
              </div>
              <Panel title="Öğrenci Listesi">
                <Table
                  rows={students}
                  columns={[
                    { key: "fullName", label: "Öğrenci" },
                    { key: "guardianPhone", label: "Veli Telefonu" },
                    { key: "productModelName", label: "Eğitim Kiti" },
                    {
                      key: "addressState",
                      label: "Adres Durumu",
                      render: (r) => (
                        <span
                          className={`badge ${r.addressLine ? "green" : "amber"}`}
                        >
                          <i />
                          {r.addressLine ? "Tamamlandı" : "Bekleniyor"}
                        </span>
                      ),
                    },
                    {
                      key: "addressLine",
                      label: "Açık Adres",
                      render: (r) => <TextPreview value={r.addressLine} />,
                    },
                    {
                      key: "publicLink",
                      label: "Adres Bağlantısı",
                      render: (r) => (
                        <TextPreview
                          label="Göster"
                          value={addressUrl(r.publicAddressToken)}
                        />
                      ),
                    },
                    {
                      key: "serialNumber",
                      label: "Atanan Kit",
                      render: (r) =>
                        r.productUnitId ? (
                          <Link to={`/portal/kits/${r.productUnitId}`}>
                            {r.serialNumber}
                          </Link>
                        ) : (
                          "—"
                        ),
                    },
                  ]}
                  actions={(r) => (
                    <>
                      {!cohort.isApproved && (
                        <>
                          <button
                            className="secondary small"
                            onClick={() => setStudent(r)}
                          >
                            Düzenle
                          </button>
                          <ConfirmAction
                            label="Sil"
                            danger
                            description={
                              r.assignmentId
                                ? "Öğrenci bilgileri anonimleştirilir. Atanmış kit, atanmayan kitler listesinde korunur."
                                : "Öğrenci sipariş listesinden kaldırılacak."
                            }
                            onConfirm={async () => {
                              await mutate(
                                core(
                                  `customer-portal/rental-periods/${id}/students/${r.id}`,
                                ),
                                null,
                                "DELETE",
                              );
                              state.reload();
                            }}
                          />
                        </>
                      )}
                      {r.assignmentId && !r.hasCompletedReturn && (
                        <>
                          <button
                            className="secondary small"
                            onClick={() => setFault(r)}
                          >
                            Arıza
                          </button>
                          {!r.hasActiveReturn && (
                            <button
                              className="secondary small"
                              onClick={() => setReturning(r)}
                            >
                              İade
                            </button>
                          )}
                        </>
                      )}
                    </>
                  )}
                />
              </Panel>
              {cohort.unassignedKits.length > 0 && (
                <Panel title="Öğrenciye Atanmayan Kitler">
                  <Table
                    rows={cohort.unassignedKits}
                    columns={[
                      { key: "serialNumber", label: "Seri Numarası" },
                      { key: "productModelName", label: "Eğitim Kiti" },
                    ]}
                    actions={(r) => (
                      <Link to={`/portal/kits/${r.productUnitId}`}>Detay</Link>
                    )}
                  />
                </Panel>
              )}
            </>
          )
        )}
      </State>
      {student !== undefined && cohort && !cohort.isApproved && (
        <Modal
          title={student ? "Öğrenciyi Düzenle" : "Öğrenci Ekle"}
          onClose={() => setStudent(undefined)}
        >
          <Form
            fields={[
              ...studentFields,
              field("productModelId", "Eğitim Kiti", { sourceRows: models }),
            ]}
            initial={student ?? {}}
            onSubmit={async (value) => {
              await mutate(
                core(
                  `customer-portal/rental-periods/${id}/students${student ? "/" + student.id : ""}`,
                ),
                { ...value, addressLine: student?.addressLine ?? "" },
                student ? "PUT" : "POST",
              );
              setStudent(undefined);
              state.reload();
              notice("Öğrenci kaydedildi.");
            }}
          />
        </Modal>
      )}
      {importing && cohort && !cohort.isApproved && (
        <ImportStudents
          models={models}
          onClose={() => setImporting(false)}
          onSave={async (rows) => {
            await mutate(
              core(`customer-portal/rental-periods/${id}/student-imports`),
              { rows },
            );
            setImporting(false);
            state.reload();
            notice(`${rows.length} öğrenci içe aktarıldı.`);
          }}
        />
      )}
      {fault && (
        <Modal title="Arıza Bildir" onClose={() => setFault(undefined)}>
          <Form
            fields={faultFields}
            initial={{
              reporterName: fault.deliveredTo || fault.fullName,
              reporterPhone: fault.deliveryPhone || fault.guardianPhone,
              reporterAddress:
                fault.deliveryAddress ||
                fault.addressLine ||
                d?.addresses?.[0]?.line1,
            }}
            onSubmit={async (value) => {
              await mutate(core("customer-portal/faults"), {
                ...value,
                assignmentId: fault.assignmentId,
              });
              setFault(undefined);
              state.reload();
              notice("Arıza kaydınız alındı.");
            }}
          />
        </Modal>
      )}
      {returning && (
        <Modal
          title="Öğrenci Kitini İade Et"
          onClose={() => setReturning(undefined)}
        >
          <Form
            fields={returnFields}
            initial={{
              requesterName: returning.deliveredTo || returning.fullName,
              requesterPhone:
                returning.deliveryPhone || returning.guardianPhone,
              returnAddress: returning.deliveryAddress || returning.addressLine,
            }}
            onSubmit={async (value) => {
              await mutate(
                core(
                  `customer-portal/rental-periods/${id}/students/${returning.id}/returns`,
                ),
                value,
              );
              setReturning(undefined);
              state.reload();
              notice("İade talebiniz oluşturuldu.");
            }}
          />
        </Modal>
      )}
    </>
  );
}
function CheckIcon() {
  return <ArrowRight size={18} />;
}
function ImportStudents({
  models,
  onClose,
  onSave,
}: {
  models: Row[];
  onClose: () => void;
  onSave: (rows: Row[]) => Promise<void>;
}) {
  const [model, setModel] = useState("");
  const [rows, setRows] = useState<Row[]>([]);
  const [error, setError] = useState("");
  const [reading, setReading] = useState(false);
  const notice = useNotice();
  return (
    <Modal
      title="Excel İle Öğrenci Ekle"
      description="İlk satır başlık; sonraki satırlar öğrenci adı ve veli telefonu olmalıdır."
      onClose={onClose}
    >
      <div className="import-tools">
        <label>
          Eğitim Kiti
          <select
            required
            value={model}
            onChange={(e) => setModel(e.target.value)}
          >
            <option value="">Kit Seçin</option>
            {models.map((m) => (
              <option key={m.id} value={m.id}>
                {m.name}
              </option>
            ))}
          </select>
        </label>
        <button
          className="secondary"
          onClick={() =>
            exportSheet(
              "ogrenci-sablonu",
              [],
              [
                { key: "fullName", label: "Öğrenci Adı Soyadı" },
                { key: "guardianPhone", label: "Veli Telefonu" },
              ],
            ).catch((e) => notice(e.message, true))
          }
        >
          <FileSpreadsheet size={16} />
          Şablon İndir
        </button>
      </div>
      <label className="upload-zone">
        <Upload size={28} />
        <strong>{reading ? "Dosya Okunuyor…" : "Excel Dosyanızı Seçin"}</strong>
        <span>.xlsx · En fazla 10 MB</span>
        <input
          type="file"
          accept=".xlsx"
          disabled={reading}
          onChange={async (e) => {
            const file = e.target.files?.[0];
            if (!file) return;
            setError("");
            setRows([]);
            if (file.size > 10 * 1024 * 1024) {
              setError("Dosya boyutu 10 MB sınırını aşıyor.");
              return;
            }
            setReading(true);
            try {
              const { Workbook } = await import("exceljs");
              const workbook = new Workbook();
              await workbook.xlsx.load(await file.arrayBuffer());
              const sheet = workbook.worksheets[0];
              if (!sheet) throw new Error("Çalışma sayfası bulunamadı.");
              const parsed: Row[] = [];
              sheet.eachRow((row, index) => {
                if (index === 1) return;
                const fullName = row.getCell(1).text.trim(),
                  guardianPhone = row.getCell(2).text.trim();
                if (!fullName && !guardianPhone) return;
                if (!fullName || !guardianPhone)
                  throw new Error(`${index}. satırda ad veya telefon eksik.`);
                parsed.push({ fullName, guardianPhone, addressLine: "" });
              });
              if (!parsed.length)
                throw new Error("Dosyada öğrenci kaydı bulunamadı.");
              setRows(parsed);
            } catch (e) {
              setError((e as Error).message);
            } finally {
              setReading(false);
            }
          }}
        />
      </label>
      {error && (
        <p className="form-error" role="alert">
          {error}
        </p>
      )}
      {rows.length > 0 && (
        <>
          <h3>Önizleme · {rows.length} Öğrenci</h3>
          <Table
            rows={rows}
            columns={[
              { key: "fullName", label: "Öğrenci" },
              { key: "guardianPhone", label: "Veli Telefonu" },
            ]}
          />
          <Form
            fields={[]}
            submitLabel={`${rows.length} Öğrenciyi İçe Aktar`}
            onSubmit={async () => {
              if (!model) throw new Error("Bir eğitim kiti seçin.");
              await onSave(rows.map((r) => ({ ...r, productModel: model })));
            }}
          />
        </>
      )}
    </Modal>
  );
}
export function PortalKit() {
  const { id } = useParams();
  const state = useApi(core("customer-portal"));
  const d = state.data;
  const kit = d?.kits.find((r: Row) => r.productUnitId === id);
  const student = d?.rentalCohorts
    .flatMap((c: Row) => c.students.map((s: Row) => ({ ...s, cohortId: c.id })))
    .find((s: Row) => s.assignmentId === kit?.assignmentId && !s.isDeleted);
  const [fault, setFault] = useState(false);
  const [returning, setReturning] = useState(false);
  const notice = useNotice();
  const activeReturn = d?.returns.some(
    (r: Row) =>
      r.status !== 3 &&
      r.items.some((i: Row) => i.assignmentId === kit?.assignmentId),
  );
  return (
    <>
      <Back to="/portal/kits" />
      <State {...state} retry={state.reload}>
        {d && !kit ? (
          <Empty title="Kit bulunamadı" />
        ) : (
          kit && (
            <>
              <Heading
                eyebrow="KİT DETAYI"
                title={kit.serialNumber}
                description={kit.kitName}
                actions={
                  <>
                    <Badge value={kit.unitStatus} />
                    {!kit.isReturned && (
                      <>
                        <button
                          className="secondary"
                          onClick={() => setFault(true)}
                        >
                          Arıza Bildir
                        </button>
                        {!activeReturn &&
                          (student ? (
                            <button onClick={() => setReturning(true)}>
                              İade Talebi
                            </button>
                          ) : (
                            <ConfirmAction
                              label="İade Talebi"
                              onConfirm={async () => {
                                await mutate(core("customer-portal/returns"), {
                                  assignmentIds: [kit.assignmentId],
                                });
                                state.reload();
                              }}
                            />
                          ))}
                      </>
                    )}
                  </>
                }
              />
              {kit.isReturned && (
                <div className="info-strip">
                  <LockKeyhole size={18} />
                  İadesi tamamlanmış bu kitin geçmişini görüntülüyorsunuz.
                </div>
              )}
              <div className="detail-facts">
                <div>
                  <small>Öğrenci</small>
                  <strong>{kit.assignedStudentName || "Atanmamış"}</strong>
                </div>
                <div>
                  <small>Sipariş</small>
                  <strong>{kit.orderNumber}</strong>
                </div>
                <div>
                  <small>Kiralama Başlangıcı</small>
                  <strong>{date(kit.startDate)}</strong>
                </div>
                <div>
                  <small>Kiralama Bitişi</small>
                  <strong>{date(kit.endDate)}</strong>
                </div>
              </div>
              {student && (
                <Panel title="Teslimat Bilgisi">
                  <div className="detail-summary">
                    <strong>{student.deliveredTo || student.fullName}</strong>
                    <p>{student.deliveryPhone || student.guardianPhone}</p>
                    <p>
                      {student.deliveryAddress ||
                        student.addressLine ||
                        "Adres henüz girilmedi."}
                    </p>
                  </div>
                </Panel>
              )}
              <Panel title="Arıza Geçmişi">
                <Table
                  rows={(d?.faults ?? []).filter(
                    (r: Row) => r.productUnitId === id,
                  )}
                  columns={[
                    { key: "number", label: "Arıza No" },
                    { key: "description", label: "Açıklama" },
                    { key: "status", label: "Durum", kind: "fault" },
                    { key: "openedAt", label: "Tarih", format: "date" },
                  ]}
                />
              </Panel>
            </>
          )
        )}
      </State>
      {fault && kit && (
        <Modal title="Arıza Bildir" onClose={() => setFault(false)}>
          <Form
            fields={faultFields}
            initial={{
              reporterName: student?.deliveredTo || student?.fullName,
              reporterPhone: student?.deliveryPhone || student?.guardianPhone,
              reporterAddress:
                student?.deliveryAddress ||
                student?.addressLine ||
                d?.addresses[0]?.line1,
            }}
            onSubmit={async (value) => {
              await mutate(core("customer-portal/faults"), {
                ...value,
                assignmentId: kit.assignmentId,
              });
              setFault(false);
              state.reload();
              notice("Arıza bildiriminiz alındı.");
            }}
          />
        </Modal>
      )}
      {returning && student && (
        <Modal title="İade Talebi" onClose={() => setReturning(false)}>
          <Form
            fields={returnFields}
            initial={{
              requesterName: student.deliveredTo || student.fullName,
              requesterPhone: student.deliveryPhone || student.guardianPhone,
              returnAddress: student.deliveryAddress || student.addressLine,
            }}
            onSubmit={async (value) => {
              await mutate(
                core(
                  `customer-portal/rental-periods/${student.cohortId}/students/${student.id}/returns`,
                ),
                value,
              );
              setReturning(false);
              state.reload();
              notice("İade talebiniz alındı.");
            }}
          />
        </Modal>
      )}
    </>
  );
}
