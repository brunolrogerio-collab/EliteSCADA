import React, { useEffect, useState } from 'react';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

export function MediaPlayer({ assetUrl, sourceId, runtime, autoPlay, muted, loop, controls, fit }: {
  assetUrl?: string; sourceId?: string; runtime: boolean; autoPlay: boolean; muted: boolean;
  loop: boolean; controls: boolean; fit: React.CSSProperties['objectFit'];
}) {
  const [protocol, setProtocol] = useState('');
  const [state, setState] = useState('connecting');
  const [attempt, setAttempt] = useState(0);
  useEffect(() => {
    if (!runtime || !sourceId) return;
    const abort = new AbortController();
    setState('connecting');
    void fetch(`${API}/api/runtime/media-sources/${encodeURIComponent(sourceId)}/info`, { credentials: 'include', signal: abort.signal })
      .then(async response => { if (!response.ok) throw new Error(); return response.json() as Promise<{ protocol: string; state: string }>; })
      .then(info => { setProtocol(info.protocol); setState(info.state === 'unsupported' ? 'unsupported' : 'connecting'); })
      .catch(() => { if (!abort.signal.aborted) setState('offline'); });
    return () => abort.abort();
  }, [runtime, sourceId, attempt]);
  if (sourceId && !runtime) return <span>Media Source · {sourceId}</span>;
  const url = sourceId ? `${API}/api/runtime/media-sources/${encodeURIComponent(sourceId)}/content?attempt=${attempt}` : assetUrl;
  const style = { width: '100%', height: '100%', objectFit: fit } as const;
  return <div style={{ width: '100%', height: '100%', position: 'relative' }} data-media-state={sourceId ? state : 'local'}>
    {url && state !== 'unsupported' && (!sourceId || protocol) ? protocol === 'mjpeg'
      ? <img src={url} alt="Live camera" style={style} onLoad={() => setState('live')} onError={() => setState('offline')}/>
      : <video key={url} src={url} autoPlay={autoPlay} muted={muted} loop={!sourceId && loop} controls={controls} playsInline preload="metadata" style={style} onLoadedData={() => setState('live')} onError={() => setState('offline')}/>
      : null}
    {sourceId && state !== 'live' && <div role="status" style={{ position: 'absolute', bottom: 0, background: '#202832', color: '#fff', padding: 4 }}>
      {state}{state === 'offline' && <button type="button" onClick={() => setAttempt(value => value + 1)}>↻</button>}
    </div>}
  </div>;
}
