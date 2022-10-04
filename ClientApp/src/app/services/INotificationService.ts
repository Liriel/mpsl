import { InjectionToken } from '@angular/core';
import { Observable } from 'rxjs';
import { ShoppingListItem } from '../models';
import { RemovedItem } from '../models/RemovedItem';
import { ConnectionState } from './ConnectionState';

export let INotificationServiceToken = new InjectionToken<INotificationService>("INotificationService");

export interface INotificationService{
    readonly ConnectionState: Observable<ConnectionState>;
    OnItemChanged(shoppingListId: number): Observable<ShoppingListItem>;
    OnItemRemoved(shoppingListId: number): Observable<RemovedItem>;
    Reconnect():Promise<void>;
}