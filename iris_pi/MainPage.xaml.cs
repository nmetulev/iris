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

        private async void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                var deferral = e.SuspendingOperation.GetDeferral();

                await CleanupCameraAsync();

                await CleanupUiAsync();

                deferral.Complete();
            }
        }

        private async void Application_Resuming(object sender, object o)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                await SetupUiAsync();

                await InitializeCameraAsync();
            }
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            await SetupUiAsync();

            await InitializeCameraAsync();
        }

        protected override async void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            // Handling of this event is included for completenes, as it will only fire when navigating between pages and this sample only includes one page

            await CleanupCameraAsync();

            await CleanupUiAsync();
        }



        #region Event handlers

        
        private async void OrientationSensor_OrientationChanged(SimpleOrientationSensor sender, SimpleOrientationSensorOrientationChangedEventArgs args)
        {
            if (args.Orientation != SimpleOrientation.Faceup && args.Orientation != SimpleOrientation.Facedown)
            {
                _deviceOrientation = args.Orientation;
            }
        }
        
        private async void DisplayInformation_OrientationChanged(DisplayInformation sender, object args)
        {
            _displayOrientation = sender.CurrentOrientation;

            if (_isPreviewing)
            {
                await SetPreviewRotationAsync();
            }
        }

        private void FaceDetectionEffect_FaceDetected(FaceDetectionEffect sender, FaceDetectedEventArgs args)
        {
            if (args.ResultFrame.DetectedFaces.Count > 0)
            {
                if (!_faceDetected && !_analyzing)
                {
                    
                    var frame = args.ResultFrame;
                    var box = frame.DetectedFaces.First().FaceBox;

                    var previewProperties = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
                    var previewStream = previewProperties as VideoEncodingProperties;

                    if (((double)box.Height / (double)previewStream.Height) < 0.5) SetStatus("Come Closer");
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
            else
            {
                Debug.WriteLine("No faces detected");
                _faceDetected = false;
            }
        }

        #endregion Event handlers


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
        
        private async Task SetPreviewRotationAsync()
        {
            // Only need to update the orientation if the camera is mounted on the device
            if (_externalCamera) return;

            // Calculate which way and how far to rotate the preview
            int rotationDegrees = ConvertDisplayOrientationToDegrees(_displayOrientation);

            // The rotation direction needs to be inverted if the preview is being mirrored
            rotationDegrees = (360 - rotationDegrees) % 360;

            // Add rotation metadata to the preview stream to make sure the aspect ratio / dimensions match when rendering and getting preview frames
            var props = _mediaCapture.VideoDeviceController.GetMediaStreamProperties(MediaStreamType.VideoPreview);
            props.Properties.Add(RotationKey, rotationDegrees);
            await _mediaCapture.SetEncodingPropertiesAsync(MediaStreamType.VideoPreview, props, null);
        }
        
        private async Task StopPreviewAsync()
        {
            // Stop the preview
            _isPreviewing = false;
            await _mediaCapture.StopPreviewAsync();

            // Use the dispatcher because this method is sometimes called from non-UI threads
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                // Cleanup the UI
                PreviewControl.Source = null;

                // Allow the device screen to sleep now that the preview is stopped
                _displayRequest.RequestRelease();
            });
        }
        
        private async Task TakePhotoAndAnalyzeAsync()
        {
            _analyzing = true;
            var stream = new InMemoryRandomAccessStream();

            try
            {
                await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

                IrisMessage message = await ReencodeAndAnalyze(stream);
                if (message.Success == "no")
                {
                    stream = new InMemoryRandomAccessStream();
                    await _mediaCapture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);
                    message = await ReencodeAndAnalyze(stream);
                }

                SendMessage(message);
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
            Status.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Status.Text = text;
            });
        }

        private void AppendStatus(string text)
        {
            Status.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Status.Text += text;
            });
        }

        /// <summary>
        /// Attempts to lock the page orientation, hide the StatusBar (on Phone) and registers event handlers for hardware buttons and orientation sensors
        /// </summary>
        /// <returns></returns>
        private async Task SetupUiAsync()
        {
            // Attempt to lock page to landscape orientation to prevent the CaptureElement from rotating, as this gives a better experience
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.Landscape;

            // Populate orientation variables with the current state
            _displayOrientation = _displayInformation.CurrentOrientation;
            if (_orientationSensor != null)
            {
                _deviceOrientation = _orientationSensor.GetCurrentOrientation();
            }

            RegisterEventHandlers();
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors, allows the StatusBar (on Phone) to show, and removes the page orientation lock
        /// </summary>
        /// <returns></returns>
        private async Task CleanupUiAsync()
        {
            UnregisterEventHandlers();

            // Revert orientation preferences
            DisplayInformation.AutoRotationPreferences = DisplayOrientations.None;
        }
        
        /// <summary>
        /// Registers event handlers for hardware buttons and orientation sensors, and performs an initial update of the UI rotation
        /// </summary>
        private void RegisterEventHandlers()
        {
            // If there is an orientation sensor present on the device, register for notifications
            if (_orientationSensor != null)
            {
                _orientationSensor.OrientationChanged += OrientationSensor_OrientationChanged;
                
            }

            _displayInformation.OrientationChanged += DisplayInformation_OrientationChanged;
        }

        /// <summary>
        /// Unregisters event handlers for hardware buttons and orientation sensors
        /// </summary>
        private void UnregisterEventHandlers()
        {
            if (_orientationSensor != null)
            {
                _orientationSensor.OrientationChanged -= OrientationSensor_OrientationChanged;
            }

            _displayInformation.OrientationChanged -= DisplayInformation_OrientationChanged;
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
            
            SetStatus("Analyzing...");

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

                        if (identifyResult.Count() == 0 || identifyResult.First().Candidates.Count() == 0)
                        {
                            SetStatus("You are nobody");
                        }
                        else
                        {
                            var candidate = identifyResult.First().Candidates.First();
                            var person = await _faceClient.GetPersonAsync(_groupName, candidate.PersonId);
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


        #region Rotation helpers

        /// <summary>
        /// Calculates the current camera orientation from the device orientation by taking into account whether the camera is external or facing the user
        /// </summary>
        /// <returns>The camera orientation in space, with an inverted rotation in the case the camera is mounted on the device and is facing the user</returns>
        private SimpleOrientation GetCameraOrientation()
        {
            if (_externalCamera)
            {
                // Cameras that are not attached to the device do not rotate along with it, so apply no rotation
                return SimpleOrientation.NotRotated;
            }

            var result = _deviceOrientation;

            // Account for the fact that, on portrait-first devices, the camera sensor is mounted at a 90 degree offset to the native orientation
            if (_displayInformation.NativeOrientation == DisplayOrientations.Portrait)
            {
                switch (result)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        result = SimpleOrientation.NotRotated;
                        break;
                    case SimpleOrientation.Rotated180DegreesCounterclockwise:
                        result = SimpleOrientation.Rotated90DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        result = SimpleOrientation.Rotated180DegreesCounterclockwise;
                        break;
                    case SimpleOrientation.NotRotated:
                        result = SimpleOrientation.Rotated270DegreesCounterclockwise;
                        break;
                }
            }

            // If the preview is being mirrored for a front-facing camera, then the rotation should be inverted
            if (_mirroringPreview)
            {
                // This only affects the 90 and 270 degree cases, because rotating 0 and 180 degrees is the same clockwise and counter-clockwise
                switch (result)
                {
                    case SimpleOrientation.Rotated90DegreesCounterclockwise:
                        return SimpleOrientation.Rotated270DegreesCounterclockwise;
                    case SimpleOrientation.Rotated270DegreesCounterclockwise:
                        return SimpleOrientation.Rotated90DegreesCounterclockwise;
                }
            }

            return result;
        }

        /// <summary>
        /// Converts the given orientation of the device in space to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the device in space</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDeviceOrientationToDegrees(SimpleOrientation orientation)
        {
            switch (orientation)
            {
                case SimpleOrientation.Rotated90DegreesCounterclockwise:
                    return 90;
                case SimpleOrientation.Rotated180DegreesCounterclockwise:
                    return 180;
                case SimpleOrientation.Rotated270DegreesCounterclockwise:
                    return 270;
                case SimpleOrientation.NotRotated:
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Converts the given orientation of the app on the screen to the corresponding rotation in degrees
        /// </summary>
        /// <param name="orientation">The orientation of the app on the screen</param>
        /// <returns>An orientation in degrees</returns>
        private static int ConvertDisplayOrientationToDegrees(DisplayOrientations orientation)
        {
            switch (orientation)
            {
                case DisplayOrientations.Portrait:
                    return 90;
                case DisplayOrientations.LandscapeFlipped:
                    return 180;
                case DisplayOrientations.PortraitFlipped:
                    return 270;
                case DisplayOrientations.Landscape:
                default:
                    return 0;
            }
        }



        #endregion Rotation helpers

        #region sensors

        private async Task PopulateMessageWithSensorData(IrisMessage message)
        {
            if (ApiInformation.IsApiContractPresent("Windows.Devices.DevicesLowLevelContract", 1))
            {
                if (_fez == null)
                    _fez = await FEZHAT.CreateAsync();
                
                message.Temperature = _fez.GetTemperature();
                message.Brightness = _fez.GetLightLevel();
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
