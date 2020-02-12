using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.Base;
using Heleus.Chain;
using Heleus.Operations;
using Heleus.ProfileService;
using Heleus.Transactions.Features;

namespace Heleus.Apps.Status
{
    public class Subscriptions : IPackable
    {
        public const string SubscriptionsTag = nameof(Subscriptions);

        public readonly long AccountId;
        public readonly int ChainId;

        public readonly Status Status;

        public readonly string DataStorageName;

        public long LastSaved { get; private set; }

        public static string GetDataStorageName(long accountId, int chainId)
        {
            return $"{SubscriptionsTag}_{chainId}_{accountId}";
        }

        long _lastProcessedTransactionId;

        public long Count => _subscriptions.Count;

        readonly Dictionary<long, SubscriptionInfo> _subscriptions = new Dictionary<long, SubscriptionInfo>();

        public SubscriptionInfo GetSubscriptionInfo(long accountId)
        {
            _subscriptions.TryGetValue(accountId, out var info);
            return info;
        }

        readonly List<SubsriptionBatch> _batches = new List<SubsriptionBatch> { new SubsriptionBatch(0) };

        readonly List<SubscriptionInfo> _recentMessages = new List<SubscriptionInfo>();
        readonly List<SubscriptionInfo> _viewedMessages = new List<SubscriptionInfo>();

        public StatusUpdate LastStatusUpdate { get; private set; }

        public IList<SubscriptionInfo> GetSubscriptions()
        {
            return _subscriptions.Values.ToArray();
        }

        public Subscriptions(long accountId, int chainId, Status status)
        {
            ChainId = chainId;
            AccountId = accountId;
            Status = status;
            DataStorageName = GetDataStorageName(accountId, chainId);
            _lastProcessedTransactionId = Operation.InvalidTransactionId;
        }

        public Subscriptions(Unpacker unpacker, Status status)
        {
            Status = status;

            unpacker.Unpack(out AccountId);
            unpacker.Unpack(out ChainId);
            DataStorageName = GetDataStorageName(AccountId, ChainId);

            LastSaved = unpacker.UnpackLong();
            _lastProcessedTransactionId = unpacker.UnpackLong();

            unpacker.Unpack(_subscriptions, (u) => new SubscriptionInfo(u, this));

            var subs = new List<SubscriptionInfo>();
            foreach (var sub in _subscriptions.Values)
            {
                AddToBatch(sub.AccountId);

                if(sub.CurrentTransactionInfo != null)
                {
                    subs.Add(sub);
                }
            }

            SortSubscriptions(subs);
        }

        public void Pack(Packer packer)
        {
            LastSaved = Time.Timestamp;

            packer.Pack(AccountId);
            packer.Pack(ChainId);
            packer.Pack(LastSaved);
            packer.Pack(_lastProcessedTransactionId);
            packer.Pack(_subscriptions);
        }

        public bool IsSubscribed(long accountId) => _subscriptions.ContainsKey(accountId);

        public SubsriptionBatch GetBatch(int index)
        {
            if (index >= 0 && index < _batches.Count)
                return _batches[index];

            return null;
        }

        void SortSubscriptions(List<SubscriptionInfo> subs)
        {
            var hashset = new HashSet<long>();
            var @new = new List<SubscriptionInfo>();
            var old = new List<SubscriptionInfo>();

            foreach (var sub in subs)
            {
                hashset.Add(sub.AccountId);

                if (sub.CurrentTransactionInfo.TransactionId > sub.LastViewedTransactionInfo.TransactionId)
                    @new.Add(sub);
                else
                    old.Add(sub);
            }

            _recentMessages.RemoveAll((s) => hashset.Contains(s.AccountId));
            _viewedMessages.RemoveAll((s) => hashset.Contains(s.AccountId));

            _recentMessages.AddRange(@new);
            _viewedMessages.AddRange(old);

            _viewedMessages.Sort((a, b) => b.CurrentTransactionInfo.TransactionId.CompareTo(a.CurrentTransactionInfo.TransactionId));
            _recentMessages.Sort((a, b) => b.CurrentTransactionInfo.TransactionId.CompareTo(a.CurrentTransactionInfo.TransactionId));

            LastStatusUpdate = new StatusUpdate(_recentMessages.ToArray(), _viewedMessages.ToArray()); ;
        }

        public bool UpdateProfile(long accountId, ProfileInfo profile)
        {
            if (profile == null)
                return false;

            if (_subscriptions.TryGetValue(accountId, out var sub))
            {
                if(sub.Profile == null)
                {
                    sub.Profile = profile;
                    return true;
                }

                if(profile.ProfileTransactionId > sub.Profile.ProfileTransactionId || profile.ImageTransactionId > sub.Profile.ImageTransactionId)
                {
                    sub.Profile = profile;
                    return true;
                }
            }

            return false;
        }

        public bool UpdateLastViewedTransactinInfo(long accountId, LastTransactionCountInfo info)
        {
            if (info == null)
                return false;

            if (_subscriptions.TryGetValue(accountId, out var sub))
            {
                if (sub.LastViewedTransactionInfo != null && info.TransactionId <= sub.LastViewedTransactionInfo.TransactionId)
                    return false;

                if (sub.LastViewedTransactionInfo == null || info.TransactionId > sub.LastViewedTransactionInfo.TransactionId)
                {
                    sub.LastViewedTransactionInfo = info;
                    if (sub.CurrentTransactionInfo.TransactionId <= info.TransactionId)
                        sub.CurrentTransactionInfo = info;

                    SortSubscriptions(new List<SubscriptionInfo> { sub });

                    return true;
                }
            }

            return false;
        }

        bool UpdateCurrentTransactionInfo(long accountId, LastTransactionCountInfo info, List<SubscriptionInfo> subs)
        {
            if (info == null)
                return false;

            if (_subscriptions.TryGetValue(accountId, out var sub))
            {
                if(sub.Profile == null)
                {

                }

                if (sub.CurrentTransactionInfo != null && info.TransactionId <= sub.CurrentTransactionInfo.TransactionId)
                    return false;

                if(sub.CurrentTransactionInfo == null || info.TransactionId > sub.CurrentTransactionInfo.TransactionId)
                {
                    sub.CurrentTransactionInfo = info;

                    if (sub.LastViewedTransactionInfo == null)
                        sub.LastViewedTransactionInfo = info;

                    subs.Add(sub);
                    return true;
                }
            }

            return false;
        }

        void AddToBatch(long accountId)
        {
            for(var i = 0; i < _batches.Count; i++)
            {
                var batch = _batches[i];
                if (batch.Contains(accountId))
                    return;
            }

            for (var i = 0; i < _batches.Count; i++)
            {
                var batch = _batches[i];
                if (batch.AddSubscription(accountId))
                    return;
            }

            var newBatch = new SubsriptionBatch(_batches.Count);
            newBatch.AddSubscription(accountId);
            _batches.Add(newBatch);
        }

        internal bool ProcessLastTransactionInfo(SubsriptionBatch batch, LastTransactionCountInfoBatch batchInfo)
        {
            var subs = new List<SubscriptionInfo>();

            batch.UpdateLastChecked();

            for (var j = 0; j < batchInfo.Count; j++)
            {
                (var found, var accountId, var info) = batchInfo.GetInfo(j);
                if (found && info != null)
                {
                    UpdateCurrentTransactionInfo(accountId, info, subs);
                }
            }

            if (subs.Count > 0)
            {
                SortSubscriptions(subs);

                return true;
            }

            return false;
        }

        internal async Task<bool> ProcessSubscriptions(FanInfo fanInfo)
        {
            var modified = false;
            var newSubscriptions = new HashSet<long>();
            var removedSubscriptions = new HashSet<long>();

            if (fanInfo.LastTransactionInfo.TransactionId <= _lastProcessedTransactionId)
                return false;

            var fans = new HashSet<long>();
            foreach(var fanId in fanInfo.Fans)
            {
                fans.Add(fanId);
                if(!_subscriptions.ContainsKey(fanId))
                {
                    var info = new SubscriptionInfo(fanId, this)
                    {
                        Profile = (await ProfileManager.Current.GetProfileInfo(fanId, ProfileDownloadType.QueryStoredData, false)).Profile
                    };

                    _subscriptions.Add(fanId, info);
                    modified = true;
                    newSubscriptions.Add(fanId);
                }

                _lastProcessedTransactionId = fanInfo.LastTransactionInfo.TransactionId;
            }

            foreach(var subscription in _subscriptions)
            {
                if(!fans.Contains(subscription.Key))
                {
                    removedSubscriptions.Add(subscription.Key);
                }
            }

            foreach(var removed in removedSubscriptions)
            {
                _subscriptions.Remove(removed);
                modified = true;
            }

            foreach (var @new in newSubscriptions)
            {
                AddToBatch(@new);
            }

            return modified;
        }
        /*
        internal async Task<bool> ProcessSubscriptionTransactions(TransactionDownloadResult<DataTransaction> download)
        {
            var modified = false;
            var newSubscriptions = new HashSet<long>();

            foreach(var transactionData in download.Transactions)
            {
                var transaction = transactionData.Transaction;
                var transactionId = transaction.TransactionId;

                if (transactionId <= LastProcessedTransactionId)
                    continue;

                try
                {
                    if (transaction.TransactionType == DataTransactionTypes.Info && transaction.Index == StatusServiceInfo.SubscriptionIndex)
                    {
                        var dataTransaction = transaction as InfoDataTransaction;
                        var subAccountId = dataTransaction.Receivers[0];
                        var subscribed = dataTransaction.Items[0].Data[0] == 1;

                        if (subscribed)
                        {
                            if (!_subscriptions.ContainsKey(subAccountId))
                            {
                                var info = new SubscriptionInfo(subAccountId, dataTransaction.Timestamp, this)
                                {
                                    Profile = (await ProfileManager.Current.GetProfileInfo(subAccountId, ProfileDownloadType.QueryStoredData, false)).Profile
                                };

                                _subscriptions.Add(subAccountId, info);
                                modified = true;
                                newSubscriptions.Add(subAccountId);
                            }
                        }
                        else
                        {
                            _subscriptions.Remove(subAccountId);
                            modified = true;
                            newSubscriptions.Remove(subAccountId);
                        }

                        LastProcessedTransactionId = transactionId;
                    }
                }
                catch (Exception ex)
                {
                    Log.IgnoreException(ex);
                }
            }

            foreach(var @new in newSubscriptions)
            {
                AddToBatch(@new);
            }

            return modified;
        }
        */
    }
}
