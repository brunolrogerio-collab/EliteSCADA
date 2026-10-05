import { useEffect, useState } from 'react';
import type { VisualAssetImportResult } from './api';

const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');
export type StaticArtwork = Readonly<{
  id: string; key: string; name: string; category: string; style: string; status: string; tags: string[];
}>;
export const staticArtworkContentUrl = (id: string) => `${API}/api/engineering/static-artwork/${encodeURIComponent(id)}/content`;

export function useStaticArtwork(enabled = true) {
  const [entries, setEntries] = useState<StaticArtwork[]>([]);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  useEffect(() => {
    if (!enabled) return;
    const abort = new AbortController();
    setLoading(true);
    void fetch(`${API}/api/engineering/static-artwork`, { credentials: 'include', signal: abort.signal })
      .then(async response => {
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        return response.json() as Promise<{ entries: StaticArtwork[] }>;
      })
      .then(result => { if (!abort.signal.aborted) { setEntries(result.entries); setError(''); } })
      .catch(reason => { if (!abort.signal.aborted) setError(String(reason)); })
      .finally(() => { if (!abort.signal.aborted) setLoading(false); });
    return () => abort.abort();
  }, [enabled]);
  return { entries, loading, error };
}

export async function importStaticArtwork(id: string, expectedVersion: number): Promise<VisualAssetImportResult> {
  const response = await fetch(`${API}/api/engineering/static-artwork/${encodeURIComponent(id)}/import`, {
    method: 'POST', credentials: 'include',
    headers: { accept: 'application/json', 'x-elitescada-workspace-version': String(expectedVersion) }
  });
  if (!response.ok) {
    const body = await response.text();
    let message = body || `HTTP ${response.status}`;
    try { message = JSON.parse(body).error ?? message; } catch { /* retain response text */ }
    throw new Error(message);
  }
  return response.json() as Promise<VisualAssetImportResult>;
}
