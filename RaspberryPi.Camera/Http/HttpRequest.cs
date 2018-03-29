using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;

namespace RaspberryPi.Camera.Http
{
    public class HttpRequest
    {
        public HttpRequestMethod Method { get; set; }
        public string HttpVersion { get; set; }
        public string Path { get; set; }
        public List<string> RawHeaders { get; set; }
        public string QueryString { get; set; }

        private HttpRequest(string request)
        {
            // TODO: Actually parse the headers.
            this.Path = "/";
        }

        private HttpRequest(List<string> headers)
        {
            string rawFirstLine = headers.First();

            string[] actionParts = rawFirstLine.Split(' ');

            this.Method = (HttpRequestMethod)Enum.Parse(typeof(HttpRequestMethod), actionParts[0]);
            this.Path = actionParts[1];
            this.HttpVersion = actionParts[2];

            if (this.Path.Contains('?'))
            {
                string[] pathParts = this.Path.Split('?');
                this.Path = pathParts[0];
                this.QueryString = pathParts[1];
            }

            this.RawHeaders = headers.Skip(1).ToList();
        }

        public static async Task<HttpRequest> FromStreamSocketAsync(StreamSocket socket)
        {
            var inputStream = socket.InputStream;

            // TODO: Make size this configurable.
            using (var test = new StreamReader(socket.InputStream.AsStreamForRead(2048)))
            {
                var t = await ReadRequestHeaders(test).ConfigureAwait(false);
                return new HttpRequest(t);
            }
        }

        private static async Task<List<string>> ReadRequestHeaders(StreamReader reader)
        {
            var request = new List<string>();

            string line = null;
            while ((line = await reader.ReadLineAsync()) != String.Empty)
            {
                request.Add(line);
            }

            return request;
        }
    }
}
