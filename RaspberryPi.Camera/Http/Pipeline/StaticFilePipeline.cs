using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                // Figure out the content type.
                // Figure out the content length.
                // Start streaming the file!
            }
        }
    }
}
