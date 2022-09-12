import { ModelBase } from "../models";

export class UserAccount extends ModelBase<UserAccount>{
  public id: string;
  public userName: string;
  public shortname: string;
  public isUser: boolean;
  public isAdmin: boolean;
}