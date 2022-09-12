import { InjectionToken } from "@angular/core";

export let IAppUiServiceToken = new InjectionToken<IAppUiService>("IAppUiService");

export interface IAppUiService {
  IsMobile: boolean;
}
