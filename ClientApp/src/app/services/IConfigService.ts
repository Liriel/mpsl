import { InjectionToken } from "@angular/core";

// this is needed to the interface like an interface in c# - if you don't
// do this the interface symbols get lost during "compilation"
export let IConfigServiceToken = new InjectionToken<IConfigService>(
  "IConfigService"
);

export interface IConfigService {
  readonly ServerUrl: string;
  readonly IsDevMode: boolean;
}
