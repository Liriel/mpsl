import { Injectable, ApplicationRef, Inject } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { Observable, BehaviorSubject, fromEvent, merge } from 'rxjs';
import { map, distinctUntilChanged, filter, take, concat } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { PushSubscription as PushSubscriptionModel, SubscriptionInfo, TestNotificationRequest, TestNotificationResponse } from '../models/PushNotification';
import { IRepo } from './IRepo';
import { IRepoToken } from './IRepo';

@Injectable({
  providedIn: 'root'
})
export class PushNotificationService {
  private readonly VAPID_PUBLIC_KEY = environment.vapidPublicKey;

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

      // Use firstValueFrom instead of deprecated toPromise()
      const subscription = await new Promise<PushSubscription | null>((resolve) => {
        this.swPush.subscription.subscribe({
          next: (sub) => resolve(sub),
          error: () => resolve(null)
        });
      });

      const isSubscribed = subscription !== null;
      console.log('Subscription check:', isSubscribed ? 'Active subscription found' : 'No subscription');
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
      const existingSubscription = await new Promise<PushSubscription | null>((resolve) => {
        this.swPush.subscription.subscribe({
          next: (sub) => resolve(sub),
          error: () => resolve(null)
        });
      });

      if (existingSubscription) {
        console.log('Already subscribed to push notifications');
        // Still send to backend in case it wasn't saved before
        try {
          const subscriptionData = this.convertSubscription(existingSubscription);
          await this.repo.Post('api/PushSubscription', 'subscribe', subscriptionData).toPromise();
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
        serverPublicKey: this.VAPID_PUBLIC_KEY
      });

      console.log('Push subscription obtained, sending to backend...');

      // Send subscription to backend
      const subscriptionData = this.convertSubscription(subscription);
      await this.repo.Post('api/PushSubscription', 'subscribe', subscriptionData).toPromise();

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
      const subscription = await this.swPush.subscription.toPromise();
      
      if (subscription) {
        // Unsubscribe from push service
        await this.swPush.unsubscribe();

        // Notify backend
        const subscriptionData = this.convertSubscription(subscription);
        await this.repo.Post('api/PushSubscription', 'unsubscribe', subscriptionData).toPromise();

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
