using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Foundation.Collections;
using Windows.Media;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;

namespace RaspberryPi.Camera.Capture
{
    public class CaptureEngine
    {
        public async Task GetSources()
        {
            var test = new MediaCapture();

            var allVideoDevices = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture);
            var device = allVideoDevices.FirstOrDefault();

            try
            {
                test.InitializeAsync(new MediaCaptureInitializationSettings()
                {
                    VideoDeviceId = device.Id
                }).AsTask().Wait();
            }
            catch (Exception e)
            {
                var t = e;
            }
        }
    }
}
