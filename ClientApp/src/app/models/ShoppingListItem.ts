import { IEntity } from "./IEntity";
import { ModelBase } from "./ModelBase";

export class ShoppingListItem extends ModelBase<ShoppingListItem> implements IEntity {
  public id: number;
  public shoppingListId: number;
  public shortName: string;
  public name: string;
  public hint: string;
  public amount: number;
  public unitShortName: string;
  public offsetX: number;
  public done: boolean;
  public animateFlyInOut: string;
}