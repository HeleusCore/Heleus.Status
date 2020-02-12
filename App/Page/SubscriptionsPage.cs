using System;
using System.Threading.Tasks;
using Heleus.Apps.Shared;

namespace Heleus.Apps.Status
{
    public class SubscriptionsPage : StackPage
    {
        readonly ServiceNodeButtonRow _serviceNode;

        public SubscriptionsPage() : base("SubscriptionsPage")
        {
            Subscribe<ServiceNodesLoadedEvent>(Loaded);
            Subscribe<ServiceAccountAuthorizedEvent>(AccountAuth);
            Subscribe<ServiceAccountImportEvent>(AccountImport);

            IsSuspendedLayout = true;
            AddTitleRow("Title");
            IsBusy = true;

            AddHeaderRow("Common.ServiceNode");
            _serviceNode = AddRow(new ServiceNodeButtonRow(this, ServiceNodesPageSelectionFlags.ActiveRequired, "sub"));
            _serviceNode.SelectionChanged = ServiceNodeChanged;
            AddInfoRow("Common.ServiceNodeInfo");
            AddFooterRow();
        }

        public override void OnOpen()
        {
            base.OnOpen();
            UIApp.Run(Update);
        }

        void UpdateServiceNode()
        {
            _serviceNode.ServiceNode = AppBase.Current.GetLastUsedServiceNode("sub");
        }

        Task AccountImport(ServiceAccountImportEvent arg)
        {
            UpdateServiceNode();
            return Task.CompletedTask;
        }

        Task AccountAuth(ServiceAccountAuthorizedEvent arg)
        {
            UpdateServiceNode();
            return Task.CompletedTask;
        }

        Task Loaded(ServiceNodesLoadedEvent arg)
        {
            UpdateServiceNode();
            return Task.CompletedTask;
        }

        async Task Update()
        {
            RemoveHeaderSection("Subscriptions");

            AddIndex = GetRow("Title");
            AddIndex = AddHeaderRow("Subscriptions");

            var count = 0;
            var status = StatusApp.Current.GetStatus(_serviceNode.ServiceNode);
            if(status != null)
            {
                var subscrptions = status.GetSubscriptions();
                foreach(var sub in subscrptions)
                {
                    var accountId = sub.AccountId;
                    var profileData = await ProfileManager.Current.GetProfileData(accountId, ProfileDownloadType.QueryStoredData, false);
                    var button = new StatusProfileButtonRow(status.ServiceNode, accountId, sub.Profile ?? profileData?.ProfileInfo, profileData, ViewProfile, false);
                    count++;

                    AddIndex = AddRow(button);
                }
            }

            if(count == 0)
            {
                AddIndex = AddInfoRow("NoSubscriptions");
            }

            AddFooterRow();

            UpdateSuspendedLayout();
        }

        async Task ViewProfile(ProfileButtonRow arg)
        {
            await StatusAccountPage.OpenStatusAccountPage(this, _serviceNode.ServiceNode, arg.AccountId, StatusAccountProfileType.Big);
        }

        Task ServiceNodeChanged(ServiceNodeButtonRow obj)
        {
            UIApp.Run(Update);
            return Task.CompletedTask;
        }

        public override async Task InitAsync()
        {
            await Update();
            IsBusy = false;
        }
    }
}
