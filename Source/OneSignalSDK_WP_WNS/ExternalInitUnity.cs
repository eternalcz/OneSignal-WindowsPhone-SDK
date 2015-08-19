using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Windows.ApplicationModel.Core;

namespace OneSignalSDK_WP_WNS {
   public class ExternalInitUnity : ExternalInit {
      public string sdkType { get { return "unity"; } }

      private static bool externalInitDone = false;
      private static MethodInfo getAppArgumentsMethod;
      private static Object appCallbackInst;

      public static void Init(string appId, OneSignal.NotificationReceived inNotificationDelegate) {

         if (externalInitDone)
            return;
         
         var appCallbacksType = Type.GetType("UnityPlayer.AppCallbacks, UnityPlayer, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime");
         appCallbackInst = appCallbacksType.GetRuntimeProperty("Instance").GetValue(null);
         getAppArgumentsMethod = appCallbacksType.GetRuntimeMethod("GetAppArguments", new Type[] {});

         CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => {
            string args = (string)getAppArgumentsMethod.Invoke(appCallbackInst, new Object[]{});
            OneSignal.Init(appId, args, inNotificationDelegate, new ExternalInitUnity());
         });

         externalInitDone = true;
      }

      public string GetAppArguments() {
         return (string)getAppArgumentsMethod.Invoke(appCallbackInst, new Object[] { });
      }
   }
}
