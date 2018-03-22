using RaspberryPi.Camera.Utilities;
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

    public interface ILogger
    {
        void Log(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, Action<ILogContext> extraInfo = null);
    }

    public interface ILogContext
    {
        ILogContext AddVariable(string key, string value);
        string Format();
    }

    public interface ILogService
    {
        void Log(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, Action<ILogContext> extraInfo = null);
        void Debug(Func<string> messageGenerator, Action<ILogContext> extraInfo = null);
        void Error(Func<string> messageGenerator, Exception exc, Action<ILogContext> extraInfo = null);
        void Info(Func<string> messageGenerator, Action<ILogContext> extraInfo = null);
        void Trace(Func<string> messageGenerator, Action<ILogContext> extraInfo = null);
    }

    public class LogContext : ILogContext
    {
        public Dictionary<string, string> Variables { get; set; } = new Dictionary<string, string>();

        public ILogContext AddVariable(string key, string value)
        {
            // TODO: Verify only one exists.
            this.Variables.Add(key, value);

            return this;
        }

        public string Format()
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;

            sb.Append("{" + Environment.NewLine);

            foreach (var variable in this.Variables)
            {
                sb.Append($"\t\"{variable.Key}\": \"{variable.Value}\"");

                if (i < (this.Variables.Count - 1))
                {
                    sb.Append(",");
                }

                sb.Append(Environment.NewLine);

                i++;
            }

            sb.Append("}");

            return sb.ToString();
        }
    }

    public class LogService : ILogService
    {
        public List<ILogger> Loggers;

        public LogService()
        {
            this.Loggers = new List<ILogger>();
        }

        public void Log(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, Action<ILogContext> extraInfo = null)
        {
            this.Loggers.AsParallel().ForAll((logger) => logger.Log(level, messageGenerator, exc, extraInfo));
        }

        public void Debug(Func<string> messageGenerator, Action<ILogContext> extraInfo = null)
        {
            this.Log(LoggingLevel.Debug, messageGenerator, null, extraInfo);
        }

        public void Error(Func<string> messageGenerator, Exception exc, Action<ILogContext> extraInfo = null)
        {
            this.Log(LoggingLevel.Error, messageGenerator, null, extraInfo);
        }

        public void Info(Func<string> messageGenerator, Action<ILogContext> extraInfo = null)
        {
            this.Log(LoggingLevel.Informational, messageGenerator, null, extraInfo);
        }

        public void Trace(Func<string> messageGenerator, Action<ILogContext> extraInfo = null)
        {
            this.Log(LoggingLevel.Trace, messageGenerator, null, extraInfo);
        }
    }

    public class FileLogger : ILogger
    {
        private object _lock = new object();
        private IStorageFile File;

        public FileLogger(string filePath, string fileName)
        {
            string path = Path.Combine(filePath, fileName);
            var fileTask = FileUtility.EnsureFileCreatedAsync(path);
            fileTask.Wait(1000);
            this.File = fileTask.Result;
        }

        public void Log(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, Action<ILogContext> extraInfo = null)
        {
            lock (this._lock)
            {
                this.InternalLog(level, messageGenerator, exc, extraInfo);
            }
        }

        private void InternalLog(LoggingLevel level, Func<string> messageGenerator, Exception exc = null, Action<ILogContext> extraInfo = null)
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
                LogContext lc = new LogContext();

                try
                {
                    str += Environment.NewLine;
                    extraInfo(lc);
                    str += lc.Format();
                }
                catch
                {
                    // TODO: Do something with this!
                }
            }

            str += Environment.NewLine;

            var writeTask = FileIO.AppendTextAsync(this.File, str, Windows.Storage.Streams.UnicodeEncoding.Utf8).AsTask();
            writeTask.Wait(1000);
        }
    }
}
