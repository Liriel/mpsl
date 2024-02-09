import { AfterViewInit, Component, Inject, Input, OnInit } from '@angular/core';
import { Recommendation } from '../models/Recommendation';
import { BehaviorSubject, startWith, debounceTime, switchMap, map, delay, of } from 'rxjs';
import { IRepoToken, IRepo } from '../services';
import { trigger, state, style, transition, animate } from '@angular/animations';
import { ShoppingListItem } from '../models';
import { deAT } from 'date-fns/locale';

@Component({
  selector: 'app-recommendations',
  templateUrl: './recommendations.component.html',
  styleUrls: ['./recommendations.component.scss', '../shared-list-item.scss'],
  animations: [
    trigger("fadeIn", [
      state("pending", style({ opacity: "0" })),
      state("done", style({ opacity: "1" })),
      transition("pending => done", [
        animate("250ms ease-in")
      ])
    ])
  ]
})
export class RecommendationsComponent implements OnInit, AfterViewInit {

  public items: Recommendation[];
  public isLoading: BehaviorSubject<boolean> = new BehaviorSubject(true);
  public timeAgoOptions = {
    addSuffix: true,
    locale: deAT
  };

  @Input("shoppingListId")
  public shoppingListId: number = 0;

  constructor(
    @Inject(IRepoToken) private repo: IRepo
  ) {

  }
  ngAfterViewInit(): void {
    this.refresh();
  }

  ngOnInit(): void {
  }


  public refresh(): void {
    //this.isLoading.next(true);
    this.repo.Get<Recommendation[]>("api/shoppinglist/" + this.shoppingListId + "/recommendations").subscribe(r => {
      this.items = r.slice(0, 7);
      this.isLoading.next(false);
    });
  }

  public getItemId(index: number, item: Recommendation): number {
    return item.id;
  }

  public addItem(item: Recommendation) {
    if (item.isProcessing || item.added)
      return;

    item.isProcessing = true;
    let shoppingListItem = new ShoppingListItem({ name: item.name, shoppingListId: this.shoppingListId });

    this.repo.Post("api/shoppinglist/" + this.shoppingListId, "add", shoppingListItem).subscribe(() => {
      item.isProcessing = false;
      item.added = true;
    });
  }

}
