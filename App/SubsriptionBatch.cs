using System.Collections.Generic;
using System.Linq;
using Heleus.Base;

namespace Heleus.Apps.Status
{
    public class SubsriptionBatch
    {
        public const long MaxBatchSize = 100;

        public readonly long Index;

        public long LastChecked { get; private set; }

        readonly HashSet<long> _subscriptions = new HashSet<long>();

        public SubsriptionBatch(long index)
        {
            Index = index;
        }

        public long[] GetSubscriptions()
        {
            return _subscriptions.ToArray();
        }

        public void UpdateLastChecked()
        {
            LastChecked = Time.Timestamp;
        }

        public bool Contains(long accountId)
        {
            return _subscriptions.Contains(accountId);
        }

        public bool AddSubscription(long accountId)
        {
            if (_subscriptions.Count > MaxBatchSize)
                return false;

            LastChecked = 0;
            _subscriptions.Add(accountId);
            return true;
        }
    }
}
