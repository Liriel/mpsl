export interface PushSubscription {
  endpoint: string;
  p256dh: string;
  auth: string;
  userAgent?: string;
}

export interface PushNotificationPayload {
  title: string;
  body: string;
  icon?: string;
  badge?: string;
  tag?: string;
  data?: any;
}

export interface ShoppingListNotificationData {
  shoppingListId: number;
  shoppingListName: string;
  addedByUser: string;
  itemCount: number;
  action: string;
}

export interface SubscriptionInfo {
  id: number;
  userId: string;
  userName: string;
  shortName?: string;
  endpoint: string;
  createdAt: Date;
  userAgent?: string;
}

export interface TestNotificationRequest {
  subscriptionId: number;
  title: string;
  body: string;
}

export interface TestNotificationResponse {
  success: boolean;
  message?: string;
  error?: string;
}
