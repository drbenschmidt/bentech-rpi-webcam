using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspberryPi.Camera.Http
{
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
}
