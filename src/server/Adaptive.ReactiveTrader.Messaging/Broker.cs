using System;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text;
using System.Threading.Tasks;
using SystemEx;
using Adaptive.ReactiveTrader.Messaging.Abstraction;
using Adaptive.ReactiveTrader.Messaging.WAMP;
using Newtonsoft.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Serilog;
using Serilog.Events;
using WampSharp.V2;
using WampSharp.V2.Core.Contracts;
using WampSharp.V2.MetaApi;

namespace Adaptive.ReactiveTrader.Messaging
{
    internal class AsyncDisposableWrapper : IAsyncDisposable
    {
        private readonly IDisposable _disposable;

        public AsyncDisposableWrapper(IDisposable disposable)
        {
            _disposable = disposable;
        }
        public Task DisposeAsync()
        {
            _disposable.Dispose();
            return Task.CompletedTask;
        }
    }
    internal class RabbitBroker : IBroker, IDisposable
    {
        private readonly IModel _channel;

        public RabbitBroker(IModel channel)
        {
            _channel = channel;
            
        }
        public Task<IAsyncDisposable> RegisterCall(string procName, Func<IRequestContext, IMessage, Task> onMessage)
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

            return Task.FromResult(new AsyncDisposableWrapper(Disposable.Create(() =>
            {
                _channel.BasicCancel(consumerTag);
            })) as IAsyncDisposable);
        }

        public Task<IAsyncDisposable> RegisterCallResponse<TResponse>(string procName, Func<IRequestContext, IMessage, Task<TResponse>> onMessage)
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

            return Task.FromResult(new AsyncDisposableWrapper(Disposable.Create(() =>
            {
                _channel.BasicCancel(consumerTag);
            })) as IAsyncDisposable);
        }

        public Task<IPrivateEndPoint<T>> GetPrivateEndPoint<T>(ITransientDestination replyTo)
        {
            var topic = ((WampTransientDestination) replyTo).Topic;
            return Task.FromResult((IPrivateEndPoint<T>)new RabbitPrivateEndpoint<T>(_channel, topic));
        }

        public Task<IEndPoint<T>> GetPublicEndPoint<T>(string topic)
        {
            return Task.FromResult((IEndPoint<T>) new RabbitEndPoint<T>(_channel, topic));
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

    internal class Broker : IBroker, IDisposable
    {
        private readonly Subject<Unit> _brokerTeardown;
        private readonly IWampChannel _channel;
        private readonly WampMetaApiServiceProxy _meta;
        private readonly IObservable<long> _sessionTeardowns;
        private readonly IObservable<long> _subscriptionTeardowns;

        public Broker(IWampChannel channel)
        {
            _channel = channel;

            _meta = _channel.RealmProxy.GetMetaApiServiceProxy();

            _sessionTeardowns =
                Observable.Create<long>(async o =>
                {
                    var r = await _meta.SubscribeTo.Session.OnLeave(o.OnNext);
                    return Disposable.Create(async () =>
                    {
                        try
                        {
                            await r.DisposeAsync();
                        }
                        catch (Exception e)
                        {
                            Log.Error("Couldn't close subscription to sessions. Perhaps broker shutdown." + e.Message);
                        }
                    });
                }).Publish().RefCount();

            _subscriptionTeardowns =
                Observable.Create<long>(
                    async observer =>
                    {
                        var r = await _meta.SubscribeTo.Subscription.OnUnsubscribe(
                            (_, subscriptionId) => { observer.OnNext(subscriptionId); });
                        return Disposable.Create(async () =>
                        {
                            try
                            {
                                await r.DisposeAsync();
                            }
                            catch (Exception e)
                            {
                                Log.Error("Couldn't close subscription to subscriptions. Perhaps broker shutdown." + e.Message);
                            }
                        });
                    }).Publish().RefCount();

            _brokerTeardown = new Subject<Unit>();
        }

        public Task<IAsyncDisposable> RegisterCall(string procName,
                                                         Func<IRequestContext, IMessage, Task> onMessage)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                Log.Information($"Registering call: [{procName}]");
            }

            var rpcOperation = new RpcOperation(procName, onMessage);
            var realm = _channel.RealmProxy;

            var registerOptions = new RegisterOptions
            {
                Invoke = "single"
            };

            // Todo this operation can cause a deadlock - even with configureawait(False)
            return realm.RpcCatalog.Register(rpcOperation, registerOptions);
        }

        public async Task<IAsyncDisposable> RegisterCallResponse<TResponse>(string procName,
                                                                            Func<IRequestContext, IMessage, Task<TResponse>> onMessage)
        {
            if (Log.IsEnabled(LogEventLevel.Information))
            {
                Log.Information($"Registering call with response: [{procName}]");
            }

            var rpcOperation = new RpcResponseOperation<TResponse>(procName, onMessage);
            var realm = _channel.RealmProxy;

            var registerOptions = new RegisterOptions
            {
                Invoke = "roundrobin"
            };

            return await realm.RpcCatalog.Register(rpcOperation, registerOptions);
        }

        public async Task<IPrivateEndPoint<T>> GetPrivateEndPoint<T>(ITransientDestination destination)
        {
            var dest = (WampTransientDestination) destination;
            var subID = await _meta.LookupSubscriptionIdAsync(dest.Topic, new SubscribeOptions {Match = "exact"});

            Log.Debug("Create subscription {subscriptionId} ({destination})", subID, dest);

            if (!subID.HasValue)
            {
                // subscription is already disposed 
                Log.Error("Subscription not found for topic {topic}", dest.Topic);
                throw new Exception("No subscribers found for private subscription.");
            }
            var sessionID = (await _meta.GetSubscribersAsync(subID.Value)).ToList().FirstOrDefault();

            if (sessionID == 0)
            {
                Log.Error("Subscription found but there are no subscriptions for topic {topic}", dest.Topic);
                throw new Exception("No subscribers found for private subscription.");
            }

            var breaker =
                _sessionTeardowns.Where(s => s == sessionID).Select(_ => Unit.Default)
                                 .Merge(_subscriptionTeardowns.Where(s => s == subID.Value).Select(_ => Unit.Default))
                                 .Merge(_brokerTeardown)
                                 .Take(1)
                                 .Do(o => Log.Debug("Remove subscription for {subscriptionId} ({destination})", subID, dest));

            var subject = _channel.RealmProxy.Services.GetSubject<T>(dest.Topic);

            return new PrivateEndPoint<T>(subject, breaker);
        }

        public Task<IEndPoint<T>> GetPublicEndPoint<T>(string destination)
        {
            var subject = _channel.RealmProxy.Services.GetSubject<T>(destination);
            return Task.FromResult((IEndPoint<T>) new EndPoint<T>(subject));
        }

        public IObservable<T> SubscribeToTopic<T>(string topic)
        {
            return _channel.RealmProxy.Services.GetSubject<T>(topic);
        }

        public void Dispose()
        {
            _brokerTeardown.OnNext(Unit.Default);
        }
    }
}
