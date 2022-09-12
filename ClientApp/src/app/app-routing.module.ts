import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './auth.guard';
import { ShoppingListComponent } from './shopping-list/shopping-list.component';
import { UserListComponent } from './user-list/user-list.component';

const routes: Routes = [
  { path: "list", component: ShoppingListComponent },
  { path: 'user', canActivate: [AuthGuard], data: { "role": "admin" }, component: UserListComponent },

];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
