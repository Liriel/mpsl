import { InjectionToken } from "@angular/core";
import { Observable } from "rxjs";

export let IAppUiServiceToken = new InjectionToken<IAppUiService>("IAppUiService");

export interface IAppUiService {
  IsMobile: Observable<boolean>;
}
