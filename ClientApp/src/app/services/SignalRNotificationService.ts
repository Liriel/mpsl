import { Inject, Injectable } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState } from '@microsoft/signalr';
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

    // the connection is started asynchronously in the constructor - callers of JoinList/LeaveList
    // await this so they can be invoked before the connection is actually up.
    private startPromise: Promise<void>;

    // group membership does not survive a reconnect, so we track what we joined and re-join
    // whenever the connection comes back (onreconnected) or is manually restarted (Reconnect()).
    private joinedLists: Set<number> = new Set<number>();

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
            this.rejoinLists();
        });

        this.startPromise = this.hubConnection
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

    public async Reconnect():Promise<void>{
        if(this.hubConnection.state == HubConnectionState.Disconnected) {
            this.startPromise = this.hubConnection.start();
            await this.startPromise;
            // group membership does not survive a reconnect - rejoin everything we had joined
            this.rejoinLists();
        }
    }

    public async JoinList(shoppingListId: number): Promise<void> {
        await this.startPromise;
        await this.hubConnection.invoke('JoinList', shoppingListId);
        this.joinedLists.add(shoppingListId);
    }

    public async LeaveList(shoppingListId: number): Promise<void> {
        await this.startPromise;
        await this.hubConnection.invoke('LeaveList', shoppingListId);
        this.joinedLists.delete(shoppingListId);
    }

    private rejoinLists(): void {
        this.joinedLists.forEach(shoppingListId => {
            this.hubConnection.invoke('JoinList', shoppingListId).catch(err => this.logger.Error(err));
        });
    }
}