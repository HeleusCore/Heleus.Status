using Heleus.Apps.Shared;
using Heleus.Network.Client;

namespace Heleus.Apps.Status
{
    public class MessageSentEvent : ClientResponseEvent
    {
        public MessageSentEvent(HeleusClientResponse result) : base(result)
        {
        }
    }
}
