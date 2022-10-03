import { Inject, Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { BehaviorSubject, Observable, Subject } from 'rxjs';
import { filter, tap } from 'rxjs/operators';
import { ShoppingListItem } from '../models';
import { RemovedItem } from '../models/RemovedItem';
import { ConnectionState } from './ConnectionState';
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
    private connStateSubject: BehaviorSubject<ConnectionState> = new BehaviorSubject<ConnectionState>(ConnectionState.Closed);

    constructor(
        @Inject(ILoggerToken) private logger: ILogger,
        @Inject(IConfigServiceToken) private config: IConfigService) {
        this.hubConnection = new HubConnectionBuilder()
            .withUrl(this.config.ServerUrl + '/live')
            .withAutomaticReconnect()
            .build();


        // connection lifecycle events before starting the connection
        // close
        this.hubConnection.onclose((err) => {
            if (err) this.logger.Error(err.message);
            this.connStateSubject.next(ConnectionState.Closed);
        });

        // connecting
        this.hubConnection.onreconnecting((err) => {
            if (err) this.logger.Error(err.message);
            this.connStateSubject.next(ConnectionState.Connecting);
        });

        // connected
        this.hubConnection.onreconnected(() => {
            this.connStateSubject.next(ConnectionState.Connected);
        });

        this.hubConnection
            .start()
            .then(() => {
                this.logger.Debug('Connection started!');
                this.connStateSubject.next(ConnectionState.Connected);
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

    public get ConnectionState(): Observable<ConnectionState> {
        return this.connStateSubject;
    }
}