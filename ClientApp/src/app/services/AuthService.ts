import { Injectable, Inject } from "@angular/core";
import { IAuthService } from "./IAuthService";
import { IRepoToken, IRepo } from "./IRepo";
import { UserInfo } from "../models/UserInfo";
import { Observable } from "rxjs";

@Injectable()
export class AuthService implements IAuthService {
  public UserInfo: Observable<UserInfo>;

  constructor(@Inject(IRepoToken) private repo: IRepo) {
    this.UserInfo = this.repo.GetUserInfo();
  }
}
