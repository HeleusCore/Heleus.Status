using System.Collections.Generic;
using Heleus.Base;
using Heleus.Chain;
using Heleus.ProfileService;

namespace Heleus.Apps.Status
{
    public class SubscriptionInfo : IPackable, IUnpackerKey<long>
    {
        public class SubscriptionInfoComparer : IComparer<SubscriptionInfo>
        {
            public int Compare(SubscriptionInfo x, SubscriptionInfo y)
            {
                return x.AccountId.CompareTo(y.AccountId);
            }
        }

        public static SubscriptionInfoComparer Comparer { get; private set; } = new SubscriptionInfoComparer();

        public long UnpackerKey => AccountId;

        public readonly long AccountId;
        public readonly Subscriptions Subscriptions;

        public ProfileInfo Profile;

        public LastTransactionCountInfo LastViewedTransactionInfo;
        public LastTransactionCountInfo CurrentTransactionInfo;

        public SubscriptionInfo(long accountId, Subscriptions subscriptions)
        {
            AccountId = accountId;
            Subscriptions = subscriptions;
        }

        public SubscriptionInfo(Unpacker unpacker, Subscriptions subscriptions)
        {
            Subscriptions = subscriptions;

            unpacker.Unpack(out AccountId);

            if (unpacker.UnpackBool())
                Profile = new ProfileInfo(unpacker);
            if (unpacker.UnpackBool())
                LastViewedTransactionInfo = new LastTransactionCountInfo(unpacker);
            if (unpacker.UnpackBool())
                CurrentTransactionInfo = new LastTransactionCountInfo(unpacker);
        }

        public void Pack(Packer packer)
        {
            packer.Pack(AccountId);

            if (packer.Pack(Profile != null))
                packer.Pack(Profile);
            if (packer.Pack(LastViewedTransactionInfo != null))
                packer.Pack(LastViewedTransactionInfo);
            if (packer.Pack(CurrentTransactionInfo != null))
                packer.Pack(CurrentTransactionInfo);
        }
    }
}
