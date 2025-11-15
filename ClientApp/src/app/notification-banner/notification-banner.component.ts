import { Component, OnInit } from '@angular/core';
import { PushNotificationService } from '../services/PushNotificationService';

@Component({
  selector: 'app-notification-banner',
  templateUrl: './notification-banner.component.html',
  styleUrl: './notification-banner.component.scss',
    standalone: false
})
export class NotificationBannerComponent implements OnInit {
  showBanner = false;
  isSubscribed = false;
  isSupported = false;
  permission: NotificationPermission = 'default';

  constructor(private pushService: PushNotificationService) {}

  ngOnInit(): void {
    this.isSupported = this.pushService.isSupported();

    // Check if user previously dismissed the banner
    const wasDismissed = localStorage.getItem('notification-banner-dismissed') === 'true';

    if (this.isSupported && !wasDismissed) {
      this.pushService.permission$.subscribe(permission => {
        this.permission = permission;
        this.updateBannerVisibility();
      });

      this.pushService.isSubscribed$.subscribe(isSubscribed => {
        this.isSubscribed = isSubscribed;
        this.updateBannerVisibility();
      });
    }
  }

  private updateBannerVisibility(): void {
    // Check if banner was dismissed
    const wasDismissed = localStorage.getItem('notification-banner-dismissed') === 'true';
    
    // Show banner if:
    // - Notifications are supported
    // - Permission not denied
    // - Not subscribed
    // - Not dismissed before
    this.showBanner = this.isSupported && 
                      this.permission !== 'denied' && 
                      !this.isSubscribed &&
                      !wasDismissed;
  }

  async enableNotifications(): Promise<void> {
    console.log('Enabling notifications...');
    const success = await this.pushService.subscribe();
    if (success) {
      console.log('Notifications enabled successfully');
      this.showBanner = false;
    } else {
      console.error('Failed to enable notifications');
      // Show error message to user (you could add a snackbar here)
      alert('Failed to enable notifications. Please check browser console for details.');
    }
  }

  dismissBanner(): void {
    this.showBanner = false;
    // Optionally store dismissal in localStorage to not show again
    localStorage.setItem('notification-banner-dismissed', 'true');
  }
}
