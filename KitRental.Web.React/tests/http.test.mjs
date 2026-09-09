import test from "node:test";
import assert from "node:assert/strict";
import {
  normalizeBaseUrl,
  problemMessage,
  collectPages,
} from "../src/lib/http.mjs";
test("gateway base supports HTTPS and deployment path prefixes", () => {
  assert.equal(
    normalizeBaseUrl("https://gateway.example.com/rental/"),
    "https://gateway.example.com/rental",
  );
  assert.equal(
    normalizeBaseUrl("http://localhost:61328/"),
    "http://localhost:61328",
  );
});
test("rejects unsafe protocols, embedded credentials, queries and fragments", () => {
  for (const value of [
    "javascript:alert(1)",
    "file:///tmp",
    "https://user:pass@example.com",
    "https://example.com?token=x",
    "https://example.com#x",
    "",
  ])
    assert.throws(() => normalizeBaseUrl(value));
});
test("surfaces model validation, domain errors and authorization separately", () => {
  assert.equal(
    problemMessage(
      { errors: { Name: ["Ad gerekli."], Phone: ["Telefon geçersiz."] } },
      400,
    ),
    "Ad gerekli. Telefon geçersiz.",
  );
  assert.equal(
    problemMessage({ detail: "Sipariş onaylanmış." }, 409),
    "Sipariş onaylanmış.",
  );
  assert.match(problemMessage(null, 403), /yetkiniz/);
});
test("exports and support lists collect all pages without silently truncating records", async () => {
  const requested = [];
  const rows = await collectPages(async (page) => {
    requested.push(page);
    return { items: [{ id: page }], totalPages: 3 };
  });
  assert.deepEqual(requested, [1, 2, 3]);
  assert.deepEqual(rows, [{ id: 1 }, { id: 2 }, { id: 3 }]);
});
test("a failed export page rejects instead of returning incomplete records", async () => {
  await assert.rejects(
    collectPages(async (page) => {
      if (page === 2) throw new Error("offline");
      return { items: [1], totalPages: 2 };
    }),
    /offline/,
  );
});
