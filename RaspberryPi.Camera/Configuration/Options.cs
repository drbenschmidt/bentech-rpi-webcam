using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspberryPi.Camera.Configuration
{
    public class Options
    {
        public int VideoCaptureDurationInSeconds { get; set; } = 60;
        public string VideoCaptureSavePath { get; set; } = "~/VideoCapture";
        public VideoCaptureOptions VideoCapture { get; set; } = new VideoCaptureOptions();
        public HttpServerOptions HttpServer { get; set; } = new HttpServerOptions();
    }

    public class VideoCaptureOptions
    {
        public bool EnableVideoCapture { get; set; } = false;
        public uint FramesPerSecond { get; set; } = 5;
        public uint Width { get; set; } = 1280;
        public uint Height { get; set; } = 720;
        public uint Bitrate { get; set; } = (uint)(0.7 * 1000 * 1000);
    }

    public class HttpServerOptions
    {
        public bool EnableHttpServer { get; set; } = true;
        public int PreviewFramesPerSecond { get; set; } = 4;
        public int JpegQuality { get; set; } = 60;
    }
}
