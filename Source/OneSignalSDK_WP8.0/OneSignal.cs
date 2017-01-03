using Microsoft.Phone.Controls;
using Microsoft.Phone.Info;
using Microsoft.Phone.Notification;
using Microsoft.Phone.Reactive;
using Microsoft.Phone.Shell;
using Microsoft.Phone.Tasks;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.IsolatedStorage;
using System.Net;
using System.Text;
using System.Windows;
using System.Xml.Linq;

using Newtonsoft.Json.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace OneSignalSDK_WP80
{
	/// <summary>
	/// One signal class.
	/// </summary>
	public class OneSignal
	{
		#region Constants

		/// <summary>
		/// Version of the SDK.
		/// </summary>
		public const string VERSION = "010002";

		/// <summary>
		/// Base URL of the one signal API.
		/// </summary>
		private const string BASE_URL = "https://onesignal.com/";

		#endregion // Constants

		#region Properties

		/// <summary>
		/// The application ID in the One Signal system.
		/// </summary>
		private static string mAppId;

		/// <summary>
		/// Gets the application ID in the One Signal system.
		/// </summary>
		public static string AppId
		{
			get { return mAppId; }
		}

		/// <summary>
		/// The player ID in the One Signal system.
		/// </summary>
		private static string mPlayerId;

		/// <summary>
		/// Gets the player ID in the One Signal system.
		/// </summary>
		public static string PlayerId
		{
			get { return mPlayerId; }
		}

		/// <summary>
		/// The uri of the created channel.
		/// </summary>
		private static string mChannelUri;

		/// <summary>
		/// Gets the uri of the created channel.
		/// </summary>
		public static string ChannelUri
		{
			get { return mChannelUri; }
		}

		/// <summary>
		/// Time ticks of the last cusseeded ping.
		/// </summary>
		private static long lastPingTime;

		/// <summary>
		/// Gets the time ticks of the last cusseeded ping.
		/// </summary>
		public static long LastPingTime
		{
			get { return lastPingTime; }
		}

		/// <summary>
		/// Application isolated storage settings.
		/// </summary>
		private static IsolatedStorageSettings settings = IsolatedStorageSettings.ApplicationSettings;

		/// <summary>
		/// The flag that indicates that the initialization of the connection is done.
		/// </summary>
		private static bool initDone = false;

		/// <summary>
		/// The flag that indicates that the initialization of the connection is done.
		/// </summary>
		public static bool InitDone
		{
			get { return initDone; }
		}

		/// <summary>
		/// FallBack.
		/// </summary>
		private static IDisposable fallBackOneSignalSession;

		/// <summary>
		/// The session call is in progress.
		/// </summary>
		private static bool sessionCallInProgress;

		/// <summary>
		/// The session call done.
		/// </summary>
		private static bool sessionCallDone;

		/// <summary>
		/// The subscription change in progress.
		/// </summary>
		private static bool subscriptionChangeInProgress;

		#endregion // Properties

		#region Events

		/// <summary>
		/// Delegate that notifies about the new notification.
		/// </summary>
		/// <param name="message">The message we received.</param>
		/// <param name="additionalData">List of key, value pairs with additional data.</param>
		/// <param name="isActive">The active flag.</param>
		public delegate void NotificationReceived(string message, IDictionary<string, string> additionalData, bool isActive);

		/// <summary>
		/// Delegate that notifies about the new notification.
		/// </summary>
		private static NotificationReceived notificationDelegate = null;


		/// <summary>
		/// Delegate that notifies that the new ids are available.
		/// </summary>
		/// <param name="playerID">The player ID in the One Signal system.</param>
		/// <param name="pushToken">The received push token.</param>
		public delegate void IdsAvailable(string playerID, string pushToken);

		/// <summary>
		/// Delegate that notifies that the new ids are available.
		/// </summary>
		public static IdsAvailable idsAvailableDelegate = null;


		/// <summary>
		/// Delegate that notifies that the tags was received.
		/// </summary>
		/// <param name="tags">Received tags.</param>
		public delegate void TagsReceived(IDictionary<string, string> tags);

		/// <summary>
		/// Delegate that notifies that the tags was received.
		/// </summary>
		public static TagsReceived tagsReceivedDelegate = null;


		/// <summary>
		/// Delegate that notifies that some error  occured.
		/// </summary>
		/// <param name="message">The message that describes the error.</param>
		public delegate void ErrorOccured(string message);

		/// <summary>
		/// Delegate that notifies that some error  occured.
		/// </summary>
		public static ErrorOccured errorOccured = null;


		#endregion // Events

		/// <summary>
		/// Initialize the OneSignal library.
		/// </summary>
		/// <param name="appId">The application ID in the One Signal system.</param>
		/// <param name="inNotificationDelegate">Delegate that notifies about the new notification.</param>
		public static void Init(string appId, NotificationReceived inNotificationDelegate = null)
		{
			if (initDone)
				return;

			mAppId = appId;
			notificationDelegate = inNotificationDelegate;
			mPlayerId = settings.Contains("GameThrivePlayerId") ? (string)settings["GameThrivePlayerId"] : null;
			mChannelUri = settings.Contains("GameThriveChannelUri") ? (string)settings["GameThriveChannelUri"] : null;

			string channelName = "GameThriveApp" + appId;

			var pushChannel = HttpNotificationChannel.Find(channelName);

			if (pushChannel == null)
			{
				pushChannel = new HttpNotificationChannel(channelName);

				SubscribeToChannelEvents(pushChannel);

				pushChannel.Open();
				pushChannel.BindToShellToast();
				fallBackOneSignalSession = new Timer((o) => Deployment.Current.Dispatcher.BeginInvoke(() => SendSession(null)), null, 20000, Timeout.Infinite);
			}
			else
			{ // Else gets run on the 2nd open of the app and after. This happens on WP8.0 but not WP8.1
				SubscribeToChannelEvents(pushChannel);

				if (!pushChannel.IsShellToastBound)
					pushChannel.BindToShellToast();

				// Since the channel was found ChannelUriUpdated does not fire so send an on session event.
				SendSession(null);
			}

			lastPingTime = DateTime.Now.Ticks;
			// Hook on the application events.
			PhoneApplicationService.Current.Closing += (s, e) => SaveActiveTime();
			PhoneApplicationService.Current.Deactivated += (s, e) => SaveActiveTime();
			PhoneApplicationService.Current.Activated += AppResumed;

			// Using Disatcher due to Unity threading issues with Application.Current.RootVisual.
			// Works fine with normal native apps too.
			Deployment.Current.Dispatcher.BeginInvoke(() =>
			{
				var startingPage = ((PhoneApplicationFrame)Application.Current.RootVisual).Content as PhoneApplicationPage;

				if (startingPage.NavigationContext.QueryString.ContainsKey("GameThriveParams"))
					NotificationOpened(null, startingPage.NavigationContext.QueryString["GameThriveParams"]);

				SendPing(GetSavedActiveTime());

				initDone = true;
			});
		}

		/// <summary>
		/// Reset saved data.
		/// </summary>
		public static void ResetSavedData()
		{
			if (settings.Contains("GameThrivePlayerId"))
				settings.Remove("GameThrivePlayerId");

			if (settings.Contains("GameThriveChannelUri"))
				settings.Remove("GameThriveChannelUri");

			if (settings.Contains("GameThriveActiveTime"))
				settings.Remove("GameThriveActiveTime");

			settings.Save();
		}

		/// <summary>
		/// Returns the saved active time if available, otherwise returns zero.
		/// </summary>
		/// <returns>The saved active time if available, otherwise returns zero.</returns>
		private static long GetSavedActiveTime()
		{
			if (settings.Contains("GameThriveActiveTime"))
				return (long)settings["GameThriveActiveTime"];

			return 0;
		}

		/// <summary>
		/// The handler called when the application is closing or deactivating.
		/// Save off the time the user was running your app so we can send it the next time the app is open/resumed.
		/// This is done as a Http call when the app is closing is not reliable and when the app is Deactivated all http requests get paused.
		/// </summary>
		private static void SaveActiveTime()
		{
			long timeToAdd = (DateTime.Now.Ticks - lastPingTime) / 10000000;

			if (settings.Contains("GameThriveActiveTime"))
				settings["GameThriveActiveTime"] = (long)settings["GameThriveActiveTime"] + timeToAdd;
			else
				settings.Add("GameThriveActiveTime", timeToAdd);

			settings.Save();
		}

		/// <summary>
		/// The handler called when the application is activated.
		/// </summary>
		/// <param name="sender">The sender.</param>
		/// <param name="e">The activated event args.</param>
		private static void AppResumed(object sender, ActivatedEventArgs e)
		{
			lastPingTime = DateTime.Now.Ticks;
			SendPing(GetSavedActiveTime());
		}

		/// <summary>
		/// Send the ping.
		/// </summary>
		/// <param name="activeTime">The active time to send.</param>
		private static void SendPing(long activeTime)
		{
			// Can not updated active_time if we haven't registered yet.
			// Also optimizing bandwidth by waiting until time is 30 secounds or more.
			if (mPlayerId == null || activeTime < 30)
				return;

			JObject jsonObject = JObject.FromObject(new
			{
				state = "ping",
				active_time = activeTime
			});

			var cli = GetWebClient();
			cli.UploadStringCompleted += (s, e) =>
			{
				if (!e.Cancelled && e.Error == null && settings.Contains("GameThriveActiveTime"))
				{
					settings.Remove("GameThriveActiveTime");
					settings.Save();
				}
				else
				{
					if (e.Error != null)
						NotifyErrorOccured(e.Error.Message);
				}
			};
			cli.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId + "/on_focus"), jsonObject.ToString());
			lastPingTime = DateTime.Now.Ticks;
		}

		#region Push Channel Events

		/// <summary>
		/// Subscribe to the channel events.
		/// </summary>
		/// <param name="pushChannel">The push channel to use.</param>
		private static void SubscribeToChannelEvents(HttpNotificationChannel pushChannel)
		{
			pushChannel.ChannelUriUpdated += new EventHandler<NotificationChannelUriEventArgs>(PushChannel_ChannelUriUpdated);
			pushChannel.ErrorOccurred += new EventHandler<NotificationChannelErrorEventArgs>(PushChannel_ErrorOccurred);
			pushChannel.ShellToastNotificationReceived += new EventHandler<NotificationEventArgs>(PushChannel_ShellToastNotificationReceived);
		}

		/// <summary>
		/// The push channel uri updated hanler.
		/// </summary>
		/// <param name="sender">The sender.</param>
		/// <param name="e">The notification channel uri event args.</param>
		private static void PushChannel_ChannelUriUpdated(object sender, NotificationChannelUriEventArgs e)
		{
			string currentChannelUri = null;
			if (e.ChannelUri != null)
			{
				currentChannelUri = e.ChannelUri.ToString();
				System.Diagnostics.Debug.WriteLine("ChannelUri:" + e.ChannelUri.ToString());
			}

			SendSession(currentChannelUri);
		}

		/// <summary>
		/// The push channel error occured hanler.
		/// </summary>
		/// <param name="sender">The sender.</param>
		/// <param name="e">The notification channel error event args.</param>
		private static void PushChannel_ErrorOccurred(object sender, NotificationChannelErrorEventArgs e)
		{
			System.Diagnostics.Debug.WriteLine("ERROR CODE:" + e.ErrorCode + ": Could not register for push notifications do to " + e.Message);

			NotifyErrorOccured(e.Message);
		}

		/// <summary>
		/// The push channel notification hanler.
		/// </summary>
		/// <param name="sender">The sender.</param>
		/// <param name="e">The notification event args.</param>
		private static void PushChannel_ShellToastNotificationReceived(object sender, NotificationEventArgs e)
		{
			if (e.Collection.ContainsKey("wp:Param"))
			{
				string message;
				if (e.Collection.ContainsKey("wp:Text2"))
					message = e.Collection["wp:Text2"];
				else
					message = e.Collection["wp:Text1"];

				NotificationOpened(message, e.Collection["wp:Param"].Replace("?GameThriveParams=", String.Empty));
			}
		}

		#endregion Push Channel Events

		#region SetSubscription

		/// <summary>
		/// Set the subscription.
		/// </summary>
		/// <param name="status">The required status.</param>
		public static void SetSubscription(bool status)
		{
			if (mPlayerId == null || mAppId == null || subscriptionChangeInProgress)
			{
				return;
			}

			subscriptionChangeInProgress = true;

			JObject jsonObject = JObject.FromObject(new
			{
				notification_types = status ? "1" : "-2"
			});

			var webClient = GetWebClient();
			webClient.UploadStringCompleted += (s, e) =>
			{
				subscriptionChangeInProgress = false;

				if (e.Error != null)
					NotifyErrorOccured(e.Error.Message);
			};

			webClient.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId), "PUT", jsonObject.ToString());
		}

		//-------------------------------------------------------------------------

		/// <summary>
		/// Set the subscription.
		/// </summary>
		/// <param name="status">The required status.</param>
		public static Task<bool> SetSubscriptionAsync(bool status)
		{
			var taskComplete = new TaskCompletionSource<bool>();
			subscriptionChangeInProgress = true;
			
			if (mPlayerId != null && mAppId != null && subscriptionChangeInProgress)
			{
				JObject jsonObject = JObject.FromObject(new
				{
					notification_types = status ? "1" : "-2"
				});

				var webClient = GetWebClient();
				webClient.UploadStringCompleted += (s, e) =>
				{
					bool result = false;
					
					if (e.Error == null)
					{
						try
						{
							var success = JObject.Parse(e.Result)["success"];
							if (success != null)
							{
								result = Convert.ToBoolean(success.ToObject<bool>());
							}
						}
						catch
						{
							result = false;
						}
					}

					subscriptionChangeInProgress = false;
					taskComplete.TrySetResult(result);
				};

				webClient.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId), "PUT", jsonObject.ToString());
			}
			else
			{
				subscriptionChangeInProgress = false;
				taskComplete.TrySetResult(false);
			}

			return taskComplete.Task;
		}

		#endregion // SetSubscription

		#region SendSession

		/// <summary>
		/// Send session.
		/// </summary>
		/// <param name="currentChannelUri">The current channel uri.</param>
		private static void SendSession(string currentChannelUri)
		{
			if (sessionCallInProgress || sessionCallDone)
				return;

			sessionCallInProgress = true;

			string adId;
			var type = Type.GetType("Windows.System.UserProfile.AdvertisingManager, Windows, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime");
			if (type != null)  // WP8.1 devices
				adId = (string)type.GetProperty("AdvertisingId").GetValue(null, null);
			else // WP8.0 devices, requires ID_CAP_IDENTITY_DEVICE
				adId = Convert.ToBase64String((byte[])DeviceExtendedProperties.GetValue("DeviceUniqueId"));

			if (currentChannelUri != null && mChannelUri != currentChannelUri)
			{
				mChannelUri = currentChannelUri;
				if (settings.Contains("GameThriveChannelUri"))
					settings["GameThriveChannelUri"] = mChannelUri;
				else
					settings.Add("GameThriveChannelUri", mChannelUri);
				settings.Save();
			}

			JObject jsonObject = JObject.FromObject(new
			{
				device_type = 3,
				app_id = mAppId,
				identifier = mChannelUri,
				ad_id = adId,
				device_model = DeviceStatus.DeviceName,
				device_os = Environment.OSVersion.Version.ToString(),
				game_version = XDocument.Load("WMAppManifest.xml").Root.Element("App").Attribute("Version").Value,
				language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToString(),
				timezone = TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds.ToString(),
				sdk = VERSION
			});

			var cli = GetWebClient();
			cli.UploadStringCompleted += (senderObj, eventArgs) =>
			{
				sessionCallInProgress = false;
				if (eventArgs.Error == null)
				{
					sessionCallDone = true;
					if (fallBackOneSignalSession != null)
						Deployment.Current.Dispatcher.BeginInvoke(() => { fallBackOneSignalSession.Dispose(); });

					// Set player id if new one received or if changed (someone removed the device record on the server).
					string newPlayerId = (string)JObject.Parse(eventArgs.Result)["id"];
					if (mPlayerId == null || ((mPlayerId != newPlayerId) && newPlayerId != null))
					{
						mPlayerId = newPlayerId;
						settings["GameThrivePlayerId"] = mPlayerId;
						settings.Save();
					}

					if (idsAvailableDelegate != null && mPlayerId != null && mChannelUri != null)
					{
						idsAvailableDelegate(mPlayerId, mChannelUri);
					}
				}
			};

			string urlString = BASE_URL + "api/v1/players";
			if (mPlayerId != null)
				urlString += "/" + mPlayerId + "/on_session";

			cli.UploadStringAsync(new Uri(urlString), jsonObject.ToString());
		}
		
		//-------------------------------------------------------------------------

		/// <summary>
		/// The async method to send session.
		/// </summary>
		/// <param name="currentChannelUri">The current channel uri.</param>
		private static Task<bool> SendSessionAsync(string currentChannelUri)
		{
			var taskComplete = new TaskCompletionSource<bool>();

			if (!sessionCallInProgress && !sessionCallDone)
			{
				sessionCallInProgress = true;

				string adId;
				var type = Type.GetType("Windows.System.UserProfile.AdvertisingManager, Windows, Version=255.255.255.255, Culture=neutral, PublicKeyToken=null, ContentType=WindowsRuntime");
				if (type != null)  // WP8.1 devices
					adId = (string)type.GetProperty("AdvertisingId").GetValue(null, null);
				else // WP8.0 devices, requires ID_CAP_IDENTITY_DEVICE
					adId = Convert.ToBase64String((byte[])DeviceExtendedProperties.GetValue("DeviceUniqueId"));

				if (currentChannelUri != null && mChannelUri != currentChannelUri)
				{
					mChannelUri = currentChannelUri;
					if (settings.Contains("GameThriveChannelUri"))
						settings["GameThriveChannelUri"] = mChannelUri;
					else
						settings.Add("GameThriveChannelUri", mChannelUri);
					settings.Save();
				}

				JObject jsonObject = JObject.FromObject(new
				{
					device_type = 3,
					app_id = mAppId,
					identifier = mChannelUri,
					ad_id = adId,
					device_model = DeviceStatus.DeviceName,
					device_os = Environment.OSVersion.Version.ToString(),
					game_version = XDocument.Load("WMAppManifest.xml").Root.Element("App").Attribute("Version").Value,
					language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.ToString(),
					timezone = TimeZoneInfo.Local.BaseUtcOffset.TotalSeconds.ToString(),
					sdk = VERSION
				});

				var webClient = GetWebClient();
				webClient.UploadStringCompleted += (s, e) =>
				{
					bool result = false;
					
					if (!e.Cancelled && e.Error == null)
					{
						sessionCallDone = true;
						if (fallBackOneSignalSession != null)
							Deployment.Current.Dispatcher.BeginInvoke(() => { fallBackOneSignalSession.Dispose(); });

						// Set player id if new one received or if changed (someone removed the device record on the server).
						string newPlayerId = (string)JObject.Parse(e.Result)["id"];
						if (mPlayerId == null || ((mPlayerId != newPlayerId) && newPlayerId != null))
						{
							mPlayerId = newPlayerId;
							settings["GameThrivePlayerId"] = mPlayerId;
							settings.Save();
						}

						if (idsAvailableDelegate != null && mPlayerId != null && mChannelUri != null)
						{
							idsAvailableDelegate(mPlayerId, mChannelUri);
						}

						result = true;
					}
					
					sessionCallInProgress = false;
					taskComplete.TrySetResult(result);
				};

				string urlString = BASE_URL + "api/v1/players";
				if (mPlayerId != null)
					urlString += "/" + mPlayerId + "/on_session";

				webClient.UploadStringAsync(new Uri(urlString), jsonObject.ToString());
			}
			else
			{
				taskComplete.TrySetResult(false);
			}

			return taskComplete.Task;
		}

		#endregion // SendSession

		/// <summary>
		/// Notify that the notification was openned.
		/// </summary>
		/// <param name="message">The message to notify.</param>
		/// <param name="jsonParams">The json params to parse.</param>
		private static void NotificationOpened(string message, string jsonParams)
		{
			JObject jObject = JObject.Parse(jsonParams);

			JObject jsonObject = JObject.FromObject(new
			{
				app_id = mAppId,
				player_id = mPlayerId,
				opened = true
			});

			GetWebClient().UploadStringAsync(new Uri(BASE_URL + "api/v1/notifications/" + (string)jObject["custom"]["i"]), "PUT", jsonObject.ToString());

			if (!initDone && jObject["custom"]["u"] != null)
			{
				WebBrowserTask webBrowserTask = new WebBrowserTask();
				webBrowserTask.Uri = new Uri((string)jObject["custom"]["u"], UriKind.Absolute);
				webBrowserTask.Show();
			}

			if (notificationDelegate != null)
			{
				var additionalDataJToken = jObject["custom"]["a"];
				IDictionary<string, string> additionalData = null;

				if (additionalDataJToken != null)
					additionalData = additionalDataJToken.ToObject<Dictionary<string, string>>();

				// The OS does not pass the notificaiton text into the process when it is tapped on. (Only when in the app is running.)
				if (jObject["custom"]["wpt2"] != null)
				{
					message = (string)jObject["custom"]["wpt2"];
					if (additionalData == null)
						additionalData = new Dictionary<string, string>();
					additionalData.Add(new KeyValuePair<string, string>("title", (string)jObject["custom"]["wpt1"]));
				}
				else if (jObject["custom"]["wpt1"] != null)
					message = (string)jObject["custom"]["wpt1"];

				if (message == null)
					message = string.Empty;

				notificationDelegate(message, additionalData, initDone);
			}
		}

		#region SentTags

		/// <summary>
		/// Send new tag.
		/// </summary>
		/// <param name="key">Key of the tag to set.</param>
		/// <param name="value">Value of the tag to set.</param>
		public static void SendTag(string key, string value)
		{
			var dictionary = new Dictionary<string, object>();
			dictionary.Add(key, value);
			SendTags((IDictionary<string, object>)dictionary);
		}

		/// <summary>
		/// Send new tags.
		/// </summary>
		/// <param name="keyValues">New tags to set.</param>
		public static void SendTags(IDictionary<string, string> keyValues)
		{
			var newDict = new Dictionary<string, object>();
			foreach (var item in keyValues)
				newDict.Add(item.Key, item.Value.ToString());

			SendTags(newDict);
		}

		/// <summary>
		/// Send new tags.
		/// </summary>
		/// <param name="keyValues">New tags to set.</param>
		public static void SendTags(IDictionary<string, int> keyValues)
		{
			var newDict = new Dictionary<string, object>();
			foreach (var item in keyValues)
				newDict.Add(item.Key, item.Value.ToString());

			SendTags(newDict);
		}

		/// <summary>
		/// Send new tags.
		/// </summary>
		/// <param name="keyValues">New tags to set.</param>
		public static void SendTags(IDictionary<string, object> keyValues)
		{
			if (mPlayerId == null)
				return;

			JObject jsonObject = JObject.FromObject(new
			{
				tags = keyValues
			});

			var webClient = GetWebClient();
			webClient.UploadStringCompleted += (s, e) =>
			{
				if (!e.Cancelled && e.Error == null)
				{
					try
					{
						var success = JObject.Parse(e.Result)["success"];
						if (success != null)
						{
							var result = Convert.ToBoolean(success.ToObject<bool>());
							if (!result)
							{
								NotifyErrorOccured("Send Tags Failed.");
							}
						}
						else
						{
							NotifyErrorOccured("Send Tags Failed.");
						}
					}
					catch (Exception ex)
					{
						NotifyErrorOccured(ex.Message);
					}
				}
				else if (e.Error != null)
					NotifyErrorOccured(e.Error.Message);
			};


			webClient.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId), "PUT", jsonObject.ToString());
		}

		//---------------------------------------------------------------------------

		/// <summary>
		/// The async method to send new tag.
		/// </summary>
		/// <param name="key">Key of the tag to set.</param>
		/// <param name="value">Value of the tag to set.</param>
		public static Task<bool> SendTagAsync(string key, string value)
		{
			var dictionary = new Dictionary<string, object>();
			dictionary.Add(key, value);

			return SendTagsAsync((IDictionary<string, object>)dictionary);
		}

		/// <summary>
		/// The async method to send new tags.
		/// </summary>
		/// <param name="keyValues">New tags to set.</param>
		public static Task<bool> SendTagsAsync(IDictionary<string, string> keyValues)
		{
			var newDict = new Dictionary<string, object>();
			foreach (var item in keyValues)
				newDict.Add(item.Key, item.Value.ToString());

			return SendTagsAsync(newDict);
		}

		/// <summary>
		/// The async method to send new tags.
		/// </summary>
		/// <param name="keyValues">New tags to set.</param>
		public static Task<bool> SendTagsAsync(IDictionary<string, int> keyValues)
		{
			var newDict = new Dictionary<string, object>();
			foreach (var item in keyValues)
				newDict.Add(item.Key, item.Value.ToString());

			return SendTagsAsync(newDict);
		}

		/// <summary>
		/// The async method to send new tags.
		/// </summary>
		/// <param name="keyValues">New tags to set.</param>
		public static Task<bool> SendTagsAsync(IDictionary<string, object> keyValues)
		{
			var taskComplete = new TaskCompletionSource<bool>();

			if (mPlayerId != null)
			{

				JObject jsonObject = JObject.FromObject(new
				{
					tags = keyValues
				});

				var webClient = GetWebClient();
				webClient.UploadStringCompleted += (s, e) =>
				{
					bool result = false;

					if (e.Error == null)
					{
						try
						{
							var success = JObject.Parse(e.Result)["success"];
							if (success != null)
							{
								result = Convert.ToBoolean(success.ToObject<bool>());
							}
						}
						catch
						{
							result = false;
						}
					}

					taskComplete.TrySetResult(result);
				};

				webClient.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId), "PUT", jsonObject.ToString());
			}
			else
			{
				taskComplete.TrySetResult(false);
			}

			return taskComplete.Task;
		}

		#endregion // SentTags

		#region DeleteTags

		/// <summary>
		/// Delete tag with the specified name.
		/// </summary>
		/// <param name="tag">Name of the tag.</param>
		public static void DeleteTag(string tag)
		{
			DeleteTags(new List<string>() { tag });
		}

		/// <summary>
		/// Delete tags with the specified names.
		/// </summary>
		/// <param name="tag">Names of the tags.</param>
		public static void DeleteTags(IList<string> tags)
		{
			if (mPlayerId == null)
				return;

			var dictionary = new Dictionary<string, string>();
			foreach (string key in tags)
				dictionary.Add(key, "");

			JObject jsonObject = JObject.FromObject(new
			{
				tags = dictionary
			});

			var webClient = new WebClient();
			webClient.UploadStringCompleted += (s, e) =>
			{
				if (!e.Cancelled && e.Error == null)
				{
					try
					{
						var success = JObject.Parse(e.Result)["success"];
						if (success != null)
						{
							var result = Convert.ToBoolean(success.ToObject<bool>());
							if (!result)
							{
								NotifyErrorOccured("Delete Tags Failed.");
							}
						}
						else
						{
							NotifyErrorOccured("Delete Tags Failed.");
						}
					}
					catch (Exception ex)
					{
						NotifyErrorOccured(ex.Message);
					}
				}
				else if (e.Error != null)
					NotifyErrorOccured(e.Error.Message);
			};

			webClient.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId), "PUT", jsonObject.ToString());
		}

		//---------------------------------------------------------------------------

		/// <summary>
		/// The async method to delete tag with the specified name.
		/// </summary>
		/// <param name="tag">Name of the tag.</param>
		public static Task<bool> DeleteTagAsync(string tag)
		{
			return DeleteTagsAsync(new List<string>() { tag });
		}

		/// <summary>
		/// The async method to delete tags with the specified names.
		/// </summary>
		/// <param name="tag">Names of the tags.</param>
		public static Task<bool> DeleteTagsAsync(IList<string> tags)
		{
			var taskComplete = new TaskCompletionSource<bool>();

			if (mPlayerId != null)
			{
				var dictionary = new Dictionary<string, string>();
				foreach (string key in tags)
					dictionary.Add(key, String.Empty);

				JObject jsonObject = JObject.FromObject(new
				{
					tags = dictionary
				});

				var webClient = GetWebClient();
				webClient.UploadStringCompleted += (s, e) =>
				{
					bool result = false;

					if (e.Error == null)
					{
						try
						{
							var success = JObject.Parse(e.Result)["success"];
							if (success != null)
							{
								result = Convert.ToBoolean(success.ToObject<bool>());
							}
						}
						catch
						{
							result = false;
						}
					}

					taskComplete.TrySetResult(result);
				};

				webClient.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId), "PUT", jsonObject.ToString());
			}
			else
			{
				taskComplete.TrySetResult(false);
			}

			return taskComplete.Task;
		}

		#endregion // DeleteTags

		#region SendPurchase

		/// <summary>
		/// Send purchase with specified amount.
		/// </summary>
		/// <param name="amount">Amount of the purchase.</param>
		public static void SendPurchase(double amount)
		{
			SendPurchase((decimal)amount);
		}

		/// <summary>
		/// Send purchase with specified amount.
		/// </summary>
		/// <param name="amount">Amount of the purchase.</param>
		public static void SendPurchase(decimal amount)
		{
			if (mPlayerId == null)
				return;

			JObject jsonObject = JObject.FromObject(new
			{
				amount = amount
			});

			var webClient = new WebClient();
			webClient.UploadStringCompleted += (s, e) =>
			{
				if (!e.Cancelled && e.Error == null)
				{
					try
					{
						var success = JObject.Parse(e.Result)["success"];
						if (success != null)
						{
							var result = Convert.ToBoolean(success.ToObject<bool>());
							if (!result)
							{
								NotifyErrorOccured("Send Purchase Failed.");
							}
						}
						else
						{
							NotifyErrorOccured("Send Purchase Failed.");
						}
					}
					catch (Exception ex)
					{
						NotifyErrorOccured(ex.Message);
					}
				}
				else if (e.Error != null)
					NotifyErrorOccured(e.Error.Message);
			};

			webClient.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId + "/on_purchase"), jsonObject.ToString());
		}

		//---------------------------------------------------------------------------

		/// <summary>
		/// The async method to send purchase with specified amount.
		/// </summary>
		/// <param name="amount">Amount of the purchase.</param>
		public static Task<bool> SendPurchaseAsync(double amount)
		{
			return SendPurchaseAsync((decimal)amount);
		}

		/// <summary>
		/// The async method to send purchase with specified amount.
		/// </summary>
		/// <param name="amount">Amount of the purchase.</param>
		public static Task<bool> SendPurchaseAsync(decimal amount)
		{
			var taskComplete = new TaskCompletionSource<bool>();

			if (mPlayerId != null)
			{
				JObject jsonObject = JObject.FromObject(new
				{
					amount = amount
				});

				var webClient = new WebClient();
				webClient.UploadStringCompleted += (s, e) =>
				{
					bool result = false;

					if (e.Error == null)
					{
						try
						{
							var success = JObject.Parse(e.Result)["success"];
							if (success != null)
							{
								result = Convert.ToBoolean(success.ToObject<bool>());
							}
						}
						catch
						{
							result = false;
						}
					}

					taskComplete.TrySetResult(result);
				};

				webClient.UploadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId + "/on_purchase"), jsonObject.ToString());
			}
			else
			{
				taskComplete.TrySetResult(false);
			}

			return taskComplete.Task;
		}

		#endregion // SendPurchase

		#region GetIdsAvailable

		/// <summary>
		/// Gets available ids.
		/// </summary>
		public static void GetIdsAvailable()
		{
			if (idsAvailableDelegate == null)
				throw new ArgumentNullException("Assign idsAvailableDelegate before calling or call GetIdsAvailable(IdsAvailable)");

			if (mPlayerId != null)
				idsAvailableDelegate(mPlayerId, mChannelUri);
		}

		/// <summary>
		/// Gets available ids.
		/// </summary>
		/// <param name="inIdsAvailableDelegate">Handler to call when ids available.</param>
		public static void GetIdsAvailable(IdsAvailable inIdsAvailableDelegate)
		{
			idsAvailableDelegate = inIdsAvailableDelegate;

			if (mPlayerId != null)
				idsAvailableDelegate(mPlayerId, mChannelUri);
		}

		#endregion // GetIdsAvailable

		#region GetTags

		/// <summary>
		/// Get tags associated with the player id.
		/// </summary>
		public static void GetTags()
		{
			if (mPlayerId == null)
				return;

			if (tagsReceivedDelegate == null)
				throw new ArgumentNullException("Assign tagsReceivedDelegate before calling or call GetTags(TagsReceived)");

			SendGetTagsMessage();
		}

		/// <summary>
		/// Get tags associated with the player id.
		/// </summary>
		/// <param name="inTagsReceivedDelegate">Handler to call when tags available.</param>
		public static void GetTags(TagsReceived inTagsReceivedDelegate)
		{
			if (mPlayerId == null)
				return;

			tagsReceivedDelegate = inTagsReceivedDelegate;

			SendGetTagsMessage();
		}

		/// <summary>
		/// Retrieve tags.
		/// </summary>
		private static void SendGetTagsMessage()
		{
			var webClient = new WebClient();
			webClient.DownloadStringCompleted += (s, e) =>
			{
				if (e.Error == null)
					if (!e.Cancelled)
						tagsReceivedDelegate(JObject.Parse(e.Result)["tags"].ToObject<Dictionary<string, string>>());
				else
					NotifyErrorOccured(e.Error.Message);
			};

			webClient.DownloadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId + ".test"));
		}

		///--------------------------------------------------------------------------

		/// <summary>
		/// The async method to get tags associated with the player id.
		/// </summary>
		async public static Task<Dictionary<string, string>> GetTagsAsync()
		{
			if (mPlayerId == null)
				return null;

			var result = await SendGetTagsMessageAsync();
			return result;
		}

		/// <summary>
		/// The async method to sends the tag message asynchronously.
		/// </summary>
		/// <returns>Task.</returns>
		private static Task<Dictionary<string, string>> SendGetTagsMessageAsync()
		{
			var taskComplete = new TaskCompletionSource<Dictionary<string, string>>();

			var webClient = new WebClient();
			webClient.DownloadStringCompleted += (s, e) =>
			{
				Dictionary<string, string> result = null;

				if (!e.Cancelled && e.Error == null)
				{
					try
					{
						result = JObject.Parse(e.Result)["tags"].ToObject<Dictionary<string, string>>();
					}
					catch
					{
						result = null;
					}
				}

				taskComplete.TrySetResult(result);
			};

			webClient.DownloadStringAsync(new Uri(BASE_URL + "api/v1/players/" + mPlayerId + ".test"));

			return taskComplete.Task;
		}

		#endregion // GetTags

		#region Helper Methods

		/// <summary>
		/// Creates new instance of the web client and sets the apropriate headers.
		/// </summary>
		/// <returns>The new instance of the web client.</returns>
		private static WebClient GetWebClient()
		{
			var webClient = new WebClient();
			webClient.Headers[HttpRequestHeader.ContentType] = "application/json";

			return webClient;
		}

		/// <summary>
		/// Notify that the error occured.
		/// </summary>
		/// <param name="message">The Error message to notify.</param>
		private static void NotifyErrorOccured(string message)
		{
			if (errorOccured != null)
				errorOccured(message);
		}

		#endregion // Helper Methods
	}
}