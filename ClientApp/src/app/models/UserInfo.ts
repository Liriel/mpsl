export class UserInfo {
  userName: string;
  isAuthenticated: boolean;
  roles: string[];
  canClaimAdmin: boolean;

  constructor(model?: Partial<UserInfo>) {
    Object.assign(this, model);
  }
}

