import { InjectionToken } from "@angular/core";
import { Observable } from "rxjs";
import { ShoppingListItem } from "../models/ShoppingListItem";

export let INotifcationServiceToken = new InjectionToken<INotifcationService>("INotificationService");
export interface INotifcationService {
  OnShoppingListChanged(shoppingListId: string): Observable<ShoppingListItem[]>;
}