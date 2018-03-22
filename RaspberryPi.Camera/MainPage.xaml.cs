using RaspberryPi.Camera.Capture;
using RaspberryPi.Camera.Configuration;
using RaspberryPi.Camera.Http;
using RaspberryPi.Camera.Logging;
using RaspberryPi.Camera.Serialization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RaspberryPi.Camera
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        private CameraAccess CameraAccess;
        private Options ConfigOptions;
        private Logging.LogService Log = new Logging.LogService();
        public HttpServer WebServer { get; private set; }

        public MainPage()
        {
            this.InitializeComponent();

            var optionsTask = OptionsHelper.LoadAsync();
            optionsTask.Wait(2000);
            this.ConfigOptions = optionsTask.Result;

            var storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            this.Log.Loggers.Add(new FileLogger(storageFolder.Path, "logs.txt"));

            Application.Current.Resuming += Application_Resuming;
            Application.Current.Suspending += Application_Suspending;
            Application.Current.UnhandledException += Current_UnhandledException;

            this.CameraAccess = new CameraAccess(this.Log);

            this.Log.Info(() => "App Started", (c) => c.AddVariable("LoggersAdded", this.Log.Loggers.Count().ToString()).AddVariable("TestKey", "TestValue"));
        }

        private void Current_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            this.Log.Error(() => e.Message, e.Exception);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            this.Log.Trace(() => "OnNavigatedTo Fired");

            await this.InitializeCameraAccess().ConfigureAwait(false);

            var encodingProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);

            this.Log.Trace(() => "Created encoding profile for 720p");

            encodingProfile.Video.Bitrate = this.ConfigOptions.VideoCapture.Bitrate;
            encodingProfile.Video.FrameRate.Numerator = this.ConfigOptions.VideoCapture.FramesPerSecond;

            this.Log.Trace(() => $"Set Video Bitrate and FPS to {this.ConfigOptions.VideoCapture.Bitrate} at {this.ConfigOptions.VideoCapture.FramesPerSecond} fps.");

            this.CameraAccess.CameraMediaCapture.VideoDeviceController.PrimaryUse = Windows.Media.Devices.CaptureUse.Video;
            var formats = this.CameraAccess.GetSupportedCaptureFormats();
            var myFormat = formats.FirstOrDefault(f =>
                f.Height == this.ConfigOptions.VideoCapture.Height &&
                f.Width == this.ConfigOptions.VideoCapture.Width &&
                f.Framerate == this.ConfigOptions.VideoCapture.FramesPerSecond
            );

            this.Log.Trace(() => $"Found {formats.Count()} formats.");
            this.Log.Debug(() => myFormat == null ? "Could not find specified capture properties in formats." : "Found capture format");

            await this.CameraAccess.SetCaptureFormat(myFormat).ConfigureAwait(false);
            this.CameraAccess.SetEncodingProflie(encodingProfile);

            this.Log.Debug(() => "Starting capture loop...");
            // NOTE: Don't await these.
            this.VideoCaptureLoop().ContinueWith((t) =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    this.Log.Debug(() => "Video Loop started.");
                }
                
                if (t.IsFaulted)
                {
                    this.Log.Error(() => "Exception while starting Video Loop", t.Exception);
                }
            });

            this.Log.Debug(() => "Starting HttpServer...");
            this.StartHttpServer().ContinueWith((t) =>
            {
                if (t.IsCompletedSuccessfully)
                {
                    this.Log.Debug(() => "Http Server started.");
                }

                if (t.IsFaulted)
                {
                    this.Log.Error(() => "Exception while starting Http Server.", t.Exception);
                }
            });

            this.Log.Debug(() => "Starting Video Preview Service...");
            this.CameraAccess.CameraFrameReader.AcquisitionMode = Windows.Media.Capture.Frames.MediaFrameReaderAcquisitionMode.Realtime;
            await this.CameraAccess.CameraFrameReader.StartAsync().AsTask().ConfigureAwait(false);
        }

        private async Task VideoCaptureLoop()
        {
            // TODO: Allow for camera names.
            string fileName = $"{DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss")}_camera1.mp4";
            var storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file = await storageFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);

            await this.CameraAccess.StartCaptureToFileAsync(file).ConfigureAwait(false);

            // TODO: Make this configurable.
            Task.Delay(30 * 1000)
                .ContinueWith(async (t) =>
                {
                    await this.CameraAccess.StopCapture().ConfigureAwait(false);

                    // NOTE: Don't await this, we're starting another itteration of capture.
                    this.VideoCaptureLoop();
                }).ConfigureAwait(false);
        }

        private async void Application_Resuming(object sender, object e)
        {
            this.Log.Trace(() => "Application_Resuming fired.");
            await this.InitializeCameraAccess().ConfigureAwait(false);
        }

        private void Application_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            this.Log.Trace(() => "Application_Suspending fired.");
            this.CameraAccess.Dispose();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.Log.Trace(() => "OnNavigatedFrom fired.");
            this.CameraAccess.Dispose();
        }

        private async Task InitializeCameraAccess()
        {
            var cam = await this.GetCamera().ConfigureAwait(false);
            await this.CameraAccess.InitializeCameraAsync(cam).ConfigureAwait(false);
        }

        private async Task<DeviceInformation> GetCamera()
        {
            var defaultCam = await this.CameraAccess.GetDefaultCamera().ConfigureAwait(false);

            return defaultCam;
        }

        private async Task StartHttpServer()
        {
            this.WebServer = new HttpServer(9092, this.Log);
            this.WebServer.AddRoute(new HttpRoute("/", (context) =>
            {
                context.Response.HttpCode = 200;
                context.Response.Headers.Add("Content-Type", "text/html; charset=utf-8");
                context.Response.Headers.Add("Connection", "Close");
                context.Response.SetPayload("<html><head><title>Test Page</title><script type='text/javascript'>var i = 0; setInterval(()=>{document.getElementById('preview').attributes['src'].value='/preview.jpg?a=' + i; i++;}, 250);</script></head><body><img id='preview' src='/preview.jpg' style='width:100%' /></body></html>");
            }));

            this.WebServer.AddRoute(new HttpRoute("/preview.jpg", (context) =>
            {
                context.Response.HttpCode = 200;
                context.Response.Headers.Add("Content-Type", "image/jpeg");
                context.Response.Headers.Add("Accept-Ranges", "bytes");
                context.Response.Headers.Add("Connection", "Close");

                this.CameraAccess.CaptureFrame().Wait(1000);

                if (this.CameraAccess.CapturedFrame != null)
                {
                    context.Response.SetPayload(this.CameraAccess.CapturedFrame.Data);
                }
                else
                {
                    context.Response.HttpCode = 404;
                }
            }));

            await this.WebServer.Start().ConfigureAwait(false);
        }
    }
}
