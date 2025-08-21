import { Component, Inject, Output, EventEmitter } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBottomSheet } from '@angular/material/bottom-sheet';
import { CommonModule } from '@angular/common';
import { Observable } from 'rxjs';
import { IAppUiService, IAppUiServiceToken } from '../services';
import { CreateListDrawerComponent } from '../create-list-drawer/create-list-drawer.component';
import { ShoppingList } from '../models/ShoppingList';

@Component({
  selector: 'app-fab-create-list',
  templateUrl: './fab-create-list.component.html',
  styleUrls: ['./fab-create-list.component.scss']
})
export class FabCreateListComponent {
  @Output() listCreated = new EventEmitter<ShoppingList>();
  public isMobile$: Observable<boolean>;

  constructor(
    @Inject(IAppUiServiceToken) private appUiService: IAppUiService,
    private bottomSheet: MatBottomSheet
  ) {
    this.isMobile$ = this.appUiService.IsMobile;
  }

  openCreateDialog() {
    const bottomSheetRef = this.bottomSheet.open(CreateListDrawerComponent);
    
    bottomSheetRef.afterDismissed().subscribe(result => {
      if (result) {
        this.listCreated.emit(result);
      }
    });
  }
}
