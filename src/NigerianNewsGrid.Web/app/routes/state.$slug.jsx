import React from 'react';
import { useLoaderData, Link, redirect } from 'react-router';
import { api } from '../../src/services/api';
import { formatTimeAgo } from '../../src/services/timeUtils';
import { getHreflangTags } from '../../src/services/localization';
import { paginatedUrl, paginationLinks } from '../utils/seo';
import Footer from '../../src/components/Footer';
import {
  MapPin,
  Building,
  HelpCircle,
  ChevronDown,
  ArrowLeft,
  Tag,
  Compass,
  ArrowRight,
  Layers
} from 'lucide-react';

export function meta({ data, location }) {
  if (!data?.state) {
    return [
      { title: 'State Not Found | News Stand NG' },
      { name: 'robots', content: 'noindex, nofollow' },
    ];
  }

  const { state, page = 1 } = data;
  const isPidgin = location?.pathname?.startsWith('/pcm');
  const baseTitle = isPidgin
    ? `${state.name} News Today: Latest Local Tori From ${state.capital} | News Stand NG`
    : `${state.name} State News Today: Latest Headlines & Local Developments | News Stand NG`;
  const title = page > 1 ? `${baseTitle} — Page ${page}` : baseTitle;
  const description = isPidgin
    ? `Read verified local news from ${state.name} State (${state.capital}), commercial updates, and local community stories on News Stand NG.`
    : `Comprehensive, verified news reporting from ${state.name} State, Nigeria. Track local politics, infrastructure, economy, and community stories from ${state.capital} and major hubs.`;
  const canonicalPath = isPidgin ? `/pcm/state/${state.slug}` : `/state/${state.slug}`;
  const canonicalUrl = paginatedUrl(canonicalPath, page);
  const hreflangs = getHreflangTags(`/state/${state.slug}`);
  const totalPages = state.totalPages || 1;

  return [
    { title },
    { name: 'description', content: description },
    { tagName: 'link', rel: 'canonical', href: canonicalUrl },
    { name: 'robots', content: 'index, follow, max-image-preview:large, max-snippet:-1, max-video-preview:-1' },
    { property: 'og:type', content: 'website' },
    { property: 'og:title', content: title },
    { property: 'og:description', content: description },
    { property: 'og:url', content: canonicalUrl },
    { property: 'og:site_name', content: 'News Stand NG' },
    { name: 'twitter:card', content: 'summary_large_image' },
    { name: 'twitter:title', content: title },
    { name: 'twitter:description', content: description },
    ...hreflangs,
    ...paginationLinks(canonicalPath, page, totalPages),
  ];
}

export async function loader({ params, request }) {
  const { slug } = params;
  if (!slug) {
    throw new Response('State slug is required', { status: 400 });
  }

  // 301 Permanent Redirect if someone requests /state/lagos-state instead of /state/lagos
  if (slug.endsWith('-state')) {
    const clean = slug.slice(0, -6);
    const prefix = new URL(request.url).pathname.startsWith('/pcm') ? '/pcm' : '';
    return redirect(`${prefix}/state/${clean}`, 301);
  }

  const url = new URL(request.url);
  const isPidgin = url.pathname.startsWith('/pcm');
  const page = Math.max(1, parseInt(url.searchParams.get('page') || '1', 10) || 1);
  const category = url.searchParams.get('category') || '';

  try {
    const state = await api.getState(slug, { page, pageSize: 18, category });
    if (!state) {
      throw new Response('State Not Found', { status: 404 });
    }
    return { state, isPidgin, page, category };
  } catch (err) {
    if (err instanceof Response) throw err;
    console.warn(`[State Loader] Failed for slug '${slug}':`, err.message);
    const status = err?.status === 404 ? 404 : 503;
    throw new Response(status === 404 ? 'State Not Found' : 'Service Temporarily Unavailable', { status });
  }
}

export default function StateDetailPage() {
  const { state, isPidgin, page = 1, category = '' } = useLoaderData();

  const homePath = isPidgin ? '/pcm' : '/';
  const canonicalStatePath = isPidgin ? `/pcm/state/${state.slug}` : `/state/${state.slug}`;
  const statesDirectoryPath = isPidgin ? '/pcm/states' : '/states';
  const newsPathPrefix = isPidgin ? '/pcm/news' : '/news';

  const totalCount = typeof state.articleCount === 'number' ? state.articleCount : (state.articles?.length || 0);
  const totalPages = state.totalPages || Math.max(1, Math.ceil(totalCount / (state.pageSize || 18)));
  const activeCategory = category || state.activeCategory || '';
  const availableCategories = state.availableCategories || [];

  const buildFilterUrl = (nextPage, nextCategory) => {
    const params = new URLSearchParams();
    if (nextCategory && nextCategory !== 'All') {
      params.set('category', nextCategory);
    }
    if (nextPage && nextPage > 1) {
      params.set('page', String(nextPage));
    }
    const qs = params.toString();
    return `${canonicalStatePath}${qs ? `?${qs}` : ''}`;
  };

  const placeJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'AdministrativeArea',
    'name': state.name === 'Federal Capital Territory' ? state.name : `${state.name} State`,
    'alternateName': state.name,
    'description': state.intro,
    'url': `https://newsstand.ng${canonicalStatePath}`,
    'containedInPlace': {
      '@type': 'Country',
      'name': 'Nigeria'
    }
  };

  const faqJsonLd = state.faqs && state.faqs.length > 0 ? {
    '@context': 'https://schema.org',
    '@type': 'FAQPage',
    'mainEntity': state.faqs.map(faq => ({
      '@type': 'Question',
      'name': faq.question,
      'acceptedAnswer': {
        '@type': 'Answer',
        'text': faq.answer
      }
    }))
  } : null;

  const breadcrumbsJsonLd = {
    '@context': 'https://schema.org',
    '@type': 'BreadcrumbList',
    'itemListElement': [
      { '@type': 'ListItem', 'position': 1, 'name': 'Home', 'item': `https://newsstand.ng${homePath}` },
      { '@type': 'ListItem', 'position': 2, 'name': '36 States', 'item': `https://newsstand.ng${statesDirectoryPath}` },
      { '@type': 'ListItem', 'position': 3, 'name': state.name, 'item': `https://newsstand.ng${canonicalStatePath}` }
    ]
  };

  const articles = state.articles || [];
  const topTopics = state.topTopics || [];
  const majorCities = state.majorCities || [];
  const faqs = state.faqs || [];

  return (
    <div className="data-page-layout">
      {/* Structured Data */}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(placeJsonLd) }}
      />
      {faqJsonLd && (
        <script
          type="application/ld+json"
          dangerouslySetInnerHTML={{ __html: JSON.stringify(faqJsonLd) }}
        />
      )}
      <script
        type="application/ld+json"
        dangerouslySetInnerHTML={{ __html: JSON.stringify(breadcrumbsJsonLd) }}
      />

      {/* Top Header */}
      <header className="article-page-header">
        <div className="article-header-container">
          <Link to={statesDirectoryPath} className="article-header-back-btn" title="Back to All States">
            <ArrowLeft size={18} />
            <span>All States</span>
          </Link>
          <Link to={homePath} className="article-header-brand">
            <span className="brand-dot" />
            <span className="brand-title">News Stand NG</span>
          </Link>
          <div className="header-badge-tag">
            <MapPin size={14} />
            <span>{state.geopoliticalZone}</span>
          </div>
        </div>
      </header>

      <main className="data-page-container">
        {/* Breadcrumb Navigation */}
        <nav className="article-breadcrumb-nav" aria-label="Breadcrumb">
          <ol>
            <li><Link to={homePath}>Home</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li><Link to={statesDirectoryPath}>36 States</Link></li>
            <li><span className="breadcrumb-sep">/</span></li>
            <li className="breadcrumb-active" aria-current="page">{state.name}</li>
          </ol>
        </nav>

        {/* Rich State Detail Hub Masthead */}
        <section className="state-detail-masthead">
          <div className="state-story-list-header" style={{ margin: 0, paddingBottom: 16, marginBottom: 16 }}>
            <div className="state-story-header-left">
              <div className="state-badge-row">
                <span className="state-zone-badge">{state.geopoliticalZone}</span>
                <span className="state-count-pill">
                  {totalCount} {totalCount === 1 ? 'Story' : 'Stories'}
                </span>
                {state.slogan && (
                  <span className="state-slogan-badge">"{state.slogan}"</span>
                )}
              </div>
              <h1 className="state-masthead-title">
                {state.name} {state.name !== 'Federal Capital Territory' && 'State'} News & Stories
              </h1>
            </div>
            <Link to={statesDirectoryPath} className="state-back-all-btn">
              <ArrowLeft size={15} />
              <span>All 36 States</span>
            </Link>
          </div>

          <div className="state-meta-strip">
            <div className="state-meta-item">
              <Building size={15} />
              <span>Capital: <strong>{state.capital}</strong></span>
            </div>
            {majorCities.length > 0 && (
              <div className="state-meta-item">
                <Compass size={15} />
                <span>Major Hubs: <strong>{majorCities.join(', ')}</strong></span>
              </div>
            )}
            <div className="state-meta-item">
              <Layers size={15} />
              <span>{totalCount} Verified Reports</span>
            </div>
          </div>

          {state.intro && (
            <p className="state-masthead-intro">{state.intro}</p>
          )}
        </section>

        {/* Related Topics & Regional Entities Bar */}
        {topTopics.length > 0 && (
          <div className="state-topics-bar" aria-label={`Trending topics in ${state.name}`}>
            <div className="topics-bar-label">
              <Tag size={14} />
              <span>{isPidgin ? 'Related Topics:' : 'Regional Topics & Entities:'}</span>
            </div>
            <div className="state-topics-list">
              {topTopics.map((topic) => (
                <Link
                  key={topic.slug || topic.id}
                  to={`/topic/${topic.slug}`}
                  className="state-topic-chip"
                >
                  {topic.name}
                </Link>
              ))}
            </div>
          </div>
        )}

        {/* Category Filter Chips */}
        {availableCategories.length > 1 && (
          <div className="zone-filter-chips" style={{ marginBottom: 24 }} role="navigation" aria-label="Filter state stories by category">
            <Link
              to={buildFilterUrl(1, '')}
              className={`zone-chip-btn ${!activeCategory ? 'active' : ''}`}
            >
              All Categories
            </Link>
            {availableCategories.map((cat) => (
              <Link
                key={cat}
                to={buildFilterUrl(1, cat)}
                className={`zone-chip-btn ${activeCategory.toLowerCase() === cat.toLowerCase() ? 'active' : ''}`}
              >
                {cat}
              </Link>
            ))}
          </div>
        )}

        {/* Stories List Feed */}
        <section className="state-stories-container" aria-label={`News stories from ${state.name}`}>
          {articles.length > 0 ? (
            <>
              <div className="state-articles-list">
                {articles.map((art) => (
                  <article key={art.id} className="state-story-item-card">
                    {art.imageUrl && (
                      <div className="state-story-item-thumb">
                        <Link to={`${newsPathPrefix}/${art.slug || art.id}`}>
                          <img
                            src={art.imageUrl}
                            alt={art.title}
                            className="state-story-img"
                            loading="lazy"
                          />
                        </Link>
                        <span className="state-story-cat-badge">
                          {art.category || 'General'}
                        </span>
                      </div>
                    )}
                    <div className="state-story-item-content">
                      <div className="state-story-meta-top">
                        <span className="state-story-source">{art.source || 'News Stand NG'}</span>
                        <span className="meta-dot">•</span>
                        <span className="state-story-time">{formatTimeAgo(art.publishedAt)}</span>
                      </div>
                      <h2 className="state-story-title">
                        <Link to={`${newsPathPrefix}/${art.slug || art.id}`} className="state-story-title-link">
                          {art.title}
                        </Link>
                      </h2>
                      {(art.excerpt || art.summary) && (
                        <p className="state-story-summary">
                          {(art.excerpt || art.summary).length > 180
                            ? `${(art.excerpt || art.summary).slice(0, 180)}...`
                            : (art.excerpt || art.summary)}
                        </p>
                      )}
                      <div className="state-story-footer-actions">
                        <Link to={`${newsPathPrefix}/${art.slug || art.id}`} className="state-read-story-btn">
                          <span>Read full story</span>
                          <ArrowRight size={13} />
                        </Link>
                      </div>
                    </div>
                  </article>
                ))}
              </div>

              {/* Pagination Controls */}
              {totalPages > 1 && (
                <div className="topic-pagination-bar" style={{ marginBottom: 48 }}>
                  {page > 1 && (
                    <Link
                      to={buildFilterUrl(page - 1, activeCategory)}
                      className="pagination-btn"
                    >
                      Previous
                    </Link>
                  )}
                  <span className="pagination-current">
                    Page {page} of {totalPages}
                  </span>
                  {page < totalPages && (
                    <Link
                      to={buildFilterUrl(page + 1, activeCategory)}
                      className="pagination-btn"
                    >
                      Next
                    </Link>
                  )}
                </div>
              )}
            </>
          ) : (
            <div className="empty-state-box">
              <p>
                {activeCategory
                  ? `No ${activeCategory} news articles have been tagged for ${state.name} yet.`
                  : `No recent news articles have been tagged for ${state.name} yet.`}
              </p>
              {activeCategory ? (
                <Link to={canonicalStatePath} className="clear-filters-btn">
                  Show All {state.name} Stories
                </Link>
              ) : (
                <Link to={statesDirectoryPath} className="clear-filters-btn">
                  Browse Other States
                </Link>
              )}
            </div>
          )}
        </section>

        {/* Visible FAQ Accordion matching FAQPage JSON-LD Schema */}
        {faqs.length > 0 && (
          <section className="story-faq-section" aria-label={`Frequently Asked Questions about ${state.name}`}>
            <h2 className="faq-section-title">
              <HelpCircle size={18} />
              <span>Frequently Asked Questions About {state.name}</span>
            </h2>
            <div className="faq-accordion-group">
              {faqs.map((faq, idx) => (
                <details key={idx} className="story-faq-item" open={idx === 0}>
                  <summary className="story-faq-question">
                    <span className="faq-q-text">{faq.question}</span>
                    <ChevronDown size={16} className="faq-chevron" />
                  </summary>
                  <div className="story-faq-answer">
                    <p>{faq.answer}</p>
                  </div>
                </details>
              ))}
            </div>
          </section>
        )}
      </main>

      <Footer />
    </div>
  );
}
