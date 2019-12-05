using System;
using System.Threading.Tasks;
using Adaptive.ReactiveTrader.Messaging.Abstraction;

namespace Adaptive.ReactiveTrader.Messaging
{
    public interface IBroker
    {
        IDisposable RegisterCall(string procName, Func<IRequestContext, IMessage, Task> onMessage);
        IDisposable RegisterCallResponse<TResponse>(string procName, Func<IRequestContext, IMessage, Task<TResponse>> onMessage);

        IPrivateEndPoint<T> GetPrivateEndPoint<T>(ITransientDestination replyTo);
        IEndPoint<T> GetPublicEndPoint<T>(string topic);

        IObservable<T> SubscribeToTopic<T>(string topic);
    }
}
