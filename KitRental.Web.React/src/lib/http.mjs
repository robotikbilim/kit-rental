export function normalizeBaseUrl(value) {
  const url = new URL(value);
  if (
    !["http:", "https:"].includes(url.protocol) ||
    url.username ||
    url.password ||
    url.search ||
    url.hash
  )
    throw new Error("Geçerli bir HTTP(S) gateway adresi girin.");
  return url.href.replace(/\/$/, "");
}
export function problemMessage(body, status) {
  if (body?.errors) return Object.values(body.errors).flat().join(" ");
  return (
    body?.detail ||
    body?.title ||
    {
      401: "Oturumunuz sona erdi. Lütfen yeniden giriş yapın.",
      403: "Bu işlem için yetkiniz bulunmuyor.",
      404: "Kayıt bulunamadı.",
      409: "Kayıt değişti. Sayfayı yenileyip tekrar deneyin.",
    }[status] ||
    `İşlem tamamlanamadı (${status}).`
  );
}
export async function collectPages(fetchPage) {
  const first = await fetchPage(1);
  if (Array.isArray(first)) return first;
  const rows = [...(first?.items ?? [])];
  for (let page = 2; page <= (first?.totalPages ?? 1); page++)
    rows.push(...(await fetchPage(page)).items);
  return rows;
}
