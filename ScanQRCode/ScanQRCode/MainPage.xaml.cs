﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.System.Display;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using ZXing;
// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace ScanQRCode
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        const int borderThickness = 5;

        private MediaCapture mediaCapture;
        private bool isInitialized = false;
        private bool isPreviewing = false;

        // Prevent the screen from sleeping while the camera is running.
        private readonly DisplayRequest displayRequest = new DisplayRequest();

        private LowLagPhotoCapture lowLagPhotoCapture;

        // UI state
        private bool _isSuspending;
        private bool _isActivePage;
        private bool _isUIActive;
        private Task _setupTask = Task.CompletedTask;


        public MainPage()
        {
            this.InitializeComponent();

            // Useful to know when to initialize/clean up the camera
            Application.Current.Suspending += Application_Suspending;
            Application.Current.Resuming += Application_Resuming;

            Window.Current.VisibilityChanged += Current_VisibilityChanged;
        }

        /// <summary>
        /// Occures on app suspending. Stops camera if initialized.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Application_Suspending(object sender, SuspendingEventArgs e)
        {
            // Handle global application events only if this page is active.
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                _isSuspending = true;
                var deferral = e.SuspendingOperation.GetDeferral();

                var task = Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                {
                    await SwitchCameraOnUIStateChanged();
                    deferral.Complete();
                });

            }
        }

        /// <summary>
        /// Occures on app resuming. Initializes camera if available.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="o"></param>
        private async void Application_Resuming(object sender, object o)
        {
            // Handle global application events only if this page is active
            if (Frame.CurrentSourcePageType == typeof(MainPage))
            {
                _isSuspending = false;
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.High, async () =>
                {
                    await SwitchCameraOnUIStateChanged();
                });
            }
        }

        private async void Current_VisibilityChanged(object sender, VisibilityChangedEventArgs e)
        {
            await SwitchCameraOnUIStateChanged();
        }


        double focusRecLength = 0d;

        private void InitFocusRec()
        {
            leftTopBorder.BorderThickness = new Thickness(borderThickness, borderThickness, 0, 0);
            rightTopBorder.BorderThickness = new Thickness(0, borderThickness, borderThickness, 0);
            leftBottomBorder.BorderThickness = new Thickness(borderThickness, 0, 0, borderThickness);
            rightBottomBorder.BorderThickness = new Thickness(0, 0, borderThickness, borderThickness);

            var borderLength = 20;
            leftTopBorder.Width = leftTopBorder.Height = borderLength;
            rightTopBorder.Width = rightTopBorder.Height = borderLength;
            leftBottomBorder.Width = leftBottomBorder.Height = borderLength;
            rightBottomBorder.Width = rightBottomBorder.Height = borderLength;

            focusRecLength = Math.Min(ActualWidth / 3, ActualHeight / 3);
            scanGrid.Width = scanGrid.Height = focusRecLength;
            scanCavas.Width = scanCavas.Height = focusRecLength;

            scanStoryboard.Stop();
            scanLine.X2 = scanCavas.Width - 20;
            scanAnimation.To = scanCavas.Height - 20;

            scanStoryboard.Begin();
        }

        private void Page_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            InitFocusRec();
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            _isActivePage = true;
            await SwitchCameraOnUIStateChanged();
        }

        protected override async void OnNavigatedFrom(NavigationEventArgs e)
        {
            _isActivePage = false;
            await SwitchCameraOnUIStateChanged();
        }

        private async Task StartCameraAsync()
        {
            if (!isInitialized)
            {
                await InitializeCameraAsync();
            }

            if (isInitialized)
            {
                PreviewControl.Visibility = Visibility.Visible;
            }

            if (isPreviewing)
            {
                Debug.WriteLine("Camera has been started");
                await StartScanQRCode();
            }
        }

        /// <summary>
        /// Initialize or clean up the camera and our UI,
        /// depending on the page state.
        /// </summary>
        /// <returns></returns>
        private async Task SwitchCameraOnUIStateChanged()
        {
            // Avoid reentrancy: Wait until nobody else is in this function.
            while (!_setupTask.IsCompleted)
            {
                await _setupTask;
            }

            if (_isUIActive != IsCurrentUIActive)
            {
                _isUIActive = IsCurrentUIActive;

                Func<Task> setupAsync = async () =>
                {
                    if (IsCurrentUIActive)
                    {
                        await StartCameraAsync();
                    }
                    else
                    {
                        await CleanupCameraAsync();
                    }
                };
                _setupTask = setupAsync();
            }

            await _setupTask;
        }

        /// <summary>
        /// Gets current ui active state.
        /// </summary>s
        private bool IsCurrentUIActive
        {
            // UI is active if
            // * We are the current active page.
            // * The window is visible.
            // * The app is not suspending.
            get { return _isActivePage && !_isSuspending && Window.Current.Visible; }
        }

        /// <summary>
        /// Queries the available video capture devices to try and find one mounted on the desired panel.
        /// </summary>
        /// <param name="desiredPanel">The panel on the device that the desired camera is mounted on.</param>
        /// <returns>A DeviceInformation instance with a reference to the camera mounted on the desired panel if available,
        ///          any other camera if not, or null if no camera is available.</returns>
        private static async Task<DeviceInformation> FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel desiredPanel)
        {
            // Get available devices for capturing pictures.
            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);

            // Get the desired camera by panel.
            DeviceInformation desiredDevice = allVideoDevices.FirstOrDefault(x => x.EnclosureLocation != null && x.EnclosureLocation.Panel == desiredPanel);

            // If there is no device mounted on the desired panel, return the first device found.
            return desiredDevice ?? allVideoDevices.FirstOrDefault();
        }

        /// Initializes the MediaCapture
        /// </summary>
        private async Task InitializeCameraAsync()
        {
            if (mediaCapture == null)
            {
                // Attempt to get the back camera if one is available, but use any camera device if not.
                var cameraDevice = await FindCameraDeviceByPanelAsync(Windows.Devices.Enumeration.Panel.Back);

                if (cameraDevice == null)
                {
                    //No camera device!
                    return;
                }

                // Create MediaCapture and its settings.
                mediaCapture = new MediaCapture();

                // Register for a notification when something goes wrong
                mediaCapture.Failed += MediaCapture_Failed;

                var settings = new MediaCaptureInitializationSettings { VideoDeviceId = cameraDevice.Id };

                // Initialize MediaCapture
                try
                {
                    await mediaCapture.InitializeAsync(settings);
                    var imageEnCodingProperties = ImageEncodingProperties.CreatePng();
                    var resolution = await SetResolutionAsync(MediaStreamType.Photo);
                    if (resolution != null)
                    {
                        imageEnCodingProperties.Width = resolution[0];
                        imageEnCodingProperties.Height = resolution[1];
                    }
                    lowLagPhotoCapture = await mediaCapture.PrepareLowLagPhotoCaptureAsync(imageEnCodingProperties);
                    isInitialized = true;
                }
                catch (UnauthorizedAccessException)
                {
                    await ShowMessage("Denied access to the camera.");
                }
                catch (Exception ex)
                {
                    await ShowMessage("Exception when init MediaCapture. " + ex.Message);
                }

                // If initialization succeeded, start the preview.
                if (isInitialized)
                {
                    await StartPreviewAsync();
                }
            }
        }

        uint desiredWidth = 1920;
        uint desiredHeight = 1080;

        /// <summary>
        /// Set desired resolution to video device controller with specified stream type.
        /// </summary>
        /// <param name="streamType"></param>
        /// <returns></returns>
        private async Task<uint[]> SetResolutionAsync(MediaStreamType streamType)
        {
            //Get the supported encoding properties. 
            var mediaStreamProperties = mediaCapture.VideoDeviceController.GetAvailableMediaStreamProperties(streamType);
            if (mediaStreamProperties == null || mediaStreamProperties.Count == 0)
                return null;

            var imageEncodingProperty = mediaStreamProperties.Select(e => e as ImageEncodingProperties)
                                                             .Where(e => e != null && e.Width <= desiredWidth
                                                                        && e.Height < desiredHeight && IsMatchingRatio(e))
                                                             .OrderByDescending(e => e.Width * e.Height)
                                                             .FirstOrDefault();
            if (imageEncodingProperty != null)
            {
                await mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(streamType, imageEncodingProperty);
                return new uint[] { imageEncodingProperty.Width, imageEncodingProperty.Height };
            }

            var videoEncodingProperty = mediaStreamProperties.Select(e => e as VideoEncodingProperties)
                                                           .Where(e => e != null && e.Width <= desiredWidth
                                                                      && e.Height < desiredHeight && IsMatchingRatio(e))
                                                           .OrderByDescending(e => e.Width * e.Height)
                                                           .FirstOrDefault();
            if (videoEncodingProperty != null)
            {
                await mediaCapture.VideoDeviceController.SetMediaStreamPropertiesAsync(streamType, videoEncodingProperty);
                return new uint[] { videoEncodingProperty.Width, videoEncodingProperty.Height };
            }

            return null;
        }

        private bool IsMatchingRatio(ImageEncodingProperties e)
        {
            double tolerance = 0.015;
            return Math.Abs(GetAspectRatio(e.Height, e.Width) - GetAspectRatio(desiredHeight, desiredWidth)) < tolerance;
        }

        private bool IsMatchingRatio(VideoEncodingProperties e)
        {
            double tolerance = 0.015;
            return Math.Abs(GetAspectRatio(e.Height, e.Width) - GetAspectRatio(desiredHeight, desiredWidth)) < tolerance;
        }

        private double GetAspectRatio(uint heiht, uint width)
        {
            return Math.Round((heiht != 0) ? (width / (double)heiht) : double.NaN, 2);
        }

        /// <summary>
        /// Starts the preview after making a request to keep the screen on and unlocks the UI.
        /// </summary>
        private async Task StartPreviewAsync()
        {
            // Prevent the device from sleeping while the preview is running.
            displayRequest.RequestActive();

            // Set the preview source in the UI.
            PreviewControl.Source = mediaCapture;
            // Start the preview.
            try
            {
                await mediaCapture.StartPreviewAsync();
                isPreviewing = true;
            }
            catch (Exception ex)
            {
                await ShowMessage("Exception starting preview." + ex.Message);
            }
        }

        private async Task StartScanQRCode()
        {
            try
            {
                Result _result = null;
                while (_result == null && lowLagPhotoCapture != null && IsCurrentUIActive)
                {
                    var capturedPhoto = await lowLagPhotoCapture.CaptureAsync();
                    if (capturedPhoto == null)
                    {
                        continue;
                    }
                    using (var stream = capturedPhoto.Frame.CloneStream())
                    {
                        //calculate the crop square's length.
                        var pixelWidth = capturedPhoto.Frame.Width;
                        var pixelHeight = capturedPhoto.Frame.Height;
                        var rate = Math.Min(pixelWidth, pixelHeight) / Math.Min(ActualWidth, ActualHeight);
                        var cropLength = focusRecLength * rate;

                        // initialize with 1,1 to get the current size of the image
                        var writeableBmp = new WriteableBitmap(1, 1);
                        var rect = new Rect(pixelWidth / 2 - cropLength / 2,
                            pixelHeight / 2 - cropLength / 2, cropLength, cropLength);
                        using (var croppedStream = await ImageHelper.GetCroppedStreamAsync(stream, rect))
                        {
                            writeableBmp.SetSource(croppedStream);
                            // and create it again because otherwise the WB isn't fully initialized and decoding
                            // results in a IndexOutOfRange
                            writeableBmp = new WriteableBitmap((int)cropLength, (int)cropLength);
                            croppedStream.Seek(0);
                            await writeableBmp.SetSourceAsync(croppedStream);
                        }

                        _result = ScanBitmap(writeableBmp);
                    }
                }

                if (_result != null)
                {
                    Frame.Navigate(typeof(ResultPage), _result.Text);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Exception when scaning QR code" + ex.Message);
            }
        }

        private Result ScanBitmap(WriteableBitmap writeableBmp)
        {
            var barcodeReader = new BarcodeReader
            {
                AutoRotate = true,
                Options = { TryHarder = true }
            };

            return barcodeReader.Decode(writeableBmp);
        }

        /// <summary>
        /// Handles MediaCapture failures. Cleans up the camera resources.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="errorEventArgs"></param>
        private async void MediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            await CleanupCameraAsync();
        }


        /// <summary>
        /// Cleans up the camera resources (after stopping the preview if necessary) and unregisters from MediaCapture events.
        /// </summary>
        private async Task CleanupCameraAsync()
        {
            if (isInitialized)
            {
                if (lowLagPhotoCapture != null)
                {
                    await lowLagPhotoCapture.FinishAsync();
                    lowLagPhotoCapture = null;
                }

                if (isPreviewing)
                {
                    // The call to stop the preview is included here for completeness, but can be
                    // safely removed if a call to MediaCapture.Dispose() is being made later,
                    // as the preview will be automatically stopped at that point
                    await StopPreviewAsync();
                }

                isInitialized = false;
            }

            if (mediaCapture != null)
            {
                mediaCapture.Failed -= MediaCapture_Failed;
                mediaCapture.Dispose();
                mediaCapture = null;
            }

            Debug.WriteLine("Camera has been cleanup");
        }

        /// <summary>
        /// Stops the preview and deactivates a display request, to allow the screen to go into power saving modes, and locks the UI
        /// </summary>
        /// <returns></returns>
        private async Task StopPreviewAsync()
        {
            try
            {
                isPreviewing = false;
                await mediaCapture.StopPreviewAsync();
            }
            catch (Exception ex)
            {
                // Use the dispatcher because this method is sometimes called from non-UI threads.
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await ShowMessage("Exception stopping preview. " + ex.Message);
                });
            }

            // Use the dispatcher because this method is sometimes called from non-UI threads.
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                PreviewControl.Source = null;

                // Allow the device to sleep now that the preview is stopped.
                displayRequest.RequestRelease();
            });
        }

        private async Task ShowMessage(string message)
        {
            var messageDialog = new MessageDialog(message);
            await messageDialog.ShowAsync();
        }
    }
}