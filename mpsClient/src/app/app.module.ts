import { Injectable, NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { FormsModule, ReactiveFormsModule } from "@angular/forms";

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { ShoppingListComponent } from './shopping-list/shopping-list.component';

import { MatButtonModule } from "@angular/material/button";
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from "@angular/material/icon";
import { IRepoToken } from './services/IRepo';
import { LocalRepo } from './services/LocalRepo';
import { INotifcationServiceToken } from './services/INotifcationService';
import { LocalNotificationService } from './services/LocalNotificationService';

// import * as Hammer from 'hammerjs';
 import { HammerGestureConfig, HAMMER_GESTURE_CONFIG, HammerModule } from '@angular/platform-browser';

@Injectable() export class MyHammerConfig extends HammerGestureConfig  {
  override = <any>{
      // override hammerjs default configuration
      'swipe': { direction: 50  }               //24 - DIRECTION_VERTICAL
  }
}

@NgModule({
  declarations: [
    AppComponent,
    ShoppingListComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    HammerModule,
    BrowserAnimationsModule,
    FormsModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatAutocompleteModule,
    MatInputModule,
    MatIconModule
  ],
  providers: [
    { provide: IRepoToken, useClass: LocalRepo },
    { provide: INotifcationServiceToken, useClass: LocalNotificationService},
    { 
      provide: HAMMER_GESTURE_CONFIG, 
      useClass: MyHammerConfig 
    } 
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
