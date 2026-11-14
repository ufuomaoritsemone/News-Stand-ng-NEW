import React, { useRef, useState, useCallback, useEffect } from 'react';
import { Link } from 'react-router';
import { Flame, Layers, ChevronRight, ChevronLeft } from 'lucide-react';
import { formatTimeAgo } from '../services/timeUtils';
import { getOptimizedImageUrl } from '../services/api';

export default function TrendingStoriesSection({ stories = [], onSelectStory }) {
  const railRef = useRef(null);
  const [activeDotIndex, setActiveDotIndex] = useState(0);

  // Track touch movement to prevent accidental clicks during horizontal swipe gestures
  const touchStartX = useRef(0);
  const touchStartY = useRef(0);
  const isSwiping = useRef(false);

  const activeStories = Array.isArray(stories) ? stories : [];

  const updateActiveDot = useCallback(() => {
    if (!railRef.current || activeStories.length === 0) return;
    const el = railRef.current;
    const scrollMax = el.scrollWidth - el.clientWidth;
    if (scrollMax <= 0) {
      setActiveDotIndex(0);
      return;
    }
    const ratio = Math.max(0, Math.min(1, el.scrollLeft / scrollMax));
    const nextIdx = Math.round(ratio * (activeStories.length - 1));
    setActiveDotIndex(nextIdx);
  }, [activeStories.length]);

  useEffect(() => {
    const el = railRef.current;
    if (!el) return;
    el.addEventListener('scroll', updateActiveDot, { passive: true });
    window.addEventListener('resize', updateActiveDot);
    return () => {
      el.removeEventListener('scroll', updateActiveDot);
      window.removeEventListener('resize', updateActiveDot);
    };
  }, [updateActiveDot]);

  if (!activeStories || activeStories.length === 0) {
    return null;
  }

  const scroll = (direction) => {
    if (railRef.current) {
      const scrollAmount = 340;
      railRef.current.scrollBy({
        left: direction === 'left' ? -scrollAmount : scrollAmount,
        behavior: 'smooth'
      });
    }
  };

  const handleTouchStart = (e) => {
    touchStartX.current = e.touches[0].clientX;
    touchStartY.current = e.touches[0].clientY;
    isSwiping.current = false;
  };

  const handleTouchMove = (e) => {
    const deltaX = Math.abs(e.touches[0].clientX - touchStartX.current);
    const deltaY = Math.abs(e.touches[0].clientY - touchStartY.current);
    // If movement is noticeable, flag as swipe so subsequent card click is suppressed
    if (deltaX > 8 || deltaY > 8) {
      isSwiping.current = true;
    }
  };

  const handleCardClick = (e, story) => {
    if (isSwiping.current) return;
    if (story.leadArticle && typeof onSelectStory === 'function') {
      e.preventDefault();
      onSelectStory(story.leadArticle);
    }
  };

  return (
    <section className="trending-stories-section app-container" aria-label="Trending Developing Stories">
      {/* Header Strip */}
      <div className="trending-section-header">
        <div className="trending-header-left">
          <div className="trending-badge-live">
            <span className="live-pulse-dot" />
            <Flame size={14} className="trending-flame-icon" />
            <span>DEVELOPING STORIES</span>
          </div>
          <h2 className="trending-section-title">
            What Nigeria Is Reading Right Now
          </h2>
          <span className="trending-subtitle">
            Cross-publisher verification & multi-source coverage
          </span>
        </div>
        <div className="trending-header-right">
          <div className="trending-rail-controls">
            <button
              type="button"
              className="trending-rail-nav-btn"
              onClick={() => scroll('left')}
              aria-label="Scroll left"
            >
              <ChevronLeft size={16} />
            </button>
            <button
              type="button"
              className="trending-rail-nav-btn"
              onClick={() => scroll('right')}
              aria-label="Scroll right"
            >
              <ChevronRight size={16} />
            </button>
          </div>
          <span className="trending-verified-pill">
            <Layers size={13} style={{ marginRight: 4 }} />
            Multi-Source Aggregation
          </span>
        </div>
      </div>

      {/* Horizontal Rail of Compact Cards */}
      <div className="trending-rail-container">
        <div
          className="trending-rail-track"
          ref={railRef}
          onTouchStart={handleTouchStart}
          onTouchMove={handleTouchMove}
        >
          {activeStories.map((story, idx) => {
            const thumbnail = story.imageUrl || story.leadArticle?.imageUrl;

            return (
              <article
                key={story.id || story.slug}
                className={`trending-rail-card ${idx === 0 ? 'trending-rail-card-top' : ''}`}
                onClick={(e) => handleCardClick(e, story)}
              >
                <div className="trending-rail-card-header">
                  <div className="trending-rail-badges">
                    <span className={`trending-rail-rank ${idx === 0 ? 'rank-gold' : ''}`}>
                      {idx === 0 && <Flame size={12} style={{ marginRight: 2 }} />}
                      #{idx + 1}
                    </span>
                    <span className="trending-rail-sources-badge">
                      <Layers size={11} style={{ marginRight: 3 }} />
                      {story.sourceCount || (story.sources ? story.sources.length : 2)} Sources
                    </span>
                  </div>
                  <div className="trending-rail-meta-right">
                    <span className="trending-rail-category">{story.category || 'News'}</span>
                    <span className="meta-bullet">•</span>
                    <span className="trending-rail-time" suppressHydrationWarning>{formatTimeAgo(story.updatedAt)}</span>
                  </div>
                </div>

                <div className="trending-rail-body">
                  {thumbnail && (
                    <div className="trending-rail-thumb-wrapper">
                      <img
                        src={getOptimizedImageUrl(thumbnail, 320)}
                        alt=""
                        width="156"
                        height="88"
                        className="trending-rail-thumb"
                        loading="lazy"
                        decoding="async"
                        onError={(e) => {
                          if (e.currentTarget.parentElement) {
                            e.currentTarget.parentElement.style.display = 'none';
                          }
                        }}
                      />
                    </div>
                  )}
                  <div className="trending-rail-content">
                    <h3 className="trending-rail-title">
                      <Link
                        to={`/story/${story.slug}`}
                        onClick={(e) => {
                          if (isSwiping.current) {
                            e.preventDefault();
                            return;
                          }
                          if (story.leadArticle && typeof onSelectStory === 'function') {
                            e.preventDefault();
                            onSelectStory(story.leadArticle);
                          }
                        }}
                      >
                        {story.title}
                      </Link>
                    </h3>
                  </div>
                </div>

                <div className="trending-rail-card-footer">
                  {story.sources && story.sources.length > 0 && (
                    <span className="trending-rail-sources-text">
                      {story.sources.slice(0, 3).join(', ')}
                      {story.sources.length > 3 && ` +${story.sources.length - 3}`}
                    </span>
                  )}
                  <span
                    className="trending-rail-arrow"
                    aria-hidden="true"
                  >
                    <ChevronRight size={15} />
                  </span>
                </div>
              </article>
            );
          })}
        </div>

        {/* Mobile Visual Pagination Dots */}
        {activeStories.length > 1 && (
          <div className="trending-mobile-dots" aria-hidden="true">
            {activeStories.map((_, i) => (
              <span
                key={i}
                className={`trending-dot ${i === activeDotIndex ? 'active' : ''}`}
              />
            ))}
          </div>
        )}
      </div>
    </section>
  );
}
