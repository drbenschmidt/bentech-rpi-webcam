using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RaspberryPi.Camera.Http
{
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
}
