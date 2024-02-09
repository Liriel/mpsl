import { IEntity } from "./IEntity";
import { ModelBase } from "./ModelBase";


export class Recommendation extends ModelBase<Recommendation> implements IEntity {
  public id: number;
  public name: string;
  public lastCheckDate: string;
  public avgDiff: number;
  public weight: number;
  public rank: number;

  // client view only properties
  public isProcessing: boolean = false;
  public added: boolean = false;
}
