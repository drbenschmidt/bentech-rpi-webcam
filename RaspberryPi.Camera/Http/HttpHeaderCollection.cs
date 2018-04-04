using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspberryPi.Camera.Http
{
    public class HttpHeaderCollection : Dictionary<string, string>
    {
        public long? ContentLength
        {
            get => this.GetLongHeader("Content-Length");
            set => this.SetLongHeader("Content-Length", value.Value);
        }

        public string ContentType
        {
            get => this.GetStringHeader("Content-Type");
            set => this.SetStringHeader("Content-Type", value);
        }

        private CookieCollection _Cookies;
        public CookieCollection Cookies
        {
            get
            {
                if (this._Cookies == null)
                {
                    this._Cookies = CookieCollection.FromHeader(this.GetStringHeader("Cookie"));
                }

                return this._Cookies;
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

        private long? GetLongHeader(string header)
        {
            if (this.ContainsKey(header))
            {
                string value = this[header];
                return Int64.Parse(value);
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

        private void SetLongHeader(string header, long value)
        {
            if (this.ContainsKey(header))
            {
                this[header] = value.ToString();
            }
            else
            {
                this.Add(header, value.ToString());
            }
        }

        private void SetStringHeader(string header, string value)
        {
            if (this.ContainsKey(header))
            {
                this[header] = value;
            }
            else
            {
                this.Add(header, value);
            }
        }
    }

    public class CookieCollection : Collection<HttpCookie>
    {
        public HttpCookie GetCookie(string name)
        {
            return this.Single((c) => c.Name == name);
        }

        public static CookieCollection FromHeader(string header)
        {
            // TODO: Implement this!
            return new CookieCollection();
        }
    }

    public class HttpCookie
    {
        public string Name { get; set; }
        public string Data { get; set; }
        public bool IsHttpOnly { get; set; }
        public bool IsSecure { get; set; }

        public HttpCookie(string name, string data = null, bool isHttpOnly = false, bool isSecure = false)
        {
            this.Name = name;
            this.Data = data;
            this.IsHttpOnly = isHttpOnly;
            this.IsSecure = IsSecure;
        }

        public static HttpCookie FromHeader(string header)
        {
            // TODO: Implement this!
            return new HttpCookie("");
        }

        public string ToHeader()
        {
            // TODO: Implement this!
            return "";
        }
    }
}
