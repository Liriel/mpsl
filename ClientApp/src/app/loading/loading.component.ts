import { Component, OnInit, Input } from "@angular/core";

@Component({
  selector: "[app-loading]",
  templateUrl: "./loading.component.html",
  styleUrls: ["./loading.component.scss"]
})
export class LoadingComponent implements OnInit {
  @Input("app-loading")
  isLoading: boolean;
  constructor() {
    this.isLoading = true;
  }
  ngOnInit() {}
}
