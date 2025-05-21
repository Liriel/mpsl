import { Component, Inject, OnInit } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { ShoppingList } from '../models/ShoppingList';
import { IRepoToken, IRepo } from '../services';
import { MatDialog } from '@angular/material/dialog';
import { Router } from '@angular/router';
import { CreateShoppingListDialogComponent } from './create-shopping-list-dialog.component';

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
    private dialog: MatDialog,
    private router: Router
  ) { 
    this.repo.GetEntities<ShoppingList>("ShoppingList").subscribe(
      result =>{
        this.shoppingLists = result.results;
        this.isLoading.next(false);
      }
    )
  }

  ngOnInit(): void {}

  openCreateListDialog(): void {
    const dialogRef = this.dialog.open(CreateShoppingListDialogComponent, {
      width: '350px',
      data: { existingNames: this.shoppingLists.map(l => l.name) }
    });
    dialogRef.afterClosed().subscribe(name => {
      if (name) {
        this.repo.Post<ShoppingList>('ShoppingList', '', { name }).subscribe(result => {
          // Refresh list and navigate to new list
          this.repo.GetEntities<ShoppingList>("ShoppingList").subscribe(r => {
            this.shoppingLists = r.results;
            const newList = this.shoppingLists.find(l => l.name === name);
            if (newList) {
              this.router.navigate([`/list/${newList.id}`]);
            }
          });
        });
      }
    });
  }
}
