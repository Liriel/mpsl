import { Component, Inject } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { IRepoToken, IRepo } from '../services';

@Component({
  selector: 'app-recommendations-page',
  templateUrl: './recommendations-page.component.html',
  styleUrls: [
    './recommendations-page.component.scss',
    '../shared-page.scss'
  ],
})
export class RecommendationsPageComponent {
  public shoppingListId: number;


  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    private route: ActivatedRoute,
  ) {
    this.shoppingListId = +this.route.snapshot.paramMap.get('id');
  }

}
