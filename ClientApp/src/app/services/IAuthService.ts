import { InjectionToken } from "@angular/core";
import { Observable } from "rxjs";
import { UserInfo } from "../models/UserInfo";

// this is needed to use IRepo like an interface in c# - if you don't
// do this the interface symbols get lost during "compilation"
export let IAuthServiceToken = new InjectionToken<IAuthService>("IAuthService");

export interface IAuthService {
  UserInfo: Observable<UserInfo>;
}
