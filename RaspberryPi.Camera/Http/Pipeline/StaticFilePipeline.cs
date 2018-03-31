using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MimeTypes;

namespace RaspberryPi.Camera.Http.Pipeline
{
    public interface IPipeline
    {
        void Execute(HttpContext context);
    }

    public class StaticFilePipeline : IPipeline
    {
        private string RootPath;

        public StaticFilePipeline(string rootPath)
        {
            this.RootPath = rootPath;
        }

        public void Execute(HttpContext context)
        {
            if (context.Request.Method != HttpRequestMethod.GET)
            {
                return;
            }

            // Resolve the path.
            string path = Path.Combine(this.RootPath, context.Request.Path);

            // If it exists, serve it up!
            if (File.Exists(path))
            {
                // TODO: Check for file segment request.
                var fs = File.Open(path, FileMode.Open);

                // Figure out the content type.
                string contentType = MimeTypeMap.GetMimeType(Path.GetExtension(path));

                // Figure out the content length.
                context.Response.Headers.ContentLength = fs.Length;

                // Start streaming the file!
                context.Response.SetPayload(fs, contentType);

                context.Response.HttpCode = 200;
            }

            // Otherwise, don't do anything because we want another pipeline
            // to potentially take over if it hasn't been handled.
        }
    }
}
