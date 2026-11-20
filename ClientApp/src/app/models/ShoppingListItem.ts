import { IEntity } from "./IEntity";
import { ModelBase } from "./ModelBase";

export class ShoppingListItem extends ModelBase<ShoppingListItem> implements IEntity {
  public id: number;
  public shoppingListId: number;
  public shortName: string;
  public name: string;
  public hint: string;
  // "Product" or "Task" (a todo / reminder rather than something to buy)
  public kind: string;
  // add response only: the server picked the kind itself
  public kindAuto: boolean;
  public amount: number;
  public unitShortName: string;
  public offsetX: number;
  public done: boolean;
  public animateFlyInOut: string;
  public lastChangeByUserShortName: string;
  public lastChangeByUserShowShortnameInList: boolean;
}

