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
        public Frame CapturedFrame { get; set; }
        private MediaEncodingProfile EncodingProfile;

        public MediaCapture CameraMediaCapture { get; set; }

        public MediaFrameReader CameraFrameReader { get; set; }

        public CameraAccess()
        {
            this.CameraMediaCapture = new MediaCapture();
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
                MemoryPreference = MediaCaptureMemoryPreference.Cpu
            });

            // TODO: Create this from the actual frame source.
            this.CameraFrameReader = await this.CameraMediaCapture.CreateFrameReaderAsync(this.CameraMediaCapture.FrameSources.First().Value);
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

        public List<CaptureFormatInfo> GetSupportedCaptureFormats()
        {
            return this.CameraMediaCapture.FrameSources.First().Value.SupportedFormats
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
                .First()
                .Value;

            var format = frameSource
                .SupportedFormats
                .FirstOrDefault(f => (f.FrameRate.Numerator / f.FrameRate.Denominator) == cfi.Framerate && f.VideoFormat.Width == cfi.Width && f.VideoFormat.Height == cfi.Height && f.Subtype == cfi.Subtype);

            if (format != null)
            {
                await frameSource.SetFormatAsync(format);
            }
        }

        public async Task StartCaptureToFileAsync(IStorageFile file)
        {
            await this.CameraMediaCapture.StartRecordToStorageFileAsync(this.EncodingProfile, file);
        }

        public async Task StopCapture()
        {
            await this.CameraMediaCapture.StopRecordAsync();
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
                    var propertySet = new Windows.Graphics.Imaging.BitmapPropertySet();
                    var qualityValue = new Windows.Graphics.Imaging.BitmapTypedValue(
                        0.5, // Quality percentage.
                        Windows.Foundation.PropertyType.Single
                    );
                    propertySet.Add("ImageQuality", qualityValue);

                    var frameReference = this.CameraFrameReader.TryAcquireLatestFrame();
                    var frame = frameReference?.VideoMediaFrame?.SoftwareBitmap;
                    
                    if (frame == null)
                    {
                        return;
                    }

                    using (var bitmap = SoftwareBitmap.Convert(frame, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore))
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

                        //if (jpegEncoder.IsThumbnailGenerated == false)
                        //{
                        //    await jpegEncoder.FlushAsync().AsTask().ConfigureAwait(false);
                        //}

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
                // Log?
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
