const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

/** Build the canonical VisualAsset content URL without importing API operations. */
export function visualAssetContentUrl(assetId: string): string {
  return `${API}/api/engineering/visual-assets/${encodeURIComponent(assetId)}/content`;
}
