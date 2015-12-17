using DTOLib;
using GHIElectronics.UWP.Shields;
using Microsoft.ProjectOxford.Emotion;
using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using Microsoft.WindowsAzure.Messaging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Devices.Enumeration;
using Windows.Devices.Sensors;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Foundation.Metadata;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.Storage.Streams;
using Windows.System.Display;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace iris_pi
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private readonly DisplayInformation _displayInformation = DisplayInformation.GetForCurrentView();
        private readonly SimpleOrientationSensor _orientationSensor = SimpleOrientationSensor.GetDefault();
        private SimpleOrientation _deviceOrientation = SimpleOrientation.NotRotated;
        private DisplayOrientations _displayOrientation = DisplayOrientations.Portrait;

        // Rotation metadata to apply to the preview stream and recorded videos (MF_MT_VIDEO_ROTATION)
        // Reference: http://msdn.microsoft.com/en-us/library/windows/apps/xaml/hh868174.aspx
        private static readonly Guid RotationKey = new Guid("C380465D-2271-428C-9B83-ECEA3B4A85C1");

        // Prevent the screen from sleeping while the camera is running
        private readonly DisplayRequest _displayRequest = new DisplayRequest();

        // MediaCapture and its state variables
        private MediaCapture _mediaCapture;
        private bool _isInitialized;
        private bool _isPreviewing;

        private FaceDetectionEffect _faceDetectionEffect;

        // Information about the camera device
        private bool _mirroringPreview;
        private bool _externalCamera;

        private EmotionServiceClient _emotionClient;
        private FaceServiceClient _faceClient;

        private bool _analyzing = false;
        private bool _faceDetected = false;

        private FEZHAT _fez;


        private string _groupName = "949fd5e0-0e26-4faf-9033-23f99ef423eb";

        private string azureConnectionString = "Endpoint=sb://pragiriseventhub-ns.servicebus.windows.net/;SharedAccessKeyName=SendRule;SharedAccessKey=sYuB6HWiGf+CQOEpUOms3BCB0bsnH+Bn1iy9LWg0oLw=";
        private Queue myQueueClient;
        private string queueName = "pragiriseventhub";
        Random ran = new Random();

        public MainPage()
        {
            this.InitializeComponent();
            _emotionClient = new EmotionServiceClient(APIKey.Emotion);
            _faceClient = new FaceServiceClient(APIKey.Face);
            
        }
        
        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await InitializeCameraAsync();

            await InitFez();

            UpdateUI("Welcome!", "Do you have what it takes to enter?", Colors.Black, Colors.White);
        }


        #region Event handlers

        private void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            if (args.ResultFrame.DetectedFaces.Count > 0)
            {
                if (!_faceDetected && !_analyzing)
                {
                    
                    var frame = args.ResultFrame;
                    var box = frame.DetectedFaces.First().FaceBox;



                    if (((double)box.Height / (double)_previewStream.Height) < 0.3)
                    {
                        UpdateUI("Hi there!", "Come closer so I can look at you better", Colors.LightGray, Colors.Black);
                        //SetStatus("Come Closer");
                    }
                    else
                    {
                        _faceDetected = true;

                        if (!_analyzing) TakePhotoAndAnalyzeAsync();
                    }

                    Debug.WriteLine("Face Detected: Height: " + box.Height + " Width: " + box.Width);
                    
                }
                else
                {
                    Debug.WriteLine("Same Face Detected!");

                }

            }
            else if (!_analyzing)
            {
                
                UpdateUI("Welcome!", "Do you have what it takes to enter?", Colors.Black, Colors.White);
                _faceDetected = false;
            }
        }

        #endregion Event handlers

        VideoEncodingProperties _previewStream;

        #region MediaCapture methods

        private async Task InitializeCameraAsync()
        {
            Debug.WriteLine("InitializeCameraAsync");

            if (_mediaCapture == null)
            {
                // Attempt to get the back camera if one is available, but use any camera device if not
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Front);

                if (cameraDevice == null)
                {
                    Debug.WriteLine("No camera device found!");
                    return;
                }

                // Create MediaCapture and its settings
                _mediaCapture = new MediaCapture();
                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id, StreamingCaptureMode = StreamingCaptureMode.Video };

                // Initialize MediaCapture
                try
                {
                    await _mediaCapture.InitializeAsync(settings);
                    _isInitialized = true;
                    CaptureElement element = new CaptureElement();
                    element.Source = _mediaCapture;
                    await _mediaCapture.StartPreviewAsync();

                    var previewProperties = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                    _previewStream = previewProperties as VideoEncodingProperties;

                    var definition = new FaceDetectionEffectDefinition();
                    definition.SynchronousDetectionEnabled = false;
                    definition.DetectionMode = FaceDetectionMode.HighPerformance;

                    _faceDetectionEffect = (await _mediaCapture.AddVideoEffectAsync(definition, MediaStreamType.VideoPreview)) as FaceDetectionEffect;

                    _faceDetectionEffect.DesiredDetectionInterval = TimeSpan.FromMilliseconds(100);
                    _faceDetectionEffect.Enabled = true;

                    _faceDetectionEffect.FaceDetected += FaceDetectionEffect_FaceDetected;
                }
                catch (UnauthorizedAccessException)
                {
                    Debug.WriteLine("The app was denied access to the camera");
                }
            }
        }
        
        private async Task StopPreviewAsync()
        {
            // Stop the preview
            _isPreviewing = false;
            await _mediaCapture.StopPreviewAsync();
        }
        
        private async Task TakePhotoAndAnalyzeAsync()
        {
            _analyzing = true;
            var stream = new InMemoryRandomAccessStream();

            try
            {
                await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

                UpdateUI("I got you now!", "Let me just see who you are...", Colors.LightGray, Colors.Black);


                IrisMessage message = await ReencodeAndAnalyze(stream);
                if (message.Success == "no")
                {
                    stream = new InMemoryRandomAccessStream();
                    await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
                    message = await ReencodeAndAnalyze(stream);
                }

                SendMessage(message);
                if (message.Success == "yes")
                {
                    SetLightsAproved();
                }
                else
                {
                    SetLightsDenied();
                    UpdateUI("Intruder, Intruder!", "You do not belong here, shoo...", Colors.Red, Colors.White);

                }

                await Task.Delay(4000);

            }
            catch (Exception ex)
            {
                // File I/O errors are reported as exceptions
                Debug.WriteLine("Exception when taking a photo: " + ex.ToString());
                SendMessage(new IrisMessage(success: "no"));
            }

            _analyzing = false;

        }
        
        private async Task CleanupCameraAsync()
        {
            Debug.WriteLine("CleanupCameraAsync");

            if (_isInitialized)
            {
                if (_isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    await StopPreviewAsync();
                }

                _isInitialized = false;
            }

            if (_mediaCapture != null)
            {
                _mediaCapture.Dispose();
                _mediaCapture = null;
            }
        }

        #endregion MediaCapture methods


        #region Helper functions

        private void SetStatus(string text)
        {
            //Status.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    Status.Text = text;
            //});
        }

        private void AppendStatus(string text)
        {
            //Status.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            //{
            //    Status.Text += text;
            //});
        }

        private void UpdateUI(string mainMessage, string subttitleMessage, Color backgroundColor, Color foregroundColor)
        {
            var t = Root.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                MainText.Text = mainMessage;
                SubtitleText.Text = subttitleMessage;
                Root.Background = new SolidColorBrush(backgroundColor);
                MainText.Foreground = new SolidColorBrush(foregroundColor);
                SubtitleText.Foreground = new SolidColorBrush(foregroundColor);
                Debug.WriteLine(subttitleMessage);
            });
        }
        
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }
        
        private async Task<IrisMessage> ReencodeAndAnalyze(IRandomAccessStream stream)
        {
            bool success = false;
            IrisMessage message = new IrisMessage();
            
            //SetStatus("Analyzing...");

            using (var inputStream = stream)
            {
                var decoder = await BitmapDecoder.CreateAsync(inputStream);

                using (var outputStream = new InMemoryRandomAccessStream())
                {
                    var encoder = await BitmapEncoder.CreateForTranscodingAsync(outputStream, decoder);

                    var properties = new BitmapPropertySet { { "System.Photo.Orientation", new BitmapTypedValue(PhotoOrientation.Normal, PropertyType.UInt16) } };

                    await encoder.BitmapProperties.SetPropertiesAsync(properties);
                    await encoder.FlushAsync();
                    
                    var faceStream = outputStream.AsStream();
                    faceStream.Seek(0, SeekOrigin.Begin);

                    MemoryStream emotionStream = new MemoryStream();
                    await faceStream.CopyToAsync(emotionStream);

                    faceStream.Seek(0, SeekOrigin.Begin);
                    emotionStream.Seek(0, SeekOrigin.Begin);


                    var faces = await _faceClient.DetectAsync(faceStream);
                    if (faces.Count() > 0)
                    {
                        var identifyResult = await _faceClient.IdentifyAsync(_groupName, faces.Select(ff => ff.FaceId).ToArray());

                        Person person = null;

                        if (identifyResult.Count() == 0 || identifyResult.First().Candidates.Count() == 0)
                        {
                           // SetStatus("You are nobody");
                        }
                        else
                        {
                            var candidate = identifyResult.First().Candidates.First();
                            person = await _faceClient.GetPersonAsync(_groupName, candidate.PersonId);

                            SetStatus("You are " + person.Name + "(" + candidate.Confidence + ")");
                            success = true;

                            message.Probability = candidate.Confidence;
                        }


                        var result = await _emotionClient.RecognizeAsync(emotionStream);

                        if (result.Count() > 0)
                        {
                            var scores = result.First().Scores;

                            message.Anger = scores.Anger;
                            message.Contempt = scores.Contempt;
                            message.Disgust = scores.Disgust;
                            message.Fear = scores.Fear;
                            message.Happiness = scores.Happiness;
                            message.Neutral = scores.Neutral;
                            message.Sadness = scores.Sadness;
                            message.Surprise = scores.Surprise;

                            var max = scores.Anger;
                            string emotion = "Angry";
                            if (scores.Contempt > max)
                            {
                                max = scores.Contempt;
                                emotion = "Contempt";
                            }
                            if (scores.Disgust > max)
                            {
                                max = scores.Disgust;
                                emotion = "Disgusted";
                            }
                            if (scores.Fear > max)
                            {
                                max = scores.Fear;
                                emotion = "Scared";
                            }
                            if (scores.Happiness > max)
                            {
                                max = scores.Happiness;
                                emotion = "Happy";
                            }
                            if (scores.Neutral > max)
                            {
                                max = scores.Neutral;
                                emotion = "Neutral";
                            }
                            if (scores.Sadness > max)
                            {
                                max = scores.Sadness;
                                emotion = "Sad";
                            }
                            if (scores.Surprise > max)
                            {
                                max = scores.Surprise;
                                emotion = "Surprised";
                            }

                            AppendStatus(" | You are " + emotion);

                            if (success && person != null)
                            {
                                UpdateUI("Hey there " + person.Name + "!", "You are feeling pretty " + emotion, Colors.Green, Colors.White);

                            }

                        }
                        else
                        {
                            AppendStatus(" | No emotion");
                        }

                        
                        
                    }
                    else
                    {
                        SetStatus("No faces");
                    }
                }
            }

            await PopulateMessageWithSensorData(message);
            message.Success = success ? "yes" : "no";
            return message;
        }

        #endregion Helper functions


        #region sensors

        DispatcherTimer timer;

        private async Task InitFez()
        {
            if (ApiInformation.IsTypePresent(typeof(Windows.Devices.Gpio.GpioController).ToString()))
            {
                if (_fez == null)
                    _fez = await FEZHAT.CreateAsync();
            }
        }

        private async Task PopulateMessageWithSensorData(IrisMessage message)
        {
            if (_fez == null) return;
                
            message.Temperature = _fez.GetTemperature();
            message.Brightness = _fez.GetLightLevel();
            //_fez.D2.Color = new FEZHAT.Color(255, 0, 0);
        }

        private void BlinkLights(FEZHAT.Color color)
        {
            var t = Root.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (_fez == null) return;

                timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromMilliseconds(100);
                double time = 0;
                bool red = false;
                timer.Tick += (s, a) =>
                {
                    time++;
                    if (time > 30)
                    {
                        timer.Stop();
                        _fez.D2.TurnOff();
                        return;
                    }

                    if (red)
                        _fez.D2.Color = color;
                    else
                        red = !red;

                };
                timer.Start();
            });
            
        }

        private void SetLightsDenied()
        {
            BlinkLights(new FEZHAT.Color(255, 0, 0));
        }

        private void SetLightsAproved()
        {

            BlinkLights(new FEZHAT.Color(0, 255, 0));
        }

        private void DisposeTimer()
        {
            if (timer != null)
            {
                timer.Stop();
                timer = null;
            }
        }

        #endregion


        #region Azure

        public async void SendMessage(IrisMessage msg)
        {

            myQueueClient = new Queue(queueName, azureConnectionString);

            // send
            //Message m = new Message(msg);
            var jsonMsg = JsonConvert.SerializeObject(msg);
            // await myQueueClient.SendAsync(jsonMsg);

            JsonObject jo = new JsonObject();

            JsonObject.TryParse(jsonMsg, out jo);
            // serialize
            await myQueueClient.SendAsync(jo);

        }

        #endregion
    }
}
