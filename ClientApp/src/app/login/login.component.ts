import { Component, OnInit, Inject } from "@angular/core";
import { IAuthServiceToken, IAuthService } from "../services/IAuthService";
import { UserInfo } from "../models/UserInfo";
import { Observable } from "rxjs";
import { Router } from "@angular/router";
import { IRepoToken, IRepo } from "../services/IRepo";
import { filter } from 'rxjs/operators';

@Component({
  selector: "app-login",
  templateUrl: "./login.component.html",
  styleUrls: ["./login.component.scss"]
})
export class LoginComponent implements OnInit {
  public UserInfo: UserInfo = new UserInfo();
  public showSignIn = true;
  public showAdminHint = false;
  public canClaimAdmin = false;
  public isLoading = true;

  constructor(
    @Inject(IAuthServiceToken) private authService: IAuthService,
    private router: Router,
    @Inject(IRepoToken) private repo: IRepo
  ) { }

  ngOnInit() {
    this.authService.UserInfo.pipe(filter(r => r != null)).subscribe(r => {
      this.UserInfo = r;
      this.showSignIn = !r.isAuthenticated;
      this.showAdminHint = r.roles && !r.roles.includes("user") && !r.canClaimAdmin;
      this.canClaimAdmin = r.canClaimAdmin;
      this.isLoading = false;
    });
  }

  public ClaimAdmin(): void {
    this.canClaimAdmin = false;
    this.repo.ClaimAdmin().subscribe(r => {
      if (r === true) {
        console.log("navigating");
        this.router.navigate([""]);
      }
    });
  }
}
