//using Microsoft.WindowsAzure.Messaging;
using Microsoft.WindowsAzure.Messaging;
using ppatierno.AzureSBLite.Messaging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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

namespace App_Receiver
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
       

        private string connstring = "Endpoint=sb://pragirismessagequeue.servicebus.windows.net/;SharedAccessKeyName=all;SharedAccessKey=L9/vm+g45Zes4WvMUYEVan7X4mDOJaIPCNAiXQHb2KI=";
        private string queueName = "pragirismessagequeueentity";
      //  private string consumerGroupName = "pragirisdooropenerconsumergroup";

        public MainPage()
        {
            this.InitializeComponent();
            ReceiveMsgs();
        }

        public async void ReceiveMsgs()
        {
            //Microsoft.WindowsAzure.Messaging.ServiceBusClient client = new ServiceBusClient(connstring);

            Queue myQueueClient = new Queue(queueName, connstring);
            myQueueClient.OnMessage((message) =>
            {
                DoSomething(message);
            });

            //var client = ppatierno.AzureSBLite.Messaging.EventHubClient.CreateFromConnectionString(connstring);
            
            //var consumerGroup = client.GetConsumerGroup(consumerGroupName);            

            //EventHubReceiver rec = consumerGroup.CreateReceiver("0");
            //while (true)
            //{
            //    await Task.Delay(1000);
            //    EventData result = rec.Receive();
            //    if (result!=null)
            //    {
            //        DoSomething(result);
            //    }
            //}
            //var reciver1 = consumerGroup.CreateReceiver("1");

            //var res = reciver.Receive();
            
            


            //var queue = new Queue("hello", connstring);
            
            //queue.OnMessage((message) => {
            //    DoSomething(message);
            //});


        }

        private void DoSomething(Message message)
        {
            var x = message;
        }

        private void DoSomething(EventData result)
        {
            var x = result;
        }

        
        //private async void DoSomething(Message message)
        //{
        //    await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
        //     {
        //         myTextBox.Text = message.ToString();
        //     });
        //}
    }
}
