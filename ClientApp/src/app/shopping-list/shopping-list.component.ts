import { trigger, state, style, transition, animate } from '@angular/animations';
import { AfterViewInit, Component, Inject, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Router, ActivatedRoute } from '@angular/router';
import { debounceTime, filter, map, Observable, of, startWith, switchMap, tap } from 'rxjs';
import { FormHelper } from '../infrastructure/FormHelper';
import { ItemDialogComponent } from '../item-dialog/item-dialog.component';
import { ItemDialogData } from '../item-dialog/ItemDialogData';
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
export class ShoppingListComponent implements OnInit, AfterViewInit {

  public items: Observable<ShoppingListItem[]>;
  public isLoading: boolean = true;
  public shoppingListId: number = 0;

  public formGroup: FormGroup = new FormGroup({
    name: new FormControl(''),
    amount: new FormControl(1),
    unit: new FormControl('')
  });
  options: string[] = ['One', 'Two', 'Three'];
  filteredOptions: Observable<string[]>;

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    private router: Router,
    private route: ActivatedRoute,
    private dialog: MatDialog
  ) { 
    this.shoppingListId = +this.route.snapshot.paramMap.get('id');
  }

  ngOnInit(): void {
    this.filteredOptions = this.formGroup.get("name").valueChanges.pipe(
      startWith(''),
      // filter(value => value != ''),
      debounceTime(200),
      switchMap((pattern) => this.repo.Get<ShoppingListItem[]>("api/shoppinglist/" + this.shoppingListId + "/search/" + pattern)),
      map(value => value.map(o => o.name))
    );
  }

  public async ngAfterViewInit(): Promise<void> {
    this.items = this.repo.Get<ShoppingListItem[]>("api/shoppinglist/" + this.shoppingListId + "/item").pipe(
      tap(r => this.isLoading = false)
    );
  }

  public openDialog(item: ShoppingListItem): void {
    const dialogRef = this.dialog.open(ItemDialogComponent, {
      // width: '250px',
      data: new ItemDialogData({ shoppingListId: this.shoppingListId, itemId: item.id })
    });

    dialogRef.afterClosed().subscribe(result => {
      console.log('The dialog was closed', result);
    });
  }

  public save(): void{
    let item = new ShoppingListItem({ name: "", amount: 0, unitName: "", shoppingListId: this.shoppingListId });
    FormHelper.UpdateModel(ShoppingListItem, item, this.formGroup);
    console.log(item);

    this.repo.Post("api/shoppinglist/" + this.shoppingListId, "add", item).subscribe(result =>{
      console.log(result);
      this.formGroup.reset();
    });
  }

  public showSwipeAnimation(event: any, rd: ShoppingListItem) {
    event.preventDefault();
    if (event.deltaX < 0 && rd.offsetX > -50) {
      rd.offsetX = event.deltaX;
    }
  }

  public onSwipeComplete(event: any, item: ShoppingListItem): void {
    if (event.toState === 'left') {
      item.done = !item.done;
      item.animateFlyInOut = "start";
    }
  }

  public onSwipeLeft(event: any, item: ShoppingListItem): void {
    console.log(event);
    item.animateFlyInOut = 'left'
  }
}
