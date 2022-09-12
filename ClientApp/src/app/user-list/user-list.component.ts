import { AfterViewInit, Component, Inject, OnInit } from '@angular/core';
import { IRepoToken, IRepo } from '../services';
import { UserAccount } from './user-list.models';

@Component({
  selector: 'app-user-list',
  templateUrl: './user-list.component.html',
  styleUrls: ['./user-list.component.scss']
})
export class UserListComponent implements AfterViewInit {

  public activeUsers: UserAccount[];
  public pendingUsers: UserAccount[];

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
  ) 
  {
    // TODO: set toolbar
  }

  ngAfterViewInit(): void {

    this.repo.Get<UserAccount[]>("Account", "UserList")
      .subscribe(ua => {
        this.activeUsers = ua.filter(v => v.isUser);
        this.pendingUsers = ua.filter(v => !v.isUser);
      });
  }

  public trackByUserId(index: number, user: UserAccount) {
    return user.id;
  }

  public async promote(userId: string) {
    await this.repo.PromoteUser(userId).toPromise();
    let user: UserAccount = this.pendingUsers.find(u => u.id === userId)
    let idx = this.pendingUsers.indexOf(user);
    if (idx >= 0) {
      this.pendingUsers.splice(idx, 1);
      this.activeUsers.push(user);
    }
  }

  public async demote(userId: string) {
    await this.repo.DemoteUser(userId).toPromise();
    let user: UserAccount = this.activeUsers.find(u => u.id === userId)
    let idx = this.activeUsers.indexOf(user);
    if (idx >= 0) {
      this.activeUsers.splice(idx, 1);
      this.pendingUsers.push(user);
    }
  }

}
