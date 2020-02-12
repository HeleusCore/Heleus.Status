using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.StatusService;

namespace Heleus.Apps.Status
{
    public class ExplorePage : StackPage
    {
        readonly ButtonRow _searchButton;
        readonly EntryRow _searchText;

        ServiceProfileSearch _profileSearch;
        readonly ServiceNodeButtonRow _serviceNode;

        public ExplorePage() : base("ExplorePage")
        {
            Subscribe<ServiceNodesLoadedEvent>(Loaded);
            Subscribe<ServiceAccountAuthorizedEvent>(AccountAuth);
            Subscribe<ServiceAccountImportEvent>(AccountImport);

            IsSuspendedLayout = true;

            AddTitleRow("Title");

            AddHeaderRow("Search");

            _searchText = AddEntryRow("", "SearchTerm");
            _searchText.SetDetailViewIcon(Icons.Coins);

            _searchButton = AddSubmitButtonRow("SearchButton", Search);
            (_searchButton.DetailView as FontIcon).Icon = Icons.Search;
            _searchButton.IsEnabled = false;

            _searchText.Edit.TextChanged += (sender, e) =>
            {
                UpdateSearch();
            };

            AddFooterRow();

            AddHeaderRow("Common.ServiceNode");
            _serviceNode = AddRow(new ServiceNodeButtonRow(this, ServiceNodesPageSelectionFlags.ActiveRequired, "explore"));
            _serviceNode.SelectionChanged = ServiceNodeChanged;
            AddInfoRow("Common.ServiceNodeInfo");
            AddFooterRow();

            UpdateSuspendedLayout();

            UIApp.Run(Update);
        }

        void UpdateServiceNode()
        {
            _serviceNode.ServiceNode = AppBase.Current.GetLastUsedServiceNode("explore");
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

        void UpdateSearch()
        {
            var sn = _serviceNode.ServiceNode;
            if (sn != null)
            {
                if (_profileSearch?.ServiceNode != sn)
                    _profileSearch = new ServiceProfileSearch(sn);
            }

            if (_profileSearch != null)
                _searchButton.IsEnabled = _profileSearch.IsValidSearchText(_searchText.Edit.Text);
            else
                _searchButton.IsEnabled = false;
        }

        async Task Update()
        {
            var serviceNode = _serviceNode.ServiceNode;
            var status = StatusApp.Current.GetStatus(serviceNode);
            if(status != null)
            {
                var result = (await serviceNode.Client.QueryDynamicServiceData<TrendingResult>(serviceNode.ChainId, "trending/result.data")).Data;
                var trending = result?.Item;

                if(trending != null)
                {
                    RemoveHeaderSection("Popular");
                    RemoveHeaderSection("Recent");
                    RemoveHeaderSection("New");

                    AddIndex = GetRow("Common.ServiceNode");
                    AddIndexBefore = true;

                    await AddList("Popular", trending.PopularAccounts);
                    await AddList("Recent", trending.RecentAccounts);
                    await AddList("New", trending.NewAccounts);

                    UpdateSuspendedLayout();
                }
            }

            AddIndex = null;
            AddIndexBefore = false;
        }

        async Task AddList(string name, IReadOnlyList<long> items)
        {
            var serviceNode = _serviceNode.ServiceNode;

            if (serviceNode != null && items.Count > 0)
            {
                AddHeaderRow(name);

                foreach (var accountId in items)
                {
                    var profileInfo = await ProfileManager.Current.GetProfileInfo(accountId, ProfileDownloadType.QueryStoredData, false);
                    var profileData = await ProfileManager.Current.GetProfileData(accountId, ProfileDownloadType.QueryStoredData, false);
                    AddRow(new StatusProfileButtonRow(serviceNode, accountId, profileInfo?.Profile, profileData, ViewProfile, false));
                }

                AddFooterRow();
            }
        }

        async Task ViewProfile(ProfileButtonRow arg)
        {
            var serviceNode = _serviceNode.ServiceNode;

            var profileData = await ProfileManager.Current.GetProfileData(arg.AccountId, ProfileDownloadType.QueryStoredData, false);
            await Navigation.PushAsync(new StatusAccountPage(profileData, serviceNode, arg.AccountId, StatusAccountProfileType.Big, 0));
        }

        Task ServiceNodeChanged(ServiceNodeButtonRow obj)
        {
            UpdateSearch();
            UIApp.Run(Update);
            return Task.CompletedTask;
        }

        async Task ProfileButton(ProfileButtonRow arg)
        {
            await StatusAccountPage.OpenStatusAccountPage(this, _serviceNode.ServiceNode, arg.AccountId, StatusAccountProfileType.Big);
        }

        async Task Search(ButtonRow arg)
        {
            if(_profileSearch != null)
            {
                IsBusy = true;
                var profiles = await _profileSearch.Search(_searchText.Edit.Text);
                IsBusy = false;

                if (profiles.Count > 0)
                    await Navigation.PushAsync(new SearchProfileResultPage(profiles, ProfileButton));
                else
                    await ErrorAsync("NotFound");
            }
        }
    }
}
