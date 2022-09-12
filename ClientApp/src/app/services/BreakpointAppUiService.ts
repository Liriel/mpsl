import { IAppUiService } from "./IAppUiService";
import { BreakpointObserver, Breakpoints } from "@angular/cdk/layout";
import { debounceTime } from "rxjs/operators";
import { Injectable } from "@angular/core";

@Injectable()
export class BreakpoingAppUiService implements IAppUiService {
  public IsMobile: boolean = this.breakpointObserver.isMatched("(max-width: 599px)");

  constructor(private breakpointObserver: BreakpointObserver) {
    //       .observe([Breakpoints.Handset])
    //       .pipe(debounceTime(200))
    //       .subscribe(result => {
    //         if (result.matches) {
    //           this.IsMobile = true;
    //         } else {
    //           this.IsMobile = false;
    //         }
    //       });
  }
}
