import { Component, Inject } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { IAppUiServiceToken, IAppUiService, IAuthServiceToken, IAuthService } from './services';

@Component({
  selector: 'app-root',
  template: `
    <router-outlet></router-outlet>
  `,
  styles: []
})
export class AppComponent {
  public title = 'mpsClient';
  public userName: string = "";
  public isAdmin: boolean = false;
  public isAuthenticated: boolean = false;

  constructor(
    private router: Router,
    @Inject(IAppUiServiceToken) private appUiService: IAppUiService,
    @Inject(IAuthServiceToken) private authService: IAuthService,
    iconRegistry: MatIconRegistry,
    sanitizer: DomSanitizer
  ){
    iconRegistry.addSvgIcon("microsoft", sanitizer.bypassSecurityTrustResourceUrl("assets/mssymbol.svg"));
    this.authService.UserInfo.subscribe(r => {
      this.isAuthenticated = r.isAuthenticated;
      this.isAdmin = r.roles ? r.roles.includes("admin") : false;
      this.userName = r.userName;
    });

  }
}
