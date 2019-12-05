using System;
using System.Reactive.Subjects;
using System.Text;
using Newtonsoft.Json;
using RabbitMQ.Client;
using Serilog;

namespace Adaptive.ReactiveTrader.Messaging
{
    internal class EndPoint<T> : IEndPoint<T>
    {
        private readonly ISubject<T> _subject;

        public EndPoint(ISubject<T> subject)
        {
            _subject = subject;
        }

        public void PushMessage(T obj)
        {
            try
            {
                _subject.OnNext(obj);
            }
            catch (Exception e)
            {
                Log.Error("Could not send message {message}", e.Message);
            }
        }

        public void PushError(Exception ex)
        {
            _subject.OnError(ex);
        }
    }

    internal class RabbitEndPoint<T> : IEndPoint<T>
    {
        private readonly IModel _channel;
        private readonly string _topic;


        public RabbitEndPoint(IModel channel, string topic)
        {
            _channel = channel;
            _topic = topic;
            _channel.ExchangeDeclare(topic, ExchangeType.Fanout);
        }

        public void PushMessage(T obj)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(obj));
                _channel.BasicPublish(_topic, string.Empty, null, body);
            }
            catch (Exception e)
            {
                Log.Error("Could not send message {message}", e.Message);
            }
        }

        public void PushError(Exception ex)
        {
            throw new InvalidOperationException("", ex); //TODO: discuss implementing proper error propagation to UI
        }
    }
}
