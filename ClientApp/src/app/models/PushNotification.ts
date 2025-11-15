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
