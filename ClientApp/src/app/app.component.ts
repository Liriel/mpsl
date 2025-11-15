import { Component, Inject, OnInit } from '@angular/core';
import { MatIconRegistry } from '@angular/material/icon';
import { DomSanitizer } from '@angular/platform-browser';
import { Router } from '@angular/router';
import { IAppUiServiceToken, IAppUiService, IAuthServiceToken, IAuthService } from './services';
import { IThemeService, IThemeServiceToken } from './services/IThemeService';
import { PushNotificationService } from './services/PushNotificationService';

@Component({
    selector: 'app-root',
    template: `
    <router-outlet></router-outlet>
  `,
    styles: [],
    standalone: false
})
export class AppComponent implements OnInit {
  public title = 'mpsClient';
  public userName: string = "";
  public isAdmin: boolean = false;
  public isAuthenticated: boolean = false;

  constructor(
    private router: Router,
    @Inject(IAppUiServiceToken) private appUiService: IAppUiService,
    @Inject(IAuthServiceToken) private authService: IAuthService,
    @Inject(IThemeServiceToken) private themeService: IThemeService,
    private pushNotificationService: PushNotificationService,
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

  ngOnInit(): void {
    // Handle notification clicks - navigate to the relevant page
    this.pushNotificationService.notificationClicks$.subscribe((click: any) => {
      console.log('Notification clicked:', click);
      
      if (click.notification?.data?.shoppingListId) {
        this.router.navigate(['/shopping-list', click.notification.data.shoppingListId]);
      }
    });

    // Listen for push messages (optional - for in-app handling)
    this.pushNotificationService.messages$.subscribe((message: any) => {
      console.log('Push message received:', message);
    });
  }
}
