# Testing Web Push Notifications - Quick Guide

## Quick Start

```bash
# From the project root directory
dotnet run
```

That's it! This command:
- Starts the .NET backend
- Launches the Angular dev server via proxy
- Opens the app at `https://localhost:44443` (or the configured port)

## Important: Service Worker in Development

The service worker is now enabled in development mode for testing push notifications. When you run `dotnet run`, the Angular development server will serve the service worker files needed for push notifications to work.

## Testing Steps

1. **Open the app in Chrome/Firefox**
   - Navigate to `https://localhost:44443` (or your configured port)
   - You should see a purple notification banner at the top

2. **Enable Notifications**
   - Click "Enable" on the banner
   - Grant permission when browser prompts
   - Banner should disappear

3. **Verify Subscription**
   - Open DevTools (F12)
   - Go to Application → Service Workers
   - Should see `ngsw-worker.js` as "activated and running"
   - Go to Application → Push Messaging
   - Should see subscription details

4. **Test the Flow**
   - Open app in two different browsers/windows
   - Log in as different users
   - User 1: Add items to a shopping list
   - User 2: Switch to another tab/window
   - After 30 seconds, User 2 should receive a notification

5. **Verify Backend**
   - Check the terminal/console where `dotnet run` is running
   - Look for: `Sent batched notification for X items to list Y`

## Troubleshooting

### "Service Worker is not enabled"
**Problem**: Service worker isn't registered  
**Solution**: 
- Clear browser cache and reload
- Make sure you're running with `dotnet run` (not just the Angular dev server)
- Check DevTools → Application → Service Workers

### "Registration failed - push service error"
**Problem**: Service worker can't register for push  
**Solutions**:
1. Make sure you're using HTTPS (or localhost)
2. Check VAPID public key is correct in environment files
3. Try in Incognito/Private mode to rule out extension conflicts
4. Clear service worker registration:
   ```javascript
   // In browser console
   navigator.serviceWorker.getRegistrations().then(registrations => {
     registrations.forEach(reg => reg.unregister());
   });
   ```
   Then refresh the page.

### Banner shows every time
**Problem**: Subscription not completing  
**Solution**: Check browser console for errors. Usually means:
- Service worker not properly registered
- VAPID key mismatch
- Backend API not responding

### No notification appears
**Checklist**:
- [ ] Browser permission granted? (Check browser settings)
- [ ] Service worker registered? (DevTools → Application → Service Workers)
- [ ] Subscription saved to backend? (Check `/api/PushSubscription/my-subscriptions`)
- [ ] User who added item is NOT the same as user waiting for notification?
- [ ] 30 seconds passed since last item was added?
- [ ] Notification panel not already open? (Close all notification panels)

### Development vs Production

**Development** (`dotnet run`):
- Service worker is enabled for testing
- Uses Angular dev server with hot reload
- No need to rebuild for changes

**Production** (`dotnet publish`):
- Optimized build
- Service worker fully functional
- Deploy to production server

## Verification Checklist

✅ Service worker is registered and active  
✅ Push subscription visible in DevTools  
✅ Notification permission granted  
✅ Backend shows subscription in database  
✅ Banner doesn't show after enabling  
✅ Notification appears when item added (in background tab)  
✅ Clicking notification opens app and navigates to list  
✅ Multiple rapid additions result in single batched notification  

## Browser-Specific Notes

### Chrome/Edge
- Full support
- Check chrome://serviceworker-internals for debugging

### Firefox
- Full support
- Check about:serviceworkers for debugging
- May need to enable "Desktop notifications" in OS settings

### Safari
- Limited support (iOS 16.4+, macOS 13+)
- Requires explicit user gesture for permission
- May not work on older versions

## Production Deployment

For production, ensure:
1. HTTPS is enabled
2. VAPID keys are in secure configuration (not appsettings.json)
3. Service worker updates are handled properly
4. Database backups include PushSubscriptions table
