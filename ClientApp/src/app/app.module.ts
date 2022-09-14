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


// import * as Hammer from 'hammerjs';
import { HammerGestureConfig, HAMMER_GESTURE_CONFIG, HammerModule } from '@angular/platform-browser';

import { IRepoToken } from './services/IRepo';
import { LocalRepo } from './services/LocalRepo';
import { INotifcationServiceToken } from './services/INotifcationService';
import { LocalNotificationService } from './services/LocalNotificationService';
import { UserListComponent } from './user-list/user-list.component';
import { ShoppingListComponent } from './shopping-list/shopping-list.component';
import { ItemDialogComponent } from './item-dialog/item-dialog.component';
import { ConsoleLogger, IConfigServiceToken, ILoggerToken, StaticConfigService, WebRepo } from './services';
import { HomeComponent } from './home/home.component';
import { HttpClientModule } from '@angular/common/http';

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
    HomeComponent
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
    MatListModule,
    MatExpansionModule
  ],
  providers: [
    { provide: IRepoToken, useClass: WebRepo },
    { provide: INotifcationServiceToken, useClass: LocalNotificationService },
    { provide: IConfigServiceToken, useClass: StaticConfigService },
    { provide: ILoggerToken, useClass: ConsoleLogger },
    {
      provide: HAMMER_GESTURE_CONFIG,
      useClass: MyHammerConfig
    }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
