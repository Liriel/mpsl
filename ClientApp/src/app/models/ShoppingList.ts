import { IEntity } from "./IEntity";
import { ModelBase } from "./ModelBase";


export class ShoppingList extends ModelBase<ShoppingList> implements IEntity {
  public id: number;
  public name: string;
}
