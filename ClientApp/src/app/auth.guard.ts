import { Injectable, Inject } from "@angular/core";
import { ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, Router } from "@angular/router";
import { Observable } from "rxjs";
import { IAuthServiceToken, IAuthService } from "./services/IAuthService";
import { map, take } from "rxjs/operators";

@Injectable({
  providedIn: "root"
})
export class AuthGuard  {
  constructor(@Inject(IAuthServiceToken) private auth: IAuthService, private router: Router) { }

  canActivate(
    next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): Observable<boolean | UrlTree> | Promise<boolean | UrlTree> | boolean | UrlTree {

    // check for the user role if none was provided
    let role: string = next.data['role'] as string ?? "user";

    // roles is guaranteed non-null now (AuthService normalizes it), so the old null-guard around r
    // is gone; take(1) is not required for correctness (the router already applies first()) but
    // makes this subscription self-terminating against a subject that never completes
    return this.auth.UserInfo.pipe(
      map(r => {
        if (!r.isAuthenticated || !r.roles.includes(role)) {
          this.router.navigate(["/login"]);
          return false;
        }
        return true;
      }),
      take(1)
    );
  }
}
