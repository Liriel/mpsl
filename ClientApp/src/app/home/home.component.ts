import { Component, Inject, OnInit } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { BehaviorSubject, Observable } from 'rxjs';
import { ShoppingList } from '../models/ShoppingList';
import { IRepoToken, IRepo } from '../services';
import { ShoppingListDialogComponent } from '../shopping-list-dialog/shopping-list-dialog.component';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss']
})
export class HomeComponent implements OnInit {
  public shoppingLists: ShoppingList[];
  public isLoading: BehaviorSubject<boolean> = new BehaviorSubject(true);

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    private dialog: MatDialog
  ) { 
    this.loadShoppingLists();
  }

  ngOnInit(): void {
  }

  private loadShoppingLists(): void {
    this.isLoading.next(true);
    this.repo.GetEntities<ShoppingList>("ShoppingList").subscribe(
      result => {
        this.shoppingLists = result.results;
        this.isLoading.next(false);
      }
    );
  }

  public openCreateDialog(): void {
    const dialogRef = this.dialog.open(ShoppingListDialogComponent, {
      width: '400px',
      data: {}
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        console.log('Shopping list created:', result);
        // Refresh the shopping lists
        this.loadShoppingLists();
      }
    });
  }

}
