using DTOLib;
using Microsoft.WindowsAzure.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace App_Sender
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private string azureConnectionString = "Endpoint=sb://pragiriseventhub-ns.servicebus.windows.net/;SharedAccessKeyName=SendRule;SharedAccessKey=sYuB6HWiGf+CQOEpUOms3BCB0bsnH+Bn1iy9LWg0oLw=";
        private Queue myQueueClient;
        private string queueName = "pragiriseventhub";
        Random ran = new Random();

        public MainPage()
        {
            this.InitializeComponent();
        }

        public async void SendMessage()
        {

            myQueueClient = new Queue(queueName, azureConnectionString);

            // create dummy values randomly
            int randomValue = ran.Next(1, 100);
            double val = 1.0 / randomValue;

            var success = (val > 0.5) ? "yes" : "no";
            var prob = val;

            // create Iris message
            var msg = new IrisMessage(success  , prob);
            

            // send
            //Message m = new Message(msg);
            var jsonMsg = JsonConvert.SerializeObject(msg);
            // await myQueueClient.SendAsync(jsonMsg);

            JsonObject jo = new JsonObject();

            JsonObject.TryParse(jsonMsg, out jo);
            // serialize
            await myQueueClient.SendAsync(jo);                        

        }

        private async void Button_Click(object sender, RoutedEventArgs e)
        {
             SendMessage();
        }
    }
}
