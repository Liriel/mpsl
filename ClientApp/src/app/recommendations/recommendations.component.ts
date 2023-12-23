import { AfterViewInit, Component, Inject, Input, OnInit } from '@angular/core';
import { Recommendation } from '../models/Recommendation';
import { BehaviorSubject, startWith, debounceTime, switchMap, map } from 'rxjs';
import { MatDialog } from '@angular/material/dialog';
import { ActivatedRoute } from '@angular/router';
import { IRepoToken, IRepo } from '../services';

@Component({
  selector: 'app-recommendations',
  templateUrl: './recommendations.component.html',
  styleUrls: ['./recommendations.component.scss', '../shared-list-item.scss']
})
export class RecommendationsComponent implements OnInit, AfterViewInit{

  public items: Recommendation[];
  public isLoading: BehaviorSubject<boolean> = new BehaviorSubject(true);
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


  public refresh():void{
    //this.isLoading.next(true);
    this.repo.Get<Recommendation[]>("api/shoppinglist/" + this.shoppingListId + "/recommendations").subscribe(r => {
      this.items = r.slice(0,5);
      this.isLoading.next(false);
    });
  }

  public getItemId(index: number, item: Recommendation): number {
    return item.id;
  }

}
