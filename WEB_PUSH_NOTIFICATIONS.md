# Web Push Notifications Implementation

This document describes the web push notification system implemented for the shopping list app.

## Overview

The system sends smart, batched push notifications to users when items are added to shared shopping lists. Notifications are only sent when users are NOT actively using the app (using Page Visibility API).

## Features

✅ **Smart Batching**: Multiple items added within 30 seconds are batched into a single notification  
✅ **User Activity Detection**: Only sends notifications when the user is not actively using the app  
✅ **Cross-Browser Support**: Works on Chrome, Firefox, Edge (desktop & mobile)  
✅ **PWA Integration**: Built on top of Angular Service Worker  
✅ **Secure**: Uses VAPID keys for authentication  

## Architecture

### Backend (.NET Core)

#### Models
- **PushSubscription** (`Model/PushSubscription.cs`): Stores user push subscription data
  - Endpoint URL
  - P256DH key
  - Auth secret
  - User agent

#### Services
- **IPushSubscriptionService** (`Services/PushSubscriptionService.cs`): Manages subscriptions
  - Subscribe/unsubscribe users
  - Query subscriptions by user or exclude user
  
- **IWebPushNotificationService** (`Services/WebPushNotificationService.cs`): Sends notifications
  - Batches rapid additions (30-second window)
  - Sends to all users except the one who made the change
  - Handles invalid subscriptions (410/404)
  - Uses WebPush library for RFC 8030 compliance

- **ShoppingListItemEntityService** (`Services/ShoppingListItemEntityService.cs`): Integrates push notifications
  - Triggers batched notifications when items are added
  - Includes user and shopping list context

#### Controllers
- **PushSubscriptionController** (`Controllers/PushSubscriptionController.cs`):
  - `GET /api/PushSubscription/vapid-public-key`: Returns VAPID public key
  - `POST /api/PushSubscription/subscribe`: Subscribe to push notifications
  - `POST /api/PushSubscription/unsubscribe`: Unsubscribe from push notifications
  - `GET /api/PushSubscription/my-subscriptions`: Get user's subscriptions

#### Configuration
- **VAPID Keys** come from configuration — a local, gitignored `appsettings.json` (copy
  `appsettings.example.json`), user secrets, or `VapidKeys__*` environment variables. Never commit
  the private key:
  ```json
  "VapidKeys": {
    "PublicKey": "<vapid-public-key>",
    "PrivateKey": "<vapid-private-key>",
    "Subject": "mailto:<you@example.com>"
  }
  ```

### Frontend (Angular)

#### Services
- **PushNotificationService** (`services/PushNotificationService.ts`):
  - Manages push subscriptions
  - Requests notification permission
  - Tracks page visibility (Page Visibility API)
  - Exposes observables for subscription state and messages
  - Integrates with Angular Service Worker

#### Components
- **NotificationBannerComponent** (`notification-banner/`):
  - Displays banner to request notification permission
  - Shows/hides based on permission state
  - Dismissible (stores in localStorage)

#### Models
- **PushNotification.ts**: TypeScript interfaces for push data
  - PushSubscription
  - PushNotificationPayload
  - ShoppingListNotificationData

## How It Works

### Subscription Flow

1. User visits the app
2. If notifications are supported and not denied, banner appears
3. User clicks "Enable" button
4. Browser requests notification permission
5. If granted, service worker subscribes to push service
6. Subscription data sent to backend via `POST /api/PushSubscription/subscribe`
7. Backend stores subscription in database

### Notification Flow

1. User A adds item(s) to shopping list
2. Backend `ShoppingListItemEntityService` triggers notification
3. `WebPushNotificationService` starts 30-second batch timer
4. If more items are added within 30 seconds, count increments
5. After 30 seconds of inactivity, notification is sent
6. Backend sends push message to all subscribed users EXCEPT User A
7. Push service delivers notification to user devices
8. Angular Service Worker receives push event
9. Service worker shows notification (if page is hidden/user inactive)
10. User clicks notification → navigates to shopping list

### Batching Logic

```
User adds Item 1 → Start 30s timer (count: 1)
User adds Item 2 → Cancel timer, restart 30s timer (count: 2)
User adds Item 3 → Cancel timer, restart 30s timer (count: 3)
[30 seconds pass with no new items]
→ Send notification: "User added 3 items to Shopping List"
```

### Page Visibility

The `PushNotificationService` tracks whether the user is actively using the app:
- Listens to `document.visibilitychange` event
- Listens to `window.focus` and `window.blur` events
- Exposes `isUserActive()` method
- Notifications are only shown when user is inactive (page hidden/unfocused)

## Dependencies

### Backend
- **WebPush** (1.0.12): RFC 8030 Web Push protocol implementation
- **Newtonsoft.Json**: JSON serialization
- **Portable.BouncyCastle**: Cryptography for VAPID

### Frontend
- **@angular/service-worker**: PWA service worker support
- **Angular Material**: UI components for banner

## Testing

### Manual Testing Steps

1. **Enable Notifications**:
   - Open app in browser
   - Click "Enable" on notification banner
   - Grant permission

2. **Test Batching**:
   - Open app in two browser windows/tabs (different users)
   - User 1: Add multiple items quickly (within 30 seconds)
   - User 2: Should receive ONE notification after 30 seconds
   - Notification should say "User1 added X items to List"

3. **Test Page Visibility**:
   - Open app, add item
   - Switch to another tab immediately
   - Should see push notification
   - Click notification → should focus app and navigate to list

4. **Test Cross-Device**:
   - Subscribe on desktop
   - Subscribe on mobile
   - Add item on desktop
   - Verify notification arrives on mobile

### Browser Support

| Browser | Desktop | Mobile | Notes |
|---------|---------|--------|-------|
| Chrome | ✅ | ✅ | Full support |
| Firefox | ✅ | ✅ | Full support |
| Edge | ✅ | ✅ | Full support |
| Safari | ⚠️ | ⚠️ | Limited (iOS 16.4+) |

### Troubleshooting

**No notifications appearing?**
- Check browser console for errors
- Verify notification permission is granted
- Check if service worker is registered: DevTools → Application → Service Workers
- Verify VAPID keys match between frontend/backend

**Notifications appearing when app is open?**
- Check Page Visibility implementation
- Verify `document.hidden` is being tracked correctly

**Subscriptions not persisting?**
- Check database migrations were applied
- Verify API endpoints are accessible
- Check authentication/authorization

## Future Enhancements

- [ ] User preferences for notification types
- [ ] Sound/vibration customization
- [ ] Notification history/log
- [ ] Group notifications by shopping list
- [ ] Silent notifications for background sync
- [ ] Push notification analytics

## Security Considerations

- VAPID keys should be kept secure (use user secrets in production)
- Subscriptions are tied to authenticated users
- Push endpoints expire and are automatically cleaned up
- No sensitive data in notification payloads

## References

- [Web Push Protocol (RFC 8030)](https://datatracker.ietf.org/doc/html/rfc8030)
- [Push API (MDN)](https://developer.mozilla.org/en-US/docs/Web/API/Push_API)
- [Page Visibility API (MDN)](https://developer.mozilla.org/en-US/docs/Web/API/Page_Visibility_API)
- [Angular Service Worker](https://angular.io/guide/service-worker-intro)
