using RaspberryPi.Camera.Http.Pipeline;
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

    public class HttpServer
    {
        private uint BoundPort;
        private ILogService Log;
        private StreamSocketListener SocketListener;
        private List<HttpRoute> Routes;
        private List<IPipeline> Pipelines = new List<IPipeline>();

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

                if (!response.IsHandled)
                {
                    foreach (var pipeline in this.Pipelines)
                    {
                        if (response.IsHandled)
                        {
                            break;
                        }

                        pipeline.Execute(context);
                    }
                }

                if (!response.IsHandled)
                {
                    // 404 for now.
                    context.Response.HttpCode = 404;
                }
            }
            catch (Exception e)
            {
                if (response != null && !response.HasSentResponse)
                {
                    response.HttpCode = 500;
                    await response.WritePayloadAsync("An error has occured.").ConfigureAwait(false);
                }

                this.Log.Error(() => "Exception in HttpServer.", e);
                // Write appopriate response.--
            }
            finally
            {
                await response.FlushAsync().ConfigureAwait(false);

                args.Socket.InputStream.Dispose();
                args.Socket.OutputStream.Dispose();
                args.Socket.Dispose();

                // TODO: Dispose context.
            }
        }
    }
}
