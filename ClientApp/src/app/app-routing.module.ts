import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './auth.guard';
import { HomeComponent } from './home/home.component';
import { LoginComponent } from './login/login.component';
import { ShoppingListComponent } from './shopping-list/shopping-list.component';
import { UserListComponent } from './user-list/user-list.component';
import { RecommendationsPageComponent } from './recommendations-page/recommendations-page.component';

const routes: Routes = [
  { path: "login", component: LoginComponent },
  { path: "list/:id", canActivate: [AuthGuard], component: ShoppingListComponent },
  { path: "list/:id/recommendations", canActivate: [AuthGuard], component: RecommendationsPageComponent},
  { path: "user", canActivate: [AuthGuard], data: { "role": "admin" }, component: UserListComponent },
  { path: "home", canActivate: [AuthGuard], component: HomeComponent},
  { path: "", redirectTo: "home", pathMatch: "full" },
];

@NgModule({
  imports: [
    RouterModule.forRoot(routes, {
      //enableTracing: true,
      onSameUrlNavigation: "reload",
    }),
  ],
  exports: [RouterModule]
})
export class AppRoutingModule { }
