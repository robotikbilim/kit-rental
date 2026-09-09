import { useState } from "react";
import {
  Box,
  Eye,
  EyeOff,
  ArrowRight,
  ShieldCheck,
  Settings2,
} from "lucide-react";
import { Navigate, useLocation } from "react-router-dom";
import { gateway, setGateway } from "../api";
import { useAuth } from "../auth";
import { managers } from "../domain";
import { Form, field, Modal } from "../ui";
export function Connection({ onClose }: { onClose: () => void }) {
  return (
    <Modal
      title="Gateway Bağlantısı"
      description="Bu arayüzün bağlanacağı gateway adresini girin."
      onClose={onClose}
    >
      <Form
        fields={[field("url", "Gateway Base URL", { type: "url" })]}
        initial={{ url: gateway }}
        onSubmit={async (value) => {
          setGateway(value.url);
          onClose();
        }}
      />
    </Modal>
  );
}
export function Login() {
  const auth = useAuth();
  const location = useLocation();
  const [settings, setSettings] = useState(!gateway);
  const [show, setShow] = useState(false);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  if (auth.session) {
    const from = location.state?.from;
    const fallback = auth.isCustomer
      ? "/portal"
      : auth.can(managers)
        ? "/"
        : "/inventory";
    return (
      <Navigate
        to={
          typeof from === "string" &&
          from.startsWith("/") &&
          !from.startsWith("//")
            ? from
            : fallback
        }
        replace
      />
    );
  }
  return (
    <div className="login-page">
      <section className="login-story">
        <LinkLogo />
        <div className="login-story-content">
          <span className="eyebrow">ÖĞRENMEYE ALAN AÇIN</span>
          <h1>
            İyi organize edilmiş
            <br />
            bir dünya.
            <br />
            <em>Sınırsız keşif.</em>
          </h1>
          <p>
            Eğitim kitlerinizi, öğrencilerinizi ve operasyonunuzu bir araya
            getiren çalışma alanınız.
          </p>
          <div className="login-art" aria-hidden="true">
            <Box />
            <div className="floating-label">
              <span className="status-dot" />
              Bir sonraki keşfe hazır
            </div>
          </div>
        </div>
        <small>Robotik Bilim · Kit Yönetim Platformu</small>
      </section>
      <section className="login-form-side">
        <button className="connection-button" onClick={() => setSettings(true)}>
          <Settings2 size={17} />
          Bağlantı Ayarları
        </button>
        <div className="login-form">
          <span className="eyebrow">KİTRENТAL'A HOŞ GELDİNİZ</span>
          <h2>
            Kaldığınız yerden
            <br />
            devam edin.
          </h2>
          <p>Hesap bilgilerinizle çalışma alanınıza giriş yapın.</p>
          <form
            onSubmit={async (e) => {
              e.preventDefault();
              if (busy) return;
              setError("");
              setBusy(true);
              const data = new FormData(e.currentTarget);
              try {
                await auth.login(
                  String(data.get("email")),
                  String(data.get("password")),
                );
              } catch (e) {
                setError((e as Error).message);
              } finally {
                setBusy(false);
              }
            }}
          >
            <label>
              E-posta / Kullanıcı Adı
              <input
                name="email"
                autoComplete="username"
                required
                placeholder="kullanici@kurum.com"
              />
            </label>
            <label>
              Şifre
              <div className="password-input">
                <input
                  name="password"
                  type={show ? "text" : "password"}
                  autoComplete="current-password"
                  required
                  placeholder="Şifrenizi girin"
                />
                <button
                  type="button"
                  className="icon-button"
                  aria-label={show ? "Şifreyi Gizle" : "Şifreyi Göster"}
                  onClick={() => setShow(!show)}
                >
                  {show ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
            </label>
            {error && (
              <p className="form-error" role="alert">
                {error}
              </p>
            )}
            <button className="login-submit" disabled={busy}>
              {busy ? "Giriş Yapılıyor…" : "Çalışma Alanına Gir"}
              <ArrowRight size={18} />
            </button>
          </form>
          <div className="login-security">
            <ShieldCheck size={17} />
            Yetkinize özel, güvenli çalışma alanı
          </div>
        </div>
        <footer>KitRental © {new Date().getFullYear()}</footer>
      </section>
      {settings && <Connection onClose={() => setSettings(false)} />}
    </div>
  );
}
export function LinkLogo() {
  return (
    <div className="logo">
      <span>
        <Box size={27} strokeWidth={1.7} />
      </span>
      <div>
        kitrental<small>ROBOTİK BİLİM</small>
      </div>
    </div>
  );
}
