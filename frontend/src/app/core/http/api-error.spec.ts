import { HttpErrorResponse } from '@angular/common/http';
import { FormArray, FormControl, FormGroup } from '@angular/forms';

import { applyServerErrors, describeApiError, handleFormError } from './api-error';

function problem(status: number, body: unknown) {
  return new HttpErrorResponse({ status, error: body });
}

describe('api-error', () => {
  const buildForm = () =>
    new FormGroup({
      code: new FormControl(''),
      addresses: new FormArray([new FormGroup({ line1: new FormControl('') })]),
    });

  it('maps camelCase validation paths, including array indexes, onto controls', () => {
    const form = buildForm();

    const unmatched = applyServerErrors(
      form,
      problem(400, { errors: { code: ['Code is taken.'], 'addresses[0].line1': ['Required.'], other: ['Unknown field.'] } }),
    );

    expect(form.get('code')?.errors).toEqual({ server: 'Code is taken.' });
    expect(form.get('addresses.0.line1')?.errors).toEqual({ server: 'Required.' });
    expect(unmatched).toEqual(['Unknown field.']);
  });

  it('attaches a 409 conflict to the conflict field instead of the banner', () => {
    const form = buildForm();

    const banner = handleFormError(form, problem(409, { detail: 'Duplicate code.' }), 'code');

    expect(banner).toBeNull();
    expect(form.get('code')?.errors).toEqual({ server: 'Duplicate code.' });
  });

  it('describes network and permission failures', () => {
    expect(describeApiError(problem(0, null))).toContain('Cannot reach the server');
    expect(describeApiError(problem(403, null))).toContain('permission');
    expect(describeApiError(problem(500, { title: 'Boom' }))).toBe('Boom');
  });
});
