import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { SwPush } from '@angular/service-worker';
import { Observable, BehaviorSubject, fromEvent, merge } from 'rxjs';
import { map, distinctUntilChanged } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { PushSubscription as PushSubscriptionModel } from '../models/PushNotification';

@Injectable({
  providedIn: 'root'
})
export class PushNotificationService {
  private readonly VAPID_PUBLIC_KEY = environment.vapidPublicKey;
  private readonly API_URL = `${environment.ServerUrl}/api/PushSubscription`;

  private isSubscribedSubject = new BehaviorSubject<boolean>(false);
  public isSubscribed$ = this.isSubscribedSubject.asObservable();

  private permissionSubject = new BehaviorSubject<NotificationPermission>('default');
  public permission$ = this.permissionSubject.asObservable();

  // Page Visibility tracking
  private isPageVisibleSubject = new BehaviorSubject<boolean>(!document.hidden);
  public isPageVisible$ = this.isPageVisibleSubject.asObservable();

  constructor(
    private http: HttpClient,
    private swPush: SwPush
  ) {
    this.checkPermission();
    this.checkSubscription();
    this.initPageVisibilityTracking();
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
      const subscription = await this.swPush.subscription.toPromise();
      this.isSubscribedSubject.next(subscription !== null);
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

      // Request permission if not granted
      if (Notification.permission !== 'granted') {
        const permission = await this.requestPermission();
        if (permission !== 'granted') {
          return false;
        }
      }

      // Subscribe to push notifications
      const subscription = await this.swPush.requestSubscription({
        serverPublicKey: this.VAPID_PUBLIC_KEY
      });

      // Send subscription to backend
      const subscriptionData = this.convertSubscription(subscription);
      await this.http.post(`${this.API_URL}/subscribe`, subscriptionData).toPromise();

      this.isSubscribedSubject.next(true);
      console.log('Successfully subscribed to push notifications');
      return true;

    } catch (error) {
      console.error('Error subscribing to push notifications:', error);
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
        await this.http.post(`${this.API_URL}/unsubscribe`, subscriptionData).toPromise();

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
}
