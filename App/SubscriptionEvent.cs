using Heleus.Apps.Shared;
using Heleus.Network.Client;

namespace Heleus.Apps.Status
{
    public class SubscriptionEvent : ClientResponseEvent
    {
        public readonly long AccountId;
        public readonly bool Subscribed;

        public SubscriptionEvent(HeleusClientResponse result, long accountId, bool subscribed) : base(result)
        {
            AccountId = accountId;
            Subscribed = subscribed;
        }
    }
}
