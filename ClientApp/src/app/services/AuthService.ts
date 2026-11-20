import { Injectable, Inject } from "@angular/core";
import { IAuthService } from "./IAuthService";
import { IRepoToken, IRepo } from "./IRepo";
import { UserInfo } from "../models/UserInfo";
import { Observable, ReplaySubject, of } from "rxjs";
import { catchError, map } from "rxjs/operators";

// AccountController returns { isAuthenticated: false } for an anonymous caller, so `roles` arrives
// as null on the wire - normalize once here so every consumer can drop its null-check
const Anonymous: UserInfo = new UserInfo({ userName: "", isAuthenticated: false, roles: [], canClaimAdmin: false });

@Injectable()
export class AuthService implements IAuthService {
  // backed by a ReplaySubject so late subscribers (guard, app/home/login components) all get the
  // last known value instead of each firing their own GET /Account
  private userInfo$ = new ReplaySubject<UserInfo>(1);
  public UserInfo: Observable<UserInfo> = this.userInfo$.asObservable();

  constructor(@Inject(IRepoToken) private repo: IRepo) {
    this.Refresh().subscribe();
  }

  public Refresh(): Observable<UserInfo> {
    // fed by the same in-flight request as UserInfo, but this one completes after a single emission
    // so a caller can tell "fresh value just arrived" from "replayed stale value"
    const result = new ReplaySubject<UserInfo>(1);

    // subscribing here (rather than inside the returned observable) fires the request immediately,
    // whether or not the caller subscribes to what Refresh() returns
    this.repo.GetUserInfo().pipe(
      map(r => new UserInfo({ ...r, roles: r.roles ?? [] })),
      // the router wraps guard results in first(); an ERROR here becomes a NavigationError and
      // cancels navigation, leaving a blank page. Failing closed to anonymous sends the user to
      // /login instead, which is the correct outcome for a failed auth check anyway
      catchError(() => of(Anonymous))
    ).subscribe(r => {
      this.userInfo$.next(r);
      result.next(r);
      result.complete();
    });

    return result.asObservable();
  }
}
