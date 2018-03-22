using RaspberryPi.Camera.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RaspberryPi.Camera.Http
{
    public class HttpRoute
    {
        public string Route { get; internal set; }
        private Action<HttpContext> Handler;

        public HttpRoute(string route, Action<HttpContext> handler)
        {
            this.Handler = handler;
            this.Route = route;
        }

        public bool IsMatch(HttpRequest request)
        {
            return (request.Path == this.Route);
        }

        public async Task TryExecuteAsync(HttpContext context)
        {
            try
            {
                await Task.Factory.StartNew(() =>
                {
                    this.Handler.Invoke(context);
                }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                // TODO: Add generic handlers and responses.
                // TODO: Add ability to log in HttpContext.
            }
        }
    }

    public class HttpHeader
    {
        public string Name { get; set; }
        public string Value { get; set; }

        public HttpHeader(string name, string value)
        {
            this.Name = name;
            this.Value = value;
        }

        public string ToOutputString()
        {
            return $"{this.Name}: {this.Value}";
        }
    }

    public class HttpHeaderCollection : Dictionary<string, string>
    {
        public int? ContentLength
        {
            get
            {
                return this.GetIntHeader("Content-Length");
            }
        }

        public string ContentType
        {
            get
            {
                return this.GetStringHeader("Content-Type");
            }
        }

        public HttpHeaderCollection()
        {

        }

        public HttpHeaderCollection(IEnumerable<string> headers)
        {
            foreach (string header in headers)
            {
                string[] parts = header.Split(':');
                this.Add(parts[0], parts[1].TrimStart(' '));
            }
        }

        public string ToHeaderString()
        {
            StringBuilder result = new StringBuilder();

            foreach (var header in this)
            {
                result.AppendLine(header.Key + ": " + header.Value);
            }

            return result.ToString();
        }

        private int? GetIntHeader(string header)
        {
            if (this.ContainsKey(header))
            {
                string value = this[header];
                return Int32.Parse(value);
            }

            return null;
        }

        private string GetStringHeader(string header)
        {
            if (this.ContainsKey(header))
            {
                return this[header];
            }

            return null;
        }
    }

    public class HttpResponse
    {
        private StreamSocket Socket;
        private IOutputStream OutputStream;
        public HttpHeaderCollection Headers { get; internal set; }
        public int HttpCode { get; internal set; }
        public string Content { get; set; }
        public byte[] BinaryContent { get; set; }
        public string HttpVersion { get; set; }
        public string ReasonPhrase { get; set; }

        public HttpResponse(StreamSocket socket)
        {
            this.Socket = socket;
            this.OutputStream = socket.OutputStream;
            this.Headers = new HttpHeaderCollection();
            this.ReasonPhrase = "OK";
            this.HttpVersion = "HTTP/1.1";
        }

        public async Task FlushAsync()
        {
            var stream = this.OutputStream.AsStreamForWrite();

            string payload = $"{this.HttpVersion} {this.HttpCode} {this.ReasonPhrase}\r\n";

            payload += this.Headers.ToHeaderString();

            if (!String.IsNullOrWhiteSpace(this.Content) && this.BinaryContent == null)
            {
                payload += $"Content-Length: {this.Content.Length}\r\n\r\n";

                payload += this.Content;

                // Write the body out.
                await stream.WriteStringAsync(payload).ConfigureAwait(false);
            }
            else if (this.BinaryContent != null)
            {
                payload += $"Content-Length: {this.BinaryContent.Length}\r\n\r\n";

                await stream.WriteStringAsync(payload).ConfigureAwait(false);

                // Send the headers.
                await stream.FlushAsync().ConfigureAwait(false);

                // Write the content.
                await stream.WriteAsync(this.BinaryContent, 0, this.BinaryContent.Length).ConfigureAwait(false);
            }
            
            await stream.FlushAsync().ConfigureAwait(false);
        }

        // TODO: Just make the string encoded binary content dummy.
        public void SetPayload(string message)
        {
            this.Content = message;
        }

        public void SetPayload(byte[] bytes)
        {
            this.BinaryContent = bytes;
        }
    }

    public static class StreamExtensions
    {
        public static async Task WriteStringAsync(this Stream stream, string str)
        {
            var bytes = Encoding.UTF8.GetBytes(str);
            await stream.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
        }
    }

    public enum HttpRequestMethod
    {
        POST,
        PUT,
        GET,
        PATCH,
        DELETE,
        DEBUG
    }

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

    public class HttpContext
    {
        public HttpRequest Request { get; internal set; }
        public HttpResponse Response { get; internal set; }

        public HttpContext(HttpRequest request, HttpResponse response)
        {
            this.Request = request;
            this.Response = response;
        }
    }

    public class HttpServer
    {
        private uint BoundPort;
        private ILogService Log;
        private StreamSocketListener SocketListener;
        private List<HttpRoute> Routes;

        public HttpServer(uint boundPort, ILogService log)
        {
            this.Routes = new List<HttpRoute>();
            this.BoundPort = boundPort;
            this.Log = log;

            this.SocketListener = new StreamSocketListener();
            this.SocketListener.ConnectionReceived += SocketListener_ConnectionReceived;
            this.SocketListener.Control.KeepAlive = false;
            this.SocketListener.Control.NoDelay = false;
            this.SocketListener.Control.QualityOfService = SocketQualityOfService.LowLatency;
        }

        public void AddRoute(HttpRoute route)
        {
            // TODO: Make sure there aren't conflicting routes.
            this.Routes.Add(route);
        }

        public async Task Start()
        {
            //await this.SocketListener.BindEndpointAsync(new Windows.Networking.HostName("192.168.1.64"), "9092").AsTask().ConfigureAwait(false);
            await this.SocketListener.BindServiceNameAsync(this.BoundPort.ToString()).AsTask().ConfigureAwait(false);
        }

        private async void SocketListener_ConnectionReceived(StreamSocketListener sender, StreamSocketListenerConnectionReceivedEventArgs args)
        {
            HttpResponse response = null;
            HttpRequest request;

            try
            {
                var socket = args.Socket;

                request = await HttpRequest.FromStreamSocketAsync(socket).ConfigureAwait(false);
                response = new HttpResponse(socket);
                var context = new HttpContext(request, response);
                // TODO: Add pipelines, better support for files, etc.

                var foundRoute = this.Routes.FirstOrDefault(route => route.IsMatch(request));

                if (foundRoute != null)
                {
                    await foundRoute.TryExecuteAsync(context).ConfigureAwait(false);
                }
                else
                {
                    // 404 for now.
                    context.Response.HttpCode = 404;
                }
            }
            catch (Exception e)
            {
                this.Log.Error(() => "Exception in HttpServer.", e);
                // Write appopriate response.--
            }
            finally
            {
                await response.FlushAsync().ConfigureAwait(false);
                //await args.Socket.OutputStream.FlushAsync().AsTask().ConfigureAwait(false);
                args.Socket.InputStream.Dispose();
                args.Socket.OutputStream.Dispose();
                args.Socket.Dispose();
            }
        }
    }
}
