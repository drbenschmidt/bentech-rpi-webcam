using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace RaspberryPi.Camera.Logging
{
    public enum LoggingLevel
    {
        Critical = 0,
        Error = 1,
        Warning = 2,
        Informational = 3,
        Debug = 4,
        Trace = 5
    }

    public class LoggingContext
    {
        public List<ILogger> Loggers;

        public LoggingContext()
        {
            this.Loggers = new List<ILogger>();
        }

        public void Log(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, object extraInfo = null)
        {
            this.Loggers.AsParallel().ForAll((logger) => logger.Log(level, messageGenerator, exc, extraInfo));
        }

        public void Debug(Func<string> messageGenerator, object extraInfo = null)
        {
            this.Log(LoggingLevel.Debug, messageGenerator, null, extraInfo);
        }

        public void Error(Func<string> messageGenerator, Exception exc, object extraInfo = null)
        {
            this.Log(LoggingLevel.Error, messageGenerator, null, extraInfo);
        }

        public void Info(Func<string> messageGenerator, object extraInfo = null)
        {
            this.Log(LoggingLevel.Informational, messageGenerator, null, extraInfo);
        }

        public void Trace(Func<string> messageGenerator, object extraInfo = null)
        {
            this.Log(LoggingLevel.Trace, messageGenerator, null, extraInfo);
        }
    }

    public interface ILogger
    {
        void Log(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, object extraInfo = null);
    }

    public class FileLogger : ILogger
    {
        private object _lock = new object();
        private IStorageFile File;

        public FileLogger(string filePath, string fileName)
        {
            
            var task = StorageFile.GetFileFromPathAsync(Path.Combine(filePath, fileName)).AsTask();
            task.Wait(5000);
            this.File = task.Result;
        }

        public void Log(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, object extraInfo = null)
        {
            lock (this._lock)
            {
                this.InternalLog(level, messageGenerator, exc, extraInfo);
            }
        }

        private void InternalLog(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, object extraInfo = null)
        {
            string str = $"{DateTime.UtcNow.ToString()} [{level.ToString()}] {messageGenerator()}";

            if (exc != null)
            {
                str += Environment.NewLine;
                str += exc.Message + Environment.NewLine + Environment.NewLine;
                str += exc.StackTrace;
            }

            if (extraInfo != null)
            {
                // TODO: Add a way to serialize it here.
            }

            var writeTask = FileIO.WriteLinesAsync(this.File, new string[] { str }, Windows.Storage.Streams.UnicodeEncoding.Utf8).AsTask();
            writeTask.Wait(1000);
        }
    }
}
