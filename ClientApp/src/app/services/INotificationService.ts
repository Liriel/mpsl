import { InjectionToken } from '@angular/core';
import { Observable } from 'rxjs';
import { ShoppingListItem } from '../models';
import { RemovedItem } from '../models/RemovedItem';

export let INotificationServiceToken = new InjectionToken<INotificationService>("INotificationService");

export interface INotificationService{
    OnItemChanged(shoppingListId: number): Observable<ShoppingListItem>;
    OnItemRemoved(shoppingListId: number): Observable<RemovedItem>;
}