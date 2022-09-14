import { IEntity } from "./IEntity";
import { ModelBase } from "./ModelBase";

export class ShoppingListItem extends ModelBase<ShoppingListItem> implements IEntity {
  public id: number;
  public avatar: string;
  public name: string;
  public hint: string;
  public amount: number;
  public unit: string;
  public offsetX: number;
  public done: boolean;
  public animateFlyInOut: string;
}