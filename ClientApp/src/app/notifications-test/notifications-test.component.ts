import { Component, OnInit } from '@angular/core';
import { PushNotificationService } from '../services/PushNotificationService';
import { SubscriptionInfo } from '../models/PushNotification';

@Component({
  selector: 'app-notifications-test',
  templateUrl: './notifications-test.component.html',
  styleUrls: ['./notifications-test.component.scss'],
  standalone: false
})
export class NotificationsTestComponent implements OnInit {
  public subscriptions: SubscriptionInfo[] = [];
  public loading = false;
  public sending = false;
  public testTitle = 'Test Notification';
  public testBody = 'This is a test notification from the admin panel';
  public selectedSubscriptionId: number | null = null;
  public message: string | null = null;
  public messageType: 'success' | 'error' = 'success';

  constructor(private pushService: PushNotificationService) {}

  ngOnInit(): void {
    this.loadSubscriptions();
  }

  public loadSubscriptions(): void {
    this.loading = true;
    this.message = null;
    
    this.pushService.getAllSubscriptions().subscribe({
      next: (subscriptions) => {
        this.subscriptions = subscriptions;
        this.loading = false;
      },
      error: (error) => {
        console.error('Error loading subscriptions:', error);
        this.showMessage('Failed to load subscriptions', 'error');
        this.loading = false;
      }
    });
  }

  public sendTestNotification(subscriptionId: number): void {
    if (!this.testTitle || !this.testBody) {
      this.showMessage('Please enter both title and body', 'error');
      return;
    }

    this.sending = true;
    this.selectedSubscriptionId = subscriptionId;
    this.message = null;

    this.pushService.sendTestNotification(subscriptionId, this.testTitle, this.testBody).subscribe({
      next: (response) => {
        this.showMessage(response.message || 'Test notification sent successfully!', 'success');
        this.sending = false;
        this.selectedSubscriptionId = null;
      },
      error: (error) => {
        console.error('Error sending test notification:', error);
        this.showMessage(error.error?.error || 'Failed to send test notification', 'error');
        this.sending = false;
        this.selectedSubscriptionId = null;
      }
    });
  }

  public formatDate(date: Date): string {
    return new Date(date).toLocaleString();
  }

  public getUserDisplay(subscription: SubscriptionInfo): string {
    return subscription.shortName || subscription.userName;
  }

  public getBrowserFromUserAgent(userAgent?: string): string {
    if (!userAgent) return 'Unknown';
    
    if (userAgent.includes('Chrome')) return 'Chrome';
    if (userAgent.includes('Firefox')) return 'Firefox';
    if (userAgent.includes('Safari')) return 'Safari';
    if (userAgent.includes('Edge')) return 'Edge';
    
    return 'Unknown';
  }

  public getEndpointDisplay(endpoint: string): string {
    try {
      const url = new URL(endpoint);
      return url.hostname;
    } catch {
      return endpoint.substring(0, 30) + '...';
    }
  }

  public trackBySubscriptionId(index: number, subscription: SubscriptionInfo): number {
    return subscription.id;
  }

  private showMessage(message: string, type: 'success' | 'error'): void {
    this.message = message;
    this.messageType = type;
    
    // Auto-hide message after 5 seconds
    setTimeout(() => {
      this.message = null;
    }, 5000);
  }
}
