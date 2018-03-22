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
        private CameraAccess CameraAccess = new CameraAccess();
        private Options ConfigOptions;
        private Logging.LoggingContext Log = new Logging.LoggingContext();

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

            this.Log.Info(() => "App Started", (c) => c.AddVariable("LoggersAdded", this.Log.Loggers.Count().ToString()).AddVariable("TestKey", "TestValue"));
        }

        private void Current_UnhandledException(object sender, Windows.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            this.Log.Error(() => e.Message, e.Exception);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            this.Log.Trace(() => "OnNavigatedTo Fired");

            await this.InitializeCameraAccess();

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
            this.Log.Trace(() => myFormat == null ? "Could not find specified capture properties in formats." : "Found capture format");

            await this.CameraAccess.SetCaptureFormat(myFormat);
            this.CameraAccess.SetEncodingProflie(encodingProfile);

            // TODO: Set better logging steps.

            this.Log.Trace("Starting capture loop...");
            // NOTE: Don't await these.
            this.VideoCaptureLoop();

            this.Log.Trace("Starting HttpServer...");
            this.StartHttpServer();

            this.Log.Trace("Starting Video Preview Service...");
            this.CameraAccess.CameraFrameReader.AcquisitionMode = Windows.Media.Capture.Frames.MediaFrameReaderAcquisitionMode.Realtime;
            await this.CameraAccess.CameraFrameReader.StartAsync();
        }

        private async Task VideoCaptureLoop()
        {
            // TODO: Allow for camera names.
            string fileName = $"{DateTime.Now.ToString("yyyy-MM-dd_hh-mm-ss")}_camera1.mp4";
            var storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file = await storageFolder.CreateFileAsync(fileName, Windows.Storage.CreationCollisionOption.ReplaceExisting);

            await this.CameraAccess.StartCaptureToFileAsync(file);

            // TODO: Make this configurable.
            Task.Delay(30 * 1000)
                .ContinueWith(async (t) =>
                {
                    await this.CameraAccess.StopCapture();

                    // NOTE: Don't await this, we're starting another itteration of capture.
                    this.VideoCaptureLoop();
                }).ConfigureAwait(false);
        }

        private int CaptureLoopCount;

        public HttpServer WebServer { get; private set; }

        private void StartCaptureLoop()
        {
            Task.Delay(2000)
                .ContinueWith((t) =>
                {
                    CaptureLoopCount++;
                    this.CapturePreview().Wait();
                    this.StartCaptureLoop();
                })
                .ConfigureAwait(false);
        }

        private async Task CapturePreview()
        {
            var storageFolder = Windows.Storage.ApplicationData.Current.LocalFolder;
            var file2 = await storageFolder.CreateFileAsync($"test{this.CaptureLoopCount}.jpg", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            try
            {
                var jpegSettings = ImageEncodingProperties.CreateJpeg();
                using (var ras = await file2.OpenAsync(Windows.Storage.FileAccessMode.ReadWrite))
                {
                    var propertySet = new Windows.Graphics.Imaging.BitmapPropertySet();
                    var qualityValue = new Windows.Graphics.Imaging.BitmapTypedValue(
                        0.6, // Quality percentage.
                        Windows.Foundation.PropertyType.Single
                    );
                    propertySet.Add("ImageQuality", qualityValue);

                    var frameReference = this.CameraAccess.CameraFrameReader.TryAcquireLatestFrame();
                    var frame = frameReference?.VideoMediaFrame?.SoftwareBitmap;

                    if (frame == null)
                    {
                        return;
                    }

                    using (var bitmap = SoftwareBitmap.Convert(frame, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore))
                    {
                        var jpegEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, ras, propertySet);

                        jpegEncoder.SetSoftwareBitmap(bitmap);

                        jpegEncoder.IsThumbnailGenerated = true;

                        try
                        {
                            await jpegEncoder.FlushAsync();
                        }
                        catch (Exception err)
                        {
                            switch (err.HResult)
                            {
                                case unchecked((int)0x88982F81): //WINCODEC_ERR_UNSUPPORTEDOPERATION
                                                                 // If the encoder does not support writing a thumbnail, then try again
                                                                 // but disable thumbnail generation.
                                    jpegEncoder.IsThumbnailGenerated = false;
                                    break;
                                default:
                                    throw err;
                            }
                        }

                        if (jpegEncoder.IsThumbnailGenerated == false)
                        {
                            await jpegEncoder.FlushAsync();
                        }
                    }
                }
            }
            catch (Exception e)
            {
                await file2.DeleteAsync();
            }
        }

        private async void Application_Resuming(object sender, object e)
        {
            await this.InitializeCameraAccess();
        }

        private void Application_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            this.CameraAccess.Dispose();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            this.CameraAccess.Dispose();
        }

        private async Task InitializeCameraAccess()
        {
            var cam = await this.GetCamera();
            await this.CameraAccess.InitializeCameraAsync(cam);
        }

        private async Task<DeviceInformation> GetCamera()
        {
            var defaultCam = await this.CameraAccess.GetDefaultCamera();

            return defaultCam;
        }

        private async Task StartHttpServer()
        {
            this.WebServer = new HttpServer(9092);
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

    public class Response
    {
        public string Body { get; set; }
    }
}
