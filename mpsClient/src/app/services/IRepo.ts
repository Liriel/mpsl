import { Observable } from "rxjs";
import { InjectionToken } from "@angular/core";
import { UserInfo } from '../models/UserInfo';
import { IEntity } from "../models/IEntity";
import { EntityOperationResult } from '../infrastructure/EntityOperationResult';
import { ShoppingListItem } from "../models/ShoppingListItem";

// this is needed to use IRepo like an interface in c# - if you don't
// do this the interface symbols get lost during "compilation"
export let IRepoToken = new InjectionToken<IRepo>("IRepo");
export interface IRepo {
  GetShoppingListItems(shoppingListId: string): Observable<ShoppingListItem[]>;
  SearchShoppingListItems(pattern: string): Observable<ShoppingListItem[]>;
  AddOrUpdateItem(item: ShoppingListItem): Observable<EntityOperationResult<ShoppingListItem>>;
  GetUserInfo(): Observable<UserInfo>;

  Get<T>(controller: string, action?: string): Observable<T>;
  GetEntity<T>(entityName: string, id: number): Observable<T>;
  SaveEntity<T>(entityName: string, entity: T & IEntity): Observable<EntityOperationResult<T & IEntity>>;
}

