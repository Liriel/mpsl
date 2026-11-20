import { Component, OnInit, OnDestroy, Inject } from "@angular/core";
import { IAuthServiceToken, IAuthService } from "../services/IAuthService";
import { UserInfo } from "../models/UserInfo";
import { Subject } from "rxjs";
import { Router } from "@angular/router";
import { IRepoToken, IRepo } from "../services/IRepo";
import { filter, switchMap, takeUntil } from 'rxjs/operators';

@Component({
    selector: "app-login",
    templateUrl: "./login.component.html",
    styleUrls: ["./login.component.scss"],
    standalone: false
})
export class LoginComponent implements OnInit, OnDestroy {
  public UserInfo: UserInfo = new UserInfo();
  public showSignIn = true;
  public showAdminHint = false;
  public canClaimAdmin = false;
  public isLoading = true;

  private destroy$: Subject<void> = new Subject<void>();

  constructor(
    @Inject(IAuthServiceToken) private authService: IAuthService,
    private router: Router,
    @Inject(IRepoToken) private repo: IRepo
  ) { }

  ngOnInit() {
    // UserInfo no longer completes (it's a cache), so this subscription must be torn down
    this.authService.UserInfo.pipe(filter(r => r != null), takeUntil(this.destroy$)).subscribe(r => {
      this.UserInfo = r;
      this.showSignIn = !r.isAuthenticated;
      // only an authenticated, non-admin, non-claimable account should see the hint - roles is
      // normalized to [] for an anonymous visitor, and [] is truthy, so without isAuthenticated
      // an anonymous visitor would be told "an admin has to confirm your account"
      this.showAdminHint = r.isAuthenticated && !r.roles.includes("user") && !r.canClaimAdmin;
      this.canClaimAdmin = r.canClaimAdmin;
      this.isLoading = false;
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  public ClaimAdmin(): void {
    this.canClaimAdmin = false;
    // refresh the cached UserInfo before navigating home - otherwise AuthGuard reads the pre-claim
    // roles (still cached from before the claim) and bounces straight back to /login
    this.repo.ClaimAdmin().pipe(
      filter(r => r === true),
      switchMap(() => this.authService.Refresh())
    ).subscribe({
      next: () => this.router.navigate([""]),
      error: () => this.canClaimAdmin = true
    });
  }
}
