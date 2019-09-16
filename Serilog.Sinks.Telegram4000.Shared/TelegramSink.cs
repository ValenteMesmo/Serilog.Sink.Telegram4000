using Newtonsoft.Json;
using Serilog.Configuration;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Serilog
{
    public class TelegramSink : ILogEventSink
    {
        private readonly IFormatProvider FormatProvider;
        private readonly string BotId;
        private readonly string ChatId;
        private DateTime lastMessageTime = DateTime.Now;
        private TimeSpan minIntervalBetweenMessages = TimeSpan.FromMinutes(1f / 15f);

        public TelegramSink(
            string BotId
            , string ChatId
            , IFormatProvider FormatProvider = null
        )
        {
            this.BotId = BotId;
            this.ChatId = ChatId;
            this.FormatProvider = FormatProvider ?? new TelegramDefaultFormatProvider();
        }

        public void Emit(LogEvent logEvent)
        {
            var currentTime = DateTime.Now;

            var timeSpan = currentTime - lastMessageTime;
            if (timeSpan.TotalMilliseconds < minIntervalBetweenMessages.TotalMilliseconds)
                Task.Delay((int)timeSpan.TotalMilliseconds * 2)
                    .GetAwaiter()
                    .GetResult();

            lastMessageTime = DateTime.Now;

            var message = logEvent.RenderMessage();

            //TODO: how to properly use customFormaters here?
            message = (FormatProvider.GetFormat(typeof(LogEvent)) as ICustomFormatter)
                .Format(message, logEvent, null);

            using (var client = new HttpClient())
                client.PostAsync(
                    $"https://api.telegram.org/bot{BotId}/sendMessage"
                    , new StringContent(
                        JsonConvert.SerializeObject(new
                        {
                            text = message,
                            chat_id = ChatId,
                            parse_mode = "markdown"
                        })
                        , Encoding.UTF8
                        , "application/json"
                    )
                ).GetAwaiter()
                .GetResult();
        }
    }

    public static class TelegramSinkExtensions
    {
        public static LoggerConfiguration Telegram(
                  this LoggerSinkConfiguration loggerConfiguration,
                  string botId,
                  string chatId,
                  IFormatProvider formatProvider = null)
        {
            return loggerConfiguration.Sink(new TelegramSink(botId, chatId, formatProvider));
        }
    }

    internal class TelegramDefaultFormatter : ICustomFormatter
    {
        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (!(arg is LogEvent logEvent))
                return format;

            var prefix = "";
            var sufix = logEvent.Exception == null
                ? ""
                : Environment.NewLine + Environment.NewLine + logEvent.Exception.ToString();

            if (logEvent.Level == LogEventLevel.Verbose)
                prefix = "*VERBOSE* ";

            if (logEvent.Level == LogEventLevel.Debug)
                prefix = "*DEBUG* ";

            if (logEvent.Level == LogEventLevel.Information)
                prefix = "*INFO* ";

            if (logEvent.Level == LogEventLevel.Warning)
                prefix = "*WARNING* ";

            if (logEvent.Level == LogEventLevel.Error)
                prefix = "ERROR: ";

            if (logEvent.Level == LogEventLevel.Fatal)
                prefix = "*FATAL* ";

            return $@"```{prefix}{format}{sufix}```";
        }
    }

    internal class TelegramDefaultFormatProvider : IFormatProvider
    {
        public object GetFormat(Type formatType)
        {
            return new TelegramDefaultFormatter();
        }
    }
}
