using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.Base;
using Heleus.Chain;
using Heleus.Network.Client;
using Heleus.Network.Results;
using Heleus.StatusService;
using Heleus.Transactions;
using Heleus.Transactions.Features;
using TinyJson;

namespace Heleus.Apps.Status
{
    public class Status : IPackable
    {
        public int ChainId => ServiceNode.ChainId;
        public long AccountId => ServiceNode.AccountId;

        public readonly ServiceNode ServiceNode;
        HeleusClient _client => ServiceNode.Client;
        SubmitAccount _submitAccount => ServiceNode.GetSubmitAccounts<SubmitAccount>(StatusServiceInfo.StatusIndex).FirstOrDefault();

        readonly Subscriptions _subscriptions;
        LastTransactionInfo _subscriptionLastTransaction;

        public StatusUpdate LastStatusUpdate => _subscriptions.LastStatusUpdate;

        public IList<SubscriptionInfo> GetSubscriptions() => _subscriptions.GetSubscriptions();

        public bool IsSubscribed(long accountId) => _subscriptions.IsSubscribed(accountId);

        public static Task<Status> LoadAsync(ServiceNode serviceNode)
        {
            return Task.Run(() => new Status(serviceNode));
        }

        readonly SemaphoreSlim _saveBusy = new SemaphoreSlim(1);

        public async Task SaveAsync()
        {
            await _saveBusy.WaitAsync();

            try
            {
                using (var packer = new Packer())
                {
                    Pack(packer);

                    var data = packer.ToByteArray();
                    await ServiceNode.CacheStorage.WriteFileBytesAsync(GetType().Name, data);
                }
            }
            catch
            {
                _ = SaveAsync();
            }

            _saveBusy.Release();
        }

        public Status(ServiceNode serviceNode)
        {
            UIApp.PubSub.Subscribe<ProfileDataResultEvent>(this, ProfileData);

            ServiceNode = serviceNode;

            try
            {
                var data = serviceNode.CacheStorage.ReadFileBytes(GetType().Name);
                if (data != null)
                {
                    using (var unpacker = new Unpacker(data))
                    {
                        _subscriptions = new Subscriptions(unpacker, this);
                        if(unpacker.UnpackBool())
                        {
                            _subscriptionLastTransaction = new LastTransactionInfo(unpacker);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.IgnoreException(ex);
            }

            if (_subscriptions == null)
                _subscriptions = new Subscriptions(AccountId, ChainId, this);
        }

        public void Pack(Packer packer)
        {
            packer.Pack(_subscriptions);
            if (packer.Pack(_subscriptionLastTransaction != null))
                packer.Pack(_subscriptionLastTransaction);
        }

        public async Task UpdateLastViewedTransactinInfo(SubscriptionInfo info)
        {
            if (info == null)
                return;

            if (_subscriptions.UpdateLastViewedTransactinInfo(info.AccountId, info.CurrentTransactionInfo))
                await SaveAsync();
        }

        public async Task UpdateLastViewedTransaction(Transaction transaction)
        {
            var info = _subscriptions.GetSubscriptionInfo(transaction.AccountId);
            if(info != null)
            {
                if (_subscriptions.UpdateLastViewedTransactinInfo(info.AccountId, new LastTransactionCountInfo(transaction.TransactionId, transaction.Timestamp, transaction.GetFeature<AccountIndex>(AccountIndex.FeatureId).TransactionCount)))
                    await SaveAsync();
            }
        }

        async Task ProfileData(ProfileDataResultEvent arg)
        {
            if (_subscriptions.UpdateProfile(arg.AccountId, arg.ProfileData.ProfileInfo))
                await SaveAsync();
        }

        protected async Task<HeleusClientResponse> SetSubmitAccount(SubmitAccount submitAccount, bool requiresSecretKey = false)
        {
            var serviceNode = submitAccount?.ServiceNode;
            if (serviceNode == null)
            {
                return new HeleusClientResponse(HeleusClientResultTypes.ServiceNodeMissing);
            }

            var account = submitAccount.ServiceAccount;
            if (account == null)
            {
                return new HeleusClientResponse(HeleusClientResultTypes.ServiceNodeAccountMissing);
            }

            if (requiresSecretKey)
            {
                var secretKey = submitAccount.DefaultSecretKey;
                if (secretKey == null)
                    return new HeleusClientResponse(HeleusClientResultTypes.ServiceNodeSecretKeyMissing);
            }

            if (!await serviceNode.Client.SetServiceAccount(account, string.Empty, false))
                return new HeleusClientResponse(HeleusClientResultTypes.InternalError);

            return null;
        }

        async Task<bool> QueryMissingProfiles()
        {
            var modified = false;

            var last = _subscriptions.LastStatusUpdate;
            if (last != null)
            {
                foreach (var info in last.RecentMessages)
                {
                    if (info.Profile == null)
                    {
                        info.Profile = (await ProfileManager.Current.GetProfileInfo(info.AccountId, ProfileDownloadType.QueryStoredData, false)).Profile;
                        modified = true;
                    }
                }

                foreach (var info in last.ViewedUpdates)
                {
                    if (info.Profile == null)
                    {
                        info.Profile = (await ProfileManager.Current.GetProfileInfo(info.AccountId, ProfileDownloadType.QueryStoredData, false)).Profile;
                        modified = true;
                    }
                }
            }

            return modified;
        }

        bool _statusBusy;

        public async Task DownloadLastStatus()
        {
            if (_statusBusy)
                return;
            _statusBusy = true;

            var modified = false;
            for (var i = 0; ; i++)
            {
                var batch = _subscriptions.GetBatch(i);
                if (batch == null)
                    break;

                //if (Time.PassedSeconds(batch.LastChecked) < 60)
                //    continue;

                var accounts = batch.GetSubscriptions();
                var transactionResult = await AccountIndex.DownloadLastTransactionInfoBatch(_client, ChainType.Data, ChainId, StatusServiceInfo.StatusDataChainIndex, accounts, StatusServiceInfo.MessageIndex);
                if (transactionResult != null)
                {
                    if (transactionResult.ResultType == ResultTypes.Ok)
                    {
                        modified |= _subscriptions.ProcessLastTransactionInfo(batch, transactionResult.Item);
                        modified |= await QueryMissingProfiles();
                    }
                }
                else
                {
                    //UIApp.Toast("");
                }
            }

            if (modified)
                await SaveAsync();

            _statusBusy = false;
        }

        bool _subBusy;

        public async Task QuerySubscriptions()
        {
            if (_subBusy)
                return;

            _subBusy = true;
            var lastTransactionInfo = (await Fan.DownloadFanofLastTransactionInfo(ServiceNode.Client, ChainType.Data, ServiceNode.ChainId, StatusServiceInfo.FanChainIndex, ServiceNode.AccountId))?.Item;
            if (lastTransactionInfo == null)
                goto end;

            var download = false;
            if (_subscriptionLastTransaction == null)
            {
                download = true;
            }
            else
            {
                if (lastTransactionInfo.TransactionId > _subscriptionLastTransaction.TransactionId)
                    download = true;
            }

            if (download)
            {
                var fanInfo = (await Fan.DownloadFanOf(ServiceNode.Client, ChainType.Data, ServiceNode.ChainId, StatusServiceInfo.FanChainIndex, ServiceNode.AccountId))?.Item;
                if (fanInfo == null)
                    goto end;

                _subscriptionLastTransaction = lastTransactionInfo;

                await _subscriptions.ProcessSubscriptions(fanInfo);
                await QueryMissingProfiles();

                await SaveAsync();
                //await ServiceNode.TransactionDownloadManager.UpdateLastAccess(id);
            }

        end:
            _subBusy = false;
        }

        public async Task<HeleusClientResponse> ChangeSubscription(long accountId, bool subscribe)
        {
            var response = await SetSubmitAccount(_submitAccount);
            if (response != null)
                goto end;

            var fanTransaction = new FeatureRequestDataTransaction(_submitAccount.AccountId, _submitAccount.ChainId, StatusServiceInfo.FanChainIndex);
            fanTransaction.SetFeatureRequest(new FanRequest(subscribe ? FanRequestMode.AddFanOf : FanRequestMode.RemoveFanOf , accountId));
            fanTransaction.PrivacyType = DataTransactionPrivacyType.PublicData;

            response = await _client.SendDataTransaction(fanTransaction, true);

            if (response.TransactionResult == TransactionResultTypes.Ok)
            {
                UIApp.Run(StatusApp.Current.QueryStatusNodes);
            }

            end:

            await UIApp.PubSub.PublishAsync(new SubscriptionEvent(response, accountId, subscribe));

            return response;
        }

        public async Task<HeleusClientResponse> SendMessage(string message, string link, byte[] imageData)
        {
            var response = await SetSubmitAccount(_submitAccount);
            if (response != null)
                goto end;

            if (string.IsNullOrWhiteSpace(message) || message.Length < 2)
            {
                response = new HeleusClientResponse(HeleusClientResultTypes.Ok, (long)ServiceUserCodes.InvalidStatusMessageLength);
                goto end;
            }

            if (!link.IsValdiUrl())
            {
                response = new HeleusClientResponse(HeleusClientResultTypes.Ok, (long)ServiceUserCodes.InvalidStatusLink);
                goto end;
            }

            var attachements = _client.NewAttachements(StatusServiceInfo.StatusDataChainIndex);

            var realName = (await ProfileManager.Current.GetProfileInfo(_submitAccount.AccountId, ProfileDownloadType.DownloadIfNotAvailable, false))?.Profile?.RealName;

            var json = new StatusJson { m = message, l = link }.ToJson();
            attachements.AddStringAttachement(StatusServiceInfo.StatusJsonFileName, json);

            if (imageData != null)
                attachements.AddBinaryAttachement(StatusServiceInfo.ImageFileName, imageData);

            response = await _client.UploadAttachements(attachements, (transaction) =>
            {
                transaction.PrivacyType = DataTransactionPrivacyType.PublicData;
                transaction.EnableFeature<AccountIndex>(AccountIndex.FeatureId).Index = StatusServiceInfo.MessageIndex;

                if (realName != null)
                {
                    transaction.EnableFeature<Payload>(Payload.FeatureId).PayloadData = Encoding.UTF8.GetBytes(realName);
                }
            });

        end:

            await UIApp.PubSub.PublishAsync(new MessageSentEvent(response));

            return response;
        }
    }
}
