using System;
using System.Linq;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.Network.Client;
using Heleus.StatusService;
using Heleus.Transactions;

namespace Heleus.Apps.Status
{
    public enum StatusAccountProfileType
    {
        None,
        Small,
        Big
    }

    public class StatusAccountPage : StackPage
    {
        readonly AccountIndexTransactionDownload _messagesDownload;
        readonly ServiceNode _serviceNode;
        readonly Status _status;
        readonly long _accountId;
        readonly Chain.Index _accountIndex;
        MessagePageHandler _messageHandler;
        readonly SwitchRow _subscribe;
        readonly SwitchRow _notification;
        readonly StatusAccountProfileType _profileType;
        readonly long _transactionId;

        public static async Task OpenStatusAccountPage(ExtContentPage page, ServiceNode serviceNode, long accountId, StatusAccountProfileType profileType, long transactionId = 0)
        {
            var profile = await ProfileManager.Current.GetProfileData(accountId, ProfileDownloadType.QueryStoredData, false);
            await page.Navigation.PushAsync(new StatusAccountPage(profile, serviceNode, accountId, profileType, transactionId));
        }

        Task ProfileData(ProfileDataResultEvent arg)
        {
            if (arg.AccountId == _accountId && _profileType == StatusAccountProfileType.Big)
            {
                var profileData = arg.ProfileData;
                if (profileData.ProfileInfoResult == ProfileDownloadResult.Available)
                {
                    if (ProfilePageSections.HasProfileSections(this))
                    {
                        if (ProfilePageSections.UpdateProfileSections(this, arg.ProfileData))
                            UpdateSuspendedLayout();
                    }
                    else
                    {
                        AddIndex = GetRow("Title");
                        ProfilePageSections.AddProfileSections(this, arg.ProfileData, "Profile", _profileType == StatusAccountProfileType.Big );
                        UpdateSuspendedLayout();
                    }
                }
            }

            return Task.CompletedTask;
        }

        public StatusAccountPage(ProfileDataResult profileData, ServiceNode serviceNode, long accountId, StatusAccountProfileType profileType, long transactionId) : base("StatusAccountPage")
        {
            Subscribe<SubscriptionEvent>(Subscription);
            Subscribe<ProfileDataResultEvent>(ProfileData);

            StackLayout.Suspended = true;

            _transactionId = transactionId;
            _profileType = profileType;
            _serviceNode = serviceNode;
            _status = StatusApp.Current.GetStatus(serviceNode);
            _accountId = accountId;
            _accountIndex = Chain.Index.New().Add(_accountId).Build();

            _messagesDownload = new AccountIndexTransactionDownload(accountId, StatusServiceInfo.MessageIndex, serviceNode.GetTransactionDownloadManager(StatusServiceInfo.StatusDataChainIndex))
            {
                Count = 10
            };

            AddTitleRow("Title");

            if (profileType == StatusAccountProfileType.Small || transactionId > 0)
            {
                var row = new StatusProfileButtonRow(serviceNode, _accountId, profileData?.ProfileInfo, profileData, async (button) =>
                {
                    await Navigation.PushAsync(new StatusAccountPage(profileData, serviceNode, accountId, StatusAccountProfileType.Big, transactionId));
                }, false);

                AddRow(row);
            }

            if (profileType == StatusAccountProfileType.Big && transactionId <= 0)
            {
                if (_status != null)
                {
                    AddHeaderRow("Subscription");

                    _subscribe = AddSwitchRow("Subscribe");
                    _subscribe.Switch.IsToggled = _status.IsSubscribed(_accountId);
                    _subscribe.Switch.ToggledAsync = Subscribe_Toggled;
                    _subscribe.SetDetailViewIcon(Icons.Check);

                    _notification = AddSwitchRow("Notification");
                    _notification.Switch.IsToggled = UIApp.Current.IsPushChannelSubscribed(_accountIndex);
                    _notification.Switch.ToggledAsync = Notification_Toggled;
                    _notification.SetDetailViewIcon(Icons.Bell);

                    AddFooterRow();
                }

                if (profileData == null || profileType == StatusAccountProfileType.Big)
                    UIApp.Run(() => ProfileManager.Current.GetProfileData(_accountId, ProfileDownloadType.ForceDownload, true));
            }

            IsBusy = true;
        }

        public override async Task InitAsync()
        {
            if (_transactionId > 0)
            {
                var transaction = (await _messagesDownload.TransactionManager.DownloadTransaction(_transactionId));
                var trh = AddHeaderRow("Message");
                var mh = new MessagePageHandler(this, trh);
                mh.HandleTransactions(transaction);
                AddFooterRow();
            }
            else
            {
                var result = await _messagesDownload.DownloadTransactions();
                var header = AddHeaderRow("LatestMessages");

                _messageHandler = new MessagePageHandler(this, header);
                AddFooterRow();

                if (result.Ok)
                {
                    foreach (var t in result.Transactions)
                        t.Tag = _serviceNode;

                    var last = result.Transactions.FirstOrDefault();
                    if (_status != null && last != null)
                        await _status?.UpdateLastViewedTransaction(last.Transaction);
                }
                _messageHandler.HandleTransactions(result);
            }

            UpdateSuspendedLayout();

            IsBusy = false;
        }

        async Task Subscription(SubscriptionEvent arg)
        {
            if (arg.AccountId == _accountId && _status != null)
            {
                IsBusy = false;
                var result = arg.Result;

                if (result.TransactionResult == TransactionResultTypes.Ok)
                {
                    await MessageAsync("SubscriptionChanged");
                }
                else
                {
                    _subscribe.Switch.IgnoreNextToggle = true;
                    _subscribe.Switch.IsToggled = _status.IsSubscribed(_accountId);

                    await ErrorTextAsync(result.GetErrorMessage());
                }
            }
        }

        async Task Subscribe_Toggled(ExtSwitch swtch)
        {
            var subscribe = swtch.IsToggled;
            if(await ConfirmAsync(subscribe ? "ConfirmSubscribe" : "ConfirmUnsubscribe"))
            {
                IsBusy = true;
                UIApp.Run(() => _status.ChangeSubscription(_accountId, subscribe));
            }
            else
            {
                swtch.SetToogle(!subscribe);
            }
        }

        async Task Notification_Toggled(ExtSwitch @switch)
        {
            var notify = @switch.IsToggled;
            if(await ConfirmAsync(notify ? "EnableNotify" : "DisableNotify"))
            {
                IsBusy = true;
                if(await UIApp.Current.ChangePushChannelSubscription(this, _accountIndex))
                {
                    await MessageAsync("NotifyChanged");
                }
                @switch.SetToogle(UIApp.Current.IsPushChannelSubscribed(_accountIndex));

                IsBusy = false;
            }
        }
    }
}
