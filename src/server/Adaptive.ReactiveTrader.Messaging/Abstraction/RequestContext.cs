namespace Adaptive.ReactiveTrader.Messaging.Abstraction
{
    public class RequestContext : IRequestContext
    {
        public RequestContext(IMessage requestMessage, string username)
        {
            RequestMessage = requestMessage;
            Username = username;
        }

        public IMessage RequestMessage { get; }
        public string Username { get; }
    }
}