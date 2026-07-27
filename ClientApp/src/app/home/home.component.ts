import { Component, Inject, OnInit } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { ShoppingList } from '../models/ShoppingList';
import { IRepoToken, IRepo, IAuthServiceToken, IAuthService } from '../services';

@Component({
    selector: 'app-home',
    templateUrl: './home.component.html',
    styleUrls: ['./home.component.scss'],
    standalone: false
})
export class HomeComponent implements OnInit {
  public shoppingLists: ShoppingList[];
  public isLoading: BehaviorSubject<boolean> = new BehaviorSubject(true);
  public isAdmin: boolean = false;

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    @Inject(IAuthServiceToken) private authService: IAuthService,
  ) { 
    this.loadShoppingLists();
    this.authService.UserInfo.subscribe(r => {
      this.isAdmin = r.roles ? r.roles.includes("admin") : false;
    });
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
