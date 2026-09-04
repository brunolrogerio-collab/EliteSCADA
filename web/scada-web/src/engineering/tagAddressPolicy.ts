export type TagAddressPolicyInput = Readonly<{
  hasDataSource: boolean;
  sourceTypeResolved: boolean;
  sourceKind?: string | null;
}>;

export function shouldShowTagAddressEditor({
  hasDataSource,
  sourceTypeResolved,
  sourceKind
}: TagAddressPolicyInput): boolean {
  if (hasDataSource && !sourceTypeResolved) return false;
  return sourceKind?.trim().toLowerCase() !== 'sourceprovider';
}
