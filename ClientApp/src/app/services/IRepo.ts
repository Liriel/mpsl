import { Observable } from "rxjs";
import { InjectionToken } from "@angular/core";
import { UserInfo } from '../models/UserInfo';
import { DataResult } from "../models/DataResult";
import { EntityOperationResult, Dictionary } from "../infrastructure";
import { IEntity, ShoppingListItem } from "../models";

// this is needed to use IRepo like an interface in c# - if you don't
// do this the interface symbols get lost during "compilation"
export let IRepoToken = new InjectionToken<IRepo>("IRepo");
export interface IRepo {
  Get<T>(controller: string, action?: string): Observable<T>;
  Post<T>(controller: string, action: string, data: any): Observable<T>;
  Put<T>(controller: string, action: string, data: any): Observable<T>;
  Delete<T>(controller: string, action: string): Observable<T>;
  GetEntity<T>(entityName: string, id: number): Observable<T>;
  SaveEntity<T>(entityName: string, entity: T & IEntity): Observable<EntityOperationResult<T & IEntity>>;
  GetEntities<T>(
    entityName: string,
    skip?: number,
    take?: number,
    filter?: Dictionary<any>,
    sort?: string,
    sortDirection?: string
  ): Observable<DataResult<T>>;

  GetUserInfo(): Observable<UserInfo>;
  ClaimAdmin(): Observable<boolean>;
  PromoteUser(userId: string): Observable<boolean>;
  DemoteUser(userId: string): Observable<boolean>;
}
