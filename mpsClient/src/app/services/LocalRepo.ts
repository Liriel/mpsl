import { Observable, of } from "rxjs";
import { UserInfo } from '../models/UserInfo';
import { IEntity } from "../models/IEntity";
import { EntityOperationResult } from '../infrastructure/EntityOperationResult';
import { ShoppingListItem } from "../models/ShoppingListItem";
import { IRepo } from "./IRepo";

export class LocalRepo implements IRepo {
  private lastId: number = 3;
  private shoppingList: ShoppingListItem[] = [
      new ShoppingListItem({
        id: 1, amount: 1, avatar: "MI", name: "Milch", hint: "länger frisch", unit: "l"
      }),
      new ShoppingListItem({
        id: 2, amount: 250, avatar: "SC", name: "Schlagobers", unit: "ml"
      }),
      new ShoppingListItem({
        id: 3, avatar: "BI", name: "Bier"
      }),
      new ShoppingListItem({
        id: 4, avatar: "ME", name: "Mehl", unit: "g", amount: 500
      }),
      new ShoppingListItem({
        id: 5, avatar: "Kü", name: "Küchenrolle"
      }),
      new ShoppingListItem({
        id: 6, avatar: "NU", name: "Nudeln"
      }),
      new ShoppingListItem({
        id: 7, avatar: "RS", name: "Regeneriersalz"
      }),
      new ShoppingListItem({
        id: 8, avatar: "Fa", name: "Faschiertes", amount: 500, unit: "g"
      }),
      new ShoppingListItem({
        id: 9, avatar: "GS", name: "Geschirrspühlmittel"
      }),
      new ShoppingListItem({
        id: 10, avatar: "TT", name: "Taschentücher"
      }),
      new ShoppingListItem({
        id: 11, avatar: "KR", name: "Küchenrolle"
      }),
      new ShoppingListItem({
        id: 12, avatar: "SF", name: "Schmierseife"
      }),
      new ShoppingListItem({
        id: 13, avatar: "BR", name: "Brot", amount: 0.5, unit: "KG", hint: "St. Pöltner"
      }),
      new ShoppingListItem({
        id: 14, avatar: "Ma", name: "Marmelade"
      }),
    ];

  GetShoppingListItems(shoppingListId: string): Observable<ShoppingListItem[]> {
    return of(this.shoppingList);
  }

  SearchShoppingListItems(pattern: string): Observable<ShoppingListItem[]> {
    if(pattern == null || pattern.length < 1) 
      return of(this.shoppingList);

    const filterValue = pattern.toLowerCase();
    return of(this.shoppingList.filter(i => i.name.toLowerCase().includes(filterValue)));
  }

  AddOrUpdateItem(item: ShoppingListItem): Observable<EntityOperationResult<ShoppingListItem>> {
    let shoppingListItem = this.shoppingList.filter(l => l.id == item.id)[0];
    if(shoppingListItem){

      // HACK: does not actually copy the values
      let idx: number = this.shoppingList.indexOf(shoppingListItem);
      this.shoppingList[idx] = shoppingListItem;
    }else{
      // create the record
      this.lastId++;
      item.id = this.lastId;

      // create an avatar
      if(!item.avatar || item.avatar.length < 2){
        item.avatar = item.name.substring(0, 2);
      }
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
