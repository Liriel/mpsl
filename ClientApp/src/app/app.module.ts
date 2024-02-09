import { Injectable, NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { FormsModule, ReactiveFormsModule } from "@angular/forms";

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';

import { MatButtonModule } from "@angular/material/button";
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from "@angular/material/icon";
import { MatDialogModule } from '@angular/material/dialog';
import { MatMenuModule } from '@angular/material/menu';
import { MatListModule } from '@angular/material/list';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatCardModule } from '@angular/material/card';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';

// import * as Hammer from 'hammerjs';
import { HammerGestureConfig, HAMMER_GESTURE_CONFIG, HammerModule } from '@angular/platform-browser';

import { IRepoToken } from './services/IRepo';
import { UserListComponent } from './user-list/user-list.component';
import { ShoppingListComponent } from './shopping-list/shopping-list.component';
import { ItemDialogComponent } from './item-dialog/item-dialog.component';
import { AuthService, BreakpointAppUiService, ConsoleLogger, IAppUiServiceToken, IAuthServiceToken, IConfigServiceToken, ILoggerToken, StaticConfigService, WebRepo } from './services';
import { HomeComponent } from './home/home.component';
import { HttpClientModule } from '@angular/common/http';
import { INotificationServiceToken } from './services/INotificationService';
import { SignalRNotificationService } from './services/SignalRNotificationService';
import { LoadingComponent } from './loading/loading.component';
import { LoginComponent } from './login/login.component';
import { ServiceWorkerModule } from '@angular/service-worker';
import { environment } from '../environments/environment';
import { RecommendationsComponent } from './recommendations/recommendations.component';
import { MatRippleModule } from '@angular/material/core';
import { RecommendationsPageComponent } from './recommendations-page/recommendations-page.component';
import { DateFnsModule } from 'ngx-date-fns';

@Injectable() export class MyHammerConfig extends HammerGestureConfig {
  override = <any>{
    // override hammerjs default configuration
    'swipe': { direction: Hammer.DIRECTION_HORIZONTAL }
  }

}
@NgModule({
  declarations: [
    AppComponent,
    ShoppingListComponent,
    ItemDialogComponent,
    UserListComponent,
    HomeComponent,
    LoadingComponent,
    LoginComponent,
    RecommendationsComponent,
    RecommendationsPageComponent
  ],
  imports: [
    HttpClientModule,
    BrowserModule,
    AppRoutingModule,
    HammerModule,
    BrowserAnimationsModule,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatAutocompleteModule,
    MatInputModule,
    MatIconModule,
    MatDialogModule,
    MatMenuModule,
    MatRippleModule,
    MatListModule,
    MatExpansionModule,
    MatCardModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    DateFnsModule,
    ServiceWorkerModule.register('ngsw-worker.js', {
      enabled: environment.production,
      // Register the ServiceWorker as soon as the application is stable
      // or after 30 seconds (whichever comes first).
      registrationStrategy: 'registerWhenStable:30000'
    })
  ],
  providers: [
    { provide: IRepoToken, useClass: WebRepo },
    { provide: INotificationServiceToken, useClass: SignalRNotificationService },
    { provide: IConfigServiceToken, useClass: StaticConfigService },
    { provide: ILoggerToken, useClass: ConsoleLogger },
    { provide: IAuthServiceToken, useClass: AuthService},
    { provide: IAppUiServiceToken, useClass: BreakpointAppUiService},
    {
      provide: HAMMER_GESTURE_CONFIG,
      useClass: MyHammerConfig
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
