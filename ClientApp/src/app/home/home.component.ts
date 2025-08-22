import { Component, Inject, OnInit } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { ShoppingList } from '../models/ShoppingList';
import { IRepoToken, IRepo } from '../services';

@Component({
    selector: 'app-home',
    templateUrl: './home.component.html',
    styleUrls: ['./home.component.scss'],
    standalone: false
})
export class HomeComponent implements OnInit {
  public shoppingLists: ShoppingList[];
  public isLoading: BehaviorSubject<boolean> = new BehaviorSubject(true);

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
  ) { 
    this.loadShoppingLists();
  }

  ngOnInit(): void {
  }

  private loadShoppingLists() {
    this.isLoading.next(true);
    this.repo.GetEntities<ShoppingList>("ShoppingList").subscribe(
      result => {
        this.shoppingLists = result.results;
        this.isLoading.next(false);
      }
    );
  }

  onListCreated(newList: ShoppingList) {
    if (newList) {
      this.loadShoppingLists();
    }
  }

}
