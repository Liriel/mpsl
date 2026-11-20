import { Component, Inject, OnDestroy, OnInit } from '@angular/core';
import { BehaviorSubject, Subject } from 'rxjs';
import { takeUntil } from 'rxjs/operators';
import { ShoppingList } from '../models/ShoppingList';
import { IRepoToken, IRepo, IAuthServiceToken, IAuthService } from '../services';

@Component({
    selector: 'app-home',
    templateUrl: './home.component.html',
    styleUrls: ['./home.component.scss'],
    standalone: false
})
export class HomeComponent implements OnInit, OnDestroy {
  public shoppingLists: ShoppingList[];
  public isLoading: BehaviorSubject<boolean> = new BehaviorSubject(true);
  public isAdmin: boolean = false;

  private destroy$: Subject<void> = new Subject<void>();

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    @Inject(IAuthServiceToken) private authService: IAuthService,
  ) {
    this.loadShoppingLists();
    // UserInfo no longer completes (it's a cache), so this subscription must be torn down
    this.authService.UserInfo.pipe(takeUntil(this.destroy$)).subscribe(r => {
      this.isAdmin = r.roles.includes("admin");
    });
  }

  ngOnInit(): void {
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private loadShoppingLists() {
    this.isLoading.next(true);
    this.repo.GetEntities<ShoppingList>("ShoppingList").subscribe({
      next: result => {
        this.shoppingLists = result.results;
        this.isLoading.next(false);
      },
      // unstick the spinner on failure - the interceptor already toasts the failure
      error: () => this.isLoading.next(false)
    });
  }

  onListCreated(newList: ShoppingList) {
    if (newList) {
      this.loadShoppingLists();
    }
  }

}
