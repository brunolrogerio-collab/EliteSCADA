const API = (import.meta.env?.VITE_SCADA_API ?? '').replace(/\/$/, '');

/** Build the canonical VisualAsset content URL without importing API operations. */
export function visualAssetContentUrl(assetId: string): string {
  const normalizedAssetId = assetId.startsWith('asset:') ? assetId.slice('asset:'.length) : assetId;
  return `${API}/api/engineering/visual-assets/${encodeURIComponent(normalizedAssetId)}/content`;
}
