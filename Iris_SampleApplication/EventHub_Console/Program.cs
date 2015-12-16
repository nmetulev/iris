using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


using System.Threading;
using Microsoft.ServiceBus.Messaging;
using Newtonsoft.Json;

namespace EventHub_ConsoleSender
{

    class Program
    {
        static string eventHubName = "pragiriseventhub";
        static string connectionString = "Endpoint=sb://pragiriseventhub-ns.servicebus.windows.net/;SharedAccessKeyName=SendRule;SharedAccessKey=sYuB6HWiGf+CQOEpUOms3BCB0bsnH+Bn1iy9LWg0oLw=";
        static void Main(string[] args)
        {

            Console.WriteLine("Press Ctrl-C to stop the sender process");
            Console.WriteLine("Press Enter to start now");
            Console.ReadLine();
            SendingRandomMessages();
        }

        static void SendingRandomMessages()
        {
            var eventHubClient = EventHubClient.CreateFromConnectionString(connectionString, eventHubName);
            for (int i =0; i<100; i++)
            {
                try
                {
                    var dto = new DTOLib.IrisMessage("yes", 0.0);
                    var json = JsonConvert.SerializeObject(dto);
                    var msg = Encoding.UTF8.GetBytes(json);
                    Console.WriteLine("{0} > Sending message: {1} as Json: {2}", DateTime.Now, msg, json);
                    
                    eventHubClient.Send(new EventData(msg));
                }
                catch (Exception exception)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine("{0} > Exception: {1}", DateTime.Now, exception.Message);
                    Console.ResetColor();
                }

                Thread.Sleep(200);
            }
        }
    }
}
