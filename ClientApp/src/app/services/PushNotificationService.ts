import { Injectable, ApplicationRef, Inject } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { Observable, BehaviorSubject, fromEvent, merge, firstValueFrom, of } from 'rxjs';
import { map, distinctUntilChanged, filter, take, concat, catchError } from 'rxjs/operators';
import { PushSubscription as PushSubscriptionModel, SubscriptionInfo, TestNotificationRequest, TestNotificationResponse } from '../models/PushNotification';
import { IRepo } from './IRepo';
import { IRepoToken } from './IRepo';

@Injectable({
  providedIn: 'root'
})
export class PushNotificationService {
  // the server's VAPID public key, fetched once (not compiled in, so one image serves any key pair)
  private serverPublicKey: Promise<string> | null = null;

  private isSubscribedSubject = new BehaviorSubject<boolean>(false);
  public isSubscribed$ = this.isSubscribedSubject.asObservable();

  private permissionSubject = new BehaviorSubject<NotificationPermission>('default');
  public permission$ = this.permissionSubject.asObservable();

  // Page Visibility tracking
  private isPageVisibleSubject = new BehaviorSubject<boolean>(!document.hidden);
  public isPageVisible$ = this.isPageVisibleSubject.asObservable();

  constructor(
    @Inject(IRepoToken) private repo: IRepo,
    private swPush: SwPush,
    private appRef: ApplicationRef
  ) {
    this.checkPermission();
    this.initPageVisibilityTracking();
    
    // Wait for app to be stable before checking subscription
    this.waitForAppStable().then(() => {
      console.log('App is stable, checking push subscription...');
      this.checkSubscription();
    });
  }

  /**
   * Wait for Angular app to be stable before initializing push notifications
   */
  private async waitForAppStable(): Promise<void> {
    return new Promise((resolve) => {
      // Wait for app to be stable (all async tasks completed)
      // Use concat to handle the case where the app is already stable
      const isStable$ = this.appRef.isStable.pipe(
        filter(stable => stable === true),
        take(1)
      );

      isStable$.subscribe(() => {
        console.log('Angular app has stabilized');
        resolve();
      });

      // Fallback timeout in case isStable never fires
      setTimeout(() => {
        console.log('Fallback: Proceeding after timeout');
        resolve();
      }, 5000);
    });
  }

  /**
   * Initialize Page Visibility API tracking
   */
  private initPageVisibilityTracking(): void {
    // Listen to visibility change events
    fromEvent(document, 'visibilitychange').subscribe(() => {
      this.isPageVisibleSubject.next(!document.hidden);
    });

    // Also track focus/blur events for more comprehensive detection
    const focus$ = fromEvent(window, 'focus').pipe(map(() => true));
    const blur$ = fromEvent(window, 'blur').pipe(map(() => false));

    merge(focus$, blur$)
      .pipe(distinctUntilChanged())
      .subscribe(isVisible => {
        // Only update if it differs from visibility state
        if (document.hidden === isVisible) {
          this.isPageVisibleSubject.next(isVisible);
        }
      });
  }

  /**
   * Check current notification permission
   */
  private checkPermission(): void {
    if ('Notification' in window) {
      this.permissionSubject.next(Notification.permission);
    }
  }

  /**
   * Check if user has an active push subscription
   */
  private async checkSubscription(): Promise<void> {
    try {
      // Wait for service worker to be ready
      if (!this.swPush.isEnabled) {
        console.log('Service Worker not enabled, skipping subscription check');
        this.isSubscribedSubject.next(false);
        return;
      }

      const subscription = await firstValueFrom(
        this.swPush.subscription.pipe(
          take(1),
          catchError(() => of(null))
        )
      );

      const isSubscribed = subscription !== null;
      console.log('Subscription check:', isSubscribed ? 'Active subscription found' : 'No subscription');

      // a subscription made under a previous key pair no longer receives anything - renew it
      // quietly (permission is already granted) so rotating the keys needs no action from users
      // a failed key lookup is no reason to report the subscription as gone - just skip the check
      const serverPublicKey = isSubscribed ? await this.getServerPublicKey().catch(() => null) : null;
      if (serverPublicKey && Notification.permission === 'granted' &&
          !this.matchesKey(subscription, serverPublicKey)) {
        console.log('Push subscription uses an outdated server key, renewing it');
        this.isSubscribedSubject.next(await this.subscribe());
        return;
      }

      this.isSubscribedSubject.next(isSubscribed);
    } catch (error) {
      console.error('Error checking subscription:', error);
      this.isSubscribedSubject.next(false);
    }
  }

  /**
   * Request notification permission from user
   */
  public async requestPermission(): Promise<NotificationPermission> {
    if (!('Notification' in window)) {
      console.warn('This browser does not support notifications');
      return 'denied';
    }

    const permission = await Notification.requestPermission();
    this.permissionSubject.next(permission);
    return permission;
  }

  /**
   * Subscribe to push notifications
   */
  public async subscribe(): Promise<boolean> {
    try {
      // Check if service worker is supported
      if (!this.swPush.isEnabled) {
        console.error('Service Worker is not enabled');
        return false;
      }

      // Check if already subscribed
      const existingSubscription = await firstValueFrom(
        this.swPush.subscription.pipe(
          take(1),
          catchError(() => of(null))
        )
      );

      const serverPublicKey = await this.getServerPublicKey();

      if (existingSubscription && !this.matchesKey(existingSubscription, serverPublicKey)) {
        // the browser refuses a second subscription with a different key, so drop the stale one
        console.log('Dropping push subscription made with an outdated server key');
        await this.unsubscribe();
      } else if (existingSubscription) {
        console.log('Already subscribed to push notifications');
        // Still send to backend in case it wasn't saved before
        try {
          const subscriptionData = this.convertSubscription(existingSubscription);
          await firstValueFrom(this.repo.Post('api/PushSubscription', 'subscribe', subscriptionData));
          this.isSubscribedSubject.next(true);
          return true;
        } catch (error) {
          console.error('Error saving existing subscription to backend:', error);
        }
      }

      // Request permission if not granted
      if (Notification.permission !== 'granted') {
        const permission = await this.requestPermission();
        if (permission !== 'granted') {
          console.log('Notification permission denied');
          return false;
        }
      }

      console.log('Requesting push subscription with VAPID key...');
      
      // Subscribe to push notifications
      const subscription = await this.swPush.requestSubscription({
        serverPublicKey
      });

      console.log('Push subscription obtained, sending to backend...');

      // Send subscription to backend
      const subscriptionData = this.convertSubscription(subscription);
      await firstValueFrom(this.repo.Post('api/PushSubscription', 'subscribe', subscriptionData));

      this.isSubscribedSubject.next(true);
      console.log('Successfully subscribed to push notifications');
      return true;

    } catch (error) {
      console.error('Error subscribing to push notifications:', error);
      if (error instanceof Error) {
        console.error('Error details:', error.message, error.stack);
      }
      return false;
    }
  }

  /**
   * Unsubscribe from push notifications
   */
  public async unsubscribe(): Promise<boolean> {
    try {
      const subscription = await firstValueFrom(this.swPush.subscription.pipe(take(1)));

      if (subscription) {
        // Unsubscribe from push service
        await this.swPush.unsubscribe();

        // Notify backend
        const subscriptionData = this.convertSubscription(subscription);
        await firstValueFrom(this.repo.Post('api/PushSubscription', 'unsubscribe', subscriptionData));

        this.isSubscribedSubject.next(false);
        console.log('Successfully unsubscribed from push notifications');
      }

      return true;

    } catch (error) {
      console.error('Error unsubscribing from push notifications:', error);
      return false;
    }
  }

  /**
   * Get push notification messages
   */
  public get messages$(): Observable<any> {
    return this.swPush.messages;
  }

  /**
   * Get notification click events
   */
  public get notificationClicks$(): Observable<any> {
    return this.swPush.notificationClicks;
  }

  /**
   * The server's VAPID public key. A failed lookup is not cached, so the next attempt retries.
   */
  private getServerPublicKey(): Promise<string> {
    if (!this.serverPublicKey) {
      this.serverPublicKey = firstValueFrom(
        this.repo.Get<{ publicKey: string }>('api/PushSubscription', 'vapid-public-key')
      ).then(r => {
        if (!r?.publicKey) {
          throw new Error('The server has no VAPID public key configured');
        }
        return r.publicKey;
      });
      this.serverPublicKey.catch(() => this.serverPublicKey = null);
    }
    return this.serverPublicKey;
  }

  /**
   * Whether a subscription was made with the given base64url VAPID public key
   */
  private matchesKey(subscription: globalThis.PushSubscription, publicKey: string): boolean {
    const key = subscription.options?.applicationServerKey;
    if (!key) {
      // the browser does not say which key was used - assume it is current rather than churn
      return true;
    }
    const encoded = btoa(String.fromCharCode(...new Uint8Array(key)))
      .replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    return encoded === publicKey.replace(/=+$/, '');
  }

  /**
   * Convert PushSubscription to our model format
   */
  private convertSubscription(subscription: globalThis.PushSubscription): PushSubscriptionModel {
    const keys = subscription.toJSON().keys;
    return {
      endpoint: subscription.endpoint,
      p256dh: keys?.['p256dh'] || '',
      auth: keys?.['auth'] || ''
    };
  }

  /**
   * Check if user is currently active (page visible and focused)
   */
  public isUserActive(): boolean {
    return this.isPageVisibleSubject.value;
  }

  /**
   * Check if push notifications are supported
   */
  public isSupported(): boolean {
    return this.swPush.isEnabled && 'Notification' in window;
  }

  /**
   * Get all subscriptions (admin only)
   */
  public getAllSubscriptions(): Observable<SubscriptionInfo[]> {
    return this.repo.Get<SubscriptionInfo[]>('api/PushSubscription', 'all-subscriptions');
  }

  /**
   * Send a test notification (admin only)
   */
  public sendTestNotification(subscriptionId: number, title: string, body: string): Observable<TestNotificationResponse> {
    const request: TestNotificationRequest = {
      subscriptionId,
      title,
      body
    };
    return this.repo.Post<TestNotificationResponse>('api/PushSubscription', 'test', request);
  }
}
