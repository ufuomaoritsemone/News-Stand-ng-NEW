import { textResponse, getSiteUrl } from "../utils/xml";

export async function loader() {
  const siteUrl = getSiteUrl();

  const robots = [
    "User-agent: *",
    "Allow: /",
    "Allow: /api/v1/img",
    "Allow: /api/img",
    "Allow: /api/v1/articles",
    "Allow: /api/v1/stories",
    "Allow: /api/v1/topics",
    "Allow: /api/v1/sources",
    "Allow: /api/v1/data",
    "Allow: /api/v1/settings/public",
    "Allow: /api/v1/briefings",
    "Disallow: /api/v1/auth/",
    "Disallow: /api/v1/admin/",
    "Disallow: /api/v1/push/",
    "Disallow: /search",
    "Disallow: /auth",
    "Disallow: /admin",
    "",
    "User-agent: AdsBot-Google",
    "User-agent: AdsBot-Google-Mobile",
    "Allow: /",
    "",
    `Sitemap: ${siteUrl}/sitemap.xml`,
    `Sitemap: ${siteUrl}/sitemap-news.xml`,
  ].join("\n");

  return textResponse(robots, 300);
}
