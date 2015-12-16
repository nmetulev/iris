using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.ServiceBus.Messaging;

using System.Threading.Tasks;

namespace EventHub_Receiver
{
    class Program
    {
        static void Main(string[] args)
        {
            string eventHubConnectionString = "Endpoint=sb://pragiriseventhub-ns.servicebus.windows.net/;SharedAccessKeyName=ReceiveRule;SharedAccessKey=BILvwe5Pgg/lXalvadQZIm3+A2HDO47ZAK70mX6TDzk=";
            string eventHubName = "pragiriseventhub";
            string storageAccountName = "pragiriseventhubstorage";
            string storageAccountKey = "6J94uWFse+nBUbmMlJ62PNtAgwf0p8o53KhxngMxnhIzMMHQABNaGaqzmXGMT5KoCWOrV7tMexLY+7dERPRM+Q==";
            string storageConnectionString = string.Format("DefaultEndpointsProtocol=https;AccountName={0};AccountKey={1}", storageAccountName, storageAccountKey);

            string eventProcessorHostName = Guid.NewGuid().ToString();
            EventProcessorHost eventProcessorHost = new EventProcessorHost(eventProcessorHostName, eventHubName, EventHubConsumerGroup.DefaultGroupName, eventHubConnectionString, storageConnectionString);
            Console.WriteLine("Registering EventProcessor...");
            eventProcessorHost.RegisterEventProcessorAsync<SimpleEventProcessor>().Wait();

            Console.WriteLine("Receiving. Press enter key to stop worker.");
            Console.ReadLine();
            eventProcessorHost.UnregisterEventProcessorAsync().Wait();
        }
    }
}
