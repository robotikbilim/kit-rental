import { Component, useEffect, useState, type ReactNode } from "react";
import {
  Link,
  NavLink,
  Navigate,
  Outlet,
  Route,
  Routes,
  useLocation,
  useNavigate,
} from "react-router-dom";
import {
  ArrowUpRight,
  Box,
  Boxes,
  ChevronDown,
  ClipboardList,
  Factory,
  FileClock,
  FolderHeart,
  History,
  LayoutDashboard,
  LogOut,
  Mail,
  Menu,
  Package,
  PanelLeftClose,
  Search,
  Settings2,
  ShieldCheck,
  ShoppingBag,
  SlidersHorizontal,
  Truck,
  Users,
  Wrench,
  X,
} from "lucide-react";
import { AuthProvider, Guard, useAuth } from "./auth";
import {
  customers,
  enums,
  managers,
  roleName,
  staff,
  warehouse,
} from "./domain";
import { resources } from "./resources";
import { Modal, Notifications } from "./ui";
import { Dashboard } from "./pages/Dashboard";
import { Connection, LinkLogo, Login } from "./pages/Login";
import { ResourcePage } from "./pages/ResourcePage";
import {
  BulkRental,
  ComponentDetail,
  CustomerDetail,
  KitDetail,
  Labels,
  ModelDetail,
  OrderDetail,
  PurchaseOrder,
  Returns,
  SupplyDetail,
} from "./pages/Details";
import { PortalKit, PortalList, PortalOrder } from "./pages/Portal";
import { PublicAddress, PublicForm, PublicQr } from "./pages/Public";
type NavItem = {
  path: string;
  label: string;
  icon: typeof Box;
  roles: string[];
  group: string;
};
const navigation: NavItem[] = [
  {
    path: "/",
    label: "Genel Bakış",
    icon: LayoutDashboard,
    roles: managers,
    group: "ÇALIŞMA ALANI",
  },
  {
    path: "/orders",
    label: "Siparişler",
    icon: ShoppingBag,
    roles: staff,
    group: "ÇALIŞMA ALANI",
  },
  {
    path: "/inventory",
    label: "Envanter",
    icon: Boxes,
    roles: staff,
    group: "ÇALIŞMA ALANI",
  },
  {
    path: "/kits",
    label: "Fiziksel Kitler",
    icon: Package,
    roles: warehouse,
    group: "ÇALIŞMA ALANI",
  },
  {
    path: "/lookup",
    label: "Kit Geçmişi",
    icon: History,
    roles: warehouse,
    group: "ÇALIŞMA ALANI",
  },
  {
    path: "/returns",
    label: "Gelen Teslimatlar",
    icon: Truck,
    roles: managers,
    group: "OPERASYON",
  },
  {
    path: "/faults",
    label: "Arıza Merkezi",
    icon: Wrench,
    roles: staff,
    group: "OPERASYON",
  },
  {
    path: "/supply",
    label: "İhtiyaç Listeleri",
    icon: ClipboardList,
    roles: warehouse,
    group: "OPERASYON",
  },
  {
    path: "/models",
    label: "Eğitim Kitleri",
    icon: FolderHeart,
    roles: warehouse,
    group: "KATALOG & DEPO",
  },
  {
    path: "/components",
    label: "Komponentler",
    icon: Box,
    roles: warehouse,
    group: "KATALOG & DEPO",
  },
  {
    path: "/manufacturing",
    label: "Üretilebilirlik",
    icon: Factory,
    roles: warehouse,
    group: "KATALOG & DEPO",
  },
  {
    path: "/locations",
    label: "Raf Düzeni",
    icon: SlidersHorizontal,
    roles: warehouse,
    group: "KATALOG & DEPO",
  },
  {
    path: "/customers",
    label: "Müşteriler",
    icon: Users,
    roles: managers,
    group: "YÖNETİM",
  },
  {
    path: "/guides",
    label: "Problem Rehberi",
    icon: FolderHeart,
    roles: managers,
    group: "YÖNETİM",
  },
  {
    path: "/emails",
    label: "E-posta Geçmişi",
    icon: Mail,
    roles: managers,
    group: "YÖNETİM",
  },
  {
    path: "/users",
    label: "Kullanıcılar",
    icon: ShieldCheck,
    roles: ["SystemAdmin"],
    group: "YÖNETİM",
  },
  {
    path: "/audit",
    label: "İşlem Geçmişi",
    icon: FileClock,
    roles: ["SystemAdmin", "Auditor"],
    group: "YÖNETİM",
  },
  {
    path: "/portal",
    label: "Genel Bakış",
    icon: LayoutDashboard,
    roles: customers,
    group: "MÜŞTERİ PORTALI",
  },
  {
    path: "/portal/orders",
    label: "Siparişlerim",
    icon: ShoppingBag,
    roles: customers,
    group: "MÜŞTERİ PORTALI",
  },
  {
    path: "/portal/kits",
    label: "Kitlerim",
    icon: Boxes,
    roles: customers,
    group: "MÜŞTERİ PORTALI",
  },
  {
    path: "/portal/faults",
    label: "Arıza Kayıtları",
    icon: Wrench,
    roles: customers,
    group: "MÜŞTERİ PORTALI",
  },
  {
    path: "/portal/returns",
    label: "İadelerim",
    icon: Truck,
    roles: customers,
    group: "MÜŞTERİ PORTALI",
  },
];
function Shell() {
  const auth = useAuth();
  const location = useLocation();
  const [collapsed, setCollapsed] = useState(
    localStorage.getItem("kit-rental.sidebar") === "collapsed",
  );
  const [mobile, setMobile] = useState(false);
  const [search, setSearch] = useState(false);
  const [query, setQuery] = useState("");
  const links = navigation.filter((n) => auth.can(n.roles));
  const current = [...links]
    .sort((a, b) => b.path.length - a.path.length)
    .find(
      (n) =>
        n.path === location.pathname ||
        (n.path !== "/" && location.pathname.startsWith(n.path + "/")),
    );
  useEffect(() => {
    setMobile(false);
    window.scrollTo(0, 0);
    document.title = `${current?.label || "Detay"} · KitRental`;
  }, [location.pathname, current?.label]);
  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if ((e.ctrlKey || e.metaKey) && e.key === "k") {
        e.preventDefault();
        setSearch((s) => !s);
      }
    };
    window.addEventListener("keydown", handler);
    return () => window.removeEventListener("keydown", handler);
  }, []);
  return (
    <div
      className={`app-shell ${collapsed ? "collapsed" : ""} ${mobile ? "mobile-open" : ""}`}
    >
      <a className="skip-link" href="#main-content">
        İçeriğe Geç
      </a>
      {mobile && (
        <button
          className="sidebar-backdrop"
          aria-label="Menüyü Kapat"
          onClick={() => setMobile(false)}
        />
      )}
      <aside className="sidebar">
        <Link
          className="brand-link"
          to={
            auth.isCustomer
              ? "/portal"
              : auth.can(managers)
                ? "/"
                : "/inventory"
          }
        >
          <LinkLogo />
        </Link>
        <div className="workspace-switch">
          <span className="workspace-icon">RB</span>
          <div>
            <strong>
              {auth.isCustomer ? "Müşteri Portalı" : "Operasyon Merkezi"}
            </strong>
            <small>Robotik Bilim</small>
          </div>
          <span className="status-dot" />
        </div>
        <nav aria-label="Ana Menü">
          {[...new Set(links.map((n) => n.group))].map((group) => (
            <div className="nav-group" key={group}>
              <span className="nav-label">{group}</span>
              {links
                .filter((n) => n.group === group)
                .map((n) => (
                  <NavLink
                    end={n.path === "/" || n.path === "/portal"}
                    to={n.path}
                    key={n.path}
                    title={n.label}
                  >
                    <n.icon size={19} />
                    <span>{n.label}</span>
                  </NavLink>
                ))}
            </div>
          ))}
        </nav>
        <div className="sidebar-bottom">
          <span className="sidebar-tip">
            <Box size={20} />
            <span>
              Her kit bir
              <br />
              <strong>keşif başlangıcı.</strong>
            </span>
          </span>
          <button
            onClick={() => {
              setCollapsed(!collapsed);
              localStorage.setItem(
                "kit-rental.sidebar",
                collapsed ? "expanded" : "collapsed",
              );
            }}
            title="Menüyü Daralt / Genişlet"
          >
            <PanelLeftClose size={18} />
            <span>Menüyü Daralt</span>
          </button>
        </div>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <div className="breadcrumb">
            <button
              className="icon-button mobile-menu"
              aria-label="Menüyü Aç"
              aria-expanded={mobile}
              onClick={() => setMobile(!mobile)}
            >
              <Menu />
            </button>
            <span>{auth.isCustomer ? "Müşteri Portalı" : "Çalışma Alanı"}</span>
            <span>/</span>
            <strong>{current?.label || "Detay"}</strong>
          </div>
          <div className="topbar-right">
            <button className="global-search" onClick={() => setSearch(true)}>
              <Search size={17} />
              <span>Bir sayfa bulun…</span>
              <kbd>Ctrl K</kbd>
            </button>
            <div className="user-avatar">
              {(auth.session?.user.displayName || "K")
                .split(" ")
                .slice(0, 2)
                .map((n) => n[0])
                .join("")}
            </div>
            <div className="user-info">
              <strong>{auth.session?.user.displayName}</strong>
              <small>
                {enums.role[Number(auth.session?.user.role)] ||
                  roleName(auth.session!.user.role)}
              </small>
            </div>
            <button
              className="icon-button"
              title="Çıkış Yap"
              aria-label="Çıkış Yap"
              onClick={auth.logout}
            >
              <LogOut size={18} />
            </button>
          </div>
        </header>
        <main id="main-content" className="main-content">
          <Outlet />
        </main>
        <footer className="workspace-footer">
          <span>KitRental · Robotik Bilim</span>
          <span>Bir sonraki keşfe hazır.</span>
        </footer>
      </div>
      {search && (
        <Modal
          title="Çalışma Alanında Gezinin"
          onClose={() => setSearch(false)}
        >
          <label className="search-input">
            <Search size={18} />
            <input
              autoFocus
              placeholder="Siparişler, kitler, müşteriler…"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
          </label>
          <div className="command-results">
            {links
              .filter((n) =>
                n.label
                  .toLocaleLowerCase("tr")
                  .includes(query.toLocaleLowerCase("tr")),
              )
              .map((n) => (
                <Link
                  key={n.path}
                  to={n.path}
                  onClick={() => {
                    setSearch(false);
                    setQuery("");
                  }}
                >
                  <n.icon size={19} />
                  {n.label}
                  <ArrowUpRight size={16} />
                </Link>
              ))}
          </div>
        </Modal>
      )}
    </div>
  );
}
function Home() {
  const auth = useAuth();
  return auth.isCustomer ? (
    <Navigate to="/portal" replace />
  ) : auth.can(managers) ? (
    <Dashboard />
  ) : (
    <Navigate to="/inventory" replace />
  );
}
class ErrorBoundary extends Component<
  { children: ReactNode },
  { error: boolean }
> {
  state = { error: false };
  static getDerivedStateFromError() {
    return { error: true };
  }
  render() {
    return this.state.error ? (
      <div className="empty">
        <h1>Ekran Açılamadı</h1>
        <p>Sayfayı yeniden yükleyerek tekrar deneyin.</p>
        <button onClick={() => window.location.reload()}>Yeniden Yükle</button>
      </div>
    ) : (
      this.props.children
    );
  }
}
export default function App() {
  return (
    <ErrorBoundary>
      <Notifications>
        <AuthProvider>
          <Routes>
            <Route path="/login" element={<Login />} />
            <Route path="/adres/:token" element={<PublicAddress />} />
            <Route path="/ariza/:qrCode" element={<PublicQr />} />
            <Route path="/ariza/form/:token" element={<PublicForm />} />
            <Route path="/ariza/form/:token/:action" element={<PublicForm />} />
            <Route
              element={
                <Guard>
                  <Shell />
                </Guard>
              }
            >
              <Route index element={<Home />} />
              {Object.entries(resources).map(([name, config]) => (
                <Route
                  key={name}
                  path={name}
                  element={
                    <Guard roles={config.roles}>
                      <ResourcePage key={name} name={name} />
                    </Guard>
                  }
                />
              ))}
              <Route
                path="orders/:id"
                element={
                  <Guard roles={staff}>
                    <OrderDetail />
                  </Guard>
                }
              />
              <Route
                path="kits/:id"
                element={
                  <Guard roles={warehouse}>
                    <KitDetail />
                  </Guard>
                }
              />
              <Route
                path="kits/bulk-rental"
                element={
                  <Guard roles={managers}>
                    <BulkRental />
                  </Guard>
                }
              />
              <Route
                path="lookup"
                element={
                  <Guard roles={warehouse}>
                    <KitDetail lookup />
                  </Guard>
                }
              />
              <Route
                path="models/:id"
                element={
                  <Guard roles={warehouse}>
                    <ModelDetail />
                  </Guard>
                }
              />
              <Route
                path="manufacturing/:id"
                element={
                  <Guard roles={warehouse}>
                    <ModelDetail manufacturing />
                  </Guard>
                }
              />
              <Route
                path="components/:id"
                element={
                  <Guard roles={warehouse}>
                    <ComponentDetail />
                  </Guard>
                }
              />
              <Route
                path="supply/:id"
                element={
                  <Guard roles={warehouse}>
                    <SupplyDetail />
                  </Guard>
                }
              />
              <Route
                path="customers/:id"
                element={
                  <Guard roles={managers}>
                    <CustomerDetail />
                  </Guard>
                }
              />
              <Route
                path="purchase-order"
                element={
                  <Guard roles={managers}>
                    <PurchaseOrder />
                  </Guard>
                }
              />
              <Route
                path="returns"
                element={
                  <Guard roles={managers}>
                    <Returns />
                  </Guard>
                }
              />
              <Route
                path="labels/:kind/:id"
                element={
                  <Guard roles={warehouse}>
                    <Labels />
                  </Guard>
                }
              />
              <Route
                path="portal"
                element={
                  <Guard roles={customers}>
                    <Dashboard />
                  </Guard>
                }
              />
              {(["orders", "kits", "faults", "returns"] as const).map(
                (kind) => (
                  <Route
                    key={kind}
                    path={`portal/${kind}`}
                    element={
                      <Guard roles={customers}>
                        <PortalList key={kind} kind={kind} />
                      </Guard>
                    }
                  />
                ),
              )}
              <Route
                path="portal/orders/:id"
                element={
                  <Guard roles={customers}>
                    <PortalOrder />
                  </Guard>
                }
              />
              <Route
                path="portal/kits/:id"
                element={
                  <Guard roles={customers}>
                    <PortalKit />
                  </Guard>
                }
              />
              <Route
                path="*"
                element={
                  <div className="empty">
                    <h1>Bu Sayfa Bulunamadı</h1>
                    <Link to="/">Çalışma Alanına Dön</Link>
                  </div>
                }
              />
            </Route>
          </Routes>
        </AuthProvider>
      </Notifications>
    </ErrorBoundary>
  );
}
