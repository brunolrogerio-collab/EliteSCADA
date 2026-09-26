export type LicenseEntitlementProjection = {
  schemaVersion?: number | null;
  interactiveSeats?: number | null;
  viewOnlySeats?: number | null;
  haRuntime?: boolean | null;
};

export type LicenseEntitlementPresentationCopy = {
  none: string;
  legacy: string;
  legacyNotSpecified: string;
  yes: string;
  no: string;
};

export function displayLicenseSchema(
  license: LicenseEntitlementProjection,
  copy: LicenseEntitlementPresentationCopy
): string {
  if (license.schemaVersion == null) return copy.none;
  if (license.schemaVersion === 1) return `ESLIC1 — ${copy.legacy}`;
  return `ESLIC${license.schemaVersion}`;
}

export function displaySignedSeatTotal(
  value: number | null | undefined,
  license: LicenseEntitlementProjection,
  copy: LicenseEntitlementPresentationCopy
): string {
  if (value != null) return value.toLocaleString();
  return license.schemaVersion === 1 ? copy.legacyNotSpecified : copy.none;
}

export function displaySignedHaEntitlement(
  value: boolean | null | undefined,
  license: LicenseEntitlementProjection,
  copy: LicenseEntitlementPresentationCopy
): string {
  if (value === true) return copy.yes;
  if (value === false) return copy.no;
  return license.schemaVersion === 1 ? copy.legacyNotSpecified : copy.none;
}
