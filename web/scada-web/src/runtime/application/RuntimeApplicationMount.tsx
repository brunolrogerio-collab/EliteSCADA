import React, { useEffect, useMemo, useRef, useState } from 'react';
import { useMobileRuntime } from '../useMobileRuntime';
import { appShellText, useAppShellLocale } from '../../appShellI18n';
import { UserSessionMenu } from '../../auth/UserSessionMenu';
import type { RuntimeHeaderDateTimeEngineering } from '../../engineering/types';
import { hasRuntimeCapability, useEffectiveCapabilities } from '../../auth/effectiveCapabilities';
import { ApplicationBrand, resolveApplicationBranding } from '../../branding/ApplicationBranding';
import type { ScriptEngineeringContext } from '../../engineering/scripts/scriptEngineeringTypes';
import { RuntimeAlarmCenter } from '../RuntimeAlarmCenter';
import { HistoricalDataBrowserRuntime } from '../historical-browser/HistoricalDataBrowserRuntime';
import { historicalBrowserCopy } from '../historical-browser/historicalBrowserI18n';
import { HistoricalPlaybackProvider, useHistoricalPlayback } from '../historical-playback/HistoricalPlaybackContext';
import { HistoricalPlaybackOverlay } from '../historical-playback/HistoricalPlaybackOverlay';
import { historicalPlaybackCopy } from '../historical-playback/historicalPlaybackI18n';
import { RuntimeVisualNavigator } from '../visual-navigation/RuntimeVisualNavigator';
import {
  loadRuntimeApplicationProjection,
  RuntimeApplicationProjectionError,
  RuntimeApplicationTransportError,
  runtimeVisualAssetContentUrl,
  type RuntimeApplicationProjection
} from './runtimeApplicationApi';
import { resolveRuntimeStartupScreen } from './runtimeStartupScreen';

const REFRESH_INTERVAL_MS = 1500;
const RETRYABLE_RUNTIME_PROJECTION_STATUSES = new Set([502, 503, 504]);
const TRUNCATED_RESPONSE_SIGNATURE = 'content-length header of network response exceeds response body';

export type RuntimeApplicationMountProps = {
  showHistoryNavigation?: boolean;
  showFullscreenControl?: boolean;
};

export function RuntimeApplicationMount({
  showHistoryNavigation = false,
  showFullscreenControl = false
}: RuntimeApplicationMountProps = {}) {
  const locale = useAppShellLocale();
  const text = appShellText(locale);
  const [projection, setProjection] = useState<RuntimeApplicationProjection | null>(null);
  const lastSuccessfulProjection = useRef<RuntimeApplicationProjection | null>(null);
  const [error, setError] = useState<Error | null>(null);

  useEffect(() => {
    let disposed = false;
    let inFlight = false;
    let activeController: AbortController | null = null;

    const refresh = async () => {
      if (disposed || inFlight) return;
      inFlight = true;
      const controller = new AbortController();
      activeController = controller;
      try {
        const next = await loadRuntimeApplicationProjection(controller.signal);
        if (disposed) return;
        lastSuccessfulProjection.current = next;
        setProjection(current => sameRuntimeProjection(current, next) ? current : next);
        setError(null);
      } catch (reason) {
        if (disposed || controller.signal.aborted) return;
        const failure = reason instanceof Error ? reason : new Error(String(reason));
        if (lastSuccessfulProjection.current && isRetryableRuntimeProjectionFailure(failure)) {
          setError(null);
          return;
        }
        lastSuccessfulProjection.current = null;
        setProjection(null);
        setError(failure);
      } finally {
        if (activeController === controller) activeController = null;
        inFlight = false;
      }
    };

    void refresh();
    const timer = window.setInterval(() => void refresh(), REFRESH_INTERVAL_MS);
    return () => {
      disposed = true;
      window.clearInterval(timer);
      activeController?.abort();
    };
  }, []);

  if (error) {
    const status = error instanceof RuntimeApplicationProjectionError ? error.status : 500;
    return <main className="shell" data-testid="runtime-application-error">
      <section className="runtime-visual-diagnostic" role="alert" data-diagnostic-code="HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE">
        <strong>HMI_RUNTIME_ACTIVE_PROJECTION_UNAVAILABLE</strong>
        <span>{text.runtimeUnavailable} ({status}) {error.message}</span>
      </section>
    </main>;
  }

  if (!projection) {
    return <main className="shell" data-testid="runtime-application-loading">
      <section className="runtime-visual-diagnostic" role="status">
        <strong>Runtime</strong><span>…</span>
      </section>
    </main>;
  }

  if (projection.mode !== 'engineering') {
    return <main className="shell" data-testid="runtime-neutral">
      <section className="runtime-visual-diagnostic" role="status" data-diagnostic-code="RUNTIME_ACTIVE_REVISION_NOT_SELECTED">
        <strong>{text.runtimeNotActive}</strong>
        <span>{text.runtimeNotActiveDescription}</span>
      </section>
    </main>;
  }
  return <EngineeringRuntimeApplication projection={projection} locale={locale} showHistoryNavigation={showHistoryNavigation} showFullscreenControl={showFullscreenControl} />;
}

function EngineeringRuntimeApplication(props: {
  projection: RuntimeApplicationProjection;
  locale: ReturnType<typeof useAppShellLocale>;
  showHistoryNavigation: boolean;
  showFullscreenControl: boolean;
}) {
  return <HistoricalPlaybackProvider><EngineeringRuntimeApplicationContent {...props} /></HistoricalPlaybackProvider>;
}

function EngineeringRuntimeApplicationContent({
  projection,
  locale,
  showHistoryNavigation,
  showFullscreenControl
}: {
  projection: RuntimeApplicationProjection;
  locale: ReturnType<typeof useAppShellLocale>;
  showHistoryNavigation: boolean;
  showFullscreenControl: boolean;
}) {
  const text = appShellText(locale);
  const playback = useHistoricalPlayback();
  const playbackText = historicalPlaybackCopy(locale);
  const { capabilities } = useEffectiveCapabilities();
  const historyText = historicalBrowserCopy(locale);
  const fullscreenRoot = useRef<HTMLElement>(null);
  const mobile = useMobileRuntime();
  const [requestedScreen, setRequestedScreen] = useState<string | null>(null);
  const [isFullscreen, setIsFullscreen] = useState(Boolean(document.fullscreenElement));
  const [alarmsOpen, setAlarmsOpen] = useState(false);
  const [historyOpen, setHistoryOpen] = useState(false);
  const [playbackOpen, setPlaybackOpen] = useState(false);
  const engineeringPackage = projection.package!;
  const header = engineeringPackage.runtimePresentation?.header;
  const compact = isFullscreen || mobile;
  const headerVisible = header?.enabled !== false;
  const playbackConfigured = engineeringPackage.runtimePresentation?.historicalPlaybackEnabled === true;
  const playbackAvailable = playbackConfigured &&
    hasRuntimeCapability(capabilities, 'View') &&
    hasRuntimeCapability(capabilities, 'TrendUse');
  const startup = useMemo(
    () => resolveRuntimeStartupScreen(engineeringPackage),
    [engineeringPackage]
  );
  const activeScreenKey = requestedScreen ?? startup.screenKey;
  const activeScreen = (engineeringPackage.screens ?? []).find(screen =>
    screen.key.toLocaleLowerCase() === activeScreenKey?.toLocaleLowerCase()
  );
  const projectTitle = projection.projectName || projection.projectKey || text.runtime;
  const headerHeight = Math.max(56, Math.min(168, header?.height ?? 56));
  const headerStyle = {
    minHeight: headerHeight,
    background: header?.backgroundColor ?? undefined,
    '--runtime-header-brand-scale': String(headerHeight / 56),
    '--runtime-header-background': header?.backgroundColor ?? 'var(--app-surface)',
    '--runtime-header-foreground': header?.backgroundColor ? runtimeHeaderContrastColor(header.backgroundColor) : 'var(--app-text-primary)'
  } as React.CSSProperties;
  const projectTitleStyle: React.CSSProperties = {
    fontFamily: header?.titleStyle?.fontFamily ?? undefined,
    fontSize: header?.titleStyle?.fontSize ?? undefined,
    fontWeight: header?.titleStyle?.fontWeight ?? 700,
    color: header?.titleStyle?.color ?? undefined
  };
  const screenTitleStyle: React.CSSProperties = {
    fontFamily: header?.screenNameStyle?.fontFamily ?? header?.titleStyle?.fontFamily ?? undefined,
    fontSize: header?.screenNameStyle?.fontSize ?? 12,
    fontWeight: header?.screenNameStyle?.fontWeight ?? 500,
    color: header?.screenNameStyle?.color ?? undefined
  };
  const branding = useMemo(
    () => resolveApplicationBranding(
      engineeringPackage.branding,
      engineeringPackage.visualAssets ?? [],
      runtimeVisualAssetContentUrl
    ),
    [engineeringPackage.branding, engineeringPackage.visualAssets]
  );

  useEffect(() => {
    const changed = () => setIsFullscreen(document.fullscreenElement === fullscreenRoot.current);
    document.addEventListener('fullscreenchange', changed);
    return () => document.removeEventListener('fullscreenchange', changed);
  }, []);

  useEffect(() => {
    if (!playbackAvailable) {
      setPlaybackOpen(false);
      if (playback.mode === 'historicalPlayback') playback.exitPlayback();
    }
  }, [playbackAvailable, playback.mode, playback.exitPlayback]);

  const scriptContext = useMemo<ScriptEngineeringContext>(() => ({
    workspace: {
      projectKey: projection.projectKey ?? null,
      projectName: projection.projectName ?? null,
      baseRevision: projection.revision ?? null,
      isDirty: false,
      changeVersion: 0
    },
    scripts: engineeringPackage.scripts ?? [],
    visualEventReferences: engineeringPackage.scriptVisualEventReferences ?? []
  }), [engineeringPackage, projection.projectKey, projection.projectName, projection.revision]);

  const toggleFullscreen = async () => {
    if (document.fullscreenElement) await document.exitFullscreen();
    else await fullscreenRoot.current?.requestFullscreen();
  };

  const toggleAlarms = () => {
    setAlarmsOpen(current => {
      const next = !current;
      if (next) { setHistoryOpen(false); setPlaybackOpen(false); }
      return next;
    });
  };

  const showOverview = () => {
    setHistoryOpen(false);
    setAlarmsOpen(false);
    setPlaybackOpen(false);
  };

  const toggleHistory = () => {
    setHistoryOpen(current => {
      const next = !current;
      if (next) { setAlarmsOpen(false); setPlaybackOpen(false); }
      return next;
    });
  };

  const togglePlayback = () => {
    setPlaybackOpen(current => {
      const next = !current;
      if (next) { setHistoryOpen(false); setAlarmsOpen(false); }
      return next;
    });
  };

  const runtimeToolbar = <div className="runtime-operator-toolbar" role="toolbar" aria-label={text.runtime}>
    {header?.overviewVisible !== false && <RuntimeOperatorTool
      label={text.runtimeOverview} icon="overview" active={!historyOpen && !alarmsOpen} onClick={showOverview}
    />}
    {showHistoryNavigation && header?.historyVisible !== false ? <RuntimeOperatorTool
      label={text.runtimeHistory} icon="history" active={historyOpen} expanded={historyOpen}
      controls="runtime-history-overlay" onClick={toggleHistory}
    /> : null}
    {header?.alarmsVisible !== false && <RuntimeOperatorTool
      label={text.alarms} icon="alarms" active={alarmsOpen} expanded={alarmsOpen}
      controls="runtime-alarm-overlay" disabled={playback.mode === 'historicalPlayback'} onClick={toggleAlarms}
    />}
    {playbackAvailable && header?.playbackVisible !== false ? <RuntimeOperatorTool
      label={playbackText.title} icon="playback" active={playback.mode === 'historicalPlayback'}
      expanded={playbackOpen} controls="runtime-playback-overlay" onClick={togglePlayback}
    /> : null}
    {showFullscreenControl && !compact ? <RuntimeOperatorTool
      label={text.fullscreen} icon="fullscreen" active={false} onClick={() => void toggleFullscreen()}
    /> : null}
    {(header?.links ?? []).map((link, index) => <button key={index} type="button" className="runtime-operator-tool"
      title={link.label} aria-label={link.label} onClick={() => { setRequestedScreen(link.screenKey); showOverview(); }}>
      {link.visualAssetId ? <img src={runtimeVisualAssetContentUrl(link.visualAssetId)} alt="" style={{ width: 28, height: 28, objectFit: 'contain' }}/> : link.label}
    </button>)}
  </div>;

  if (!startup.screenKey) {
    return <main className="shell" data-testid="runtime-engineering-application">
      <section className="runtime-visual-diagnostic" role="alert" data-diagnostic-code={startup.diagnosticCode ?? undefined}>
        <strong>{startup.diagnosticCode}</strong>
        <span>{text.runtimeUnavailable} {startup.detail}</span>
      </section>
    </main>;
  }

  return <main
    ref={fullscreenRoot}
    className="runtime-operator-application"
    style={{ gridTemplateRows: headerVisible ? undefined : 'minmax(0, 1fr)' }}
    data-testid="runtime-engineering-application"
    data-runtime-project-key={projection.projectKey ?? undefined}
    data-runtime-revision={projection.revision ?? undefined}
    data-runtime-fullscreen={isFullscreen || undefined}
    data-runtime-temporal-mode={playback.mode === 'historicalPlayback' ? 'historical-playback' : 'live'}
    data-runtime-historical-at={playback.atUtc ?? undefined}
  >
    {headerVisible ? <header className={`runtime-operator-bar${compact ? ' runtime-operator-bar--fullscreen' : ''}${headerHeight > 56 ? ' runtime-operator-bar--brand-expanded' : ''}`} style={headerStyle}>
      <div className="runtime-operator-side runtime-operator-side--left">
        {compact ? <div className="runtime-operator-brand">
          <ApplicationBrand branding={branding} defaultSubtitle={text.subtitle} href="/"/>
        </div> : null}
        {header?.dateTime?.mode && header.dateTime.mode !== 'off' && (header.dateTime.position ?? 'right') === 'left'
          ? <RuntimeHeaderDateTime locale={locale} settings={header.dateTime} style={{ order: header.dateTime.order ?? 1 }}/> : null}
        {header?.controlsPosition === 'left' ? <div className="runtime-operator-toolbar-slot" style={{ order: header.controlsOrder ?? 2 }}>{runtimeToolbar}</div> : null}
      </div>
      <div className="runtime-operator-context" style={{ textAlign: header?.titlePosition ?? 'left' }} title={projectTitle}>
        <div className="runtime-operator-context__titles">
          <strong style={projectTitleStyle}>{projectTitle}</strong>
          {header?.showScreenName && activeScreen ? <span className="runtime-operator-context__screen" style={screenTitleStyle}>{activeScreen.name || activeScreen.key}</span> : null}
          {!compact ? <span className="runtime-operator-context__revision">rev {projection.revision}</span> : null}
        </div>
        {playback.mode === 'historicalPlayback' && playback.atUtc ? <>
          <span className="runtime-playback-badge">
            {playbackText.playback} · {new Intl.DateTimeFormat(locale,{hour:'2-digit',minute:'2-digit',second:'2-digit'}).format(new Date(playback.atUtc))} · {playbackText.readOnly}
          </span>
          <button type="button" className="runtime-playback-live-shortcut" onClick={playback.exitPlayback}>{playbackText.backLive}</button>
        </> : null}
      </div>
      <div className="runtime-operator-side runtime-operator-side--right">
        {header?.controlsPosition !== 'left' ? <div className="runtime-operator-toolbar-slot" style={{ order: header?.controlsOrder ?? 2 }}>{runtimeToolbar}</div> : null}
        {header?.dateTime?.mode && header.dateTime.mode !== 'off' && (header.dateTime.position ?? 'right') === 'right'
          ? <RuntimeHeaderDateTime locale={locale} settings={header.dateTime} style={{ order: header.dateTime.order ?? 1 }}/> : null}
        {compact ? <div className="runtime-operator-user-slot" data-testid="runtime-user-summary">
          <UserSessionMenu locale={locale} includeRuntimeSessionControls
            runtimeFullscreenExit={isFullscreen ? { label: text.exitFullscreen, onActivate: () => void toggleFullscreen() } : undefined}/>
        </div> : null}
      </div>
    </header> : <div className="runtime-hidden-header-session" aria-label={text.runtime}>
      <UserSessionMenu locale={locale} includeRuntimeSessionControls runtimeFullscreenExit={isFullscreen ? { label: text.exitFullscreen, onActivate: () => void toggleFullscreen() } : undefined}/>
    </div>}

    <section className="runtime-engineering-canvas" data-testid="runtime-engineering-canvas">
      <RuntimeVisualNavigator
        engineeringPackage={engineeringPackage}
        initialScreenKey={requestedScreen ?? startup.screenKey}
        mobileScreens={engineeringPackage.runtimePresentation?.mobileScreens ?? undefined}
        mobileOrientation={engineeringPackage.runtimePresentation?.mobileOrientation ?? 'landscape'}
        locale={locale}
        scriptContext={scriptContext}
        emptyLabel={text.emptyVisual}
        visualAssetUrl={runtimeVisualAssetContentUrl}
        onVisualContextChange={playback.setVisualScope}
      />
    </section>

    {historyOpen ? <aside id="runtime-history-overlay" className="runtime-operator-overlay runtime-operator-overlay--history" aria-label={historyText.title} data-testid="runtime-history-overlay">
      <div className="runtime-operator-overlay-header">
        <strong>{historyText.title}</strong>
        <button type="button" className="runtime-operator-button" onClick={() => setHistoryOpen(false)}>{historyText.closeHistory}</button>
      </div>
      <div className="runtime-operator-overlay-content runtime-history-overlay-content">
        <HistoricalDataBrowserRuntime locale={locale} />
      </div>
    </aside> : null}

    {playbackOpen && playbackAvailable ? <HistoricalPlaybackOverlay locale={locale} onClose={() => setPlaybackOpen(false)} /> : null}

    <aside id="runtime-alarm-overlay" className="runtime-operator-overlay" aria-label={text.alarms} hidden={!alarmsOpen}>
      <div className="runtime-operator-overlay-header">
        <strong>{text.alarms}</strong>
        <button type="button" className="runtime-operator-button" onClick={() => setAlarmsOpen(false)}>{text.closeAlarms}</button>
      </div>
      <div className="runtime-operator-overlay-content">
        <RuntimeAlarmCenter locale={locale} visible={alarmsOpen} />
      </div>
    </aside>
  </main>;
}

type RuntimeOperatorIconName = 'overview' | 'history' | 'alarms' | 'playback' | 'fullscreen' | 'exitFullscreen';

function RuntimeOperatorTool({
  label,
  icon,
  active,
  expanded,
  controls,
  disabled = false,
  onClick
}: {
  label: string;
  icon: RuntimeOperatorIconName;
  active: boolean;
  expanded?: boolean;
  controls?: string;
  disabled?: boolean;
  onClick: () => void;
}) {
  return <button
    type="button"
    className={`runtime-operator-tool${active ? ' active' : ''}`}
    aria-label={label}
    aria-pressed={active}
    aria-expanded={expanded}
    aria-controls={controls}
    aria-disabled={disabled || undefined}
    disabled={disabled}
    title={label}
    data-tooltip={label}
    onClick={onClick}
  >
    <RuntimeOperatorIcon name={icon} />
    <span className="sr-only">{label}</span>
  </button>;
}

function RuntimeHeaderDateTime({ locale, settings, style }: {
  locale: 'pt-BR' | 'en' | 'es'; settings: RuntimeHeaderDateTimeEngineering; style?: React.CSSProperties;
}) {
  const mode = settings.mode ?? 'off';
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    if (mode === 'date') return;
    const timer = window.setInterval(() => setNow(new Date()), 1000);
    return () => window.clearInterval(timer);
  }, [mode]);
  if (mode === 'off') return null;
  const time = new Intl.DateTimeFormat(locale, {
    hour: '2-digit', minute: '2-digit', second: '2-digit', hour12: settings.timeFormat === '12h'
  }).format(now);
  const date = formatRuntimeHeaderDate(now, settings.dateFormat ?? 'dd/MM/yyyy');
  return <time className="runtime-header-datetime" dateTime={now.toISOString()} style={style} data-testid="runtime-header-datetime">
    {mode !== 'date' ? <span>{time}</span> : null}
    {mode !== 'time' ? <span>{date}</span> : null}
  </time>;
}

function formatRuntimeHeaderDate(date: Date, format: NonNullable<RuntimeHeaderDateTimeEngineering['dateFormat']>): string {
  const parts = new Intl.DateTimeFormat('en-CA', { day: '2-digit', month: '2-digit', year: 'numeric' }).formatToParts(date);
  const values = Object.fromEntries(parts.map(part => [part.type, part.value]));
  if (format === 'MM/dd/yyyy') return `${values.month}/${values.day}/${values.year}`;
  if (format === 'yyyy-MM-dd') return `${values.year}-${values.month}-${values.day}`;
  return `${values.day}/${values.month}/${values.year}`;
}

function runtimeHeaderContrastColor(color: string): string {
  const match = /^#([0-9a-f]{6})/i.exec(color);
  if (!match) return 'var(--app-text-primary)';
  const channels = [0, 2, 4].map(offset => Number.parseInt(match[1].slice(offset, offset + 2), 16) / 255)
    .map(channel => channel <= 0.04045 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4);
  const luminance = channels[0] * 0.2126 + channels[1] * 0.7152 + channels[2] * 0.0722;
  return luminance > 0.42 ? '#17212b' : '#ffffff';
}

function RuntimeOperatorIcon({ name }: { name: RuntimeOperatorIconName }) {
  if (name === 'overview') {
    return <svg className="runtime-operator-tool__icon" viewBox="0 0 20 20" aria-hidden="true">
      <path d="M3.5 9.2 10 3.8l6.5 5.4v6.3a1 1 0 0 1-1 1h-11a1 1 0 0 1-1-1Z" />
      <path d="M8 16.5v-5h4v5" />
    </svg>;
  }
  if (name === 'history') {
    return <svg className="runtime-operator-tool__icon" viewBox="0 0 20 20" aria-hidden="true">
      <path d="M4.2 6.1A7 7 0 1 1 3 10" />
      <path d="M3.2 3.8v3.1h3.1M10 6.2V10l2.6 1.6" />
    </svg>;
  }
  if (name === 'alarms') {
    return <svg className="runtime-operator-tool__icon" viewBox="0 0 20 20" aria-hidden="true">
      <path d="M5.2 13.7h9.6l-1.2-1.8V8.7a3.6 3.6 0 0 0-7.2 0v3.2Z" />
      <path d="M8.2 15.1a1.9 1.9 0 0 0 3.6 0" />
    </svg>;
  }
  if (name === 'playback') {
    return <svg className="runtime-operator-tool__icon" viewBox="0 0 20 20" aria-hidden="true">
      <circle cx="10" cy="10" r="6.5" /><path d="M8.2 6.8 13 10l-4.8 3.2Z" />
    </svg>;
  }
  if (name === 'fullscreen') {
    return <svg className="runtime-operator-tool__icon" viewBox="0 0 20 20" aria-hidden="true">
      <path d="M7.2 3.5H3.5v3.7M12.8 3.5h3.7v3.7M7.2 16.5H3.5v-3.7M12.8 16.5h3.7v-3.7" />
    </svg>;
  }
  return <svg className="runtime-operator-tool__icon" viewBox="0 0 20 20" aria-hidden="true">
    <path d="M3.5 7.2h3.7V3.5M16.5 7.2h-3.7V3.5M3.5 12.8h3.7v3.7M16.5 12.8h-3.7v3.7" />
  </svg>;
}

function isRetryableRuntimeProjectionFailure(failure: Error): boolean {
  if (failure instanceof RuntimeApplicationTransportError) return true;
  if (!(failure instanceof RuntimeApplicationProjectionError)) return false;
  if (RETRYABLE_RUNTIME_PROJECTION_STATUSES.has(failure.status)) return true;
  return failure.status === 500 &&
    failure.message.toLowerCase().includes(TRUNCATED_RESPONSE_SIGNATURE);
}

function sameRuntimeProjection(
  current: RuntimeApplicationProjection | null,
  next: RuntimeApplicationProjection
): boolean {
  if (!current || current.mode !== next.mode) return false;
  if (next.mode !== 'engineering') return true;
  return current.projectKey === next.projectKey &&
    current.revision === next.revision &&
    current.activatedAtUtc === next.activatedAtUtc;
}
