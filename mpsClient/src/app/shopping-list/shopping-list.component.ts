import { trigger, state, style, transition, animate } from '@angular/animations';
import { Component, Inject, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { map, Observable, of, startWith, switchMap } from 'rxjs';
import { FormHelper } from '../infrastructure/FormHelper';
import { ShoppingListItem } from '../models/ShoppingListItem';
import { IRepo, IRepoToken } from '../services/IRepo';

@Component({
  selector: 'app-shopping-list',
  templateUrl: './shopping-list.component.html',
  styleUrls: ['./shopping-list.component.scss'],
  animations: [
    trigger("flyInOut", [
      state("start", style({ left: 0 })),
      state("success", style({ left: 0 })),
      state("left", style({ left: "-100%" })),
      state("right", style({ left: "100%" })),

      transition("* => start", [animate("300ms ease-in")]),
      transition("* => success", [animate("10ms ease-in")]),
      transition("* => left", [animate("300ms ease-out")]),
      transition("* => right", [animate("300ms ease-out")])
    ]),
    trigger("fadeInOut", [
      state("0", style({ opacity: "1" })),
      transition("* => 1", [
        animate("500ms ease-out", style({ opacity: "0" }))
      ])
    ])
  ]
})
export class ShoppingListComponent implements OnInit {

  public items: Observable<ShoppingListItem[]>;

  public formGroup: FormGroup = new FormGroup({
    name: new FormControl(''),
    amount: new FormControl(1),
    unit: new FormControl('')
  });
  options: string[] = ['One', 'Two', 'Three'];
  filteredOptions: Observable<string[]>;

  constructor(
    @Inject(IRepoToken) private repo: IRepo
  ) {
    this.items = this.repo.GetShoppingListItems("asdf");
  }

  ngOnInit(): void {
    this.filteredOptions = this.formGroup.get("name").valueChanges.pipe(
      startWith(''),
      switchMap((pattern) => this.repo.SearchShoppingListItems(pattern)),
      map(value => value.map(o => o.name))
    );
  }

  public async save(): Promise<void> {
    let item = new ShoppingListItem({ avatar: "", hint: "", name: "", amount: 0, unit: "" });
    FormHelper.UpdateModel(ShoppingListItem, item, this.formGroup);
    console.log(item);

    let result = await this.repo.AddOrUpdateItem(item).toPromise();
    this.formGroup.reset();
  }

  public showSwipeAnimation(event: any, rd: ShoppingListItem) {
    event.preventDefault();
    if (event.deltaX < 0 && rd.offsetX > -50) {
      rd.offsetX = event.deltaX;
    }
  }

  public onSwipeComplete(event: any, item: ShoppingListItem): void {
    if(event.toState === 'left'){
      item.done = !item.done;
      item.animateFlyInOut = "start";
    }
  }

  public onSwipeLeft(event: any, item: ShoppingListItem):void {
    console.log(event);
    item.animateFlyInOut = 'left'
  }
}
