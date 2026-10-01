import { AbstractControl } from '@angular/forms';

/** The message to show under a form field, preferring server-side errors. */
export function firstError(control: AbstractControl | null): string {
  const errors = control?.errors;
  if (!errors) {
    return '';
  }
  if (errors['server']) return errors['server'];
  if (errors['required']) return 'This field is required.';
  if (errors['email']) return 'Enter a valid email address.';
  if (errors['maxlength']) return `Must be at most ${errors['maxlength'].requiredLength} characters.`;
  if (errors['min']) return `Must be at least ${errors['min'].min}.`;
  if (errors['max']) return `Must be at most ${errors['max'].max}.`;
  return 'Invalid value.';
}
