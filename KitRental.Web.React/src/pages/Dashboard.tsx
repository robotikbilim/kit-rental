import {
  ArrowDownLeft,
  ArrowRight,
  ArrowUpRight,
  Box,
  Boxes,
  CheckCheck,
  Clock3,
  MapPin,
  PackageCheck,
  Plus,
  RotateCcw,
  ShoppingBag,
  TriangleAlert,
  Users,
} from "lucide-react";
import { Link } from "react-router-dom";
import { core, mutate } from "../api";
import { useAuth } from "../auth";
import { date, number } from "../domain";
import { KitMap } from "../KitMap";
import {
  ConfirmAction,
  Heading,
  Panel,
  State,
  Stat,
  Table,
  useApi,
} from "../ui";
export function Dashboard() {
  const auth = useAuth();
  const portal = auth.isCustomer;
  const state = useApi(core(portal ? "customer-portal" : "dashboard"));
  const d = state.data;
  const metrics = portal
    ? [
        ["Toplam Kiralık Kit", d?.totalRentedKitCount, Boxes, "/portal/kits"],
        [
          "Öğrenciye Atanan",
          d?.activeKitCount,
          Users,
          "/portal/kits?assignmentState=assigned",
        ],
        [
          "Atanmayan Kit",
          d?.unassignedKitCount,
          Box,
          "/portal/kits?assignmentState=unassigned",
        ],
        [
          "Açık Arıza",
          d?.openFaultCount,
          TriangleAlert,
          "/portal/faults?state=open",
        ],
        [
          "Çözülen Arıza",
          d?.completedFaultCount,
          CheckCheck,
          "/portal/faults?state=closed",
        ],
        [
          "İade Bekleyen",
          d?.expiredRentalKitCount,
          Clock3,
          "/portal/kits?expiry=expired",
        ],
        [
          "İade Sürecinde",
          d?.returnProcessStartedKitCount,
          RotateCcw,
          "/portal/returns",
        ],
        [
          "İade Edilen",
          d?.returnedKitCount,
          PackageCheck,
          "/portal/kits?returned=true",
        ],
      ]
    : [
        ["Toplam Fiziksel Kit", d?.productUnits, Boxes, "/inventory"],
        [
          "Kiralamadaki Kit",
          d?.rentedKits,
          ArrowUpRight,
          "/inventory?status=5",
        ],
        [
          "Kullanıma Hazır",
          d?.availableKits,
          PackageCheck,
          "/inventory?status=1",
        ],
        ["Aktif Sipariş", d?.activeOrders, ShoppingBag, "/orders"],
      ];
  return (
    <>
      <Heading
        eyebrow={portal ? "MÜŞTERİ PORTALI" : "HER ŞEY KONTROL ALTINDA"}
        title={`Merhaba, ${auth.session?.user.displayName?.split(" ")[0] || "hoş geldiniz"} 👋`}
        description={`${date(new Date().toISOString())} · ${portal ? "Kitlerinizin ve siparişlerinizin güncel durumu." : "Kit operasyonunuzun bugünkü görünümü."}`}
        actions={
          <Link
            className="button"
            to={portal ? "/portal/orders" : "/orders?new=1"}
          >
            <Plus size={18} />
            Yeni Sipariş
          </Link>
        }
      />
      <State {...state} retry={state.reload}>
        {d && (
          <>
            <div className="overview-banner">
              <div>
                <span className="eyebrow">
                  {portal ? d.customerName : "KİTRENТAL / ÇALIŞMA ALANI"}
                </span>
                <h2>Bir kit, binlerce keşif.</h2>
                <p>
                  {portal
                    ? "Öğrencilerinize ulaşan her kitin yolculuğunu buradan takip edin."
                    : "Öğrenme yolculuğunun arkasındaki tüm operasyon, tek bir yerde."}
                </p>
                <Link to={portal ? "/portal/kits" : "/inventory"}>
                  Kitleri Keşfet <ArrowRight size={17} />
                </Link>
              </div>
              <div className="banner-cubes" aria-hidden="true">
                <Box />
                <Box />
                <Box />
              </div>
            </div>
            <div className={`stats-grid ${portal ? "portal-stats" : ""}`}>
              {metrics.map(([label, value, Icon, href], i) => {
                const Component = Icon as typeof Boxes;
                return (
                  <Stat
                    key={String(label)}
                    label={String(label)}
                    value={value}
                    href={String(href)}
                    icon={<Component size={21} />}
                    tone={["mint", "blue", "peach", "lilac"][i % 4]}
                  />
                );
              })}
            </div>
            <div className="dashboard-columns">
              <Panel
                title="İlginizi Bekleyenler"
                subtitle="Bir sonraki adımı birlikte tamamlayalım."
                className="attention-panel"
              >
                {(portal
                  ? [
                      [
                        "Açık Arızalar",
                        d.openFaultCount,
                        "/portal/faults",
                        TriangleAlert,
                      ],
                      [
                        "İade Sürecindeki Kitler",
                        d.returnProcessStartedKitCount,
                        "/portal/returns",
                        RotateCcw,
                      ],
                      [
                        "Atama Bekleyen Kitler",
                        d.unassignedKitCount,
                        "/portal/kits?assignmentState=unassigned",
                        Users,
                      ],
                    ]
                  : [
                      [
                        "Onay Bekleyen Siparişler",
                        d.ordersAwaitingApproval,
                        "/orders",
                        ShoppingBag,
                      ],
                      [
                        "Gelen Teslimatlar",
                        d.returnsInProgress.length,
                        "/returns",
                        ArrowDownLeft,
                      ],
                      [
                        "Süresi Dolan Kiralamalar",
                        d.expiredRentalKits.length,
                        "/inventory?rentalExpiry=expired",
                        Clock3,
                      ],
                      [
                        "Yaklaşan Kiralama Bitişleri",
                        d.expiringRentalKits.length,
                        "/inventory?rentalExpiry=upcoming",
                        Clock3,
                      ],
                    ]
                ).map(([label, value, href, Icon]) => {
                  const Component = Icon as typeof Clock3;
                  return (
                    <Link
                      className="attention-row"
                      to={String(href)}
                      key={String(label)}
                    >
                      <span className="attention-icon">
                        <Component size={19} />
                      </span>
                      <span>{String(label)}</span>
                      <strong>{number(value)}</strong>
                      <ArrowRight size={16} />
                    </Link>
                  );
                })}
              </Panel>
              <Panel title="Kit Dağılımı" subtitle="Operasyonun anlık dengesi.">
                <div className="distribution">
                  {(portal
                    ? [
                        ["Öğrenciye Atanan", d.activeKitCount, "#438f78"],
                        ["Atama Bekleyen", d.unassignedKitCount, "#a3bd76"],
                        [
                          "İade Sürecinde",
                          d.returnProcessStartedKitCount,
                          "#87a5d5",
                        ],
                        ["İade Edilen", d.returnedKitCount, "#ddd6c9"],
                      ]
                    : [
                        ["Müşteride", d.rentedKits, "#438f78"],
                        ["Hazır", d.availableKits, "#a3bd76"],
                        ["Arızalı", d.faultyKits, "#df9984"],
                        ["Hazırlanıyor", d.preparingKits, "#87a5d5"],
                        ["Satılan", d.soldKits, "#ddd6c9"],
                      ]
                  ).map(([label, value, color]) => (
                    <div key={String(label)}>
                      <div>
                        <span>
                          <i style={{ background: String(color) }} />
                          {label}
                        </span>
                        <strong>{number(value)}</strong>
                      </div>
                      <div className="bar-track">
                        <div
                          style={{
                            width: `${Math.min(100, (Number(value) / Math.max(1, portal ? d.totalRentedKitCount + d.returnedKitCount : d.productUnits)) * 100)}%`,
                            background: String(color),
                          }}
                        />
                      </div>
                    </div>
                  ))}
                </div>
              </Panel>
            </div>
            {portal && (
              <Panel
                title="Son Siparişler"
                actions={
                  <Link className="subtle-link" to="/portal/orders">
                    Tümünü Gör <ArrowRight size={15} />
                  </Link>
                }
              >
                <Table
                  rows={d.rentalCohorts.slice(0, 5)}
                  search={false}
                  columns={[
                    { key: "name", label: "Dönem" },
                    { key: "orderNumber", label: "Sipariş No" },
                    { key: "studentCount", label: "Öğrenci" },
                    { key: "orderStatus", label: "Durum", kind: "order" },
                  ]}
                  actions={(r) => (
                    <Link className="row-link" to={`/portal/orders/${r.id}`}>
                      Detay
                    </Link>
                  )}
                />
              </Panel>
            )}
            <Panel
              title="Kitler Nerede?"
              subtitle="Eğitimin ulaştığı her noktayı görün."
              actions={
                !portal ? (
                  <ConfirmAction
                    label="Konumları Güncelle"
                    icon={<MapPin size={15} />}
                    description="Adresi olan ve koordinatı eksik kitler için konum güncelleme işi başlatılacak."
                    onConfirm={async () => {
                      const result = await mutate(
                        core("dashboard/kit-location-geocoding-jobs"),
                      );
                      if (!result.isConfigured)
                        throw new Error(
                          "Sunucuda konum çözümleme servisi yapılandırılmamış.",
                        );
                    }}
                  />
                ) : undefined
              }
            >
              <KitMap rows={d.kitLocations} />
            </Panel>
          </>
        )}
      </State>
    </>
  );
}
