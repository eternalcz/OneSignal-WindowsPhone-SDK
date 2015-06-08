using Microsoft.Phone.Controls;

using System.Collections.Generic;
using System.Windows;
using System.Windows.Navigation;
using System.Linq;

using OneSignalSDK;

namespace OneSignalExample {
    public partial class MainPage : PhoneApplicationPage {
        
        public MainPage() {
            InitializeComponent();
            SendTagsButton.Click += SendTagsButton_Click;
            SendPurchaseButton.Click += SendPurchaseButton_Click;
        }

        void SendPurchaseButton_Click(object sender, RoutedEventArgs e) {
            OneSignal.SendPurchase(1.99);
        }

        void SendTagsButton_Click(object sender, RoutedEventArgs e) {
            OneSignal.SendTag("WPKey", "WPValue");
        }

        protected override void OnNavigatedTo(NavigationEventArgs navEventArgs) {
            base.OnNavigatedTo(navEventArgs);

            OneSignal.Init("b2f7f966-d8cc-11e4-bed1-df8f05be55ba", ReceivedNotification);
        }

        // Called when the user opens a notification or one comes in while using the app.
        // The name of the method can be anything as long as the signature matches.
        // Method must be static or be in a class where the same instance stays alive with the app.
        private static void ReceivedNotification(string message, IDictionary<string, string> additionalData, bool isActive) {
            System.Diagnostics.Debug.WriteLine("message: " + message);
            if (additionalData != null)
                System.Diagnostics.Debug.WriteLine("\nadditionalData:\n" + string.Join(";", additionalData.Select(x => x.Key + "=" + x.Value).ToArray()));
        }
    }
}