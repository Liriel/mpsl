import { Component, OnDestroy, OnInit } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { Subject, takeUntil } from 'rxjs';
import { PushNotificationService } from '../services/PushNotificationService';

@Component({
  selector: 'app-notification-banner',
  templateUrl: './notification-banner.component.html',
  styleUrl: './notification-banner.component.scss',
    standalone: false
})
export class NotificationBannerComponent implements OnInit, OnDestroy {
  showBanner = false;
  isSubscribed = false;
  isSupported = false;
  permission: NotificationPermission = 'default';
  private subscriptionChecked = false; // Track if we've checked the actual subscription status
  private destroy$ = new Subject<void>();

  constructor(private pushService: PushNotificationService, private snackBar: MatSnackBar) {}

  ngOnInit(): void {
    this.isSupported = this.pushService.isSupported();

    // Check if user previously dismissed the banner
    const wasDismissed = localStorage.getItem('notification-banner-dismissed') === 'true';

    if (this.isSupported && !wasDismissed) {
      // Get initial values from the service (which may already be set)
      this.pushService.permission$.pipe(takeUntil(this.destroy$)).subscribe(permission => {
        this.permission = permission;
        this.updateBannerVisibility();
      });

      this.pushService.isSubscribed$.pipe(takeUntil(this.destroy$)).subscribe(isSubscribed => {
        this.isSubscribed = isSubscribed;
        this.subscriptionChecked = true; // Mark that we've received the actual status
        this.updateBannerVisibility();
      });
    }
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  private updateBannerVisibility(): void {
    // Check if banner was dismissed
    const wasDismissed = localStorage.getItem('notification-banner-dismissed') === 'true';

    // Show banner if:
    // - Notifications are supported
    // - Permission not denied
    // - Not subscribed
    // - Not dismissed before
    // - We've checked the actual subscription status (avoid flickering)
    this.showBanner = this.isSupported &&
                      this.permission !== 'denied' &&
                      !this.isSubscribed &&
                      !wasDismissed &&
                      this.subscriptionChecked;
  }

  async enableNotifications(): Promise<void> {
    console.log('Enabling notifications...');
    const success = await this.pushService.subscribe();
    if (success) {
      console.log('Notifications enabled successfully');
      this.showBanner = false;
    } else {
      console.error('Failed to enable notifications');
      this.snackBar.open('Failed to enable notifications. Please check browser console for details.', 'Close', { duration: 3000 });
    }
  }

  dismissBanner(): void {
    this.showBanner = false;
    // Optionally store dismissal in localStorage to not show again
    localStorage.setItem('notification-banner-dismissed', 'true');
  }
}
