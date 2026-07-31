import { HttpErrorResponse } from '@angular/common/http';
import { FormGroup } from '@angular/forms';
import { FormHelper } from './FormHelper';
import { ValidationError } from './ValidationError';

// every write endpoint answers a rejected request with 400 and the
// { success, entity, validationErrors } body, so a failed save can be shown on the form instead of
// looking like it worked
export class ServerValidation {

  // pull the validation errors out of a rejected write, or an empty list if the failure carried
  // none (a network error, a 500, a 403 - anything that is not a validation problem)
  public static GetErrors(error: unknown): ValidationError[] {
    const body = (error as HttpErrorResponse)?.error;
    return Array.isArray(body?.validationErrors) ? body.validationErrors : [];
  }

  // place what can be placed next to the offending input and return a summary of everything left
  // over, so no error from the server goes unreported
  public static Show(error: unknown, formGroup: FormGroup, fallback: string): string {
    const unplaced = FormHelper.ShowErrors(ServerValidation.GetErrors(error), formGroup);
    const message = unplaced.map(e => e.message).join(' ');
    return message || fallback;
  }
}
