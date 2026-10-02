/**
 * Produces a whitespace-free backend reference from the single name entered in
 * the simplified creation forms. Existing references remain untouched on edit.
 */
export function backendReferenceFromName(name: string): string {
  return name.trim().replace(/\s+/g, '_');
}
