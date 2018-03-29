using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspberryPi.Camera.Http
{
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
}
