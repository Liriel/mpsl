import { Injectable, Injector } from '@angular/core';
import { HttpErrorResponse, HttpEvent, HttpHandler, HttpInterceptor, HttpRequest } from '@angular/common/http';
import { Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { IAuthService, IAuthServiceToken } from '../services/IAuthService';

const TOAST_COOLDOWN_MS = 5000;

@Injectable()
export class HttpErrorInterceptor implements HttpInterceptor {
  private lastToastAt = 0;

  // HTTP_INTERCEPTORS is resolved lazily, on the FIRST request - which is AuthService's constructor
  // calling Refresh(). Injecting IAuthServiceToken directly here would create a cyclic dependency
  // at bootstrap (interceptor -> AuthService -> HttpClient -> interceptor). Router and MatSnackBar
  // are safe to inject normally since neither depends on HttpClient; IAuthService is resolved lazily
  // via the injector, inside the handler, well after bootstrap has finished.
  constructor(private router: Router, private snackBar: MatSnackBar, private injector: Injector) { }

  intercept(req: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    return next.handle(req).pipe(
      catchError((error: HttpErrorResponse) => {
        if (error.status === 401 || error.status === 403) {
          // refresh the cached UserInfo first, so the login page renders from the now-expired
          // session instead of greeting it with "successfully authenticated"
          const authService = this.injector.get<IAuthService>(IAuthServiceToken);
          authService.Refresh().subscribe();

          // not loop prevention (GET /Account is anonymous and never 401s) - this just collapses a
          // burst of parallel failures that would each queue a navigation
          if (this.router.url.split('?')[0] !== '/login') {
            this.router.navigate(['/login']);
          }
        } else if (error.status === 0 || error.status >= 500) {
          // status 0 also covers an aborted request (e.g. navigating away mid-request), which can
          // toast spuriously - the cooldown below is the whole mitigation, nothing cleverer
          const now = Date.now();
          if (now - this.lastToastAt > TOAST_COOLDOWN_MS) {
            this.lastToastAt = now;
            this.snackBar.open('Could not reach the server. Please check your connection.', 'Close', { duration: 3000 });
          }
        }
        // 400 passes straight through - ServerValidation.Show() and per-component error callbacks
        // own that shape

        return throwError(() => error);
      })
    );
  }
}
