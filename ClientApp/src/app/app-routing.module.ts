import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './auth.guard';
import { HomeComponent } from './home/home.component';
import { ShoppingListComponent } from './shopping-list/shopping-list.component';
import { UserListComponent } from './user-list/user-list.component';

const routes: Routes = [
  { path: "list/:id", component: ShoppingListComponent },
  { path: "user", canActivate: [AuthGuard], data: { "role": "admin" }, component: UserListComponent },
  { path: "home", component: HomeComponent},
  { path: "", redirectTo: "home", pathMatch: "full" },
];

@NgModule({
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
