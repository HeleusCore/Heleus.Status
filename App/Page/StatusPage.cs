using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Xamarin.Forms;

namespace Heleus.Apps.Status
{
    public class StatusPage : StackPage
    {
        HeaderRow _recentUpdateHeader;
        ButtonRow _recentMore;
        HeaderRow _viewedUpdateHeader;
        ButtonRow _viewedMore;

        readonly List<StatusProfileButtonRow> _recentUpdateRows = new List<StatusProfileButtonRow>();
        readonly List<StatusProfileButtonRow> _viewedUpdateRows = new List<StatusProfileButtonRow>();

        void Update(SubscriptionInfo[] updates, StackRow addIndex, string headerName, ref HeaderRow header, List<StatusProfileButtonRow> rows, ref ButtonRow more, int max, bool recent)
        {
            var count = updates.Length;

            if(count == 0)
            {
                RemoveHeaderSection(header);
                rows.Clear();
                header = null;

                return;
            }

            if(header == null)
            {
                AddIndex = addIndex;
                AddIndex = header = AddHeaderRow(headerName);
                var footer = AddFooterRow();
                footer.Identifier = $"{headerName}Footer";
            }

            var modCount = Math.Min(count, max);
            var requiresMore = count >= max;
            var rowCount = rows.Count;

            if (!requiresMore && more != null)
            {
                RemoveView(more);
                more = null;
            }

            for (var i = 0; i < Math.Min(rowCount, modCount); i++)
            {
                var info = updates[i];
                var row = rows[i];
                row.Update(info.AccountId, ProfileManager.Current.GetCachedProfileData(info.AccountId), info.Profile);
                row.UpdateMessagesCount(info);

                row.Tag = info;
            }

            AddIndexBefore = false;

            var newRows = modCount - rowCount;
            if (newRows >= 0)
            {
                if (rowCount == 0)
                    AddIndex = header;
                else
                    AddIndex = rows[rowCount - 1];

                for (var i = 0; i < newRows; i++)
                {
                    var info = updates[rowCount + i];
                    var row = new StatusProfileButtonRow(info.Subscriptions.Status.ServiceNode, info.AccountId, info.Profile, ProfileManager.Current.GetCachedProfileData(info.AccountId), ViewMessages, recent);
                    row.UpdateMessagesCount(info);

                    row.Tag = info;

                    AddRow(row);
                    AddIndex = row;
                    rows.Add(row);
                }
            }
            else
            {
                for (var i = rowCount - 1; i >= modCount; i--)
                {
                    RemoveView(rows[i]);
                    rows.RemoveAt(i);
                }
            }

            if (requiresMore && more == null)
            {
                AddIndexBefore = false;
                AddIndex = rows[rows.Count - 1];
                more = AddButtonRow("More", More);
                more.RowLayout.Children.Remove(more.FontIcon);
                more.SetDetailViewIcon(Icons.AngleDoubleRight);
                more.Margin = new Thickness(20, 0, 0, 0);
            }

            if (more != null)
                more.Tag = new Tuple<bool, SubscriptionInfo[]>(recent, updates);
        }

        void Start()
        {
            if (GetRow("StartInfo") == null)
            {
                AddInfoRow("StartInfo");
                var exp = AddButtonRow("ExplorePage.Title", Explore);
                exp.SetDetailViewIcon(Icons.Search);

                var write = AddButtonRow("WritePage.Title", Write);
                write.SetDetailViewIcon(Icons.Pencil);
                AddFooterRow();
            }
        }

        async Task Write(ButtonRow arg)
        {
            await UIApp.Current.ShowPage(typeof(WritePage));
        }

        async Task Explore(ButtonRow arg)
        {
            await UIApp.Current.ShowPage(typeof(ExplorePage));
        }

        Task StatusUpdate(StatusQueryEvent arg)
        {
            if(arg.QueryEventType == StatusQueryEventType.QueryStart)
            {
                IsBusy = true;
                return Task.CompletedTask;
            }

            var recent = new List<SubscriptionInfo>();
            var viewed = new List<SubscriptionInfo>();
            foreach(var status in StatusApp.Current.GetAllStatus())
            {
                if(status.LastStatusUpdate != null)
                {
                    recent.AddRange(status.LastStatusUpdate.RecentMessages);
                    viewed.AddRange(status.LastStatusUpdate.ViewedUpdates);
                }
            }

            recent.Sort((a, b) => b.CurrentTransactionInfo.TransactionId.CompareTo(a.CurrentTransactionInfo.TransactionId));
            viewed.Sort((a, b) => b.CurrentTransactionInfo.TransactionId.CompareTo(a.CurrentTransactionInfo.TransactionId));

            Update(recent.ToArray(), GetRow("Title"), "RecentUpdates", ref _recentUpdateHeader, _recentUpdateRows, ref _recentMore, 25, true);
            Update(viewed.ToArray(), GetRow("RecentUpdatesFooter") ?? GetRow("Title"), "ViewedUpdates", ref _viewedUpdateHeader, _viewedUpdateRows, ref _viewedMore, 10, false);

            if(_recentUpdateHeader == null && _viewedUpdateHeader == null)
            {
                Start();
            }
            else
            {
                RemoveView(GetRow("StartInfo"));
                RemoveView(GetRow("ExplorePage.Title"));
                RemoveView(GetRow("WritePage.Title"));

                if (!UIAppSettings.AppReady)
                {
                    UIAppSettings.AppReady = true;
                    UIApp.Current.SaveSettings();
                }
            }

            IsBusy = false;
            UpdateSuspendedLayout();

            return Task.CompletedTask;
        }

        async Task ViewMessages(ButtonRow button)
        {
            if (button.Tag is SubscriptionInfo info)
            {
                await StatusAccountPage.OpenStatusAccountPage(this, info.Subscriptions.Status.ServiceNode, info.AccountId, StatusAccountProfileType.Small);
                await info.Subscriptions.Status.UpdateLastViewedTransactinInfo(info);
            }
        }

        async Task More(ButtonRow button)
        {
            var updates = button.Tag as Tuple<bool, SubscriptionInfo[]>;
            if (updates != null)
                await Navigation.PushAsync(new MoreUpdatesPage(updates.Item2, updates.Item1));
        }

        public StatusPage() : base("StatusPage")
        {
            Subscribe<ServiceAccountAuthorizedEvent>(AccountAuthorized);
            Subscribe<ServiceAccountImportEvent>(AccountImported);
            Subscribe<StatusQueryEvent>(StatusUpdate);

            IsSuspendedLayout = true;

            SetupPage();
        }

        Task AccountImported(ServiceAccountImportEvent arg)
        {
            var status = StatusApp.Current.GetStatus(arg.ServiceNode);
            if (status != null)
                UIApp.Run(() => ProfileManager.Current.GetProfileData(status.AccountId, ProfileDownloadType.ForceDownload, true));

            SetupPage();
            return Task.CompletedTask;
        }

        Task AccountAuthorized(ServiceAccountAuthorizedEvent arg)
        {
            var status = StatusApp.Current.GetStatus(arg.ServiceNode);
            if (status != null)
                UIApp.Run(() => ProfileManager.Current.GetProfileData(status.AccountId, ProfileDownloadType.ForceDownload, true));

            SetupPage();
            return Task.CompletedTask;
        }

        void SetupPage()
        {
            StackLayout.Children.Clear();
            _recentUpdateRows.Clear();
            _viewedUpdateRows.Clear();

            AddTitleRow("Title");

            if (!ServiceNodeManager.Current.HadUnlockedServiceNode)
            {
                AddInfoRow("StatusInfo", Tr.Get("App.FullName"));

                ServiceNodesPage.AddAuthorizeSection(this, false);
            }
            else
            {
                if (!UIAppSettings.AppReady)
                    Start();
            }

            UpdateSuspendedLayout();
        }
    }
}
