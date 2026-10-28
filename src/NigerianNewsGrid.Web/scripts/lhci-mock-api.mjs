/**
 * Lighthouse CI mock NewsApi.
 *
 * A zero-dependency Node HTTP server that returns deterministic fixture data for the endpoints the SSR
 * home route needs. Lighthouse CI points `API_BASE_URL` at this server so audits measure the page as
 * real users see it (with articles and a hero image) instead of the "backend unreachable" fallback.
 *
 * Usage: node scripts/lhci-mock-api.mjs   (PORT defaults to 5055)
 */
import http from "node:http";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const PORT = Number(process.env.MOCK_API_PORT || process.env.PORT || 5055);
const here = path.dirname(fileURLToPath(import.meta.url));
const heroImage = readFileSync(path.join(here, "..", "public", "newsstand-masthead.png"));

const categories = ["Politics", "Business", "Technology", "Sports", "Entertainment", "International"];
const sources = ["Punch Newspaper", "Premium Times", "Vanguard News", "Thisday", "Channels Television"];
const baseTime = Date.parse("2026-10-28T06:00:00Z");

const articles = Array.from({ length: 24 }, (_, i) => {
  const category = categories[i % categories.length];
  const source = sources[i % sources.length];
  return {
    id: `lhci-${i + 1}`,
    slug: `lhci-fixture-article-${i + 1}`,
    title: `Fixture headline ${i + 1}: ${category} update from ${source}`,
    summary: "Deterministic fixture article used by Lighthouse CI to audit realistic page rendering.",
    description: "Deterministic fixture article used by Lighthouse CI to audit realistic page rendering.",
    url: `https://example.com/news/${i + 1}`,
    imageUrl: `https://example.com/images/${i + 1}.png`,
    source,
    sourceName: source,
    category,
    contentType: "Article",
    publishedAt: new Date(baseTime - i * 15 * 60 * 1000).toISOString(),
  };
});

const routes = {
  "/api/v1/articles": () => ({ articles, totalCount: articles.length, page: 1, pageSize: articles.length }),
  "/api/v1/articles/sponsored": () => [],
  "/api/v1/sources": () => sources.map((name) => ({ name, slug: name.toLowerCase().replace(/\s+/g, "-") })),
  "/api/v1/stories/trending": () => ({ totalCount: 0, stories: [] }),
  "/api/v1/stories": () => ({ totalCount: 0, stories: [] }),
  "/api/v1/video-stories": () => [],
  "/api/v1/video-stories/trending": () => [],
  "/api/v1/market": () => [],
  "/api/v1/auth/config": () => ({ googleClientId: "", emailOtpEnabled: false }),
};

const server = http.createServer((req, res) => {
  const { pathname } = new URL(req.url, `http://${req.headers.host}`);

  if (pathname === "/api/v1/img") {
    res.writeHead(200, { "Content-Type": "image/png", "Cache-Control": "public, max-age=86400" });
    res.end(heroImage);
    return;
  }

  if (req.method === "POST") {
    res.writeHead(204);
    res.end();
    return;
  }

  const handler = routes[pathname];
  res.writeHead(handler ? 200 : 404, { "Content-Type": "application/json" });
  res.end(JSON.stringify(handler ? handler() : { error: "Not mocked" }));
});

server.listen(PORT, "127.0.0.1", () => {
  console.log(`[lhci-mock-api] listening on http://127.0.0.1:${PORT}`);
});

