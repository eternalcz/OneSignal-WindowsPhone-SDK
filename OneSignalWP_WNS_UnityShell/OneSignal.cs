using System;
using System.Collections.Generic;

namespace OneSignalSDK_WP_WNS {

   public class OneSignal {

      public const string VERSION = "010100";

      public delegate void NotificationReceived(string message, IDictionary<string, string> additionalData, bool isActive);
      public static NotificationReceived notificationDelegate = null;

      public delegate void IdsAvailable(string playerID, string pushToken);
      public static IdsAvailable idsAvailableDelegate = null;

      public delegate void TagsReceived(IDictionary<string, string> tags);
      public static TagsReceived tagsReceivedDelegate = null;

      public static void UnityInit(string appId, NotificationReceived inNotificationDelegate) {
      }

      public static void SendTag(string key, string value) {
      }

      public static void SendTags(IDictionary<string, string> keyValues) {
      }

      public static void SendTags(IDictionary<string, int> keyValues) {
      }

      public static void SendTags(IDictionary<string, object> keyValues) {
      }

      public static void DeleteTags(IList<string> tags) {
      }

      public static void DeleteTag(string tag) {
      }

      public static void SendPurchase(double amount) {
      }

      public static void SendPurchase(decimal amount) {
      }

      public static void GetIdsAvailable() {
      }

      public static void GetIdsAvailable(IdsAvailable inIdsAvailableDelegate) {
      }

      public static void GetTags() {
      }

      public static void GetTags(TagsReceived inTagsReceivedDelegate) {
      }

      private static void SendGetTagsMessage() {
      }
   }
}