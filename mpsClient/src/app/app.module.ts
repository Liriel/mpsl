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
import {MatDialogModule} from '@angular/material/dialog';

import { IRepoToken } from './services/IRepo';
import { LocalRepo } from './services/LocalRepo';
import { INotifcationServiceToken } from './services/INotifcationService';
import { LocalNotificationService } from './services/LocalNotificationService';

// import * as Hammer from 'hammerjs';
 import { HammerGestureConfig, HAMMER_GESTURE_CONFIG, HammerModule } from '@angular/platform-browser';
import { ItemDialogComponent } from './item-dialog/item-dialog.component';

@Injectable() export class MyHammerConfig extends HammerGestureConfig  {
  override = <any>{
      // override hammerjs default configuration
      'swipe': { direction: Hammer.DIRECTION_HORIZONTAL } 
  }

}
@NgModule({
  declarations: [
    AppComponent,
    ShoppingListComponent,
    ItemDialogComponent
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
    MatIconModule,
    MatDialogModule
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
