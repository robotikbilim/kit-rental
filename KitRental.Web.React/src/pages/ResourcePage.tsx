import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import {
  ArrowDownToLine,
  ArrowRight,
  Boxes,
  Pencil,
  Plus,
  RefreshCw,
  Trash2,
  X,
} from "lucide-react";
import { all, core, download, imageUrl, mutate, request } from "../api";
import { useAuth } from "../auth";
import { managers, type Row } from "../domain";
import { endpoint, resources } from "../resources";
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
  field,
  selectEnum,
  useLoad,
  useNotice,
} from "../ui";

export function ResourcePage({ name }: { name: string }) {
  const config = resources[name];
  const auth = useAuth();
  const notice = useNotice();
  const [params, setParams] = useSearchParams();
  const [editor, setEditor] = useState<Row | null>();
  const [fault, setFault] = useState<Row>();
  const page = Math.max(1, Number(params.get("page") || 1));
  useEffect(() => {
    if (params.get("new") === "1" && auth.can(config.writeRoles ?? [])) {
      setEditor(null);
      const next = new URLSearchParams(params);
      next.delete("new");
      setParams(next, { replace: true });
    }
  }, [params]);
  const path = endpoint(config.path);
  const query = new URLSearchParams(params);
  if (config.server) {
    query.set("page", String(page));
    query.set("pageSize", "20");
  }
  const state = useLoad(
    (signal) =>
      config.server
        ? request(`${path}?${query}`, { signal })
        : all(`${path}${query.size ? "?" + query : ""}`, signal),
    [path, query.toString()],
  );
  const rows: Row[] = config.server
    ? (state.data?.items ?? [])
    : (state.data ?? []);
  const writable = auth.can(config.writeRoles ?? []);
  function filter(key: string, value: string) {
    const next = new URLSearchParams(params);
    value ? next.set(key, value) : next.delete(key);
    next.delete("page");
    setParams(next, { replace: true });
  }
  function actions(row: Row) {
    return (
      <>
        {config.detail && (
          <Link
            className="row-link"
            to={`${config.detail}${row.id ?? row.productModelId}`}
          >
            Detay
            <ArrowRight size={13} />
          </Link>
        )}
        {writable && config.fields && (
          <button
            className="icon-button"
            aria-label="Düzenle"
            onClick={() => setEditor(row)}
          >
            <Pencil size={15} />
          </button>
        )}
        {writable && config.canDelete && (
          <ConfirmAction
            label={name === "customers" ? "Pasifleştir" : "Sil"}
            danger
            icon={<Trash2 size={14} />}
            onConfirm={async () => {
              await mutate(`${path}/${row.id}`, null, "DELETE");
              state.reload();
            }}
          />
        )}
        {name === "faults" && auth.can([...managers, "ServiceTechnician"]) && (
          <button className="secondary small" onClick={() => setFault(row)}>
            İncele
          </button>
        )}
      </>
    );
  }
  return (
    <>
      <Heading
        title={config.title}
        description={config.description}
        actions={
          <>
            <button
              className="secondary icon-button"
              aria-label="Yenile"
              onClick={state.reload}
            >
              <RefreshCw size={18} />
            </button>
            {name === "inventory" && auth.can(managers) && (
              <button
                className="secondary"
                onClick={() =>
                  request<Blob>(core("reports/inventory.csv"), { blob: true })
                    .then((b) => download(b, "envanter.csv"))
                    .catch((e) => notice(e.message, true))
                }
              >
                <ArrowDownToLine size={17} />
                Dışa Aktar
              </button>
            )}
            {name === "kits" && auth.can(managers) && (
              <Link className="button secondary" to="/kits/bulk-rental">
                Toplu Kirala
              </Link>
            )}
            {name === "orders" && writable && (
              <Link className="button secondary" to="/purchase-order">
                Satın Alma
              </Link>
            )}
            {name === "supply" && writable && (
              <ConfirmAction
                label="Önerileri Güncelle"
                onConfirm={async () => {
                  await mutate(core("supply-need-recommendation-refreshes"));
                  state.reload();
                }}
              />
            )}
            {writable && (config.createFields || config.fields) && (
              <button onClick={() => setEditor(null)}>
                <Plus size={18} />
                {name === "orders"
                  ? "Yeni Sipariş"
                  : name === "kits"
                    ? "Kit Oluştur"
                    : "Yeni Kayıt"}
              </button>
            )}
          </>
        }
      />
      {config.filters && (
        <div className="filter-bar">
          {config.filters.map((f) => (
            <label key={f.name}>
              <span>{f.label}</span>
              {f.source ? (
                <ModelFilter
                  value={params.get(f.name) || ""}
                  onChange={(v) => filter(f.name, v)}
                />
              ) : f.options ? (
                <select
                  value={params.get(f.name) || ""}
                  onChange={(e) => filter(f.name, e.target.value)}
                >
                  <option value="">Tümü</option>
                  {f.options.map((o) => (
                    <option key={o.value} value={o.value}>
                      {o.label}
                    </option>
                  ))}
                </select>
              ) : (
                <input
                  type={f.type || "text"}
                  value={params.get(f.name) || ""}
                  onChange={(e) => filter(f.name, e.target.value)}
                />
              )}
            </label>
          ))}
          {params.size > 0 && (
            <button
              className="icon-button"
              title="Filtreleri Temizle"
              onClick={() => setParams({})}
            >
              <X size={17} />
            </button>
          )}
        </div>
      )}
      <State {...state} retry={state.reload}>
        {config.cards ? (
          <div className="catalog-grid">
            {!rows.length && <Empty />}
            {rows.map((row) => (
              <Panel
                key={row.id ?? row.productModelId}
                className="catalog-card"
              >
                <div className="kit-art">
                  {imageUrl(row.imageUrl ?? row.productImageUrl) ? (
                    <img
                      src={imageUrl(row.imageUrl ?? row.productImageUrl)}
                      alt={row.name ?? row.productName}
                    />
                  ) : (
                    <Boxes size={72} strokeWidth={1} />
                  )}
                  <span className="badge">{row.sku ?? row.productSku}</span>
                </div>
                <div className="catalog-body">
                  <h2>{row.name ?? row.productName}</h2>
                  <p>{row.description || "Robotik eğitim ve uygulama seti"}</p>
                  {name === "manufacturing" && (
                    <div className="production-count">
                      <strong>{row.buildableQuantity}</strong>
                      <span>kit üretilebilir</span>
                    </div>
                  )}
                  <div className="catalog-actions">{actions(row)}</div>
                </div>
              </Panel>
            ))}
          </div>
        ) : (
          <Panel>
            <Table
              rows={rows}
              columns={config.columns}
              actions={actions}
              exportName={name}
              page={config.server ? page : undefined}
              total={config.server ? state.data?.totalCount : undefined}
              totalPages={config.server ? state.data?.totalPages : undefined}
              onPage={
                config.server
                  ? (p) => {
                      const next = new URLSearchParams(params);
                      next.set("page", String(p));
                      setParams(next);
                    }
                  : undefined
              }
              onQuery={
                config.server && name !== "audit"
                  ? (q) => filter("query", q)
                  : undefined
              }
            />
          </Panel>
        )}
      </State>
      {editor !== undefined && (
        <Modal
          title={editor ? "Kaydı Düzenle" : `${config.title} · Yeni Kayıt`}
          onClose={() => setEditor(undefined)}
        >
          <Form
            fields={
              editor
                ? (config.fields ?? [])
                : (config.createFields ?? config.fields ?? [])
            }
            initial={editor ?? {}}
            onCancel={() => setEditor(undefined)}
            onSubmit={async (value) => {
              await mutate(
                editor
                  ? `${endpoint(config.editPath ?? config.path)}/${editor.id}`
                  : endpoint(config.createPath ?? config.path),
                value,
                editor ? "PUT" : "POST",
              );
              setEditor(undefined);
              state.reload();
              notice("Kayıt başarıyla kaydedildi.");
            }}
          />
        </Modal>
      )}
      {fault && (
        <Modal
          title={`${fault.number} · Arıza Kaydı`}
          description={fault.description}
          onClose={() => setFault(undefined)}
        >
          <div className="detail-summary">
            <span>{fault.reporterName}</span>
            <span>{fault.reporterPhone}</span>
            <p>{fault.reporterAddress}</p>
            <Badge value={fault.status} kind="fault" />
          </div>
          <Form
            fields={[
              selectEnum("status", "Yeni Durum", "fault"),
              field("note", "İşlem Notu", { type: "textarea" }),
            ]}
            initial={{ status: fault.status }}
            onSubmit={async (value) => {
              await mutate(core(`faults/${fault.id}/status-events`), value);
              setFault(undefined);
              state.reload();
              notice("Arıza durumu güncellendi.");
            }}
          />
        </Modal>
      )}
    </>
  );
}
function ModelFilter({
  value,
  onChange,
}: {
  value: string;
  onChange: (v: string) => void;
}) {
  const state = useLoad((signal) => all(core("product-models"), signal), []);
  return (
    <select value={value} onChange={(e) => onChange(e.target.value)}>
      <option value="">Tüm Kitler</option>
      {state.data?.map((r) => (
        <option key={r.id} value={r.id}>
          {r.name}
        </option>
      ))}
    </select>
  );
}
