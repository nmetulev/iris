using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using Windows.Devices.Gpio;
using System.Diagnostics; // Contains Stopwatch 
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Messaging;


// Die Vorlage "Leere Seite" ist unter http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409 dokumentiert.

namespace door_pi
{
    /// <summary>
    /// Eine leere Seite, die eigenständig verwendet oder zu der innerhalb eines Rahmens navigiert werden kann.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private const int SERVO_PIN = 18;
        private GpioPin servoPin;
        private DispatcherTimer timer;
        private double BEAT_PACE = 1000; // Switch side every second
        private double CounterClockwiseDanceMove = 1;
        private double ClockwiseDanceMove = 2;
        private double currentDirection;
        private double PulseFrequency = 20;
        Stopwatch stopwatch;
        private string connstring = "Endpoint=sb://pragirismessagequeue.servicebus.windows.net/;SharedAccessKeyName=all;SharedAccessKey=L9/vm+g45Zes4WvMUYEVan7X4mDOJaIPCNAiXQHb2KI=";
        private string queueName = "pragirismessagequeueentity";
       
       


        public MainPage()
        {


            this.InitializeComponent();
            
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            // Preparing our GPIO controller 
            var gpio = GpioController.GetDefault();
            if (gpio == null)
            {
                servoPin = null;
                Debug.WriteLine("No GPIO controller found");
                return;
            }

            // Servo set up 

            servoPin = gpio.OpenPin(SERVO_PIN);
            servoPin.SetDriveMode(GpioPinDriveMode.Output);

            stopwatch = Stopwatch.StartNew();

            currentDirection = 1; // Initially we aren't dancing at all.
           
            Debug.WriteLine("GPIO pin ready");

            Queue myQueueClient = new Queue(queueName, connstring);
            myQueueClient.OnMessage( async (message) =>
            {
                Debug.WriteLine("Opening door via Azure ;)");
                OpenDoor();

                await Task.Delay(5000);
                CloseDoor();
                Debug.WriteLine("Door has closed automatically");
            });

            if (servoPin != null)
            {
                await Windows.System.Threading.ThreadPool.RunAsync(this.MotorThread, Windows.System.Threading.WorkItemPriority.High);

                //Set UI light on
                //lightImg.Source = new BitmapImage(new Uri("ms-appx:/Images/Bombilla_verde_-_green_Edison_lamp.png", UriKind.Absolute));

                //Wait(2000); 
                //servoPin.Write(GpioPinValue.Low);

                //Set UI light off
                //lightImg.Source = new BitmapImage(new Uri("ms-appx:/Images/Bombilla_roja_-_red_Edison_lamp.png", UriKind.Absolute));
            }

            
        }

        private async void OpenDoor()
        {
            currentDirection = ClockwiseDanceMove;
            

            await Task.Delay(1000);
            currentDirection = 0;
        }

        private async void CloseDoor()
        {
            currentDirection = CounterClockwiseDanceMove;

            await Task.Delay(1000);
            currentDirection = 0;

            //Set UI light off
            //lightImg.Source = new BitmapImage(new Uri("ms-appx:/Images/Bombilla_roja_-_red_Edison_lamp.png", UriKind.Absolute));
        }

        private void MotorThread(IAsyncAction action)
        {
            while (true)
            {
                if (currentDirection != 0)
                {
                    servoPin.Write(GpioPinValue.High);
                    
                }

                Wait(currentDirection);

                servoPin.Write(GpioPinValue.Low);
                Wait(PulseFrequency - currentDirection);
            }
        }


        private void Wait(double milliseconds)
         { 
             long initialTick = stopwatch.ElapsedTicks; 
             long initialElapsed = stopwatch.ElapsedMilliseconds; 
             double desiredTicks = milliseconds / 1000.0 * Stopwatch.Frequency; 
             double finalTick = initialTick + desiredTicks; 
             while (stopwatch.ElapsedTicks<finalTick) 
             { 
 
 
             } 
         }

        private void Beat(object sender, object e)
        {
            if (currentDirection != ClockwiseDanceMove)
            {
                currentDirection = ClockwiseDanceMove;
            }
            else
            {
                currentDirection = CounterClockwiseDanceMove;
            }
        }


        private void openBtn_Click(object sender, RoutedEventArgs e)
        {
            OpenDoor();
            Debug.WriteLine("Opening door...");
        }

        private void closeBtn_Click(object sender, RoutedEventArgs e)
        {
            CloseDoor();
            Debug.WriteLine("Closing door...");
        }
    }
}
