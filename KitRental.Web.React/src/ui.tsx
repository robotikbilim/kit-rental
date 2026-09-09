import {
  createContext,
  useContext,
  useEffect,
  useId,
  useRef,
  useState,
  type ReactNode,
} from "react";
import {
  AlertCircle,
  ArrowDownToLine,
  ArrowUpDown,
  Check,
  ChevronLeft,
  ChevronRight,
  Copy,
  Inbox,
  LoaderCircle,
  Plus,
  Search,
  Trash2,
  X,
} from "lucide-react";
import { Link } from "react-router-dom";
import { all, core, download, request } from "./api";
import { date, enums, number, type Row } from "./domain";

export function useLoad<T = Row>(
  loader: (signal: AbortSignal) => Promise<T>,
  deps: unknown[] = [],
) {
  const [data, setData] = useState<T>();
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [version, setVersion] = useState(0);
  useEffect(() => {
    const controller = new AbortController();
    setLoading(true);
    setError("");
    setData(undefined);
    loader(controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) setData(value);
      })
      .catch((e) => {
        if (!controller.signal.aborted) setError(e.message);
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [...deps, version]);
  return { data, error, loading, reload: () => setVersion((v) => v + 1) };
}
export const useApi = <T = Row,>(path: string) =>
  useLoad<T>((signal) => request<T>(path, { signal }), [path]);
const NoticeContext = createContext<(message: string, error?: boolean) => void>(
  () => {},
);
export const useNotice = () => useContext(NoticeContext);
export function Notifications({ children }: { children: ReactNode }) {
  const [notice, setNotice] = useState<{ message: string; error?: boolean }>();
  useEffect(() => {
    if (notice) {
      const timer = setTimeout(() => setNotice(undefined), 6000);
      return () => clearTimeout(timer);
    }
  }, [notice]);
  return (
    <NoticeContext.Provider
      value={(message, error) => setNotice({ message, error })}
    >
      {children}
      {notice && (
        <div className={`toast ${notice.error ? "error" : ""}`} role="status">
          {notice.error ? <AlertCircle size={20} /> : <Check size={20} />}
          <span>{notice.message}</span>
          <button
            className="icon-button"
            aria-label="Bildirimi Kapat"
            onClick={() => setNotice(undefined)}
          >
            <X size={16} />
          </button>
        </div>
      )}
    </NoticeContext.Provider>
  );
}
export function Heading({
  eyebrow = "OPERASYON MERKEZİ",
  title,
  description,
  actions,
}: {
  eyebrow?: string;
  title: string;
  description?: string;
  actions?: ReactNode;
}) {
  return (
    <div className="page-heading">
      <div>
        <span className="eyebrow">{eyebrow}</span>
        <h1>{title}</h1>
        {description && <p>{description}</p>}
      </div>
      <div className="actions">{actions}</div>
    </div>
  );
}
export function Panel({
  title,
  subtitle,
  actions,
  children,
  className = "",
}: {
  title?: string;
  subtitle?: string;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}) {
  return (
    <section className={`panel ${className}`}>
      {title && (
        <div className="panel-heading">
          <div>
            <h2>{title}</h2>
            {subtitle && <p>{subtitle}</p>}
          </div>
          {actions}
        </div>
      )}
      {children}
    </section>
  );
}
export function State({
  loading,
  error,
  retry,
  children,
}: {
  loading?: boolean;
  error?: string;
  retry?: () => void;
  children: ReactNode;
}) {
  if (loading)
    return (
      <div className="loading-state" role="status">
        <LoaderCircle className="spin" />
        <p>Çalışma alanınız hazırlanıyor…</p>
        <div className="skeleton" />
        <div className="skeleton short" />
      </div>
    );
  if (error)
    return (
      <div className="empty error" role="alert">
        <AlertCircle size={32} />
        <h2>Veriler Yüklenemedi</h2>
        <p>{error}</p>
        {retry && <button onClick={retry}>Tekrar Dene</button>}
      </div>
    );
  return <>{children}</>;
}
export function Empty({
  title = "Henüz kayıt yok",
  description = "Kayıtlarınız eklendiğinde burada görüntülenecek.",
}: {
  title?: string;
  description?: string;
}) {
  return (
    <div className="empty">
      <div className="empty-icon">
        <Inbox size={28} />
      </div>
      <h3>{title}</h3>
      <p>{description}</p>
    </div>
  );
}
export function Badge({ value, kind = "unit" }: { value: any; kind?: string }) {
  const text = enums[kind]?.[value] ?? String(value ?? "—");
  const tone =
    /Hazır|Aktif|Tamamlandı|Onaylandı|Çözüldü|Kapandı|Teslim Alındı|Tedarik Edildi/.test(
      text,
    )
      ? "green"
      : /Arıza|Kritik|Gecik|Reddedildi|Kayıp|Karantina/.test(text)
        ? "red"
        : /Bekli|Talep|Öneri|Hazırlan|İncele|Bakım/.test(text)
          ? "amber"
          : "blue";
  return (
    <span className={`badge ${tone}`}>
      <i />
      {text}
    </span>
  );
}
export function Stat({
  label,
  value,
  icon,
  href,
  tone = "mint",
  foot,
}: {
  label: string;
  value: any;
  icon: ReactNode;
  href?: string;
  tone?: string;
  foot?: string;
}) {
  const content = (
    <>
      <div className="stat-top">
        <span>{label}</span>
        <span className={`stat-icon ${tone}`}>{icon}</span>
      </div>
      <strong>{number(value)}</strong>
      <div className="stat-foot">
        {foot || "Güncel kayıtlar"}
        {href && <ChevronRight size={15} />}
      </div>
    </>
  );
  return href ? (
    <Link className="stat" to={href}>
      {content}
    </Link>
  ) : (
    <div className="stat">{content}</div>
  );
}
export function Modal({
  title,
  description,
  children,
  onClose,
}: {
  title: string;
  description?: string;
  children: ReactNode;
  onClose: () => void;
}) {
  const ref = useRef<HTMLDialogElement>(null);
  const id = useId();
  useEffect(() => {
    const element = ref.current;
    const prior = document.activeElement as HTMLElement;
    element?.showModal();
    document.body.style.overflow = "hidden";
    return () => {
      element?.close();
      document.body.style.overflow = "";
      prior?.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      className="modal"
      aria-labelledby={id}
      onCancel={(e) => {
        e.preventDefault();
        onClose();
      }}
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
    >
      <div className="modal-header">
        <div>
          <h2 id={id}>{title}</h2>
          {description && <p>{description}</p>}
        </div>
        <button
          className="icon-button"
          aria-label="Pencereyi Kapat"
          onClick={onClose}
        >
          <X />
        </button>
      </div>
      <div className="modal-body">{children}</div>
    </dialog>
  );
}
export function TextPreview({
  value,
  label = "Göster",
}: {
  value?: string;
  label?: string;
}) {
  const [open, setOpen] = useState(false);
  const notice = useNotice();
  if (!value) return null;
  return (
    <>
      <button
        className="text-preview"
        onClick={() => setOpen(true)}
        title={value}
      >
        {label === "Göster" ? value : label}
      </button>
      {open && (
        <Modal title="Kayıt Bilgisi" onClose={() => setOpen(false)}>
          <p className="preserve">{value}</p>
          <button
            onClick={() =>
              navigator.clipboard
                .writeText(value)
                .then(() => notice("Panoya kopyalandı."))
                .catch(() =>
                  notice(
                    "Kopyalanamadı. Metni seçerek kopyalayabilirsiniz.",
                    true,
                  ),
                )
            }
          >
            <Copy size={16} />
            Kopyala
          </button>
        </Modal>
      )}
    </>
  );
}
export interface Column {
  key: string;
  label: string;
  render?: (row: Row) => ReactNode;
  format?: "date" | "number";
  kind?: string;
}
export function Table({
  rows,
  columns,
  actions,
  page: serverPage,
  total,
  totalPages,
  onPage,
  onQuery,
  search = true,
  exportName,
}: {
  rows: Row[];
  columns: Column[];
  actions?: (row: Row) => ReactNode;
  page?: number;
  total?: number;
  totalPages?: number;
  onPage?: (page: number) => void;
  onQuery?: (q: string) => void;
  search?: boolean;
  exportName?: string;
}) {
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [sort, setSort] = useState<{ key: string; descending: boolean }>();
  const notice = useNotice();
  const filtered = rows.filter(
    (row) =>
      !query ||
      onQuery ||
      columns.some((c) =>
        String(row[c.key] ?? "")
          .toLocaleLowerCase("tr")
          .includes(query.toLocaleLowerCase("tr")),
      ),
  );
  const sorted = sort
    ? [...filtered].sort(
        (a, b) =>
          (typeof a[sort.key] === "number"
            ? a[sort.key] - b[sort.key]
            : String(a[sort.key] ?? "").localeCompare(
                String(b[sort.key] ?? ""),
                "tr",
                { numeric: true },
              )) * (sort.descending ? -1 : 1),
      )
    : filtered;
  const pages = totalPages ?? Math.max(1, Math.ceil(sorted.length / 20));
  const activePage = serverPage ?? Math.min(page, pages);
  const visible = serverPage
    ? sorted
    : sorted.slice((activePage - 1) * 20, activePage * 20);
  const changePage = (p: number) => (onPage ? onPage(p) : setPage(p));
  return (
    <div className="data-table">
      {(search || exportName) && (
        <div className="table-toolbar">
          {search && (
            <label className="search-input">
              <Search size={17} />
              <input
                aria-label={onQuery ? "Tüm Kayıtlarda Ara" : "Listede Ara"}
                placeholder={
                  onQuery
                    ? "Tüm kayıtlarda ara…"
                    : serverPage
                      ? "Bu sayfada ara…"
                      : "Listede ara…"
                }
                value={query}
                onChange={(e) => {
                  setQuery(e.target.value);
                  setPage(1);
                  onQuery?.(e.target.value);
                }}
              />
            </label>
          )}
          <span className="record-count">
            {number(total ?? filtered.length)} kayıt
          </span>
          {exportName && (
            <button
              className="secondary small"
              onClick={() =>
                exportSheet(exportName, filtered, columns).catch((e) =>
                  notice(e.message, true),
                )
              }
            >
              <ArrowDownToLine size={15} />
              Excel{serverPage ? " · Bu Sayfa" : ""}
            </button>
          )}
        </div>
      )}
      <div className="table-scroll">
        <table>
          <thead>
            <tr>
              {columns.map((c) => (
                <th key={c.key}>
                  <button
                    onClick={() =>
                      setSort({
                        key: c.key,
                        descending:
                          sort?.key === c.key ? !sort.descending : false,
                      })
                    }
                  >
                    {c.label}
                    <ArrowUpDown size={12} />
                  </button>
                </th>
              ))}
              {actions && <th className="action-col">İşlemler</th>}
            </tr>
          </thead>
          <tbody>
            {visible.map((row, index) => (
              <tr
                key={
                  row.id ??
                  row.assignmentId ??
                  row.productUnitId ??
                  `${row.componentId}-${row.storageLocationId}-${index}`
                }
              >
                {columns.map((c) => (
                  <td key={c.key}>
                    {c.render ? (
                      c.render(row)
                    ) : c.kind ? (
                      <Badge value={row[c.key]} kind={c.kind} />
                    ) : c.format === "date" ? (
                      date(row[c.key])
                    ) : c.format === "number" ? (
                      number(row[c.key])
                    ) : typeof row[c.key] === "boolean" ? (
                      row[c.key] ? (
                        "Evet"
                      ) : (
                        "Hayır"
                      )
                    ) : (
                      String(row[c.key] ?? "—")
                    )}
                  </td>
                ))}
                {actions && (
                  <td>
                    <div className="row-actions">{actions(row)}</div>
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {!visible.length && (
        <Empty
          title="Gösterilecek kayıt bulunamadı"
          description="Filtrelerinizi değiştirebilir veya yeni bir kayıt ekleyebilirsiniz."
        />
      )}
      <div className="pagination">
        <span>
          {number(total ?? filtered.length)} kayıttan{" "}
          {visible.length ? (activePage - 1) * 20 + 1 : 0}–
          {Math.min(activePage * 20, total ?? filtered.length)} arası
        </span>
        <div>
          <button
            className="icon-button"
            disabled={activePage <= 1}
            aria-label="Önceki Sayfa"
            onClick={() => changePage(activePage - 1)}
          >
            <ChevronLeft size={17} />
          </button>
          <span>
            {activePage} / {Math.max(1, pages)}
          </span>
          <button
            className="icon-button"
            disabled={activePage >= pages}
            aria-label="Sonraki Sayfa"
            onClick={() => changePage(activePage + 1)}
          >
            <ChevronRight size={17} />
          </button>
        </div>
      </div>
    </div>
  );
}
export async function exportSheet(
  name: string,
  rows: Row[],
  columns: Column[],
) {
  const { Workbook } = await import("exceljs");
  const workbook = new Workbook();
  const sheet = workbook.addWorksheet("Kayıtlar");
  sheet.columns = columns.map((c) => ({
    header: c.label,
    key: c.key,
    width: 28,
  }));
  rows.forEach((row) =>
    sheet.addRow(
      Object.fromEntries(
        columns.map((c) => [
          c.key,
          c.kind
            ? (enums[c.kind]?.[row[c.key]] ?? row[c.key])
            : (row[c.key] ?? ""),
        ]),
      ),
    ),
  );
  sheet.getRow(1).font = { bold: true, color: { argb: "FFFFFFFF" } };
  sheet.getRow(1).fill = {
    type: "pattern",
    pattern: "solid",
    fgColor: { argb: "FF152B29" },
  };
  sheet.views = [{ state: "frozen", ySplit: 1 }];
  download(
    new Blob([(await workbook.xlsx.writeBuffer()) as ArrayBuffer], {
      type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
    }),
    `${name}.xlsx`,
  );
}
export type Field = {
  name: string;
  label: string;
  type?: string;
  required?: boolean;
  options?: { value: any; label: string }[];
  source?: string;
  sourceRows?: Row[];
  children?: Field[];
  hint?: string;
  default?: any;
  min?: number;
  step?: number;
  nullable?: boolean;
};
export const field = (
  name: string,
  label: string,
  extra: Partial<Field> = {},
): Field => ({ name, label, required: true, ...extra });
export const selectEnum = (name: string, label: string, kind: string): Field =>
  field(name, label, {
    type: "number-select",
    options: Object.entries(enums[kind]).map(([value, label]) => ({
      value: Number(value),
      label,
    })),
  });
function defaults(fields: Field[], initial: Row): Row {
  return Object.fromEntries(
    fields.map((f) => [
      f.name,
      initial[f.name] ??
        f.default ??
        (f.type === "array"
          ? []
          : f.type === "object"
            ? defaults(f.children ?? [], {})
            : f.type === "boolean"
              ? false
              : f.type === "multi"
                ? []
                : ""),
    ]),
  );
}
function Inputs({
  fields,
  value,
  onChange,
}: {
  fields: Field[];
  value: Row;
  onChange: (value: Row) => void;
}) {
  return (
    <>
      {fields.map((f) => (
        <Input
          key={f.name}
          field={f}
          value={value[f.name]}
          onChange={(v) => onChange({ ...value, [f.name]: v })}
        />
      ))}
    </>
  );
}
function Input({
  field: f,
  value,
  onChange,
}: {
  field: Field;
  value: any;
  onChange: (value: any) => void;
}) {
  const id = useId();
  const source = useLoad(
    (signal) =>
      f.source
        ? all(f.source.startsWith("/") ? f.source : core(f.source), signal)
        : Promise.resolve(f.sourceRows ?? []),
    [f.source, f.sourceRows],
  );
  const options =
    f.options ??
    (source.data ?? []).map((row) => ({
      value: row.id ?? row.productModelId,
      label:
        row.name ??
        row.kitName ??
        row.code ??
        row.displayName ??
        row.serialNumber,
    }));
  if (f.type === "object")
    return (
      <fieldset className="form-section">
        <legend>{f.label}</legend>
        <div className="form-grid">
          <Inputs
            fields={f.children ?? []}
            value={value || {}}
            onChange={onChange}
          />
        </div>
      </fieldset>
    );
  if (f.type === "array")
    return (
      <fieldset className="form-section">
        <legend>
          {f.label} <span className="count-pill">{value?.length ?? 0}</span>
        </legend>
        {(value || []).map((row: Row, i: number) => (
          <div className="form-array-row" key={i}>
            <div className="form-grid">
              <Inputs
                fields={f.children ?? []}
                value={row}
                onChange={(next) =>
                  onChange(
                    value.map((r: Row, n: number) => (n === i ? next : r)),
                  )
                }
              />
            </div>
            <button
              type="button"
              className="icon-button danger"
              aria-label={`${i + 1}. Satırı Sil`}
              onClick={() =>
                onChange(value.filter((_: Row, n: number) => n !== i))
              }
            >
              <Trash2 size={17} />
            </button>
          </div>
        ))}
        <button
          type="button"
          className="secondary small"
          onClick={() =>
            onChange([...(value || []), defaults(f.children ?? [], {})])
          }
        >
          <Plus size={16} />
          Satır Ekle
        </button>
        {f.hint && <small>{f.hint}</small>}
      </fieldset>
    );
  return (
    <div
      className={`form-field ${f.type === "textarea" || f.type === "multi" ? "full" : ""}`}
    >
      <label htmlFor={id}>
        {f.label}
        {f.required && f.type !== "boolean" && (
          <span className="required"> *</span>
        )}
      </label>
      {f.type === "textarea" ? (
        <textarea
          id={id}
          rows={4}
          value={value ?? ""}
          required={f.required}
          onChange={(e) => onChange(e.target.value)}
        />
      ) : f.type === "boolean" ? (
        <label className="switch">
          <input
            id={id}
            type="checkbox"
            checked={!!value}
            onChange={(e) => onChange(e.target.checked)}
          />
          <span>{value ? "Etkin" : "Kapalı"}</span>
        </label>
      ) : f.type === "multi" ? (
        <div className="multi-options">
          {options.map((o) => (
            <label key={o.value}>
              <input
                type="checkbox"
                checked={(value || []).includes(o.value)}
                onChange={(e) =>
                  onChange(
                    e.target.checked
                      ? [...(value || []), o.value]
                      : value.filter((v: any) => v !== o.value),
                  )
                }
              />
              {o.label}
            </label>
          ))}
        </div>
      ) : f.source || f.sourceRows || f.options ? (
        <select
          id={id}
          value={value ?? ""}
          required={f.required}
          onChange={(e) =>
            onChange(
              f.type === "number-select" && e.target.value
                ? Number(e.target.value)
                : e.target.value || (f.nullable ? null : ""),
            )
          }
        >
          <option value="">
            {source.loading && f.source ? "Yükleniyor…" : "Seçiniz"}
          </option>
          {options.map((o) => (
            <option value={o.value} key={o.value}>
              {o.label}
            </option>
          ))}
        </select>
      ) : (
        <input
          id={id}
          type={f.type || "text"}
          value={value ?? ""}
          required={f.required}
          min={f.min}
          step={f.step ?? (f.type === "number" ? "any" : undefined)}
          autoComplete={f.type === "password" ? "new-password" : undefined}
          onChange={(e) =>
            onChange(
              f.type === "number" && e.target.value !== ""
                ? Number(e.target.value)
                : e.target.value,
            )
          }
        />
      )}
      {f.hint && <small>{f.hint}</small>}
      {source.error && f.source && (
        <small className="error" role="alert">
          {source.error}
        </small>
      )}
    </div>
  );
}
export function Form({
  fields,
  initial = {},
  onSubmit,
  submitLabel = "Kaydet",
  onCancel,
}: {
  fields: Field[];
  initial?: Row;
  onSubmit: (value: Row) => Promise<unknown>;
  submitLabel?: string;
  onCancel?: () => void;
}) {
  const [value, setValue] = useState<Row>(() => defaults(fields, initial));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  return (
    <form
      onSubmit={async (e) => {
        e.preventDefault();
        if (busy) return;
        setError("");
        if (
          value.startDate &&
          value.endDate &&
          value.endDate < value.startDate
        ) {
          setError("Bitiş tarihi başlangıç tarihinden önce olamaz.");
          return;
        }
        if (
          fields.some(
            (f) => f.type === "array" && f.required && !value[f.name]?.length,
          )
        ) {
          setError("En az bir satır ekleyin.");
          return;
        }
        setBusy(true);
        try {
          await onSubmit(value);
        } catch (e) {
          setError((e as Error).message);
        } finally {
          setBusy(false);
        }
      }}
    >
      <fieldset disabled={busy} className="form-reset">
        <div className="form-grid">
          <Inputs fields={fields} value={value} onChange={setValue} />
        </div>
        {error && (
          <p className="form-error" role="alert">
            <AlertCircle size={17} />
            {error}
          </p>
        )}
        <div className="form-footer">
          {onCancel && (
            <button type="button" className="secondary" onClick={onCancel}>
              Vazgeç
            </button>
          )}
          <button type="submit">
            {busy ? (
              <LoaderCircle size={17} className="spin" />
            ) : (
              <Check size={17} />
            )}{" "}
            {busy ? "Kaydediliyor…" : submitLabel}
          </button>
        </div>
      </fieldset>
    </form>
  );
}
export function ConfirmAction({
  label,
  title,
  description,
  onConfirm,
  danger = false,
  icon,
}: {
  label: string;
  title?: string;
  description?: string;
  onConfirm: () => Promise<unknown>;
  danger?: boolean;
  icon?: ReactNode;
}) {
  const [open, setOpen] = useState(false);
  const notice = useNotice();
  return (
    <>
      <button
        className={`${danger ? "danger" : "secondary"} small`}
        title={label}
        onClick={() => setOpen(true)}
      >
        {icon}
        {label}
      </button>
      {open && (
        <Modal
          title={title || label}
          description={
            description || "Bu işlem kayıt üzerinde değişiklik yapacaktır."
          }
          onClose={() => setOpen(false)}
        >
          <Form
            fields={[]}
            submitLabel={label}
            onCancel={() => setOpen(false)}
            onSubmit={async () => {
              await onConfirm();
              setOpen(false);
              notice("İşlem tamamlandı.");
            }}
          />
        </Modal>
      )}
    </>
  );
}
