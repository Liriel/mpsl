import { Observable, of } from "rxjs";
import { UserInfo } from '../models/UserInfo';
import { IEntity } from "../models/IEntity";
import { EntityOperationResult } from '../infrastructure/EntityOperationResult';
import { ShoppingListItem } from "../models/ShoppingListItem";
import { IRepo } from "./IRepo";

export class LocalRepo implements IRepo {
  private shoppingList: ShoppingListItem[] = [
      new ShoppingListItem({
        id: 1, amount: 1, avatar: "MI", name: "Milch", hint: "länger frisch", unit: "l"
      }),
      new ShoppingListItem({
        id: 2, amount: 250, avatar: "SC", name: "Schlagobers", unit: "ml"
      }),
      new ShoppingListItem({
        id: 3, avatar: "BI", name: "Bier"
      })];

  GetShoppingListItems(shoppingListId: string): Observable<ShoppingListItem[]> {
    return of(this.shoppingList);
  }

  AddOrUpdateItem(item: ShoppingListItem): Observable<EntityOperationResult<ShoppingListItem>> {
    let shoppingListItem = this.shoppingList.filter(l => l.id == item.id)[0];
    if(shoppingListItem){

      // HACK: does not actually copy the values
      let idx: number = this.shoppingList.indexOf(shoppingListItem);
      this.shoppingList[idx] = shoppingListItem;
    }else{
      this.shoppingList.push(item);
    }

    return of(new EntityOperationResult<ShoppingListItem>({success: true, entity: item }));
  }

  GetUserInfo(): Observable<UserInfo> {
    throw new Error("Method not implemented.");
  }

  Get<T>(controller: string, action?: string | undefined): Observable<T> {
    throw new Error("Method not implemented.");
  }

  GetEntity<T>(entityName: string, id: number): Observable<T> {
    throw new Error("Method not implemented.");
  }

  SaveEntity<T>(entityName: string, entity: T & IEntity): Observable<EntityOperationResult<T & IEntity>> {
    throw new Error("Method not implemented.");
  }
}
