using AppTrackingTransparency;
using Foundation;
using UIKit;
using UserNotifications;

namespace NigerianNewGrid;

[Register("AppDelegate")]
public class AppDelegate : MauiUIApplicationDelegate
{
    private bool _trackingRequested;

    protected override MauiApp CreateMauiApp()
    {
        UNUserNotificationCenter.Current.Delegate = new Platforms.iOS.iOSNotificationDelegate();
        return MauiProgram.CreateMauiApp();
    }

    public override void OnActivated(UIApplication application)
    {
        base.OnActivated(application);

        if (!_trackingRequested &&
            OperatingSystem.IsIOSVersionAtLeast(14) &&
            ATTrackingManager.TrackingAuthorizationStatus == ATTrackingManagerAuthorizationStatus.NotDetermined)
        {
            _trackingRequested = true;
            _ = ATTrackingManager.RequestTrackingAuthorizationAsync();
        }
    }
}
