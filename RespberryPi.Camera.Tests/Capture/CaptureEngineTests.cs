using Microsoft.VisualStudio.TestTools.UnitTesting;
using RaspberryPi.Camera.Capture;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RespberryPi.Camera.Tests.Capture
{
    [TestClass]
    public class CaptureEngineTests
    {
        [TestMethod]
        public void CanGetCaptureSources()
        {
            var engine = new CaptureEngine();

            engine.GetSources().Wait();
        }
    }
}
