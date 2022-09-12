import { Injectable } from "@angular/core";
import { IConfigService } from "./IConfigService";
import { environment } from "../../environments/environment";

@Injectable()
export class StaticConfigService implements IConfigService{
    ServerUrl: string = environment.ServerUrl;
    IsDevMode: boolean = !environment.production;
}