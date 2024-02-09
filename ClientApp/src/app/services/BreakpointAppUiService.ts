import { IAppUiService } from "./IAppUiService";
import { BreakpointObserver, Breakpoints } from "@angular/cdk/layout";
import { debounceTime, map } from "rxjs/operators";
import { Injectable } from "@angular/core";
import { Observable } from "rxjs";

@Injectable()
export class BreakpointAppUiService implements IAppUiService {
  public IsMobile: Observable<boolean>;

  constructor(private breakpointObserver: BreakpointObserver) {
    this.IsMobile = this.breakpointObserver.observe([Breakpoints.Handset])
      .pipe(
        debounceTime(200),
        map(r => r.matches)
      );
  }
}
