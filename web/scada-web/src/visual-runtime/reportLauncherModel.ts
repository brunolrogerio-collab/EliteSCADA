/** Build the canonical Runtime destination for a configured Report identity. */
export function runtimeReportHref(reportKey: string): string | null {
  const key = reportKey.trim();
  if (!key || /[\u0000-\u001F\u007F]/.test(key)) return null;
  return `/runtime/reports?report=${encodeURIComponent(key)}`;
}
