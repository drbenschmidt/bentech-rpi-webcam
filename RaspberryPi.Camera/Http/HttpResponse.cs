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
        public HttpHeaderCollection Headers { get; internal set; }
        public int HttpCode { get; internal set; }
        public string Content { get; set; }
        public byte[] BinaryContent { get; set; }
        public string HttpVersion { get; set; }
        public string ReasonPhrase { get; set; }

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
}
