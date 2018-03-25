using RaspberryPi.Camera.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Graphics.Imaging;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;
using Windows.Storage;
using Windows.Storage.Streams;

namespace RaspberryPi.Camera.Capture
{
    public interface ICameraAccess : IDisposable
    {
        Task InitializeCameraAsync(DeviceInformation camera);
        Task<DeviceInformation> GetDefaultCamera();
        List<CaptureFormatInfo> GetSupportedCaptureFormats();
        MediaCapture CameraMediaCapture { get; set; }
        Task StartCaptureToFileAsync(IStorageFile file);
        void SetEncodingProflie(MediaEncodingProfile encodingProfile);
        Task StopCapture();
    }

    public class Frame
    {
        public byte[] Data { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public string Format { get; set; }
    }

    public class CameraAccess : ICameraAccess
    {
        public ILogService Log { get; set; }
        public Frame CapturedFrame { get; set; }
        private MediaEncodingProfile EncodingProfile;

        public MediaCapture CameraMediaCapture { get; set; }

        public MediaFrameReader CameraFrameReader { get; set; }

        public CameraAccess(ILogService log)
        {
            this.CameraMediaCapture = new MediaCapture();
            this.Log = log;

            this.CameraMediaCapture.Failed += CameraMediaCapture_Failed;
            
        }

        private void CameraMediaCapture_CaptureDeviceExclusiveControlStatusChanged(MediaCapture sender, MediaCaptureDeviceExclusiveControlStatusChangedEventArgs args)
        {
            this.Log.Debug(() => $"CameraMediaCapture exclusive controle state changed to {args.Status}");
        }

        private void CameraMediaCapture_CameraStreamStateChanged(MediaCapture sender, object args)
        {
            this.Log.Debug(() => $"CameraMediaCapture state changed to {sender.CameraStreamState.ToString()}");
        }

        private void CameraMediaCapture_Failed(MediaCapture sender, MediaCaptureFailedEventArgs errorEventArgs)
        {
            this.Log.Error(() => $"CameraMediaCapture Exception: {errorEventArgs.Message}", null, (c) => c.AddVariable("ErrorCode", errorEventArgs.Code.ToString()));
        }

        public async Task<DeviceInformation> GetDefaultCamera()
        {
            var cameras = await this.GetCameraDevices();

            return cameras.FirstOrDefault();
        }

        public async Task InitializeCameraAsync(DeviceInformation camera)
        {
            await this.CameraMediaCapture.InitializeAsync(new MediaCaptureInitializationSettings()
            {
                VideoDeviceId = camera.Id,
                // Use Cpu so we get Software capture and not D3D objects that I don't know how to use.
                MemoryPreference = MediaCaptureMemoryPreference.Auto,
                MediaCategory = MediaCategory.Other,
                AudioProcessing = Windows.Media.AudioProcessing.Default,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                StreamingCaptureMode = StreamingCaptureMode.AudioAndVideo
            }).AsTask().ConfigureAwait(false);

            this.CameraMediaCapture.CameraStreamStateChanged += CameraMediaCapture_CameraStreamStateChanged;
            this.CameraMediaCapture.CaptureDeviceExclusiveControlStatusChanged += CameraMediaCapture_CaptureDeviceExclusiveControlStatusChanged;

            // TODO: Create this from the actual frame source.
            //this.CameraFrameReader = await this.CameraMediaCapture.CreateFrameReaderAsync(this.CameraMediaCapture.FrameSources.Where(fs => fs.Value.SupportedFormats.Count > 1).First().Value);
        }

        private async Task<DeviceInformationCollection> GetCameraDevices()
        {
            return await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    this.CameraFrameReader?.StopAsync().AsTask().Wait();
                    this.CameraFrameReader?.Dispose();

                    this.CameraMediaCapture.CameraStreamStateChanged -= CameraMediaCapture_CameraStreamStateChanged;
                    this.CameraMediaCapture.CaptureDeviceExclusiveControlStatusChanged -= CameraMediaCapture_CaptureDeviceExclusiveControlStatusChanged;
                    this.CameraMediaCapture.Failed -= CameraMediaCapture_Failed;

                    // TODO: Flag if we were recording or not.
                    this.CameraMediaCapture.StopRecordAsync().AsTask().Wait();
                    this.CameraMediaCapture?.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Default() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

        // TODO: Some day, this needs to be _per capture device_ and _per frame source_.

        public List<CaptureFormatInfo> GetSupportedCaptureFormats()
        {
            this.Log.Trace(() => "GetSupportedCaptureFormats called.");
            this.Log.Debug(() => $"FrameSources found: {this.CameraMediaCapture.FrameSources.Count}");
            
            /*
            foreach (var fs in this.CameraMediaCapture.FrameSources)
            {
                this.Log.Debug(() => $"FrameSource {(fs.Value == null ? "is null" : "is not null")}");
                this.Log.Debug(() => $"FrameSource: {fs.Key}");
                this.Log.Debug(() => $"FrameSource: {fs.Value.SupportedFormats.Count}");

                foreach (var format in fs.Value.SupportedFormats)
                {
                    if (format.FrameRate.Denominator == 0)
                    {
                        this.Log.Debug(() => $"Bad framerate found, numerator={format.FrameRate.Numerator} denominator={format.FrameRate.Denominator}");
                        continue;
                    }

                    double framerate = (format.FrameRate.Numerator / format.FrameRate.Denominator);
                    this.Log.Debug(() => $"{fs.Key}: {format.VideoFormat.Width}x{format.VideoFormat.Height}@{Math.Round(framerate, 3)}fps {format.Subtype}");
                }
            }
            */

            return this.CameraMediaCapture.FrameSources.Where(fs => fs.Value.SupportedFormats.Count > 1).First().Value.SupportedFormats
                    .Select(format => new CaptureFormatInfo()
                    {
                        Width = format.VideoFormat.Width,
                        Height = format.VideoFormat.Height,
                        Framerate = (format.FrameRate.Numerator / format.FrameRate.Denominator),
                        Subtype = format.Subtype
                    }).ToList();
        }

        public async Task SetCaptureFormat(CaptureFormatInfo cfi)
        {
            var frameSource = this.CameraMediaCapture
                .FrameSources
                .Where(fs => fs.Value.SupportedFormats.Count > 1)
                .First()
                .Value;

            var format = frameSource
                .SupportedFormats
                .FirstOrDefault(f => (f.FrameRate.Numerator / f.FrameRate.Denominator) == cfi.Framerate && f.VideoFormat.Width == cfi.Width && f.VideoFormat.Height == cfi.Height && f.Subtype == cfi.Subtype);

            if (format != null)
            {
                await frameSource.SetFormatAsync(format).AsTask().ConfigureAwait(false);
            }
        }

        public async Task StartCaptureToFileAsync(IStorageFile file)
        {
            await this.CameraMediaCapture.StartRecordToStorageFileAsync(this.EncodingProfile, file).AsTask().ConfigureAwait(false);
        }

        public async Task StopCapture()
        {
            await this.CameraMediaCapture.StopRecordAsync().AsTask().ConfigureAwait(false);
        }

        public void SetEncodingProflie(MediaEncodingProfile encodingProfile)
        {
            this.EncodingProfile = encodingProfile;
        }

        public async Task CaptureFrame()
        {
            try
            {
                using (var ms = new InMemoryRandomAccessStream())
                {
                    SoftwareBitmap bitmap = null;
                    var propertySet = new Windows.Graphics.Imaging.BitmapPropertySet();
                    var qualityValue = new Windows.Graphics.Imaging.BitmapTypedValue(
                        0.5, // Quality percentage.
                        Windows.Foundation.PropertyType.Single
                    );
                    propertySet.Add("ImageQuality", qualityValue);

                    var frameReference = this.CameraFrameReader.TryAcquireLatestFrame();
                    var frame = frameReference?.VideoMediaFrame?.SoftwareBitmap;
                    this.CameraFrameReader.AcquisitionMode = MediaFrameReaderAcquisitionMode.Realtime;

                    if (frame == null)
                    {
                        var surface = frameReference?.VideoMediaFrame?.Direct3DSurface;
                        if (surface != null)
                        {
                            bitmap = await SoftwareBitmap.CreateCopyFromSurfaceAsync(surface, BitmapAlphaMode.Ignore).AsTask().ConfigureAwait(false);

                            // JPEG Encoder no likey YUY2.
                            if (bitmap.BitmapPixelFormat != BitmapPixelFormat.Bgra8)
                            {
                                bitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
                            }
                        }
                    }
                    else
                    {
                        bitmap = SoftwareBitmap.Convert(frame, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore);
                    }

                    if (bitmap == null)
                    {
                        return;
                    }

                    using (bitmap)
                    {
                        var jpegEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, ms, propertySet).AsTask().ConfigureAwait(false);
                        jpegEncoder.SetSoftwareBitmap(bitmap);

                        // No thumbnail for just "streaming".
                        //jpegEncoder.IsThumbnailGenerated = false;

                        try
                        {
                            await jpegEncoder.FlushAsync().AsTask().ConfigureAwait(false);
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

                        if (ms.Size > 0)
                        {
                            // TODO: make this more efficient.
                            byte[] data = (byte[])Array.CreateInstance(typeof(byte), (int)ms.Size);
                            ms.AsStreamForRead().Read(data, 0, (int)ms.Size);
                            this.CapturedFrame = new Frame()
                            {
                                Data = data,
                                Format = "jpeg",
                                Width = (uint)bitmap.PixelWidth,
                                Height = (uint)bitmap.PixelHeight
                            };
                        }
                    }
                }
            }
            catch (Exception e)
            {
                this.Log.Error(() => "Exception while capturing frame.", e);
            }
        }
    }

    public class CaptureFormatInfo
    {
        public uint Width { get; set; }
        public uint Height { get; set; }
        public double Framerate { get; set; }
        public string Subtype { get; set; }
    }
}
