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

    if (this.isSupported) {
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
    // Show banner if notifications are supported, not denied, and not subscribed
    this.showBanner = this.isSupported && 
                      this.permission !== 'denied' && 
                      !this.isSubscribed;
  }

  async enableNotifications(): Promise<void> {
    const success = await this.pushService.subscribe();
    if (!success) {
      console.error('Failed to enable notifications');
    }
  }

  dismissBanner(): void {
    this.showBanner = false;
    // Optionally store dismissal in localStorage to not show again
    localStorage.setItem('notification-banner-dismissed', 'true');
  }
}
