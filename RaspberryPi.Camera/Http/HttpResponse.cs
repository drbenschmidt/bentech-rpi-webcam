using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace RaspberryPi.Camera.Http
{
    public class HttpResponse
    {
        private StreamSocket Socket;
        private IOutputStream OutputStream;
        private Stream PayloadStream;

        public HttpHeaderCollection Headers { get; internal set; }
        public int HttpCode { get; internal set; }
        public string Content { get; set; }
        public MemoryStream Buffer { get; internal set; } = new MemoryStream();
        public string HttpVersion { get; set; }
        public string ReasonPhrase { get; set; }

        public bool IsHandled { get; internal set; }
        public bool HasSentResponse
        {
            get
            {
                if (this.OutputStream == null)
                {
                    return false;
                }

                return this.OutputStream.AsStreamForWrite().Position > 0;
            }
        }

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

            // TODO: Actually make this right. reason != OK all the time.
            string payload = $"{this.HttpVersion} {this.HttpCode} {this.ReasonPhrase}\r\n";

            if (this.PayloadStream != null)
            {
                this.Headers.ContentLength = this.PayloadStream.Length;
            }
            else
            {
                this.Headers.ContentLength = this.Buffer.Length;
            }

            payload += this.Headers.ToHeaderString();

            payload += "\r\n";

            // Write headers to stream
            await stream.WriteStringAsync(payload).ConfigureAwait(false);

            if (this.PayloadStream != null)
            {
                this.PayloadStream.Position = 0;
                await this.PayloadStream.CopyToAsync(stream).ConfigureAwait(false);
            }
            else
            {
                //await stream.WriteAsync(this.Buffer.ToArray(), 0, (int)this.Buffer.Length).ConfigureAwait(false);
                this.Buffer.Position = 0;
                await this.Buffer.CopyToAsync(stream).ConfigureAwait(false);
            }

            // Send result to client.
            await stream.FlushAsync().ConfigureAwait(false);
        }

        public async Task WritePayloadAsync(string message)
        {
            var encodedBytes = Encoding.UTF8.GetBytes(message);

            // TODO: Make sure length isn't beyond Int32.MaxLength.
            await this.Buffer.WriteAsync(encodedBytes, 0, encodedBytes.Length).ConfigureAwait(false);
            this.IsHandled = true;
        }

        public async Task WritePayloadAsync(byte[] bytes, string contentType)
        {
            this.Headers.ContentType = contentType;

            // TODO: Make sure length isn't beyond Int32.MaxLength.
            await this.Buffer.WriteAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            this.IsHandled = true;
        }

        public void SetPayload(Stream stream, string contentType)
        {
            this.Headers.ContentType = contentType;
            this.PayloadStream = stream;
            this.IsHandled = true;
        }
    }
}
