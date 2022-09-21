import { IEntity } from "./IEntity";
import { ModelBase } from "./ModelBase";

export class Unit extends ModelBase<Unit> implements IEntity {
  public id: number;
  public shortName: string;
  public name: string;
}
