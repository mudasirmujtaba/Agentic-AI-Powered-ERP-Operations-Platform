import { HttpErrorResponse } from '@angular/common/http';
import { AbstractControl, FormGroup } from '@angular/forms';

interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

/** Human-readable message for any API failure. */
export function describeApiError(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'Something went wrong.';
  }

  const problem = error.error as ProblemDetails | null;
  switch (error.status) {
    case 0:
      return 'Cannot reach the server. Check that the API is running.';
    case 401:
      return 'Your session has expired. Please sign in again.';
    case 403:
      return "You don't have permission to do that.";
    default:
      return problem?.detail ?? problem?.title ?? 'Something went wrong.';
  }
}

/**
 * Copies 400 ValidationProblemDetails field errors onto matching form controls as `{ server: message }`.
 * Keys arrive as camelCase paths such as `addresses[0].line1`.
 * Returns messages that did not match a control so the caller can show them elsewhere.
 */
export function applyServerErrors(form: FormGroup, error: unknown): string[] {
  if (!(error instanceof HttpErrorResponse) || error.status !== 400) {
    return [];
  }

  const errors = (error.error as ProblemDetails | null)?.errors ?? {};
  const unmatched: string[] = [];

  for (const [path, messages] of Object.entries(errors)) {
    const control: AbstractControl | null = path ? form.get(path.replace(/\[(\d+)\]/g, '.$1')) : null;
    const message = messages.join(' ');
    if (control) {
      control.setErrors({ ...control.errors, server: message });
      control.markAsTouched();
    } else {
      unmatched.push(message);
    }
  }

  return unmatched;
}

/**
 * Routes a failed save onto the form: field errors go to their controls, a 409 (duplicate code/name) goes to
 * `conflictField`. Returns a banner message for anything that couldn't be attached to a field, or null.
 */
export function handleFormError(form: FormGroup, error: unknown, conflictField?: string): string | null {
  if (error instanceof HttpErrorResponse && error.status === 409 && conflictField) {
    const control = form.get(conflictField);
    if (control) {
      control.setErrors({ server: describeApiError(error) });
      control.markAsTouched();
      return null;
    }
  }

  if (error instanceof HttpErrorResponse && error.status === 400) {
    const unmatched = applyServerErrors(form, error);
    return unmatched.length ? unmatched.join(' ') : null;
  }

  return describeApiError(error);
}
