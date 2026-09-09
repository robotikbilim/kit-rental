import {
  createContext,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import { Navigate, useLocation } from "react-router-dom";
import { readSession, request, saveSession } from "./api";
import { customers, roleName, type Session } from "./domain";
const Context = createContext<{
  session: Session | null;
  login: (email: string, password: string) => Promise<void>;
  logout: () => void;
  can: (roles: string[]) => boolean;
  isCustomer: boolean;
}>({
  session: null,
  login: async () => {},
  logout: () => {},
  can: () => false,
  isCustomer: false,
});
export const useAuth = () => useContext(Context);
export function AuthProvider({ children }: { children: ReactNode }) {
  const [session, setSession] = useState<Session | null>(readSession);
  function logout() {
    saveSession(null);
    setSession(null);
  }
  useEffect(() => {
    window.addEventListener("session-expired", logout);
    return () => window.removeEventListener("session-expired", logout);
  }, []);
  useEffect(() => {
    if (!session) return;
    const timeout = setTimeout(
      logout,
      Math.max(0, Date.parse(session.expiresAt) - Date.now()),
    );
    return () => clearTimeout(timeout);
  }, [session]);
  return (
    <Context.Provider
      value={{
        session,
        logout,
        login: async (email, password) => {
          const s = await request<Session>("/identity/api/auth/login", {
            method: "POST",
            body: { email, password },
          });
          saveSession(s);
          setSession(s);
        },
        can: (allowed) =>
          !!session && allowed.includes(roleName(session.user.role)),
        isCustomer:
          !!session && customers.includes(roleName(session.user.role)),
      }}
    >
      {children}
    </Context.Provider>
  );
}
export function Guard({
  children,
  roles,
}: {
  children: ReactNode;
  roles?: string[];
}) {
  const auth = useAuth();
  const location = useLocation();
  if (!auth.session)
    return (
      <Navigate
        to="/login"
        state={{ from: location.pathname + location.search }}
        replace
      />
    );
  if (roles && !auth.can(roles))
    return (
      <div className="empty">
        <h1>Erişim Yetkisi Gerekli</h1>
        <p>Bu ekran hesabınızın yetkileri dışında.</p>
      </div>
    );
  return <>{children}</>;
}
