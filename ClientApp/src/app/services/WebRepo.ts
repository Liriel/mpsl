import { Observable } from "rxjs";
import { Injectable, Inject } from "@angular/core";

import { IRepo } from "./IRepo";
import { UserInfo } from '../models/UserInfo';
import { HttpClient, HttpHeaders, HttpParams } from "@angular/common/http";
import { Dictionary, EntityOperationResult } from "../infrastructure";
import { IConfigServiceToken, IConfigService } from "./IConfigService";
import { ILoggerToken, ILogger } from "./ILogger";
import { DataResult, IEntity, ShoppingListItem } from "../models";

@Injectable()
export class WebRepo implements IRepo {
  constructor(private http: HttpClient, @Inject(ILoggerToken) private logger: ILogger, @Inject(IConfigServiceToken) private config: IConfigService) { }

  private headers: HttpHeaders = new HttpHeaders().set('Content-Type', 'application/json; charset=utf-8');

  public GetEntity<T>(entityName: string, id: number): Observable<T> {
    return this.Get("api", entityName + "/" + id.toString());
  }

  public GetEntities<T>(
    entityName: string,
    skip?: number,
    take?: number,
    filter?: Dictionary<any>,
    sort?: string,
    sortDirection?: string
  ): Observable<DataResult<T>> {
    let params: HttpParams = new HttpParams();
    if (skip) params = params.append("skip", skip.toString());
    if (take) params = params.append("take", take.toString());
    if (filter) {
      for (let key in filter) {
        let value = filter[key];
        if (value) {
          params = params.append(key, value);
        }
      }
    }
    if (sort) params = params.append("sort", sort);
    if (sortDirection) params = params.append("sortDirection", sortDirection);

    var reqUrl = this.config.ServerUrl + "/api/" + entityName;
    this.logger.Debug("GET Entities REQ to " + reqUrl);
    return this.http.get<DataResult<T>>(reqUrl, { params: params });
  }

  public Get<T>(controller: string, action?: string): Observable<T> {
    var reqUrl = this.config.ServerUrl + '/' + controller + ((action && action.length > 0) ? '/' + action + '/' : '');
    this.logger.Debug("GET REQ to " + reqUrl);
    return this.http.get<T>(reqUrl);
  }

  public SaveEntity<T>(entityName: string, entity: T & IEntity): Observable<EntityOperationResult<T & IEntity>> {
    if (entity.id > 0) {
      // Update (PUT)
      return this.Put("api/" + entityName, entity.id.toString(), entity);
    } else {
      // Craete (POST)
      return this.Post("api", entityName, entity);
    }
  }

  public GetUserInfo(): Observable<UserInfo> {
    return this.Get<UserInfo>("Account");
  }

  public ClaimAdmin(): Observable<boolean> {
    return this.Get<boolean>("Account", "Claim");
  }

  public PromoteUser(userId: string): Observable<boolean> {
    return this.Put<boolean>("Account", "Promote/" + userId, null);
  }

  public DemoteUser(userId: string): Observable<boolean> {
    return this.Put<boolean>("Account", "Demote/" + userId, null);
  }

  public Post<T>(controller: string, action: string, data: any): Observable<T> {
    var reqUrl = this.config.ServerUrl + '/' + controller + '/' + action;
    this.logger.Debug("POST REQ to " + reqUrl);
    return this.http.post<T>(reqUrl, data, { headers: this.headers, withCredentials: true });
  }

  public Put<T>(controller: string, action: string, data: any): Observable<T> {
    var reqUrl = this.config.ServerUrl + '/' + controller + '/' + action;
    this.logger.Debug("PUT REQ to " + reqUrl);
    return this.http.put<T>(reqUrl, data, { headers: this.headers, withCredentials: true });
  }

  public Delete<T>(controller: string, action: string): Observable<T> {
    var reqUrl = this.config.ServerUrl + '/' + controller + '/' + action;
    this.logger.Debug("DELETE REQ to " + reqUrl);
    return this.http.delete<T>(reqUrl, { headers: this.headers, withCredentials: true });
  }
}
