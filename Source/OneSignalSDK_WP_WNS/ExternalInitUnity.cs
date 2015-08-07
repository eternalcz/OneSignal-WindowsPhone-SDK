using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using UnityPlayer;

namespace OneSignalSDK_WP_WNS {
   class ExternalInitUnity : ExternalInit {

      private static bool externalInitDone = false;

      public static void Init(string appId, OneSignal.NotificationReceived inNotificationDelegate) {

         if (externalInitDone)
            return;

         // Type appCallbacksType = Type.GetType("AppCallbacks");
         // MethodInfo method = appCallbacksType.GetMethod("Bar", BindingFlags.Static | BindingFlags.Public);

         AppCallbacks.Instance.InvokeOnUIThread(() => {
            OneSignal.Init(appId, AppCallbacks.Instance.GetAppArguments(), inNotificationDelegate, new ExternalInitUnity());
         }, false);

         externalInitDone = true;
      }

      public string GetAppArguments() {
         return AppCallbacks.Instance.GetAppArguments();
      }
   }
}
