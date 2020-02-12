namespace Heleus.Apps.Status
{
    public class StatusUpdate
    {
        public readonly SubscriptionInfo[] RecentMessages;
        public readonly SubscriptionInfo[] ViewedUpdates;

        public StatusUpdate(SubscriptionInfo[] recentMessages, SubscriptionInfo[] viewedMessages)
        {
            RecentMessages = recentMessages;
            ViewedUpdates = viewedMessages;
        }
    }
}
