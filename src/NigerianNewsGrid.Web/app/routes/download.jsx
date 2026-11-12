import React, { useState, useEffect } from "react";
import { Link, useLoaderData, useSearchParams } from "react-router";
import Footer from "../../src/components/Footer";
import IosInstallModal from "../../src/components/IosInstallModal";
import { usePwaInstall } from "../../src/hooks/usePwaInstall";
import { api } from "../../src/services/api";
import { trackAppStoreClick } from "../../src/services/analytics";
import {
  ArrowLeft,
  Download,
  Smartphone,
  Monitor,
  Zap,
  WifiOff,
  Bell,
  Headphones,
  CheckCircle2,
  Clock,
  Sparkles,
  ExternalLink,
  Share2,
  Copy,
  Check,
  QrCode,
  ShieldCheck,
  TrendingUp,
} from "lucide-react";

export function meta() {
  const title = "Download News Stand NG App — Android, iOS, Windows & Mac";
  const description =
    "Download the official News Stand NG mobile and desktop app on Google Play Store, Apple App Store, Windows, and macOS. Get real-time Nigerian breaking news, AI summaries, audio briefings, and live Naira FX alerts.";
  const canonicalUrl = "https://newsstand.ng/download";

  return [
    { title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    {
      name: "robots",
      content: "index, follow, max-image-preview:large, max-snippet:-1",
    },
    { property: "og:type", content: "website" },
    { property: "og:site_name", content: "News Stand NG" },
    { property: "og:title", content: title },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { property: "og:image", content: "https://newsstand.ng/icon-512.png" },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: title },
    { name: "twitter:description", content: description },
    { name: "twitter:image", content: "https://newsstand.ng/icon-512.png" },
  ];
}

function detectOsFromUserAgent(ua = "") {
  const str = String(ua || "");
  if (/android/i.test(str)) return "android";
  if (/iPad|iPhone|iPod/i.test(str)) return "ios";
  if (/macintosh|mac os x/i.test(str)) return "mac";
  if (/windows/i.test(str)) return "windows";
  return "android"; // Default mobile-first priority for Nigeria
}

export async function loader({ request }) {
  const ua = request?.headers?.get("user-agent") || "";
  const detectedOs = detectOsFromUserAgent(ua);

  try {
    const appStores = await api.getAppStoreLinks();
    return { appStores, detectedOs };
  } catch {
    return {
      appStores: {
        androidUrl: "",
        iosUrl: "",
        windowsUrl: "",
        macUrl: "",
        hasAndroid: false,
        hasIos: false,
        hasWindows: false,
        hasMac: false,
        hasAnyStore: false,
      },
      detectedOs,
    };
  }
}

/* ── Official Store Brand SVG Icons ── */
function GooglePlayIcon({ size = 24 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path
        d="M3.609 1.814L13.792 12 3.61 22.186a1.75 1.75 0 0 1-.61-1.34V3.154c0-.518.227-.998.609-1.34z"
        fill="#32BBFF"
      />
      <path
        d="M17.196 8.596L13.792 12l3.404 3.404 3.842-2.185c.948-.54.948-1.898 0-2.438l-3.842-2.185z"
        fill="#FFCE00"
      />
      <path
        d="M13.792 12L3.609 22.186c.42.376 1.035.47 1.576.162l12.011-6.944L13.792 12z"
        fill="#F43249"
      />
      <path
        d="M13.792 12l3.404-3.404L5.185 1.652a1.425 1.425 0 0 0-1.576.162L13.792 12z"
        fill="#00EE76"
      />
    </svg>
  );
}

function AppleLogoIcon({ size = 24 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="currentColor" aria-hidden="true">
      <path d="M17.05 20.28c-.98.95-2.05.8-3.08.35-1.09-.46-2.09-.48-3.24 0-1.44.62-2.2.44-3.06-.35C2.79 15.25 3.51 7.59 9.05 7.31c1.35.07 2.29.74 3.08.8 1.18-.24 2.31-.93 3.57-.84 1.51.12 2.65.72 3.4 1.8-3.12 1.87-2.38 5.98.48 7.13-.57 1.5-1.31 2.99-2.54 4.09l.01-.01zM12.03 7.25c-.15-2.23 1.66-4.07 3.74-4.25.29 2.58-2.34 4.5-3.74 4.25z" />
    </svg>
  );
}

function WindowsLogoIcon({ size = 24 }) {
  return (
    <svg width={size} height={size} viewBox="0 0 24 24" fill="none" aria-hidden="true">
      <path d="M2 3.5L10.9 2.2V11.4H2V3.5Z" fill="#00A4EF" />
      <path d="M12.1 2.05L22 0.6V11.4H12.1V2.05Z" fill="#7FBA00" />
      <path d="M2 12.6H10.9V21.8L2 20.5V12.6Z" fill="#F25022" />
      <path d="M12.1 12.6H22V23.4L12.1 21.95V12.6Z" fill="#FFB900" />
    </svg>
  );
}

export default function DownloadAppsPage() {
  const loaderData = useLoaderData();
  const [searchParams] = useSearchParams();

  const [appStores, setAppStores] = useState(
    loaderData?.appStores || {
      androidUrl: "",
      iosUrl: "",
      windowsUrl: "",
      macUrl: "",
      hasAndroid: false,
      hasIos: false,
      hasWindows: false,
      hasMac: false,
      hasAnyStore: false,
    }
  );
  const [detectedOs, setDetectedOs] = useState(loaderData?.detectedOs || "android");
  const [copiedLink, setCopiedLink] = useState(false);
  const [redirectingToStore, setRedirectingToStore] = useState(false);

  const {
    isInstalled,
    promptInstall,
    showIosModal,
    setShowIosModal,
  } = usePwaInstall();

  // Client-side OS refinement & hot-reloaded settings fetch
  useEffect(() => {
    if (typeof window !== "undefined") {
      const clientOs = detectOsFromUserAgent(window.navigator.userAgent);
      setDetectedOs(clientOs);
    }

    api
      .getAppStoreLinks()
      .then((fresh) => {
        if (fresh && typeof fresh === "object") {
          setAppStores(fresh);
        }
      })
      .catch(() => {});
  }, []);

  const platforms = [
    {
      id: "android",
      name: "Android",
      storeName: "Google Play Store",
      badgeTopText: "GET IT ON",
      badgeMainText: "Google Play",
      requirements: "Android 8.0+ · Phones & Tablets",
      url: (appStores?.androidUrl || "").trim(),
      isConfigured: Boolean((appStores?.androidUrl || "").trim()),
      accentColor: "#10b981",
      icon: <GooglePlayIcon size={28} />,
      description:
        "Official Android app with instant push notifications, background audio briefings, and offline story caching.",
    },
    {
      id: "ios",
      name: "iPhone & iPad",
      storeName: "Apple App Store",
      badgeTopText: "Download on the",
      badgeMainText: "App Store",
      requirements: "iOS 15.0+ / iPadOS 15.0+",
      url: (appStores?.iosUrl || "").trim(),
      isConfigured: Boolean((appStores?.iosUrl || "").trim()),
      accentColor: "#38bdf8",
      icon: <AppleLogoIcon size={28} />,
      description:
        "Designed for iOS and iPadOS with lock-screen breaking alerts, CarPlay audio briefings, and dark mode.",
    },
    {
      id: "windows",
      name: "Windows PC",
      storeName: "Microsoft Store",
      badgeTopText: "Get it from",
      badgeMainText: "Microsoft",
      requirements: "Windows 10 & Windows 11 (64-bit)",
      url: (appStores?.windowsUrl || "").trim(),
      isConfigured: Boolean((appStores?.windowsUrl || "").trim()),
      accentColor: "#00a4ef",
      icon: <WindowsLogoIcon size={26} />,
      description:
        "Desktop news command center with live multi-source clusters, Naira FX charts, and native taskbar notifications.",
    },
    {
      id: "mac",
      name: "macOS",
      storeName: "Mac App Store",
      badgeTopText: "Download on the",
      badgeMainText: "Mac App Store",
      requirements: "macOS 12+ (Apple Silicon & Intel)",
      url: (appStores?.macUrl || "").trim(),
      isConfigured: Boolean((appStores?.macUrl || "").trim()),
      accentColor: "#a855f7",
      icon: <AppleLogoIcon size={28} />,
      description:
        "Native macOS experience optimized for Apple M-series chips with menu bar FX ticker and instant search.",
    },
  ];

  const primaryPlatform =
    platforms.find((p) => p.id === detectedOs) || platforms[0];

  // Sponsored campaign ?auto=1 or ?redirect=1 handler
  useEffect(() => {
    const autoRedirect =
      searchParams.get("auto") === "1" ||
      searchParams.get("redirect") === "1" ||
      searchParams.get("auto") === "true";

    if (!autoRedirect || typeof window === "undefined") return;

    const target = platforms.find((p) => p.id === detectedOs);
    if (target && target.isConfigured && target.url) {
      setRedirectingToStore(true);
      trackAppStoreClick(target.id, target.url, "sponsored_auto_redirect");
      const timer = setTimeout(() => {
        window.location.replace(target.url);
      }, 650);
      return () => clearTimeout(timer);
    }
  }, [searchParams, detectedOs, appStores]);

  const handleStoreButtonClick = (platform, sourcePlacement = "store_grid") => {
    if (platform.isConfigured && platform.url) {
      trackAppStoreClick(platform.id, platform.url, sourcePlacement);
    }
  };

  const handleCopyDownloadLink = () => {
    if (typeof window === "undefined") return;
    const shareUrl = "https://newsstand.ng/download";
    navigator.clipboard?.writeText(shareUrl).then(() => {
      setCopiedLink(true);
      setTimeout(() => setCopiedLink(false), 2500);
    });
  };

  const handleNativeShare = async () => {
    if (typeof window === "undefined") return;
    const shareData = {
      title: "Download News Stand NG App",
      text: "Get real-time Nigerian breaking news, AI 60-second summaries, audio briefings, and live Naira FX rates on the News Stand NG app.",
      url: "https://newsstand.ng/download",
    };
    if (navigator.share) {
      try {
        await navigator.share(shareData);
        return;
      } catch {
        // Fallback to copy
      }
    }
    handleCopyDownloadLink();
  };

  const downloadJsonLd = [
    {
      "@context": "https://schema.org",
      "@type": "SoftwareApplication",
      name: "News Stand NG",
      operatingSystem: "Android, iOS, Windows, macOS",
      applicationCategory: "NewsApplication",
      description:
        "Real-time Nigerian news aggregator delivering verified breaking headlines, AI executive summaries, daily Nigerian English audio briefings, and live Naira FX trackers.",
      url: "https://newsstand.ng/download",
      offers: {
        "@type": "Offer",
        price: "0",
        priceCurrency: "NGN",
      },
      publisher: {
        "@type": "NewsMediaOrganization",
        name: "News Stand NG",
        url: "https://newsstand.ng",
      },
    },
    {
      "@context": "https://schema.org",
      "@type": "BreadcrumbList",
      itemListElement: [
        {
          "@type": "ListItem",
          position: 1,
          name: "Home",
          item: "https://newsstand.ng",
        },
        {
          "@type": "ListItem",
          position: 2,
          name: "Download Apps",
          item: "https://newsstand.ng/download",
        },
      ],
    },
  ];

  return (
    <div className="static-page-layout">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(downloadJsonLd) }}
      />

      {/* Top Navigation Header */}
      <header className="article-page-header">
        <div className="article-header-container">
          <Link to="/" className="article-header-back-btn" title="Back to Headlines">
            <ArrowLeft size={18} />
            <span>Headlines</span>
          </Link>

          <Link to="/" className="article-header-brand">
            <span className="brand-dot" />
            <span className="brand-title">News Stand NG</span>
          </Link>

          <button
            type="button"
            onClick={handleNativeShare}
            className="article-header-back-btn"
            style={{ cursor: "pointer", background: "transparent" }}
            title="Share Download Page"
          >
            <Share2 size={16} />
            <span>Share</span>
          </button>
        </div>
      </header>

      <main
        className="static-policy-container"
        style={{ maxWidth: 1120, paddingBottom: 64 }}
      >
        {/* Breadcrumb */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li>
              <Link to="/">Home</Link>
            </li>
            <li>
              <span className="breadcrumb-sep">/</span>
            </li>
            <li className="breadcrumb-active" aria-current="page">
              Download Mobile &amp; Desktop Apps
            </li>
          </ol>
        </nav>

        {/* Auto-Redirect Notice Banner (when ?auto=1 is used in Sponsored Campaigns) */}
        {redirectingToStore && primaryPlatform.isConfigured && (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              flexWrap: "wrap",
              gap: 12,
              padding: "14px 20px",
              marginBottom: 24,
              borderRadius: 14,
              background: "rgba(16, 185, 129, 0.14)",
              border: "1px solid rgba(16, 185, 129, 0.4)",
              color: "#ecfdf5",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: 10 }}>
              <Sparkles size={18} style={{ color: "#10b981" }} />
              <span>
                Redirecting you to <strong>{primaryPlatform.storeName}</strong> for{" "}
                <strong>{primaryPlatform.name}</strong>...
              </span>
            </div>
            <a
              href={primaryPlatform.url}
              style={{
                color: "#10b981",
                fontWeight: 700,
                textDecoration: "underline",
                fontSize: "0.9rem",
              }}
            >
              Click here if not redirected
            </a>
          </div>
        )}

        {/* ── HERO SECTION: Smart OS-Detected Primary CTA + Phone Preview ── */}
        <section
          style={{
            position: "relative",
            overflow: "hidden",
            borderRadius: 24,
            background:
              "linear-gradient(135deg, rgba(6, 78, 59, 0.45) 0%, rgba(15, 23, 42, 0.95) 60%, rgba(2, 6, 23, 0.98) 100%)",
            border: "1px solid rgba(16, 185, 129, 0.28)",
            padding: "clamp(24px, 4vw, 48px)",
            marginBottom: 36,
            boxShadow: "0 24px 60px rgba(0, 0, 0, 0.45)",
          }}
        >
          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(300px, 1fr))",
              gap: 36,
              alignItems: "center",
            }}
          >
            {/* Left Column: Headline & Smart Device CTA */}
            <div>
              <div
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 8,
                  padding: "6px 14px",
                  borderRadius: 999,
                  background: "rgba(16, 185, 129, 0.15)",
                  border: "1px solid rgba(16, 185, 129, 0.35)",
                  color: "#34d399",
                  fontSize: "0.78rem",
                  fontWeight: 700,
                  letterSpacing: "0.04em",
                  textTransform: "uppercase",
                  marginBottom: 18,
                }}
              >
                <Smartphone size={14} />
                <span>Recommended for your {primaryPlatform.name} device</span>
              </div>

              <h1
                style={{
                  fontSize: "clamp(1.85rem, 3.6vw, 2.75rem)",
                  fontWeight: 800,
                  lineHeight: 1.15,
                  letterSpacing: "-0.025em",
                  color: "var(--text-primary, #f8fafc)",
                  margin: "0 0 16px 0",
                }}
              >
                Nigeria’s Fastest Newsroom,{" "}
                <span style={{ color: "#10b981" }}>In Your Pocket.</span>
              </h1>

              <p
                style={{
                  fontSize: "1.02rem",
                  lineHeight: 1.65,
                  color: "var(--text-secondary, #cbd5e1)",
                  margin: "0 0 28px 0",
                  maxWidth: 540,
                }}
              >
                Get verified breaking news across Nigeria’s top publications, 60-second AI executive briefings, 8AM &amp; 6PM audio broadcasts, and live Naira exchange rates — engineered for zero data lag.
              </p>

              {/* Primary Detected OS Action Area */}
              <div
                style={{
                  display: "flex",
                  flexWrap: "wrap",
                  alignItems: "center",
                  gap: 14,
                  marginBottom: 22,
                }}
              >
                {primaryPlatform.isConfigured ? (
                  <a
                    href={primaryPlatform.url}
                    target="_blank"
                    rel="noopener noreferrer"
                    onClick={() =>
                      handleStoreButtonClick(primaryPlatform, "hero_primary_cta")
                    }
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 14,
                      padding: "14px 26px",
                      borderRadius: 14,
                      background: "linear-gradient(135deg, #10b981 0%, #059669 100%)",
                      color: "#ffffff",
                      textDecoration: "none",
                      fontWeight: 700,
                      boxShadow: "0 10px 28px rgba(16, 185, 129, 0.35)",
                      transition: "transform 0.15s ease, box-shadow 0.15s ease",
                    }}
                  >
                    {primaryPlatform.icon}
                    <div style={{ textAlign: "left" }}>
                      <div
                        style={{
                          fontSize: "0.7rem",
                          textTransform: "uppercase",
                          letterSpacing: "0.06em",
                          opacity: 0.9,
                        }}
                      >
                        {primaryPlatform.badgeTopText}
                      </div>
                      <div style={{ fontSize: "1.12rem", lineHeight: 1.2 }}>
                        {primaryPlatform.badgeMainText}
                      </div>
                    </div>
                    <ExternalLink size={16} style={{ marginLeft: 4, opacity: 0.85 }} />
                  </a>
                ) : (
                  <div
                    style={{
                      display: "flex",
                      flexWrap: "wrap",
                      alignItems: "center",
                      gap: 12,
                    }}
                  >
                    {/* Coming Soon Store Badge */}
                    <div
                      style={{
                        display: "inline-flex",
                        alignItems: "center",
                        gap: 12,
                        padding: "12px 20px",
                        borderRadius: 14,
                        background: "rgba(30, 41, 59, 0.85)",
                        border: "1px solid rgba(148, 163, 184, 0.28)",
                        color: "var(--text-primary, #f8fafc)",
                      }}
                    >
                      {primaryPlatform.icon}
                      <div style={{ textAlign: "left" }}>
                        <div
                          style={{
                            fontSize: "0.68rem",
                            textTransform: "uppercase",
                            letterSpacing: "0.06em",
                            color: "#fbbf24",
                            fontWeight: 700,
                          }}
                        >
                          Coming Soon to
                        </div>
                        <div style={{ fontSize: "1rem", fontWeight: 700 }}>
                          {primaryPlatform.storeName}
                        </div>
                      </div>
                    </div>

                    {/* Instant PWA Fallback Button */}
                    {!isInstalled && (
                      <button
                        type="button"
                        onClick={() => promptInstall("download_page_hero")}
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: 10,
                          padding: "14px 24px",
                          borderRadius: 14,
                          background:
                            "linear-gradient(135deg, #10b981 0%, #059669 100%)",
                          color: "#ffffff",
                          border: "none",
                          cursor: "pointer",
                          fontWeight: 700,
                          fontSize: "0.96rem",
                          boxShadow: "0 10px 25px rgba(16, 185, 129, 0.35)",
                        }}
                      >
                        <Download size={18} />
                        <span>Install Instant Web App (Less than 1 MB)</span>
                      </button>
                    )}
                  </div>
                )}

                {/* Secondary Web App Quick Install when native store IS configured */}
                {primaryPlatform.isConfigured && !isInstalled && (
                  <button
                    type="button"
                    onClick={() => promptInstall("download_page_secondary")}
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: 8,
                      padding: "13px 18px",
                      borderRadius: 14,
                      background: "rgba(30, 41, 59, 0.75)",
                      border: "1px solid rgba(148, 163, 184, 0.25)",
                      color: "var(--text-primary, #f8fafc)",
                      cursor: "pointer",
                      fontWeight: 600,
                      fontSize: "0.88rem",
                    }}
                  >
                    <Zap size={16} style={{ color: "#10b981" }} />
                    <span>Or Launch Instant Web App</span>
                  </button>
                )}
              </div>

              {/* Trust & Performance Pills */}
              <div
                style={{
                  display: "flex",
                  flexWrap: "wrap",
                  gap: 16,
                  fontSize: "0.82rem",
                  color: "var(--text-secondary, #94a3b8)",
                }}
              >
                <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
                  <ShieldCheck size={15} style={{ color: "#10b981" }} />
                  100% Free · No Subscription Required
                </span>
                <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
                  <WifiOff size={15} style={{ color: "#10b981" }} />
                  Offline Saved Stories
                </span>
                <span style={{ display: "inline-flex", alignItems: "center", gap: 6 }}>
                  <Headphones size={15} style={{ color: "#10b981" }} />
                  Daily Nigerian Audio Briefings
                </span>
              </div>
            </div>

            {/* Right Column: Interactive Mobile App Preview Card */}
            <div
              style={{
                display: "flex",
                justifyContent: "center",
              }}
            >
              <div
                style={{
                  width: "100%",
                  maxWidth: 340,
                  borderRadius: 28,
                  padding: 18,
                  background: "rgba(15, 23, 42, 0.92)",
                  border: "2px solid rgba(148, 163, 184, 0.2)",
                  boxShadow: "0 20px 50px rgba(0, 0, 0, 0.6)",
                }}
              >
                {/* Mock Phone Status Bar */}
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    fontSize: "0.72rem",
                    color: "#94a3b8",
                    marginBottom: 14,
                    paddingBottom: 10,
                    borderBottom: "1px solid rgba(148, 163, 184, 0.12)",
                  }}
                >
                  <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
                    <img
                      src="/logo.png"
                      alt="News Stand NG"
                      style={{ width: 18, height: 18, borderRadius: 4 }}
                    />
                    <strong style={{ color: "#f8fafc" }}>NEWS STAND NG</strong>
                  </div>
                  <span
                    style={{
                      padding: "2px 8px",
                      borderRadius: 999,
                      background: "rgba(16, 185, 129, 0.15)",
                      color: "#10b981",
                      fontWeight: 700,
                    }}
                  >
                    LIVE
                  </span>
                </div>

                {/* Mock Audio Briefing Player */}
                <div
                  style={{
                    padding: 12,
                    borderRadius: 14,
                    background:
                      "linear-gradient(135deg, rgba(16, 185, 129, 0.18), rgba(6, 78, 59, 0.35))",
                    border: "1px solid rgba(16, 185, 129, 0.3)",
                    marginBottom: 12,
                  }}
                >
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "space-between",
                      marginBottom: 6,
                    }}
                  >
                    <span
                      style={{
                        fontSize: "0.7rem",
                        fontWeight: 700,
                        color: "#34d399",
                        textTransform: "uppercase",
                      }}
                    >
                      🎧 Daily Audio Briefing
                    </span>
                    <span style={{ fontSize: "0.7rem", color: "#94a3b8" }}>
                      02:15
                    </span>
                  </div>
                  <div
                    style={{
                      fontSize: "0.84rem",
                      fontWeight: 700,
                      color: "#f8fafc",
                      marginBottom: 4,
                    }}
                  >
                    Morning National Roundup &amp; Naira FX Watch
                  </div>
                  <div
                    style={{
                      height: 4,
                      borderRadius: 999,
                      background: "rgba(255,255,255,0.12)",
                      overflow: "hidden",
                    }}
                  >
                    <div
                      style={{
                        width: "65%",
                        height: "100%",
                        background: "#10b981",
                      }}
                    />
                  </div>
                </div>

                {/* Mock AI Summary Card */}
                <div
                  style={{
                    padding: 12,
                    borderRadius: 14,
                    background: "rgba(30, 41, 59, 0.7)",
                    border: "1px solid rgba(148, 163, 184, 0.15)",
                    marginBottom: 12,
                  }}
                >
                  <div
                    style={{
                      fontSize: "0.68rem",
                      color: "#38bdf8",
                      fontWeight: 700,
                      marginBottom: 4,
                    }}
                  >
                    ✨ AI 60-SECOND EXECUTIVE BRIEF
                  </div>
                  <div
                    style={{
                      fontSize: "0.82rem",
                      fontWeight: 600,
                      color: "#f1f5f9",
                      lineHeight: 1.4,
                    }}
                  >
                    Multi-source synthesis from Punch, Vanguard, Premium Times &amp; Channels TV — zero clickbait.
                  </div>
                </div>

                {/* Mock FX Ticker Row */}
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "space-between",
                    padding: "10px 12px",
                    borderRadius: 12,
                    background: "rgba(15, 23, 42, 0.9)",
                    border: "1px solid rgba(148, 163, 184, 0.14)",
                    fontSize: "0.78rem",
                  }}
                >
                  <span style={{ color: "#94a3b8", fontWeight: 600 }}>
                    🇳🇬 Live Naira FX Alerts
                  </span>
                  <span style={{ color: "#10b981", fontWeight: 700 }}>
                    CBN &amp; Parallel Rates
                  </span>
                </div>
              </div>
            </div>
          </div>
        </section>

        {/* ── ALL 4 PLATFORMS STORE DIRECTORY (Android, iOS, Windows, macOS) ── */}
        <section style={{ marginBottom: 44 }} aria-labelledby="platforms-heading">
          <div
            style={{
              display: "flex",
              flexWrap: "wrap",
              alignItems: "flex-end",
              justifyContent: "space-between",
              gap: 12,
              marginBottom: 20,
            }}
          >
            <div>
              <h2
                id="platforms-heading"
                style={{
                  fontSize: "1.45rem",
                  fontWeight: 800,
                  color: "var(--text-primary, #f8fafc)",
                  margin: "0 0 6px 0",
                }}
              >
                Choose Your Platform
              </h2>
              <p
                style={{
                  fontSize: "0.92rem",
                  color: "var(--text-secondary, #94a3b8)",
                  margin: 0,
                }}
              >
                Download News Stand NG across mobile phones, tablets, and desktop computers.
              </p>
            </div>
          </div>

          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(250px, 1fr))",
              gap: 18,
            }}
          >
            {platforms.map((platform) => {
              const isDetected = platform.id === detectedOs;
              return (
                <article
                  key={platform.id}
                  style={{
                    display: "flex",
                    flexDirection: "column",
                    justifyContent: "space-between",
                    padding: 22,
                    borderRadius: 18,
                    background: isDetected
                      ? "linear-gradient(180deg, rgba(16, 185, 129, 0.1) 0%, rgba(15, 23, 42, 0.92) 100%)"
                      : "var(--bg-card, rgba(15, 23, 42, 0.8))",
                    border: isDetected
                      ? "1.5px solid rgba(16, 185, 129, 0.5)"
                      : "1px solid var(--border-card, rgba(148, 163, 184, 0.18))",
                    boxShadow: isDetected
                      ? "0 12px 32px rgba(16, 185, 129, 0.14)"
                      : "none",
                  }}
                >
                  <div>
                    {/* Top Row: Icon + Status Badge */}
                    <div
                      style={{
                        display: "flex",
                        alignItems: "center",
                        justifyContent: "space-between",
                        marginBottom: 16,
                      }}
                    >
                      <div
                        style={{
                          width: 52,
                          height: 52,
                          borderRadius: 14,
                          background: "rgba(30, 41, 59, 0.9)",
                          border: "1px solid rgba(148, 163, 184, 0.2)",
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                          color: "#f8fafc",
                        }}
                      >
                        {platform.icon}
                      </div>

                      <div style={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 4 }}>
                        {isDetected && (
                          <span
                            style={{
                              fontSize: "0.68rem",
                              fontWeight: 700,
                              padding: "2px 8px",
                              borderRadius: 999,
                              background: "rgba(16, 185, 129, 0.2)",
                              color: "#34d399",
                              textTransform: "uppercase",
                            }}
                          >
                            Your Device
                          </span>
                        )}
                        {platform.isConfigured ? (
                          <span
                            style={{
                              display: "inline-flex",
                              alignItems: "center",
                              gap: 4,
                              fontSize: "0.72rem",
                              fontWeight: 700,
                              padding: "3px 10px",
                              borderRadius: 999,
                              background: "rgba(16, 185, 129, 0.14)",
                              color: "#10b981",
                              border: "1px solid rgba(16, 185, 129, 0.3)",
                            }}
                          >
                            <CheckCircle2 size={12} />
                            Available Now
                          </span>
                        ) : (
                          <span
                            style={{
                              display: "inline-flex",
                              alignItems: "center",
                              gap: 4,
                              fontSize: "0.72rem",
                              fontWeight: 700,
                              padding: "3px 10px",
                              borderRadius: 999,
                              background: "rgba(245, 158, 11, 0.14)",
                              color: "#fbbf24",
                              border: "1px solid rgba(245, 158, 11, 0.3)",
                            }}
                          >
                            <Clock size={12} />
                            Coming Soon
                          </span>
                        )}
                      </div>
                    </div>

                    <h3
                      style={{
                        fontSize: "1.2rem",
                        fontWeight: 800,
                        color: "var(--text-primary, #f8fafc)",
                        margin: "0 0 4px 0",
                      }}
                    >
                      {platform.name}
                    </h3>
                    <div
                      style={{
                        fontSize: "0.78rem",
                        color: "var(--text-muted, #94a3b8)",
                        marginBottom: 12,
                        fontWeight: 500,
                      }}
                    >
                      {platform.requirements}
                    </div>
                    <p
                      style={{
                        fontSize: "0.86rem",
                        lineHeight: 1.55,
                        color: "var(--text-secondary, #cbd5e1)",
                        margin: "0 0 20px 0",
                      }}
                    >
                      {platform.description}
                    </p>
                  </div>

                  {/* Card Footer Action Button */}
                  <div>
                    {platform.isConfigured ? (
                      <a
                        href={platform.url}
                        target="_blank"
                        rel="noopener noreferrer"
                        onClick={() =>
                          handleStoreButtonClick(platform, `card_${platform.id}`)
                        }
                        style={{
                          display: "flex",
                          alignItems: "center",
                          justifyContent: "center",
                          gap: 10,
                          width: "100%",
                          padding: "12px 16px",
                          borderRadius: 12,
                          background: "#f8fafc",
                          color: "#0f172a",
                          textDecoration: "none",
                          fontWeight: 800,
                          fontSize: "0.9rem",
                          transition: "opacity 0.15s ease",
                        }}
                      >
                        <span>
                          {platform.badgeTopText} {platform.badgeMainText}
                        </span>
                        <ExternalLink size={15} />
                      </a>
                    ) : (
                      <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
                        <div
                          style={{
                            textAlign: "center",
                            padding: "9px 12px",
                            borderRadius: 10,
                            background: "rgba(30, 41, 59, 0.65)",
                            border: "1px dashed rgba(148, 163, 184, 0.25)",
                            color: "#94a3b8",
                            fontSize: "0.8rem",
                            fontWeight: 600,
                          }}
                        >
                          {platform.storeName} — Coming Soon
                        </div>
                        <button
                          type="button"
                          onClick={() => promptInstall(`card_fallback_${platform.id}`)}
                          style={{
                            display: "flex",
                            alignItems: "center",
                            justifyContent: "center",
                            gap: 8,
                            width: "100%",
                            padding: "10px 14px",
                            borderRadius: 10,
                            background: "rgba(16, 185, 129, 0.15)",
                            border: "1px solid rgba(16, 185, 129, 0.35)",
                            color: "#34d399",
                            cursor: "pointer",
                            fontWeight: 700,
                            fontSize: "0.84rem",
                          }}
                        >
                          <Download size={15} />
                          <span>Use Instant Web App Now</span>
                        </button>
                      </div>
                    )}
                  </div>
                </article>
              );
            })}
          </div>
        </section>

        {/* ── SCAN TO DOWNLOAD ON MOBILE & SHARE SECTION ── */}
        <section
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))",
            gap: 24,
            padding: "28px",
            borderRadius: 20,
            background: "var(--bg-card, rgba(15, 23, 42, 0.85))",
            border: "1px solid var(--border-card, rgba(148, 163, 184, 0.18))",
            marginBottom: 44,
            alignItems: "center",
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 20, flexWrap: "wrap" }}>
            <div
              style={{
                width: 112,
                height: 112,
                borderRadius: 16,
                background: "#ffffff",
                padding: 8,
                display: "flex",
                alignItems: "center",
                justifyContent: "center",
                flexShrink: 0,
                boxShadow: "0 8px 24px rgba(0,0,0,0.3)",
              }}
            >
              <img
                src="https://api.qrserver.com/v1/create-qr-code/?size=160x160&margin=0&data=https%3A%2F%2Fnewsstand.ng%2Fdownload%3Fauto%3D1"
                alt="QR Code to download News Stand NG mobile app"
                width={96}
                height={96}
                loading="lazy"
                style={{ display: "block", borderRadius: 6 }}
                onError={(e) => {
                  e.currentTarget.style.display = "none";
                }}
              />
            </div>
            <div style={{ flex: 1, minWidth: 200 }}>
              <div
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 6,
                  fontSize: "0.75rem",
                  fontWeight: 700,
                  color: "#10b981",
                  textTransform: "uppercase",
                  marginBottom: 6,
                }}
              >
                <QrCode size={14} />
                <span>Scan with your phone camera</span>
              </div>
              <h3
                style={{
                  fontSize: "1.18rem",
                  fontWeight: 800,
                  color: "var(--text-primary, #f8fafc)",
                  margin: "0 0 6px 0",
                }}
              >
                On a Computer? Send the App to Your Phone
              </h3>
              <p
                style={{
                  fontSize: "0.86rem",
                  color: "var(--text-secondary, #94a3b8)",
                  margin: 0,
                  lineHeight: 1.5,
                }}
              >
                Point your Android or iPhone camera at the QR code to open the download page directly on your mobile device, or copy the link below.
              </p>
            </div>
          </div>

          <div
            style={{
              display: "flex",
              flexDirection: "column",
              gap: 10,
            }}
          >
            <div
              style={{
                display: "flex",
                alignItems: "center",
                gap: 8,
                padding: "10px 14px",
                borderRadius: 12,
                background: "rgba(2, 6, 23, 0.65)",
                border: "1px solid rgba(148, 163, 184, 0.2)",
              }}
            >
              <code
                style={{
                  flex: 1,
                  color: "#38bdf8",
                  fontSize: "0.86rem",
                  overflow: "hidden",
                  textOverflow: "ellipsis",
                  whiteSpace: "nowrap",
                }}
              >
                https://newsstand.ng/download
              </code>
              <button
                type="button"
                onClick={handleCopyDownloadLink}
                style={{
                  display: "inline-flex",
                  alignItems: "center",
                  gap: 6,
                  padding: "7px 14px",
                  borderRadius: 8,
                  background: copiedLink ? "#10b981" : "rgba(30, 41, 59, 0.95)",
                  color: "#ffffff",
                  border: "1px solid rgba(148, 163, 184, 0.25)",
                  cursor: "pointer",
                  fontWeight: 700,
                  fontSize: "0.8rem",
                }}
              >
                {copiedLink ? <Check size={14} /> : <Copy size={14} />}
                <span>{copiedLink ? "Copied!" : "Copy Link"}</span>
              </button>
            </div>
          </div>
        </section>

        {/* ── WHY DOWNLOAD NEWS STAND NG (KEY FEATURES) ── */}
        <section aria-labelledby="features-heading">
          <h2
            id="features-heading"
            style={{
              fontSize: "1.35rem",
              fontWeight: 800,
              color: "var(--text-primary, #f8fafc)",
              margin: "0 0 18px 0",
            }}
          >
            Built for Nigerian Readers Everywhere
          </h2>

          <div
            style={{
              display: "grid",
              gridTemplateColumns: "repeat(auto-fit, minmax(230px, 1fr))",
              gap: 16,
            }}
          >
            <div
              style={{
                padding: 20,
                borderRadius: 16,
                background: "var(--bg-card, rgba(15, 23, 42, 0.75))",
                border: "1px solid var(--border-card, rgba(148, 163, 184, 0.14))",
              }}
            >
              <Bell size={22} style={{ color: "#10b981", marginBottom: 10 }} />
              <h3
                style={{
                  fontSize: "1rem",
                  fontWeight: 700,
                  color: "var(--text-primary, #f8fafc)",
                  margin: "0 0 6px 0",
                }}
              >
                Real-Time Breaking Alerts
              </h3>
              <p
                style={{
                  fontSize: "0.84rem",
                  color: "var(--text-secondary, #94a3b8)",
                  margin: 0,
                  lineHeight: 1.55,
                }}
              >
                Instant notifications when major stories break across Punch, Vanguard, Premium Times, Channels TV, and Arise News.
              </p>
            </div>

            <div
              style={{
                padding: 20,
                borderRadius: 16,
                background: "var(--bg-card, rgba(15, 23, 42, 0.75))",
                border: "1px solid var(--border-card, rgba(148, 163, 184, 0.14))",
              }}
            >
              <Headphones size={22} style={{ color: "#38bdf8", marginBottom: 10 }} />
              <h3
                style={{
                  fontSize: "1rem",
                  fontWeight: 700,
                  color: "var(--text-primary, #f8fafc)",
                  margin: "0 0 6px 0",
                }}
              >
                8AM &amp; 6PM Audio Briefings
              </h3>
              <p
                style={{
                  fontSize: "0.84rem",
                  color: "var(--text-secondary, #94a3b8)",
                  margin: 0,
                  lineHeight: 1.55,
                }}
              >
                Listen to natural Nigerian English voice briefings on your commute without staring at your screen.
              </p>
            </div>

            <div
              style={{
                padding: 20,
                borderRadius: 16,
                background: "var(--bg-card, rgba(15, 23, 42, 0.75))",
                border: "1px solid var(--border-card, rgba(148, 163, 184, 0.14))",
              }}
            >
              <WifiOff size={22} style={{ color: "#fbbf24", marginBottom: 10 }} />
              <h3
                style={{
                  fontSize: "1rem",
                  fontWeight: 700,
                  color: "var(--text-primary, #f8fafc)",
                  margin: "0 0 6px 0",
                }}
              >
                Offline Reading &amp; Data Saver
              </h3>
              <p
                style={{
                  fontSize: "0.84rem",
                  color: "var(--text-secondary, #94a3b8)",
                  margin: 0,
                  lineHeight: 1.55,
                }}
              >
                Optimized for 3G/4G networks. Save stories to read offline anytime with ultra-lightweight data usage.
              </p>
            </div>

            <div
              style={{
                padding: 20,
                borderRadius: 16,
                background: "var(--bg-card, rgba(15, 23, 42, 0.75))",
                border: "1px solid var(--border-card, rgba(148, 163, 184, 0.14))",
              }}
            >
              <TrendingUp size={22} style={{ color: "#a855f7", marginBottom: 10 }} />
              <h3
                style={{
                  fontSize: "1rem",
                  fontWeight: 700,
                  color: "var(--text-primary, #f8fafc)",
                  margin: "0 0 6px 0",
                }}
              >
                Naira FX &amp; 36 States Hubs
              </h3>
              <p
                style={{
                  fontSize: "0.84rem",
                  color: "var(--text-secondary, #94a3b8)",
                  margin: 0,
                  lineHeight: 1.55,
                }}
              >
                Track live parallel market vs. CBN exchange rates, fuel prices, and local news across all 36 states and FCT.
              </p>
            </div>
          </div>
        </section>
      </main>

      {/* iOS Safari Home Screen Install Modal */}
      <IosInstallModal
        isOpen={showIosModal}
        onClose={() => setShowIosModal(false)}
      />

      <Footer onSelectCategory={() => {}} />
    </div>
  );
}

