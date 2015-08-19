namespace OneSignalSDK_WP_WNS {
   public class ExternalInitUnity {
      public string sdkType { get { return "unity"; } }

      public static void Init(string appId, OneSignal.NotificationReceived inNotificationDelegate) {
      }

      public string GetAppArguments() {
         return "";
      }
   }
}
