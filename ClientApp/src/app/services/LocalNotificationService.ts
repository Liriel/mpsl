import { filter, Observable, Subject } from "rxjs";
import { ShoppingListItem } from "../models/ShoppingListItem";
import { INotifcationService } from "./INotifcationService";


export class LocalNotificationService implements INotifcationService {
  private shoppingListSubject: Subject<ShoppingListItem[]> = new Subject<ShoppingListItem[]>();

  OnShoppingListChanged(shoppingListId: string): Observable<ShoppingListItem[]> {
    return this.shoppingListSubject;
  }
}
