import React, { useState } from 'react';
import { Play, X, Tv, ExternalLink } from 'lucide-react';

export default function VideoReel({ videos = [] }) {
  const [activeVideo, setActiveVideo] = useState(null);

  if (!videos || videos.length === 0) return null;

  // Extract YouTube ID if present and generate responsive iframe embed URL
  const getEmbedUrl = (vid) => {
    if (!vid) return null;

    // Check if videoId is direct 11-char YouTube ID (e.g. 'r8FW2pqYrq4')
    const directId = vid.videoId || (typeof vid.id === 'string' && vid.id.length === 11 ? vid.id : null);
    if (directId && directId.length === 11) {
      return `https://www.youtube.com/embed/${directId}?autoplay=1&rel=0&playsinline=1&modestbranding=1`;
    }

    // Check videoUrl, url or embedUrl
    const rawUrl = vid.videoUrl || vid.url || vid.embedUrl;
    if (rawUrl) {
      const match = rawUrl.match(/(?:youtu\.be\/|youtube\.com\/(?:embed\/|v\/|watch\?v=|watch\?.+&v=))([\w-]{11})/);
      if (match && match[1]) {
        return `https://www.youtube.com/embed/${match[1]}?autoplay=1&rel=0&playsinline=1&modestbranding=1`;
      }
      return rawUrl;
    }

    return null;
  };

  const getWatchUrl = (vid) => {
    if (!vid) return 'https://www.youtube.com';
    if (vid.videoUrl) return vid.videoUrl;
    const vId = vid.videoId || (typeof vid.id === 'string' && vid.id.length === 11 ? vid.id : null);
    if (vId) return `https://www.youtube.com/watch?v=${vId}`;
    if (vid.url) return vid.url;
    return 'https://www.youtube.com';
  };

  return (
    <section className="video-section app-container" aria-label="Video News Stories">
      <div className="section-header">
        <div>
          <h2 className="section-title">
            <Tv size={22} style={{ color: 'var(--danger)' }} />
            Video Bulletins & Broadcasts
          </h2>
          <div className="section-subtitle">
            Curated daily broadcast reports from Channels TV, Arise News & TVC News
          </div>
        </div>
      </div>

      <div className="video-reel">
        {videos.slice(0, 8).map((vid) => (
          <div
            key={vid.id || vid.videoId || vid.videoUrl || vid.url}
            className="video-card"
            onClick={() => setActiveVideo(vid)}
            role="button"
            tabIndex={0}
            onKeyDown={(e) => (e.key === 'Enter' || e.key === ' ') && setActiveVideo(vid)}
            aria-label={`Play broadcast: ${vid.title}`}
          >
            <div className="video-thumb-wrapper">
              <img
                src={vid.thumbnailUrl || (vid.videoId ? `https://i.ytimg.com/vi/${vid.videoId}/hqdefault.jpg` : 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=600&q=80')}
                alt={vid.title}
                className="video-thumb"
                loading="lazy"
                onError={(e) => {
                  e.currentTarget.src = 'https://images.unsplash.com/photo-1585829365295-ab7cd400c167?auto=format&fit=crop&w=600&q=80';
                }}
              />
              <div className="play-overlay-btn">
                <div className="play-icon-circle">
                  <Play size={20} fill="#FFFFFF" />
                </div>
              </div>
            </div>
            <div className="video-info">
              <h3 className="video-title">{vid.title}</h3>
              <div className="video-channel">{vid.channelName || vid.channelTitle || vid.source || 'News Broadcast'}</div>
            </div>
          </div>
        ))}
      </div>

      {/* Video Player Modal */}
      {activeVideo && (
        <div
          className="search-overlay"
          onClick={() => setActiveVideo(null)}
          role="dialog"
          aria-modal="true"
        >
          <div
            style={{
              position: 'relative',
              width: '100%',
              maxWidth: 880,
              background: '#0f172a',
              borderRadius: 16,
              overflow: 'hidden',
              boxShadow: 'var(--shadow-lg)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
            }}
            onClick={(e) => e.stopPropagation()}
          >
            {/* Modal Controls Bar */}
            <div
              style={{
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                padding: '10px 16px',
                background: 'rgba(0, 0, 0, 0.75)',
                borderBottom: '1px solid rgba(255, 255, 255, 0.08)',
              }}
            >
              <span
                style={{
                  color: '#f8fafc',
                  fontSize: '0.88rem',
                  fontWeight: 600,
                  whiteSpace: 'nowrap',
                  overflow: 'hidden',
                  textOverflow: 'ellipsis',
                  paddingRight: 12,
                }}
              >
                {activeVideo.title}
              </span>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
                <a
                  href={getWatchUrl(activeVideo)}
                  target="_blank"
                  rel="noopener noreferrer"
                  className="icon-btn"
                  style={{
                    display: 'inline-flex',
                    alignItems: 'center',
                    gap: 6,
                    padding: '6px 12px',
                    borderRadius: 8,
                    background: 'rgba(255, 255, 255, 0.1)',
                    color: '#e2e8f0',
                    fontSize: '0.78rem',
                    textDecoration: 'none',
                  }}
                  title="Watch directly on YouTube"
                >
                  <ExternalLink size={14} />
                  <span>YouTube</span>
                </a>
                <button
                  type="button"
                  className="icon-btn"
                  style={{
                    background: 'rgba(255, 255, 255, 0.15)',
                    color: '#FFF',
                    borderRadius: '50%',
                    width: 32,
                    height: 32,
                    display: 'inline-flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                  }}
                  onClick={() => setActiveVideo(null)}
                  aria-label="Close video"
                >
                  <X size={18} />
                </button>
              </div>
            </div>

            {/* Video iFrame Container */}
            <div style={{ position: 'relative', paddingBottom: '56.25%', height: 0, background: '#000' }}>
              {getEmbedUrl(activeVideo) ? (
                <iframe
                  src={getEmbedUrl(activeVideo)}
                  title={activeVideo.title}
                  allow="accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share"
                  allowFullScreen
                  referrerPolicy="strict-origin-when-cross-origin"
                  style={{
                    position: 'absolute',
                    top: 0,
                    left: 0,
                    width: '100%',
                    height: '100%',
                    border: 0,
                  }}
                />
              ) : (
                <div
                  style={{
                    position: 'absolute',
                    inset: 0,
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    justifyContent: 'center',
                    color: '#94a3b8',
                    gap: 12,
                  }}
                >
                  <p>Unable to embed this broadcast.</p>
                  <a
                    href={getWatchUrl(activeVideo)}
                    target="_blank"
                    rel="noopener noreferrer"
                    style={{
                      color: 'var(--brand-green, #10b981)',
                      textDecoration: 'underline',
                      fontWeight: 600,
                    }}
                  >
                    Watch video directly on YouTube
                  </a>
                </div>
              )}
            </div>
          </div>
        </div>
      )}
    </section>
  );
}
