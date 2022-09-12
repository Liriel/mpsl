import { Injectable, Inject } from "@angular/core";
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, Router } from "@angular/router";
import { Observable } from "rxjs";
import { IAuthServiceToken, IAuthService } from "./services/IAuthService";
import { map } from "rxjs/operators";

@Injectable({
  providedIn: "root"
})
export class AuthGuard implements CanActivate {
  constructor(@Inject(IAuthServiceToken) private auth: IAuthService, private router: Router) { }

  canActivate(
    next: ActivatedRouteSnapshot,
    state: RouterStateSnapshot
  ): Observable<boolean | UrlTree> | Promise<boolean | UrlTree> | boolean | UrlTree {

    // check for the user role if none was provided
    let role: string = next.data['role'] as string ?? "user";

    return this.auth.UserInfo.pipe(
      map(r => {
        if (r) {
          if ((!r.isAuthenticated) || !r.roles.includes(role)) {
            this.router.navigate(["/login"]);
            return false;
          }
          return true;
        } else {
          return false;
        }
      })
    );
  }
}
