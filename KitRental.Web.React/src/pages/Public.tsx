import { useEffect, useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import {
  ArrowLeft,
  ArrowRight,
  CheckCircle2,
  MapPin,
  RotateCcw,
  TriangleAlert,
} from "lucide-react";
import { all, core, gateway, mutate, request } from "../api";
import type { Row } from "../domain";
import { LocateButton } from "../KitMap";
import { faultFields, returnFields } from "../resources";
import { Empty, Form, Panel, State, field, useApi, useLoad } from "../ui";
import districts from "../turkey-districts.json";
import { Connection, LinkLogo } from "./Login";
const regions: Record<string, string[]> = districts;
export function PublicShell({ children }: { children: ReactNode }) {
  const [settings, setSettings] = useState(!gateway);
  return (
    <div className="public-page">
      <header>
        <Link to="/login">
          <LinkLogo />
        </Link>
        <span>Kit Destek Merkezi</span>
      </header>
      <main className="public-card">{children}</main>
      <footer>
        Robotik Bilim · Birlikte öğreniyor, birlikte keşfediyoruz.
      </footer>
      {settings && <Connection onClose={() => setSettings(false)} />}
    </div>
  );
}
export function PublicQr() {
  const { qrCode } = useParams();
  const navigate = useNavigate();
  const state = useLoad(
    () =>
      mutate(core(`public/form-access/${encodeURIComponent(qrCode || "")}`)),
    [qrCode],
  );
  useEffect(() => {
    if (state.data?.token)
      navigate(`/ariza/form/${encodeURIComponent(state.data.token)}`, {
        replace: true,
      });
  }, [state.data, navigate]);
  return (
    <PublicShell>
      <State {...state} retry={state.reload}>
        <p>Kit bilgileri açılıyor…</p>
      </State>
    </PublicShell>
  );
}
export function PublicForm() {
  const { token, action } = useParams();
  const escaped = encodeURIComponent(token || "");
  const state = useLoad(
    async (signal) => {
      const kit = await request(core(`public/faults/kit/${escaped}`), {
        signal,
      });
      if (!action)
        return { kit, context: {} as Row, guides: [], delivery: null };
      const contextPath =
        action === "iade"
          ? "returns"
          : action === "teslim"
            ? "deliveries"
            : "faults";
      const [context, guides, delivery] = await Promise.all([
        request(core(`public/${contextPath}/context/${escaped}`), { signal }),
        action === "ariza"
          ? all(core(`public/fault-guides/${escaped}`), signal)
          : Promise.resolve([]),
        action === "iade"
          ? request(core(`public/deliveries/context/${escaped}`), { signal })
          : Promise.resolve(null),
      ]);
      return { kit, context: context ?? {}, guides, delivery };
    },
    [token, action],
  );
  const [success, setSuccess] = useState(false);
  const [guideDone, setGuideDone] = useState(false);
  const [deliveryMethod, setDeliveryMethod] = useState(1);
  const [coordinates, setCoordinates] = useState<Row>({
    latitude: null,
    longitude: null,
  });
  const d = state.data;
  useEffect(() => {
    if (d) {
      setDeliveryMethod(d.context.deliveryMethod ?? 1);
      setCoordinates({
        latitude: d.context.latitude ?? d.delivery?.latitude ?? null,
        longitude: d.context.longitude ?? d.delivery?.longitude ?? null,
      });
    }
  }, [d]);
  useEffect(() => {
    setSuccess(false);
    setGuideDone(false);
  }, [token, action]);
  if (success)
    return (
      <PublicShell>
        <div className="public-success">
          <CheckCircle2 size={58} />
          <span className="eyebrow">İŞLEM TAMAMLANDI</span>
          <h1>
            {action === "iade"
              ? "İade Talebiniz Alındı"
              : action === "teslim"
                ? "Teslimat Kaydedildi"
                : "Bildiriminiz Alındı"}
          </h1>
          <p>
            Bilgileriniz başarıyla kaydedildi. Ekibimiz işleminizi takip edecek.
          </p>
          {action === "iade" && deliveryMethod === 2 && (
            <div className="cargo-code">
              <small>Aras Kargo İade Kodu</small>
              <strong>1234567890</strong>
              <p>Kiti bu kodla Aras Kargo şubesine bırakabilirsiniz.</p>
            </div>
          )}
          <Link className="button secondary" to={`/ariza/form/${escaped}`}>
            Kit Menüsüne Dön
          </Link>
        </div>
      </PublicShell>
    );
  return (
    <PublicShell>
      <State {...state} retry={state.reload}>
        {d && (
          <>
            <span className="eyebrow">ROBOTİK BİLİM / KİT DESTEĞİ</span>
            <h1>
              {!action
                ? "Size Nasıl Yardımcı Olabiliriz?"
                : action === "iade"
                  ? "Kit İade Talebi"
                  : action === "teslim"
                    ? "Kit Teslim Al"
                    : "Arıza Bildirimi"}
            </h1>
            <div className="public-kit-summary">
              <strong>{d.kit.kitName}</strong>
              <span>{d.kit.serialNumber}</span>
            </div>
            {!action ? (
              <div className="public-choices">
                <Link to={`/ariza/form/${escaped}/ariza`}>
                  <span className="choice-icon peach">
                    <TriangleAlert />
                  </span>
                  <div>
                    <h2>Bir Sorun Yaşıyorum</h2>
                    <p>Çözümlere göz atın veya arıza bildirin.</p>
                  </div>
                  <ArrowRight />
                </Link>
                <Link to={`/ariza/form/${escaped}/iade`}>
                  <span className="choice-icon mint">
                    <RotateCcw />
                  </span>
                  <div>
                    <h2>Kitimi İade Edeceğim</h2>
                    <p>İade yönteminizi seçin, talebinizi oluşturun.</p>
                  </div>
                  <ArrowRight />
                </Link>
              </div>
            ) : action === "ariza" && !guideDone && d.guides.length > 0 ? (
              <>
                <p>Önce kitinize özel çözümleri birlikte kontrol edelim.</p>
                <div className="fault-guides">
                  {d.guides.map((g: Row) => (
                    <details key={g.id}>
                      <summary>{g.title}</summary>
                      <p>{g.problem}</p>
                      <div className="solution-text">{g.solution}</div>
                    </details>
                  ))}
                </div>
                <button
                  className="full-button"
                  onClick={() => setGuideDone(true)}
                >
                  Çözülmedi, Servise Gönder
                  <ArrowRight size={17} />
                </button>
              </>
            ) : (
              <>
                {action === "iade" && (
                  <div className="delivery-options">
                    <button
                      className={deliveryMethod === 1 ? "selected" : ""}
                      onClick={() => setDeliveryMethod(1)}
                    >
                      Adresimden Alınsın
                    </button>
                    <button
                      className={deliveryMethod === 2 ? "selected" : ""}
                      onClick={() => setDeliveryMethod(2)}
                    >
                      Kendim Bırakacağım
                    </button>
                  </div>
                )}
                {(action !== "iade" || deliveryMethod === 1) && (
                  <div className="location-tools">
                    <LocateButton
                      onLocation={(latitude, longitude) =>
                        setCoordinates({ latitude, longitude })
                      }
                    />
                    <small>
                      {coordinates.latitude
                        ? "Konum eklendi."
                        : "Konum isteğe bağlıdır; açık adresiniz yeterlidir."}
                    </small>
                  </div>
                )}
                <Form
                  key={`${action}-${d.context.faultId ?? ""}-${d.context.returnId ?? ""}`}
                  fields={
                    action === "iade"
                      ? returnFields.map((f) =>
                          f.name === "returnAddress"
                            ? {
                                ...f,
                                required: false,
                                hint: "Adresimden Alınsın seçeneğinde zorunludur.",
                              }
                            : f,
                        )
                      : action === "teslim"
                        ? [
                            field("recipientName", "Ad Soyad"),
                            field("recipientPhone", "Telefon", { type: "tel" }),
                            field("addressLine", "Açık Adres", {
                              type: "textarea",
                            }),
                          ]
                        : faultFields
                  }
                  initial={
                    action === "iade"
                      ? {
                          requesterName:
                            d.context.requesterName ??
                            d.delivery?.recipientName,
                          requesterPhone:
                            d.context.requesterPhone ??
                            d.delivery?.recipientPhone,
                          returnAddress:
                            d.context.returnAddress ?? d.delivery?.addressLine,
                          returnReason: d.context.returnReason,
                        }
                      : d.context
                  }
                  submitLabel={
                    action === "iade"
                      ? "İade Talebini Kaydet"
                      : "Bilgileri Kaydet"
                  }
                  onSubmit={async (value) => {
                    if (
                      action === "iade" &&
                      deliveryMethod === 1 &&
                      !value.returnAddress?.trim()
                    )
                      throw new Error("İade için açık adresinizi girin.");
                    const route =
                      action === "iade"
                        ? "returns"
                        : action === "teslim"
                          ? "deliveries"
                          : "faults";
                    await mutate(core(`public/${route}`), {
                      ...value,
                      ...coordinates,
                      token,
                      faultId: d.context.faultId ?? null,
                      ...(action === "iade"
                        ? {
                            deliveryMethod,
                            returnAddress:
                              deliveryMethod === 2
                                ? "Aras Kargo şubesine bırakılacak"
                                : value.returnAddress,
                          }
                        : {}),
                    });
                    setSuccess(true);
                  }}
                />
              </>
            )}
            {action && (
              <Link className="back-link" to={`/ariza/form/${escaped}`}>
                <ArrowLeft size={15} />
                Geri Dön
              </Link>
            )}
          </>
        )}
      </State>
    </PublicShell>
  );
}
export function PublicAddress() {
  const { token } = useParams();
  const state = useApi(
    core(`public/student-addresses/${encodeURIComponent(token || "")}`),
  );
  const [city, setCity] = useState("");
  const [district, setDistrict] = useState("");
  const [address, setAddress] = useState("");
  const [coordinates, setCoordinates] = useState<Row>({
    latitude: null,
    longitude: null,
  });
  const [success, setSuccess] = useState(false);
  const d = state.data;
  useEffect(() => {
    if (d) {
      const match = /^(.+?) \/ (.+?) - ([\s\S]*)$/.exec(d.addressLine ?? "");
      if (match && regions[match[1]]?.includes(match[2])) {
        setCity(match[1]);
        setDistrict(match[2]);
        setAddress(match[3]);
      } else setAddress(d.addressLine ?? "");
      setCoordinates({ latitude: d.latitude, longitude: d.longitude });
    }
  }, [d]);
  return (
    <PublicShell>
      <State {...state} retry={state.reload}>
        {success ? (
          <div className="public-success">
            <CheckCircle2 size={60} />
            <h1>Adresiniz Kaydedildi</h1>
            <p>Kitinizin teslimatı için adres bilgileriniz güncellendi.</p>
            <button className="secondary" onClick={() => setSuccess(false)}>
              Adresi Düzenle
            </button>
          </div>
        ) : (
          d && (
            <>
              <span className="eyebrow">ÖĞRENCİ / TESLİMAT ADRESİ</span>
              <h1>Kitinizin Yolculuğu Burada Başlıyor.</h1>
              <p>Teslimat için güncel ve açık adresinizi kaydedin.</p>
              <div className="public-kit-summary">
                <strong>{d.studentName}</strong>
                <span>
                  {d.guardianPhone} · {d.productName}
                </span>
                <small>
                  {d.customerName} · {d.orderNumber}
                </small>
              </div>
              <div className="form-grid address-region">
                <label>
                  İl
                  <select
                    required
                    value={city}
                    onChange={(e) => {
                      setCity(e.target.value);
                      setDistrict("");
                    }}
                  >
                    <option value="">İl Seçin</option>
                    {Object.keys(regions).map((c) => (
                      <option key={c}>{c}</option>
                    ))}
                  </select>
                </label>
                <label>
                  İlçe
                  <select
                    required
                    value={district}
                    disabled={!city}
                    onChange={(e) => setDistrict(e.target.value)}
                  >
                    <option value="">İlçe Seçin</option>
                    {regions[city]?.map((c) => (
                      <option key={c}>{c}</option>
                    ))}
                  </select>
                </label>
              </div>
              <div className="location-tools">
                <LocateButton
                  onLocation={(latitude, longitude) =>
                    setCoordinates({ latitude, longitude })
                  }
                />
                <small>
                  {coordinates.latitude
                    ? "Konum eklendi."
                    : "İsteğe bağlı: teslimat konumunuzu ekleyin."}
                </small>
              </div>
              <Form
                key={address}
                fields={[
                  field("addressLine", "Açık Adres", {
                    type: "textarea",
                    hint: "Mahalle, cadde/sokak, bina ve daire numarasını yazın.",
                  }),
                ]}
                initial={{ addressLine: address }}
                submitLabel="Adresi Kaydet"
                onSubmit={async (value) => {
                  if (!city || !district)
                    throw new Error("Lütfen il ve ilçe seçin.");
                  await mutate(
                    core(
                      `public/student-addresses/${encodeURIComponent(token || "")}`,
                    ),
                    {
                      addressLine: `${city} / ${district} - ${value.addressLine.trim()}`,
                      ...coordinates,
                    },
                  );
                  setAddress(value.addressLine);
                  setSuccess(true);
                }}
              />
            </>
          )
        )}
      </State>
    </PublicShell>
  );
}
