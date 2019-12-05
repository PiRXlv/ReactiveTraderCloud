using System;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;
using Adaptive.ReactiveTrader.Messaging.Abstraction;
using Adaptive.ReactiveTrader.Messaging.WAMP;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using Serilog.Events;

namespace Adaptive.ReactiveTrader.Messaging
{
    internal class Broker : IBroker, IDisposable
    {
        private readonly IModel _channel;

        public Broker(IModel channel)
        {
            _channel = channel;
            
        }
        public IDisposable RegisterCall(string procName, Func<IRequestContext, IMessage, Task> onMessage)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                Log.Information($"Registering call: [{procName}]");
            }

            //TODO: encapsulate in something similar -> var rpcOperation = new RpcOperation(procName, onMessage);
            _channel.QueueDeclare(queue: procName, durable: false,
                exclusive: false, autoDelete: true, arguments: null);
            _channel.BasicQos(0, 1, false);

            var consumer = new EventingBasicConsumer(_channel);
            var consumerTag = _channel.BasicConsume(queue: procName,
                autoAck: false, consumer: consumer);
            consumer.Received += async (sender, args) =>
            {
                var body = args.Body;
                var requestProps = args.BasicProperties;
                var replyProps = _channel.CreateBasicProperties();
                replyProps.CorrelationId = requestProps.CorrelationId;

                try
                {
                    var request = Encoding.UTF8.GetString(body);
                    var x = JsonConvert.DeserializeObject<MessageDto>(request);

                    var payload = x.Payload.ToString();

                    var message = new Message
                    {
                        ReplyTo = x.ReplyTo,
                        Payload = Encoding.UTF8.GetBytes(payload)
                    };

                    var userContext = new RequestContext(message, x.Username);
                    await onMessage(userContext, message); 
                    _channel.BasicAck(deliveryTag: args.DeliveryTag,
                        multiple: false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception while processing RPC {procName}", procName);
                    _channel.BasicNack(args.DeliveryTag, false, requeue:false); // TODO: should we requeue?
                }
            };

            return Disposable.Create(() => { _channel.BasicCancel(consumerTag); });
        }

        public IDisposable RegisterCallResponse<TResponse>(string procName, Func<IRequestContext, IMessage, Task<TResponse>> onMessage)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                Log.Information($"Registering call: [{procName}]");
            }

            //TODO: encapsulate in something similar -> var rpcOperation = new RpcOperation(procName, onMessage);
            _channel.QueueDeclare(queue: procName, durable: false,
                exclusive: false, autoDelete: true, arguments: null);
            _channel.BasicQos(0, 1, false);
            string reply = string.Empty;
            var consumer = new EventingBasicConsumer(_channel);
            var consumerTag = _channel.BasicConsume(queue: procName,
                autoAck: false, consumer: consumer);
            consumer.Received += async (sender, args) =>
            {
                var body = args.Body;
                var requestProps = args.BasicProperties;
                var replyProps = _channel.CreateBasicProperties();
                replyProps.CorrelationId = requestProps.CorrelationId;

                try
                {
                    var request = Encoding.UTF8.GetString(body);
                    var x = JsonConvert.DeserializeObject<MessageDto>(request);

                    var payload = x.Payload.ToString();
                    
                    var message = new Message
                    {
                        ReplyTo = x.ReplyTo,
                        Payload = Encoding.UTF8.GetBytes(payload)
                    };

                    var userContext = new RequestContext(message, x.Username);
                    var result = await onMessage(userContext, message);
                    var responseBytes = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
                    _channel.BasicPublish(exchange: "", routingKey: requestProps.ReplyTo,
                        basicProperties: replyProps, body: responseBytes);
                    _channel.BasicAck(deliveryTag: args.DeliveryTag,
                        multiple: false);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Exception while processing RPC {procName}", procName);
                    _channel.BasicNack(args.DeliveryTag, false, requeue: false); // TODO: should we requeue?
                }
            };

            return Disposable.Create(() => { _channel.BasicCancel(consumerTag); });
        }

        public IPrivateEndPoint<T> GetPrivateEndPoint<T>(string replyTo)
        {
            return new PrivateEndpoint<T>(_channel, replyTo);
        }

        public IEndPoint<T> GetPublicEndPoint<T>(string topic)
        {
            return new EndPoint<T>(_channel, topic);
        }

        public IObservable<T> SubscribeToTopic<T>(string topic)
        {
            _channel.ExchangeDeclare(topic, ExchangeType.Fanout);
            var queueName = _channel.QueueDeclare().QueueName;
            _channel.QueueBind(queue: queueName, exchange: topic, routingKey: string.Empty);

            var consumer = new EventingBasicConsumer(_channel);
            var observable = Observable.FromEventPattern<BasicDeliverEventArgs>(
                    x => consumer.Received += x,
                    x => consumer.Received -= x)
                .Select(GetJsonPayload<T>);
            _channel.BasicConsume(queueName, true, consumer);

            return observable;
        }

        private static TResult GetJsonPayload<TResult>(EventPattern<BasicDeliverEventArgs> arg)
        {
            var body = Encoding.UTF8.GetString(arg.EventArgs.Body);
            return JsonConvert.DeserializeObject<TResult>(body);
        }

        public void Dispose()
        {
        }
    }
}
