using System.IO;
using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace Extensions.Container;

public static class ConsumeEx
{
    extension(Consume)
    {
        public static IOutputConsumer RedirectStdoutAndStderrToLogger(ILogger? logger = null)
        {
            logger ??= new SerilogLoggerFactory(Log.Logger).CreateLogger("TestContainer");
            return new StdoutAndStderrToLoggerConsumer(logger);
        }
    }
    
    private class StdoutAndStderrToLoggerConsumer(ILogger logger) : IOutputConsumer
    {
        public void Dispose()
        {
            Stdout.Dispose();
            Stderr.Dispose();
        }

        public bool Enabled { get; } = true;
        public Stream Stdout { get; } = new LoggerStream(logger, "Output:", LogLevel.Information);
        public Stream Stderr { get; } = new LoggerStream(logger, "Error:", LogLevel.Error);

        private class LoggerStream(ILogger logger, string prefix, LogLevel level) : Stream
        {
            private readonly StringBuilder _buffer = new();

            public override void Write(byte[] buffer, int offset, int count)
            {
                var text = Encoding.UTF8.GetString(buffer, offset, count);
                _buffer.Append(text);
                
                var str = _buffer.ToString();
                var nl = str.IndexOf('\n');
                
                while (nl >= 0)
                {
                    var line = str[..nl].TrimEnd('\r');
                    if (logger.IsEnabled(level)) 
                        logger.Log(level, "{Prefix} {Output}", prefix, line);
                    str = str[(nl + 1)..];
                    nl = str.IndexOf('\n');
                }
                
                _buffer.Clear().Append(str);
            }

            public override void Flush()
            {
                if (_buffer.Length <= 0) 
                    return;
                
                if (logger.IsEnabled(level)) 
                    logger.Log(level, "{Prefix} {Output}", prefix, _buffer.ToString().TrimEnd('\r'));
                
                _buffer.Clear();
            }

            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => 0;
            public override long Position { get => 0; set { } }
            
            public override void SetLength(long value) { }
            public override int Read(byte[] buffer, int offset, int count) => 0;
            public override long Seek(long offset, SeekOrigin origin) => 0;
        }
    }
}