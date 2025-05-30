import { trigger, state, style, transition, animate } from '@angular/animations';
import { AfterViewInit, Component, HostListener, Inject, OnInit } from '@angular/core';
import { FormControl, FormGroup } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { Router, ActivatedRoute } from '@angular/router';
import { BehaviorSubject, debounceTime, filter, map, merge, Observable, of, startWith, Subject, switchMap, tap } from 'rxjs';
import { FormHelper } from '../infrastructure/FormHelper';
import { ItemDialogComponent } from '../item-dialog/item-dialog.component';
import { ItemDialogData } from '../item-dialog/ItemDialogData';
import { RemovedItem } from '../models/RemovedItem';
import { ShoppingListItem } from '../models/ShoppingListItem';
import { Unit } from '../models/Unit';
import { ConnectionState } from '../services/ConnectionState';
import { INotificationService, INotificationServiceToken } from '../services/INotificationService';
import { IRepo, IRepoToken } from '../services/IRepo';
import { ShoppingList } from '../models/ShoppingList';
import { IAppUiService, IAppUiServiceToken } from '../services';

@Component({
  selector: 'app-shopping-list',
  templateUrl: './shopping-list.component.html',
  styleUrls: [
    './shopping-list.component.scss', 
    './dot-pulse.scss', 
    '../shared-list-item.scss',
    '../shared-page.scss'
  ],
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

  public items: ShoppingListItem[];
  public isLoading: BehaviorSubject<boolean> = new BehaviorSubject(true);
  public shoppingListId: number = 0;
  public ConnState: Observable<ConnectionState>;
  public shoppingListName: Observable<string>;

  // make enum available in template
  public ConnectionState: typeof ConnectionState = ConnectionState;
  public OnFormReset: Subject<any> = new Subject();

  // breakpoint observer
  public isMobile: Observable<boolean>;


  public formGroup: FormGroup = new FormGroup({
    name: new FormControl(''),
    amount: new FormControl(''),
    unitShortName: new FormControl('')
  });
  filteredItems: Observable<string[]>;
  filteredUnits: Observable<string[]>;
  hideComplete: boolean = false;

  private windowConnState: BehaviorSubject<ConnectionState> = new BehaviorSubject<ConnectionState>(ConnectionState.Connected);

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    @Inject(INotificationServiceToken) private notificationService: INotificationService,
    @Inject(IAppUiServiceToken) private appUiSvc: IAppUiService,
    private route: ActivatedRoute,
    private dialog: MatDialog
  ) {
    this.shoppingListId = +this.route.snapshot.paramMap.get('id');
    this.shoppingListName = this.repo.GetEntity<ShoppingList>("ShoppingList", this.shoppingListId).pipe(
      map(r => r.name)
    );
    this.notificationService.OnItemChanged(this.shoppingListId).subscribe(item => this.OnItemChanged(item));
    this.notificationService.OnItemRemoved(this.shoppingListId).subscribe(item => this.OnItemRemoved(item));
    this.ConnState = merge(this.notificationService.ConnectionState, this.windowConnState);
    this.isMobile = this.appUiSvc.IsMobile;
  }

  private OnItemRemoved(item: RemovedItem): void {
    console.log("items", this.items);
    let shoppingListItem = this.items.filter(l => l.id == item.id)[0];
    if (shoppingListItem) {
      let idx: number = this.items.indexOf(shoppingListItem);
      this.items.splice(idx, 1);
    }
  }

  @HostListener('window:focus', ['$event'])
  handleFocus(event: FocusEvent) {
    this.notificationService.Reconnect();
    this.refresh();
  }

  @HostListener('window:offline', ['$event'])
  handleOffline(event: Event) {
    this.windowConnState.next(ConnectionState.Closed);
  }

  @HostListener('window:online', ['$event'])
  handleOnline(event: Event) {
    this.windowConnState.next(ConnectionState.Connected);
  }

  // called if an item was changed on the server
  private OnItemChanged(item: ShoppingListItem): void {
    let shoppingListItem = this.items.filter(l => l.id == item.id)[0];
    if (shoppingListItem) {

      // replace the existing item
      let idx: number = this.items.indexOf(shoppingListItem);
      this.items[idx] = item;
    } else {

      // create the record
      this.items.push(item);
    }

  }

  ngOnInit(): void {

    this.filteredItems = merge(this.formGroup.get("name").valueChanges, this.OnFormReset).pipe(
      startWith(''),
      // filter(value => value != ''),
      debounceTime(200),
      switchMap((pattern) => this.repo.Get<ShoppingListItem[]>("api/shoppinglist/" + this.shoppingListId + "/search/" + pattern)),
      map(value => value.map(o => o.name))
    );

    this.filteredUnits = merge(this.formGroup.get("unitShortName").valueChanges, this.OnFormReset).pipe(
      startWith(''),
      // filter(value => value != ''),
      debounceTime(200),
      switchMap((pattern) => this.repo.Get<Unit[]>("api/unit/search/" + pattern)),
      map(value => value.map(o => o.shortName))
    );
  }

  public async ngAfterViewInit(): Promise<void> {
    await this.refresh();
  }

  public refresh():void{
    //this.isLoading.next(true);
    this.repo.Get<ShoppingListItem[]>("api/shoppinglist/" + this.shoppingListId + "/item").subscribe(r => {
      this.items = r;
      this.isLoading.next(false);
    });
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

  public save(): void {
    let item = new ShoppingListItem({ name: "", amount: 0, unitShortName: "", shoppingListId: this.shoppingListId });
    FormHelper.UpdateModel(ShoppingListItem, item, this.formGroup);

    this.repo.Post("api/shoppinglist/" + this.shoppingListId, "add", item).subscribe(result => {
      this.formGroup.reset();
      this.OnFormReset.next('');
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
      this.repo.Put<void>("api/item/" + item.id, "toggle", null).subscribe();
      item.animateFlyInOut = "start";
    }
  }

  public onSwipeLeft(event: any, item: ShoppingListItem): void {
    console.log(event);
    item.animateFlyInOut = 'left'
  }

  public getItemId(index: number, item: ShoppingListItem): number {
    return item.id;
  }
}
