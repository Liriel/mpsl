import { IEntity } from "./IEntity";
import { ModelBase } from "./ModelBase";


export class Recommendation extends ModelBase<Recommendation> implements IEntity {
  public id: number;
  public name: string;
  public lastCheckDate: Date;
  public avgDiff: number;
  public weight: number;
  public rank: number;
}
