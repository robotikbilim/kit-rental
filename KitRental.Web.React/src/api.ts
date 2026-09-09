import { normalizeBaseUrl, problemMessage, collectPages } from "./lib/http.mjs";
import type { Session, Row } from "./domain";
declare global {
  interface Window {
    __KIT_RENTAL_CONFIG__?: { gatewayBaseUrl?: string };
  }
}
const configured =
  window.__KIT_RENTAL_CONFIG__?.gatewayBaseUrl ||
  import.meta.env.VITE_GATEWAY_BASE_URL ||
  localStorage.getItem("kit-rental.gateway") ||
  "";
export let gateway = configured;
export function setGateway(value: string) {
  gateway = normalizeBaseUrl(value);
  localStorage.setItem("kit-rental.gateway", gateway);
}
const sessionKey = () => `kit-rental.session:${gateway}`;
export function readSession(): Session | null {
  try {
    const s = JSON.parse(sessionStorage.getItem(sessionKey()) || "null");
    return s && Date.parse(s.expiresAt) > Date.now() ? s : null;
  } catch {
    return null;
  }
}
export function saveSession(value: Session | null) {
  value
    ? sessionStorage.setItem(sessionKey(), JSON.stringify(value))
    : sessionStorage.removeItem(sessionKey());
}
export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
  ) {
    super(message);
  }
}
export async function request<T = Row>(
  path: string,
  options: {
    method?: string;
    body?: unknown;
    signal?: AbortSignal;
    blob?: boolean;
  } = {},
): Promise<T> {
  if (!gateway)
    throw new ApiError("Önce gateway bağlantısını yapılandırın.", 0);
  const token = readSession()?.accessToken;
  let response: Response;
  try {
    response = await fetch(`${normalizeBaseUrl(gateway)}${path}`, {
      method: options.method || "GET",
      signal: options.signal,
      credentials: "omit",
      headers: {
        Accept: options.blob ? "*/*" : "application/json",
        ...(options.body !== undefined
          ? { "Content-Type": "application/json" }
          : {}),
        ...(token && !path.includes("/public/") && !path.endsWith("/login")
          ? { Authorization: `Bearer ${token}` }
          : {}),
      },
      body:
        options.body === undefined ? undefined : JSON.stringify(options.body),
    });
  } catch (error) {
    if ((error as Error).name === "AbortError") throw error;
    throw new ApiError(
      "Gateway bağlantısı kurulamadı. Adresi, ağ bağlantısını ve CORS ayarını kontrol edin.",
      0,
    );
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    if (
      response.status === 401 &&
      !path.includes("/public/") &&
      !path.endsWith("/login")
    ) {
      saveSession(null);
      window.dispatchEvent(new Event("session-expired"));
    }
    throw new ApiError(problemMessage(body, response.status), response.status);
  }
  if (options.blob) return (await response.blob()) as T;
  if (response.status === 204 || response.headers.get("content-length") === "0")
    return null as T;
  const text = await response.text();
  return (text ? JSON.parse(text) : null) as T;
}
export const core = (path: string) => `/core/api/${path}`;
export const mutate = (path: string, body: unknown = {}, method = "POST") =>
  request(path, { method, body: method === "DELETE" ? undefined : body });
export const all = (path: string, signal?: AbortSignal): Promise<Row[]> =>
  collectPages((page: number) =>
    request(
      `${path}${path.includes("?") ? "&" : "?"}page=${page}&pageSize=500`,
      { signal },
    ),
  );
export function download(blob: Blob, name: string) {
  const url = URL.createObjectURL(blob);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
export function imageUrl(value?: string) {
  if (!value) return undefined;
  if (value.startsWith("/images/") || value.startsWith("images/"))
    return `/${value.replace(/^\//, "")}`;
  try {
    const url = new URL(value, `${gateway}/`);
    return ["https:", "http:"].includes(url.protocol) ? url.href : undefined;
  } catch {
    return undefined;
  }
}
