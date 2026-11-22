import React, { useEffect, useRef, useState } from 'react';
import { useAppShellLocale } from '../../appShellI18n';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
type MediaState = 'connecting' | 'live' | 'offline' | 'auth' | 'unsupported' | 'busy';

export function MediaPlayer({ assetUrl, sourceId, runtime, autoPlay, muted, loop, controls, fit }: {
  assetUrl?: string; sourceId?: string; runtime: boolean; autoPlay: boolean; muted: boolean;
  loop: boolean; controls: boolean; fit: React.CSSProperties['objectFit'];
}) {
  const locale = useAppShellLocale();
  const [protocol, setProtocol] = useState('');
  const [state, setState] = useState<MediaState>('connecting');
  const [attempt, setAttempt] = useState(0);
  const retries = useRef(0);
  const video = useRef<HTMLVideoElement>(null);
  const pendingProbe = useRef<AbortController | null>(null);
  const labels: Record<MediaState, string> = locale === 'pt-BR'
    ? { connecting: 'Conectando', live: 'Ao vivo', offline: 'Sem conexão', auth: 'Credencial necessária ou recusada', unsupported: 'Formato ou gateway não suportado', busy: 'Limite de conexões' }
    : locale === 'es' ? { connecting: 'Conectando', live: 'En vivo', offline: 'Sin conexión', auth: 'Credencial requerida o rechazada', unsupported: 'Formato o gateway no compatible', busy: 'Límite de conexiones' }
    : { connecting: 'Connecting', live: 'Live', offline: 'Offline', auth: 'Credential required or rejected', unsupported: 'Unsupported format or gateway', busy: 'Connection limit' };
  const base = sourceId ? `${API}/api/runtime/media-sources/${encodeURIComponent(sourceId)}` : '';
  const url = sourceId ? `${base}/content?attempt=${attempt}` : assetUrl;
  useEffect(() => { retries.current = 0; setAttempt(0); setProtocol(''); return () => pendingProbe.current?.abort(); }, [sourceId, runtime]);
  const failure = () => {
    if (!sourceId || !runtime) return;
    pendingProbe.current?.abort();
    const abort = new AbortController(); pendingProbe.current = abort;
    setState('offline');
    void fetch(`${base}/probe`, { credentials: 'include', signal: abort.signal })
      .then(async response => {
        if (response.status === 401 || response.status === 403) return 'auth';
        if (response.status === 429) return 'busy';
        const body = await response.json() as { state: string };
        return ['auth', 'unsupported', 'busy'].includes(body.state) ? body.state : 'offline';
      }).then(next => { if (!abort.signal.aborted) setState(next as MediaState); })
      .catch(() => { /* Preserve the media failure, not a potentially sensitive transport diagnostic. */ });
  };
  useEffect(() => {
    if (!runtime || !sourceId) return;
    const abort = new AbortController();
    setState('connecting');
    void fetch(`${base}/info`, { credentials: 'include', signal: abort.signal })
      .then(async response => { if (response.status === 401 || response.status === 403) { setState('auth'); return null; } if (!response.ok) throw new Error(); return response.json() as Promise<{ protocol: string; state: string }>; })
      .then(info => { if (info && !abort.signal.aborted) { setProtocol(info.protocol); setState(info.state === 'unsupported' ? 'unsupported' : 'connecting'); } })
      .catch(() => { if (!abort.signal.aborted) setState('offline'); });
    return () => abort.abort();
  }, [runtime, sourceId, base, attempt]);
  useEffect(() => {
    if (!runtime || !sourceId || protocol !== 'hls' || !video.current) return;
    const element = video.current;
    let alive = true;
    let destroy: (() => void) | undefined;
    void import('hls.js').then(({ default: Hls }) => {
      if (!alive) return;
      if (Hls.isSupported()) {
        const player = new Hls({ enableWorker: true, maxBufferLength: 20, backBufferLength: 10, xhrSetup: xhr => { xhr.withCredentials = true; } });
        destroy = () => player.destroy();
        player.on(Hls.Events.ERROR, (_event, data) => {
          if (data.fatal) {
            player.stopLoad();
            if (data.type === Hls.ErrorTypes.MEDIA_ERROR || data.details === Hls.ErrorDetails.MANIFEST_INCOMPATIBLE_CODECS_ERROR) setState('unsupported');
            else failure();
          }
        });
        player.attachMedia(element); player.loadSource(url!);
      } else if (element.canPlayType('application/vnd.apple.mpegurl')) element.src = url!;
      else setState('unsupported');
    }).catch(() => { if (alive) setState('unsupported'); });
    return () => { alive = false; destroy?.(); element.removeAttribute('src'); element.load(); };
  }, [runtime, sourceId, protocol, url]);
  // Bounded reconnect, cancelled on unmount/source change. Authentication and codec failures do not loop.
  useEffect(() => {
    if (!runtime || !sourceId || (state !== 'offline' && state !== 'busy') || retries.current >= 4) return;
    const timer = window.setTimeout(() => { retries.current++; setAttempt(value => value + 1); }, 1000 * 2 ** retries.current);
    return () => window.clearTimeout(timer);
  }, [runtime, sourceId, state, attempt]);
  useEffect(() => {
    if (state !== 'live') return;
    const timer = window.setTimeout(() => { retries.current = 0; }, 30000);
    return () => window.clearTimeout(timer);
  }, [state]);
  if (sourceId && !runtime) return <span>Media Source · {sourceId}</span>;
  const style = { width: '100%', height: '100%', objectFit: fit } as const;
  return <div style={{ width: '100%', height: '100%', position: 'relative' }} data-media-state={sourceId ? state : 'local'}>
    {url && state !== 'unsupported' && (!sourceId || protocol) ? protocol === 'mjpeg'
      ? <img key={url} src={url} alt="Live camera" style={style} onLoad={() => setState('live')} onError={failure}/>
      : <video ref={video} key={url} src={protocol === 'hls' ? undefined : url} autoPlay={autoPlay} muted={muted} loop={!sourceId && loop} controls={controls} playsInline preload="metadata" style={style} onLoadedData={() => setState('live')} onPlaying={() => setState('live')} onError={failure}/>
      : null}
    {sourceId && state !== 'live' && <div role="status" style={{ position: 'absolute', bottom: 0, background: '#202832', color: '#fff', padding: 4 }}>
      {labels[state]}{(state === 'offline' || state === 'busy' || state === 'auth') && <button type="button" aria-label="Reconnect" onClick={() => { retries.current = 0; setAttempt(value => value + 1); }}>↻</button>}
    </div>}
  </div>;
}
