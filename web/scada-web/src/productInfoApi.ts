export type ProductIdentityView = Readonly<{
  productName: string;
  channel: string;
  version: string;
  displayVersion: string;
  informationalVersion: string;
  buildCommit?: string | null;
}>;

export async function getProductIdentity(
  signal?: AbortSignal
): Promise<ProductIdentityView> {
  const response = await fetch('/api/product/info', {
    method: 'GET',
    credentials: 'include',
    signal
  });

  if (!response.ok) {
    throw new Error(`Product identity request failed with HTTP ${response.status}.`);
  }

  return await response.json() as ProductIdentityView;
}
