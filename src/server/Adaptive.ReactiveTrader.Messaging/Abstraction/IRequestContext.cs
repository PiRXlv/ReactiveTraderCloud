namespace Adaptive.ReactiveTrader.Messaging.Abstraction
{
    public interface IRequestContext
    {
        IMessage RequestMessage { get; }
        string Username { get; }
    }
}