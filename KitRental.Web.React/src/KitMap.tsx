import { useEffect, useRef, useState } from "react";
import L from "leaflet";
import "leaflet/dist/leaflet.css";
import { LocateFixed, MapPin } from "lucide-react";
import type { Row } from "./domain";
import { useNotice } from "./ui";
const colors: Record<string, string> = {
  active: "#31977b",
  faulty: "#d66654",
  returning: "#5d80c0",
  expired: "#d7a345",
};
export function KitMap({ rows = [] }: { rows: Row[] }) {
  const ref = useRef<HTMLDivElement>(null);
  const [category, setCategory] = useState("all");
  const valid = rows.filter(
    (r) =>
      typeof r.latitude === "number" &&
      typeof r.longitude === "number" &&
      Math.abs(r.latitude) <= 90 &&
      Math.abs(r.longitude) <= 180,
  );
  useEffect(() => {
    if (!ref.current) return;
    const map = L.map(ref.current, { scrollWheelZoom: false }).setView(
      [39, 35],
      6,
    );
    L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
      attribution:
        '© <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
      maxZoom: 19,
    }).addTo(map);
    const points = valid.filter(
      (r) => category === "all" || r.locationCategory === category,
    );
    points.forEach((r) => {
      const popup = document.createElement("div");
      const title = document.createElement("strong");
      title.textContent = `${r.serialNumber} · ${r.kitName}`;
      popup.append(title);
      const text = document.createElement("p");
      text.textContent = `${r.recipientName || ""} — ${r.addressLine || ""}`;
      popup.append(text);
      L.circleMarker([r.latitude, r.longitude], {
        radius: 8,
        color: "#fff",
        weight: 3,
        fillColor: colors[r.locationCategory] || colors.active,
        fillOpacity: 1,
      })
        .bindPopup(popup)
        .addTo(map);
    });
    if (points.length)
      map.fitBounds(
        L.latLngBounds(points.map((r) => [r.latitude, r.longitude])),
        { padding: [40, 40], maxZoom: 12 },
      );
    const observer = new ResizeObserver(() => map.invalidateSize());
    observer.observe(ref.current);
    return () => {
      observer.disconnect();
      map.remove();
    };
  }, [rows, category]);
  return (
    <>
      <div className="map-toolbar">
        <div className="map-legend">
          {[
            ["all", "Tümü"],
            ["active", "Aktif"],
            ["faulty", "Arızalı"],
            ["returning", "İade Sürecinde"],
            ["expired", "Süresi Dolmuş"],
          ].map(([key, label]) => (
            <button
              className={category === key ? "selected" : ""}
              key={key}
              onClick={() => setCategory(key)}
            >
              <i style={{ background: colors[key] || "#244640" }} />
              {label}
            </button>
          ))}
        </div>
        <span>
          <MapPin size={14} />
          {valid.length} konum · {rows.length - valid.length} eksik
        </span>
      </div>
      <div ref={ref} className="kit-map" aria-label="Kit konumları haritası" />
      {!valid.length && (
        <p className="map-note">
          Koordinat bilgisi bulunan kitler burada gösterilir.
        </p>
      )}
    </>
  );
}
export function LocateButton({
  onLocation,
}: {
  onLocation: (latitude: number, longitude: number) => void;
}) {
  const notice = useNotice();
  const [busy, setBusy] = useState(false);
  return (
    <button
      type="button"
      className="secondary"
      disabled={busy}
      onClick={() => {
        if (!navigator.geolocation) {
          notice("Tarayıcınız konum bilgisini desteklemiyor.", true);
          return;
        }
        setBusy(true);
        navigator.geolocation.getCurrentPosition(
          (p) => {
            onLocation(p.coords.latitude, p.coords.longitude);
            setBusy(false);
            notice("Konum eklendi. Açık adresinizi de kontrol edin.");
          },
          () => {
            setBusy(false);
            notice(
              "Konum alınamadı. Açık adresinizle devam edebilirsiniz.",
              true,
            );
          },
          { timeout: 10000, enableHighAccuracy: true },
        );
      }}
    >
      <LocateFixed size={17} />
      {busy ? "Konum Alınıyor…" : "Konumumu Ekle"}
    </button>
  );
}
