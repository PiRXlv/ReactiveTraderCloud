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
            consumer.Received += (sender, args) =>
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
                        ReplyTo = new WampTransientDestination(x.ReplyTo),
                        Payload = Encoding.UTF8.GetBytes(payload)
                    };

                    var userSession = new UserSession
                    {
                        Username = x.Username
                    };

                    var userContext = new RequestContext(message, userSession);
                    onMessage(userContext, message); //TODO this feels weird
                }
                catch (Exception ex)
                {
                    // error handling
                }
                finally
                {
                    _channel.BasicAck(deliveryTag: args.DeliveryTag,
                        multiple: false);
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
            consumer.Received += (sender, args) =>
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
                        ReplyTo = new WampTransientDestination(x.ReplyTo),
                        Payload = Encoding.UTF8.GetBytes(payload)
                    };

                    var userSession = new UserSession
                    {
                        Username = x.Username
                    };

                    var userContext = new RequestContext(message, userSession);
                    var result = onMessage(userContext, message).Result; //TODO this definitely needs to be fixed. Probably with abstraction it will happen naturally
                    reply = JsonConvert.SerializeObject(result);
                }
                catch (Exception ex)
                {
                    // error handling
                }
                finally
                {
                    var responseBytes = Encoding.UTF8.GetBytes(reply);
                    _channel.BasicPublish(exchange: "", routingKey: requestProps.ReplyTo,
                            basicProperties: replyProps, body: responseBytes);
                    _channel.BasicAck(deliveryTag: args.DeliveryTag,
                        multiple: false);
                }
            };

            return Disposable.Create(() => { _channel.BasicCancel(consumerTag); });
        }

        public IPrivateEndPoint<T> GetPrivateEndPoint<T>(ITransientDestination replyTo)
        {
            var topic = ((WampTransientDestination) replyTo).Topic;
            return new RabbitPrivateEndpoint<T>(_channel, topic);
        }

        public IEndPoint<T> GetPublicEndPoint<T>(string topic)
        {
            return new RabbitEndPoint<T>(_channel, topic);
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

        private TResult GetJsonPayload<TResult>(EventPattern<BasicDeliverEventArgs> arg)
        {
            var body = Encoding.UTF8.GetString(arg.EventArgs.Body);
            return JsonConvert.DeserializeObject<TResult>(body);
        }

        public void Dispose()
        {
        }
    }
}
