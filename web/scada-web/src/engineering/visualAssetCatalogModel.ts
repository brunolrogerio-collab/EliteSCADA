import type { VisualAssetEngineering } from './types';

const dynamoArtworkOrigin = 'original-elitescada-vector-factory';

export function isDynamoArtworkAsset(asset: Pick<VisualAssetEngineering, 'metadata'> | null | undefined): boolean {
  const metadata = asset?.metadata;
  return metadata?.assetRole?.toLocaleLowerCase() === 'dynamoartwork' ||
    metadata?.assetOrigin?.toLocaleLowerCase() === dynamoArtworkOrigin;
}

export function listUserVisualAssets<T extends VisualAssetEngineering>(assets: readonly T[] | null | undefined): T[] {
  return (assets ?? []).filter(asset => !isDynamoArtworkAsset(asset));
}
