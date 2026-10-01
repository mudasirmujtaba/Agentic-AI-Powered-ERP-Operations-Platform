/** `<input type="date">` value (yyyy-mm-dd) to the API's UTC ISO format, or null when empty. */
export function dateInputToUtc(value: string | null | undefined): string | null {
  return value ? `${value}T00:00:00Z` : null;
}

/** API UTC ISO timestamp to an `<input type="date">` value. */
export function utcToDateInput(value: string | null | undefined): string {
  return value ? value.substring(0, 10) : '';
}
