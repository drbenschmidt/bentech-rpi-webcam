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
        private int _HttpCode;
        public int HttpCode
        {
            get => this._HttpCode;
            internal set
            {
                if (HttpResponseCodeHelper.CodeMap.ContainsKey(value))
                {
                    this.ReasonPhrase = HttpResponseCodeHelper.CodeMap[value];
                }
                else
                {
                    this.ReasonPhrase = null;
                }

                this._HttpCode = value;
            }
        }
        public string Content { get; set; }
        public MemoryStream Buffer { get; internal set; } = new MemoryStream();
        public string HttpVersion { get; set; }
        public string ReasonPhrase { get; internal set; }

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
            this.HttpCode = 200;
            this.HttpVersion = "HTTP/1.1";
        }

        public async Task FlushAsync()
        {
            var stream = this.OutputStream.AsStreamForWrite();

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

            // TODO: Determine if we're compressing the payload, or
            // if we should be chunking it to the client.

            if (this.PayloadStream != null)
            {
                this.PayloadStream.Position = 0;
                await this.PayloadStream.CopyToAsync(stream).ConfigureAwait(false);
            }
            else
            {
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

    public static class HttpResponseCodeHelper
    {
        /// <summary>
        /// 
        /// </summary>
        /// <see cref="https://www.w3.org/Protocols/rfc2616/rfc2616-sec6.html"/>
        public static Dictionary<int, string> CodeMap = new Dictionary<int, string>()
        {
            { 100, "Continue" },
            { 101, "Switching Protocols" },
            { 200, "OK" },
            { 201, "Created" },
            { 202, "Accepted" },
            { 203, "Non-Authoritative Information" },
            { 204, "No Content" },
            { 205, "Reset Content" },
            { 206, "Partial Content" },
            { 300, "Multiple Choices" },
            { 301, "Moved Permanently" },
            { 302, "Found" },
            { 303, "See Other" },
            { 304, "Not Modified" },
            { 305, "Use Proxy" },
            { 307, "Temporary Redirect" },
            { 400, "Bad Request" },
            { 401, "Unauthorized" },
            { 402, "Payment Required" },
            { 403, "Forbidden" },
            { 404, "Not Found" },
            { 405, "Method Not Allowed" },
            { 406, "Not Acceptable" },
            { 407, "Proxy Authentication Required" },
            { 408, "Request Time-out" },
            { 409, " Conflict" },
            { 410, "Gone" },
            { 411, "Length Required" },
            { 412, "Precondition Failed" },
            { 413, "Request Entity Too Large" },
            { 414, "Request-URI Too Large" },
            { 415, "Unsupported Media Type" },
            { 416, "Requested range not satisfiable" },
            { 417, "Expectation Failed" },
            { 500, "Internal Server Error" },
            { 501, "Not Implemented" },
            { 502, "Bad Gateway" },
            { 503, "Service Unavailable" },
            { 504, "Gateway Time-out" },
            { 505, "HTTP Version not supported" }
        };
    }
}
