import { Component, Inject, OnInit } from '@angular/core';
import { BehaviorSubject, Observable } from 'rxjs';
import { ShoppingList } from '../models/ShoppingList';
import { IRepoToken, IRepo } from '../services';

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
  ) { 
    this.repo.GetEntities<ShoppingList>("ShoppingList").subscribe(
      result =>{
        this.shoppingLists = result.results;
        this.isLoading.next(false);
      }
    )
  }

  ngOnInit(): void {
  }

}
