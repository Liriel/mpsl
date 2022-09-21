import { Inject, Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { Observable, Subject } from 'rxjs';
import { filter, tap } from 'rxjs/operators';
import { ShoppingListItem } from '../models';
import { RemovedItem } from '../models/RemovedItem';
import { IConfigService, IConfigServiceToken } from './IConfigService';
import { ILogger, ILoggerToken } from './ILogger';
import { INotificationService } from './INotificationService';


/**
 * Maps SingalR events to INotificationService observables.
 *
 * @export
 * @class SignalRNotificationService
 * @implements {INotificationService}
 */
@Injectable()
export class SignalRNotificationService implements INotificationService {

    private hubConnection: HubConnection;
    private itemChangedSubject: Subject<ShoppingListItem> = new Subject<ShoppingListItem>();
    private itemRemovedSubject: Subject<RemovedItem> = new Subject<RemovedItem>();

    constructor(
        @Inject(ILoggerToken) private logger: ILogger,
        @Inject(IConfigServiceToken) private config: IConfigService) {
        this.hubConnection = new HubConnectionBuilder()
            .withUrl(this.config.ServerUrl + '/live')
            .withAutomaticReconnect()
            .build();


        this.hubConnection
            .start()
            .then(() => 
            {
                this.logger.Debug('Connection started!');
            })
            .catch(err => this.logger.Error(err));
        

        this.hubConnection.on('OnItemChanged', (item: ShoppingListItem) => {
            this.itemChangedSubject.next(item);
        });

        this.hubConnection.on('OnItemRemoved', (item: RemovedItem) => {
            this.itemRemovedSubject.next(item);
        });
    }

    OnItemChanged(shoppingListId: number): Observable<ShoppingListItem> {
        return this.itemChangedSubject.pipe(filter(r => r.shoppingListId == shoppingListId));
    }

    OnItemRemoved(shoppingListId: number): Observable<RemovedItem> {
        return this.itemRemovedSubject.pipe(filter(r => r.shoppingListId == shoppingListId));
    }
}