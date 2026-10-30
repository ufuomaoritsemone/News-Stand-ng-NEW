import React, { useState, useEffect } from "react";
import { Link, useLoaderData } from "react-router";
import Footer from "../../src/components/Footer";
import { WhatsAppIcon } from "../../src/components/SocialIcons";
import {
  ArrowLeft,
  Mail,
  MapPin,
  Phone,
  Building,
  Send,
  CheckCircle2,
  AlertCircle,
  MessageSquare,
  Sparkles,
} from "lucide-react";
import { api } from "../../src/services/api";

export function meta() {
  const title = "Contact News Stand NG — WhatsApp Desk, Editorial & Support";
  const description =
    "Reach the News Stand NG editorial newsroom, administrative team at Admin@newsstand.ng, or chat directly via our verified WhatsApp desk.";
  const canonicalUrl = "https://newsstand.ng/contact";

  return [
    { title },
    { name: "description", content: description },
    { tagName: "link", rel: "canonical", href: canonicalUrl },
    { name: "robots", content: "index, follow" },
    { property: "og:type", content: "website" },
    { property: "og:title", content: title },
    { property: "og:description", content: description },
    { property: "og:url", content: canonicalUrl },
    { name: "twitter:card", content: "summary_large_image" },
    { name: "twitter:title", content: title },
    { name: "twitter:description", content: description },
  ];
}

export async function loader() {
  try {
    const contactInfo = await api.getContactInfo();
    return { contactInfo };
  } catch {
    return {
      contactInfo: {
        whatsAppNumber: "",
        defaultMessage: "Hello News Stand NG Admin, I have an inquiry.",
        recipientEmail: "Admin@newsstand.ng",
        hasWhatsApp: false,
      },
    };
  }
}

function sanitizeWhatsAppDigits(phone) {
  let digits = (phone || "").replace(/[^\d]/g, "");
  if (digits.startsWith("0") && digits.length === 11) {
    digits = "234" + digits.substring(1);
  }
  return digits;
}

export default function ContactPage() {
  const loaderData = useLoaderData();
  const [contactInfo, setContactInfo] = useState(
    loaderData?.contactInfo || {
      whatsAppNumber: "",
      defaultMessage: "Hello News Stand NG Admin, I have an inquiry.",
      recipientEmail: "Admin@newsstand.ng",
      hasWhatsApp: false,
    }
  );

  const [formData, setFormData] = useState({
    name: "",
    email: "",
    phone: "",
    category: "General Inquiry",
    message: "",
  });

  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [errorMsg, setErrorMsg] = useState("");

  // Sync client-side in case settings were recently hot-reloaded
  useEffect(() => {
    api.getContactInfo().then((info) => {
      if (info && typeof info === "object") {
        setContactInfo(info);
      }
    }).catch(() => { });
  }, []);

  const targetWhatsApp = sanitizeWhatsAppDigits(contactInfo?.whatsAppNumber);
  const hasWhatsApp = Boolean(targetWhatsApp);
  const adminEmail = contactInfo?.recipientEmail || "Admin@newsstand.ng";

  const contactJsonLd = {
    "@context": "https://schema.org",
    "@type": "ContactPage",
    "name": "Contact News Stand NG",
    "url": "https://newsstand.ng/contact",
    "description": "Contact information for News Stand NG newsroom, admin desk, WhatsApp contact, and partnerships.",
    "mainEntity": {
      "@type": "NewsMediaOrganization",
      "name": "News Stand NG",
      "url": "https://newsstand.ng",
      "contactPoint": [
        {
          "@type": "ContactPoint",
          "contactType": "administrative desk",
          "email": adminEmail,
          ...(hasWhatsApp ? { "telephone": `+${targetWhatsApp}` } : {})
        },
        {
          "@type": "ContactPoint",
          "contactType": "editorial newsroom",
          "email": "editorial@newsstand.ng"
        },
        {
          "@type": "ContactPoint",
          "contactType": "corrections desk",
          "email": "corrections@newsstand.ng"
        }
      ]
    }
  };

  const handleChange = (e) => {
    const { name, value } = e.target;
    setFormData((prev) => ({ ...prev, [name]: value }));
    setErrorMsg("");
  };

  // 1. WhatsApp Router: formats form fields into a pre-filled WhatsApp message
  const handleSendViaWhatsApp = (e) => {
    if (e) e.preventDefault();

    if (!formData.message.trim()) {
      setErrorMsg("Please enter a message before launching WhatsApp.");
      return;
    }

    const lines = [
      `*News Stand NG — Contact Form Submission*`,
      `*From:* ${formData.name.trim() || "Anonymous Reader"}`,
      formData.email.trim() ? `*Email:* ${formData.email.trim()}` : null,
      formData.phone.trim() ? `*Phone/WhatsApp:* ${formData.phone.trim()}` : null,
      `*Category:* ${formData.category}`,
      `*Routed to:* ${adminEmail}`,
      ``,
      `*Message:*`,
      formData.message.trim(),
    ].filter(Boolean);

    const text = encodeURIComponent(lines.join("\n"));
    const baseUrl = hasWhatsApp
      ? `https://wa.me/${targetWhatsApp}`
      : "https://api.whatsapp.com/send";
    const waUrl = `${baseUrl}?text=${text}`;

    if (typeof window !== "undefined") {
      window.open(waUrl, "_blank", "noopener,noreferrer");
    }
  };

  // 2. Email / Server Submission: sends to backend /api/v1/feedback and Admin@newsstand.ng
  const handleSendViaEmail = async (e) => {
    if (e) e.preventDefault();

    if (!formData.message.trim() || formData.message.trim().length < 3) {
      setErrorMsg("Please provide a message with at least 3 characters.");
      return;
    }

    setSubmitting(true);
    setErrorMsg("");

    try {
      await api.submitFeedback({
        rating: 5,
        category: formData.category,
        message: `[Routed to ${adminEmail}]\nPhone: ${formData.phone.trim() || "N/A"}\n\n${formData.message.trim()}`,
        userEmail: formData.email.trim(),
        userName: formData.name.trim(),
        platform: "Web Contact Page",
      });

      setSubmitted(true);
    } catch (err) {
      console.warn("Server submission fallback:", err.message);
      // Fallback: open mailto directly to admin email
      const subject = encodeURIComponent(`[Contact Form - ${formData.category}] from ${formData.name || "Reader"}`);
      const body = encodeURIComponent(
        `Name: ${formData.name}\nEmail: ${formData.email}\nPhone: ${formData.phone}\nCategory: ${formData.category}\n\nMessage:\n${formData.message}`
      );
      if (typeof window !== "undefined") {
        window.location.href = `mailto:${adminEmail}?subject=${subject}&body=${body}`;
      }
      setSubmitted(true);
    } finally {
      setSubmitting(false);
    }
  };

  const directWaUrl = hasWhatsApp
    ? `https://wa.me/${targetWhatsApp}?text=${encodeURIComponent(contactInfo?.defaultMessage || "Hello News Stand NG Admin, I would like to get in touch.")}`
    : `https://api.whatsapp.com/send?text=${encodeURIComponent(contactInfo?.defaultMessage || "Hello News Stand NG Admin, I would like to get in touch.")}`;

  return (
    <div className="static-page-layout">
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(contactJsonLd) }}
      />

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

          <div style={{ width: 80 }} />
        </div>
      </header>

      <main className="static-policy-container">
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to="/">Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">Contact Us</li>
          </ol>
        </nav>

        <article className="static-policy-card">
          <div className="policy-badge-verified">
            <Sparkles size={14} />
            <span>Official Newsroom & Admin Channels</span>
          </div>

          <h1 className="static-policy-title">Contact News Stand NG</h1>
          <p className="static-policy-subtitle">
            {hasWhatsApp ? (
              <>
                Reach our administrative office directly on WhatsApp at <strong style={{ color: "#25D366" }}>+{targetWhatsApp}</strong>, or route your inquiry to <a href={`mailto:${adminEmail}`} style={{ color: "var(--brand-green, #10b981)", fontWeight: 700 }}>{adminEmail}</a>.
              </>
            ) : (
              <>
                Reach our administrative office directly at <a href={`mailto:${adminEmail}`} style={{ color: "var(--brand-green, #10b981)", fontWeight: 700 }}>{adminEmail}</a>, or submit your news tips and feedback through the form below.
              </>
            )}
          </p>

          {/* Quick WhatsApp Banner */}
          <div
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
              flexWrap: "wrap",
              gap: "16px",
              padding: "16px 20px",
              background: "linear-gradient(135deg, rgba(37, 211, 102, 0.12), rgba(16, 185, 129, 0.08))",
              border: "1px solid rgba(37, 211, 102, 0.3)",
              borderRadius: "14px",
              margin: "20px 0 32px 0",
            }}
          >
            <div style={{ display: "flex", alignItems: "center", gap: "14px" }}>
              <div
                style={{
                  width: 44,
                  height: 44,
                  borderRadius: "50%",
                  background: "#25D366",
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "center",
                  flexShrink: 0,
                  boxShadow: "0 4px 12px rgba(37, 211, 102, 0.35)",
                }}
              >
                <WhatsAppIcon size={24} color="#FFFFFF" />
              </div>
              <div>
                <h2 style={{ margin: 0, fontSize: "1.05rem", fontWeight: 700, color: "var(--text-primary, #f8fafc)" }}>
                  {hasWhatsApp ? "Direct WhatsApp Support Active" : "Direct WhatsApp Desk"}
                </h2>
                <p style={{ margin: "2px 0 0 0", fontSize: "0.85rem", color: "var(--text-secondary, #94a3b8)" }}>
                  {hasWhatsApp
                    ? `Chat directly with our administrative desk at +${targetWhatsApp}.`
                    : "Need immediate assistance or have breaking tips? Open WhatsApp to chat."}
                </p>
              </div>
            </div>

            <a
              href={directWaUrl}
              target="_blank"
              rel="noopener noreferrer"
              style={{
                display: "inline-flex",
                alignItems: "center",
                gap: "8px",
                background: "#25D366",
                color: "#FFFFFF",
                fontWeight: 700,
                fontSize: "0.88rem",
                padding: "10px 18px",
                borderRadius: "9999px",
                textDecoration: "none",
                boxShadow: "0 2px 8px rgba(37, 211, 102, 0.3)",
                transition: "all 0.2s ease",
              }}
            >
              <WhatsAppIcon size={18} color="#FFFFFF" />
              <span>{hasWhatsApp ? "Chat on WhatsApp" : "Open WhatsApp"}</span>
            </a>
          </div>

          {/* Interactive Contact Form */}
          <section
            style={{
              background: "var(--bg-elevated, rgba(255, 255, 255, 0.02))",
              border: "1px solid var(--border-subtle, rgba(255, 255, 255, 0.08))",
              borderRadius: "16px",
              padding: "24px",
              margin: "24px 0 36px 0",
            }}
            aria-label="Direct Contact Form"
          >
            <div style={{ marginBottom: "20px" }}>
              <div style={{ display: "flex", alignItems: "center", gap: "8px", marginBottom: "4px" }}>
                <h2 style={{ margin: 0, fontSize: "1.25rem", fontWeight: 700 }}>
                  Send Us a Message
                </h2>
                {hasWhatsApp && (
                  <span
                    style={{
                      background: "rgba(37, 211, 102, 0.15)",
                      color: "#25D366",
                      border: "1px solid rgba(37, 211, 102, 0.3)",
                      borderRadius: "9999px",
                      padding: "2px 10px",
                      fontSize: "0.72rem",
                      fontWeight: 700,
                      letterSpacing: "0.03em",
                    }}
                  >
                    WHATSAPP PREFERRED
                  </span>
                )}
              </div>
              <p style={{ margin: 0, fontSize: "0.88rem", color: "var(--text-secondary, #94a3b8)" }}>
                {hasWhatsApp
                  ? "Fill out the form below. With WhatsApp routing active, sending via WhatsApp is prioritized for fast replies, or you may submit via email."
                  : `Fill out the form below. Submissions are delivered directly to ${adminEmail}.`}
              </p>
            </div>

            {submitted ? (
              <div
                style={{
                  padding: "24px",
                  borderRadius: "12px",
                  background: "rgba(16, 185, 129, 0.12)",
                  border: "1px solid rgba(16, 185, 129, 0.3)",
                  textAlign: "center",
                }}
              >
                <CheckCircle2 size={40} color="var(--brand-green, #10b981)" style={{ margin: "0 auto 12px auto" }} />
                <h3 style={{ margin: "0 0 8px 0", color: "var(--brand-green, #10b981)", fontSize: "1.15rem" }}>
                  Message Sent Successfully!
                </h3>
                <p style={{ margin: "0 0 16px 0", fontSize: "0.9rem", color: "var(--text-secondary, #94a3b8)" }}>
                  Your submission has been delivered to <strong>{adminEmail}</strong>. Our team will review and follow up promptly.
                </p>
                <div style={{ display: "flex", justifyContent: "center", gap: "12px", flexWrap: "wrap" }}>
                  <button
                    type="button"
                    onClick={handleSendViaWhatsApp}
                    style={{
                      display: "inline-flex",
                      alignItems: "center",
                      gap: "8px",
                      background: "#25D366",
                      color: "#FFFFFF",
                      padding: "8px 16px",
                      borderRadius: "8px",
                      border: "none",
                      fontWeight: 600,
                      cursor: "pointer",
                      fontSize: "0.85rem",
                    }}
                  >
                    <WhatsAppIcon size={16} color="#FFFFFF" />
                    <span>Also Forward to WhatsApp</span>
                  </button>
                  <button
                    type="button"
                    onClick={() => {
                      setSubmitted(false);
                      setFormData({
                        name: "",
                        email: "",
                        phone: "",
                        category: "General Inquiry",
                        message: "",
                      });
                    }}
                    style={{
                      background: "transparent",
                      color: "var(--text-primary, #f8fafc)",
                      padding: "8px 16px",
                      borderRadius: "8px",
                      border: "1px solid var(--border-subtle, rgba(255, 255, 255, 0.2))",
                      fontWeight: 600,
                      cursor: "pointer",
                      fontSize: "0.85rem",
                    }}
                  >
                    Send Another Message
                  </button>
                </div>
              </div>
            ) : (
              <form onSubmit={hasWhatsApp ? handleSendViaWhatsApp : handleSendViaEmail}>
                {errorMsg && (
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      gap: "8px",
                      padding: "10px 14px",
                      borderRadius: "8px",
                      background: "rgba(239, 68, 68, 0.12)",
                      border: "1px solid rgba(239, 68, 68, 0.3)",
                      color: "#f87171",
                      fontSize: "0.88rem",
                      marginBottom: "16px",
                    }}
                  >
                    <AlertCircle size={16} />
                    <span>{errorMsg}</span>
                  </div>
                )}

                <div
                  style={{
                    display: "grid",
                    gridTemplateColumns: "repeat(auto-fit, minmax(240px, 1fr))",
                    gap: "16px",
                    marginBottom: "16px",
                  }}
                >
                  {/* Name */}
                  <div>
                    <label
                      htmlFor="contact-name"
                      style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "6px" }}
                    >
                      Your Name
                    </label>
                    <input
                      id="contact-name"
                      name="name"
                      type="text"
                      placeholder="e.g. Chukwuma Adeleke"
                      value={formData.name}
                      onChange={handleChange}
                      style={{
                        width: "100%",
                        padding: "10px 12px",
                        borderRadius: "8px",
                        border: "1px solid var(--border-subtle, rgba(255, 255, 255, 0.15))",
                        background: "var(--bg-card, rgba(0, 0, 0, 0.25))",
                        color: "var(--text-primary, #f8fafc)",
                        fontSize: "0.9rem",
                      }}
                    />
                  </div>

                  {/* Email */}
                  <div>
                    <label
                      htmlFor="contact-email"
                      style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "6px" }}
                    >
                      Your Email Address
                    </label>
                    <input
                      id="contact-email"
                      name="email"
                      type="email"
                      placeholder="e.g. name@example.com"
                      value={formData.email}
                      onChange={handleChange}
                      style={{
                        width: "100%",
                        padding: "10px 12px",
                        borderRadius: "8px",
                        border: "1px solid var(--border-subtle, rgba(255, 255, 255, 0.15))",
                        background: "var(--bg-card, rgba(0, 0, 0, 0.25))",
                        color: "var(--text-primary, #f8fafc)",
                        fontSize: "0.9rem",
                      }}
                    />
                  </div>

                  {/* Phone / WhatsApp */}
                  <div>
                    <label
                      htmlFor="contact-phone"
                      style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "6px" }}
                    >
                      WhatsApp Phone Number
                    </label>
                    <input
                      id="contact-phone"
                      name="phone"
                      type="tel"
                      placeholder="e.g. +234 803 123 4567"
                      value={formData.phone}
                      onChange={handleChange}
                      style={{
                        width: "100%",
                        padding: "10px 12px",
                        borderRadius: "8px",
                        border: "1px solid var(--border-subtle, rgba(255, 255, 255, 0.15))",
                        background: "var(--bg-card, rgba(0, 0, 0, 0.25))",
                        color: "var(--text-primary, #f8fafc)",
                        fontSize: "0.9rem",
                      }}
                    />
                  </div>

                  {/* Department / Category */}
                  <div>
                    <label
                      htmlFor="contact-category"
                      style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "6px" }}
                    >
                      Inquiry Department
                    </label>
                    <select
                      id="contact-category"
                      name="category"
                      value={formData.category}
                      onChange={handleChange}
                      style={{
                        width: "100%",
                        padding: "10px 12px",
                        borderRadius: "8px",
                        border: "1px solid var(--border-subtle, rgba(255, 255, 255, 0.15))",
                        background: "var(--bg-card, rgba(0, 0, 0, 0.25))",
                        color: "var(--text-primary, #f8fafc)",
                        fontSize: "0.9rem",
                      }}
                    >
                      <option value="General Inquiry">General Inquiry</option>
                      <option value="Editorial & Tips">Editorial News Tips & Reports</option>
                      <option value="Corrections Desk">Corrections & Fact-Check Request</option>
                      <option value="Partnerships & Ads">Commercial Partnerships & Ads</option>
                      <option value="Technical Support">Technical & App Support</option>
                    </select>
                  </div>
                </div>

                {/* Message */}
                <div style={{ marginBottom: "20px" }}>
                  <label
                    htmlFor="contact-message"
                    style={{ display: "block", fontSize: "0.82rem", fontWeight: 600, marginBottom: "6px" }}
                  >
                    Your Message
                  </label>
                  <textarea
                    id="contact-message"
                    name="message"
                    rows={4}
                    placeholder="Provide details about your inquiry, tip, or partnership proposal..."
                    value={formData.message}
                    onChange={handleChange}
                    required
                    style={{
                      width: "100%",
                      padding: "12px",
                      borderRadius: "8px",
                      border: "1px solid var(--border-subtle, rgba(255, 255, 255, 0.15))",
                      background: "var(--bg-card, rgba(0, 0, 0, 0.25))",
                      color: "var(--text-primary, #f8fafc)",
                      fontSize: "0.9rem",
                      resize: "vertical",
                    }}
                  />
                </div>

                {/* Submit Actions: Ordered conditionally based on hasWhatsApp */}
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    gap: "12px",
                    flexWrap: "wrap",
                  }}
                >
                  {hasWhatsApp ? (
                    <>
                      {/* 1. Primary: WhatsApp (Preferred when configured) */}
                      <button
                        type="button"
                        onClick={handleSendViaWhatsApp}
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "8px",
                          background: "#25D366",
                          color: "#FFFFFF",
                          padding: "12px 22px",
                          borderRadius: "10px",
                          border: "none",
                          fontWeight: 700,
                          fontSize: "0.92rem",
                          cursor: "pointer",
                          boxShadow: "0 2px 10px rgba(37, 211, 102, 0.3)",
                          transition: "all 0.2s ease",
                        }}
                      >
                        <WhatsAppIcon size={18} color="#FFFFFF" />
                        <span>Send via WhatsApp (Preferred)</span>
                      </button>

                      {/* 2. Secondary: Email */}
                      <button
                        type="submit"
                        onClick={handleSendViaEmail}
                        disabled={submitting}
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "8px",
                          background: "rgba(255, 255, 255, 0.08)",
                          color: "var(--text-primary, #f8fafc)",
                          padding: "12px 20px",
                          borderRadius: "10px",
                          border: "1px solid var(--border-subtle, rgba(255, 255, 255, 0.15))",
                          fontWeight: 600,
                          fontSize: "0.92rem",
                          cursor: submitting ? "not-allowed" : "pointer",
                          opacity: submitting ? 0.7 : 1,
                          transition: "all 0.2s ease",
                        }}
                      >
                        <Send size={15} />
                        <span>{submitting ? "Sending..." : `Or Send via Email to ${adminEmail}`}</span>
                      </button>
                    </>
                  ) : (
                    <>
                      {/* 1. Primary: Email (Default when no WhatsApp configured) */}
                      <button
                        type="submit"
                        disabled={submitting}
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "8px",
                          background: "var(--brand-green, #10b981)",
                          color: "#FFFFFF",
                          padding: "12px 22px",
                          borderRadius: "10px",
                          border: "none",
                          fontWeight: 700,
                          fontSize: "0.92rem",
                          cursor: submitting ? "not-allowed" : "pointer",
                          opacity: submitting ? 0.7 : 1,
                          boxShadow: "0 2px 10px rgba(16, 185, 129, 0.25)",
                          transition: "all 0.2s ease",
                        }}
                      >
                        <Send size={16} />
                        <span>{submitting ? "Routing to Admin..." : `Submit to ${adminEmail}`}</span>
                      </button>

                      {/* 2. Secondary: WhatsApp */}
                      <button
                        type="button"
                        onClick={handleSendViaWhatsApp}
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          gap: "8px",
                          background: "#25D366",
                          color: "#FFFFFF",
                          padding: "12px 20px",
                          borderRadius: "10px",
                          border: "none",
                          fontWeight: 700,
                          fontSize: "0.92rem",
                          cursor: "pointer",
                          boxShadow: "0 2px 10px rgba(37, 211, 102, 0.25)",
                          transition: "all 0.2s ease",
                        }}
                      >
                        <WhatsAppIcon size={17} color="#FFFFFF" />
                        <span>Send via WhatsApp</span>
                      </button>
                    </>
                  )}
                </div>
              </form>
            )}
          </section>

          {/* Department Directory Cards */}
          <div className="static-policy-content">
            <h2 style={{ fontSize: "1.2rem", fontWeight: 700, margin: "28px 0 16px 0" }}>
              Direct Newsroom & Management Desks
            </h2>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(260px, 1fr))", gap: "20px", margin: "16px 0 36px 0" }}>
              {/* Admin Desk */}
              <div style={{ background: "rgba(255, 255, 255, 0.03)", border: "1px solid var(--border-card, rgba(255, 255, 255, 0.1))", borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "var(--brand-green, #10b981)", marginBottom: "8px", fontWeight: 700 }}>
                  <Mail size={18} />
                  <span>Administrative Office</span>
                </div>
                <p style={{ margin: "0 0 6px 0", fontSize: "0.88rem" }}>Executive matters, corporate inquiries, and administration:</p>
                <a href={`mailto:${adminEmail}`} style={{ fontWeight: 600, color: "var(--brand-green, #10b981)" }}>{adminEmail}</a>
              </div>

              {/* Editorial & Newsroom */}
              <div style={{ background: "rgba(255, 255, 255, 0.03)", border: "1px solid var(--border-card, rgba(255, 255, 255, 0.1))", borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "#38bdf8", marginBottom: "8px", fontWeight: 700 }}>
                  <MessageSquare size={18} />
                  <span>Editorial & Newsroom</span>
                </div>
                <p style={{ margin: "0 0 6px 0", fontSize: "0.88rem" }}>Breaking tips, press releases, and editorial feedback:</p>
                <a href="mailto:editorial@newsstand.ng" style={{ fontWeight: 600 }}>editorial@newsstand.ng</a>
              </div>

              {/* Corrections & Retractions */}
              <div style={{ background: "rgba(255, 255, 255, 0.03)", border: "1px solid var(--border-card, rgba(255, 255, 255, 0.1))", borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "#f59e0b", marginBottom: "8px", fontWeight: 700 }}>
                  <AlertCircle size={18} />
                  <span>Corrections & Fact-Checking</span>
                </div>
                <p style={{ margin: "0 0 6px 0", fontSize: "0.88rem" }}>Report factual inaccuracies or broken publisher links:</p>
                <a href="mailto:corrections@newsstand.ng" style={{ fontWeight: 600 }}>corrections@newsstand.ng</a>
              </div>

              {/* Headquarters */}
              <div style={{ background: "rgba(255, 255, 255, 0.03)", border: "1px solid var(--border-card, rgba(255, 255, 255, 0.1))", borderRadius: "12px", padding: "20px" }}>
                <div style={{ display: "flex", alignItems: "center", gap: "10px", color: "#ec4899", marginBottom: "8px", fontWeight: 700 }}>
                  <MapPin size={18} />
                  <span>Headquarters & Bureaus</span>
                </div>
                <p style={{ margin: "0 0 4px 0", fontSize: "0.88rem" }}>Lagos: Victoria Island, Lagos State, Nigeria</p>
                <p style={{ margin: 0, fontSize: "0.88rem" }}>Abuja: Central Business District, FCT, Nigeria</p>
              </div>
            </div>

            <h2>Social Handles & Verified Profiles</h2>
            <p>
              Connect with our live alert streams and community discussions across platforms:
            </p>
            <ul>
              <li><strong>WhatsApp Channel & Alerts:</strong> <a href={directWaUrl} target="_blank" rel="noopener noreferrer">News Stand NG on WhatsApp</a></li>
              <li><strong>X (Twitter):</strong> <a href="https://x.com/newsstandng" target="_blank" rel="noopener noreferrer">@newsstandng</a></li>
              <li><strong>Facebook:</strong> <a href="https://facebook.com/newsstandng" target="_blank" rel="noopener noreferrer">facebook.com/newsstandng</a></li>
              <li><strong>LinkedIn:</strong> <a href="https://linkedin.com/company/newsstandng" target="_blank" rel="noopener noreferrer">linkedin.com/company/newsstandng</a></li>
            </ul>

            <div className="static-policy-note-box">
              <strong>Emergency DMCA or Copyright Inquiries:</strong> If you are a copyright owner wishing to adjust excerpting parameters or submit takedown requests, contact our legal counsel directly at <a href="mailto:legal@newsstand.ng">legal@newsstand.ng</a> or copy <a href={`mailto:${adminEmail}`}>{adminEmail}</a>.
            </div>
          </div>
        </article>
      </main>

      <Footer onSelectCategory={() => { }} />
    </div>
  );
}
