using System.Collections.Generic;
using System.Threading.Tasks;
using Heleus.Apps.Shared;

namespace Heleus.Apps.Status
{
    public class MoreUpdatesPage : StackPage
    {
        readonly List<StatusProfileButtonRow> _updates = new List<StatusProfileButtonRow>();

        async Task ViewMessages(ProfileButtonRow button)
        {
            if (button.Tag is SubscriptionInfo info)
            {
                await StatusAccountPage.OpenStatusAccountPage(this, info.Subscriptions.Status.ServiceNode, info.AccountId, StatusAccountProfileType.Small);
                await info.Subscriptions.Status.UpdateLastViewedTransactinInfo(info);
                (button as StatusProfileButtonRow)?.UpdateMessagesCount(info);
            }
        }

        public MoreUpdatesPage(SubscriptionInfo[] updates, bool recent) : base("MoreUpdatesPage")
        {
            Subscribe<ProfileDataResultEvent>(UpdateProfileData);

            AddTitleRow("Title");

            if (recent)
                AddHeaderRow("StatusPage.RecentUpdates");
            else
                AddHeaderRow("StatusPage.ViewedUpdates");

            foreach (var info in updates)
            {
                var row = new StatusProfileButtonRow(info.Subscriptions.Status.ServiceNode, info.AccountId, info.Profile, ProfileManager.Current.GetCachedProfileData(info.AccountId), ViewMessages, recent);
                row.UpdateMessagesCount(info);

                row.Tag = info;

                _updates.Add(row);

                AddRow(row);
            }

            AddFooterRow();

            UIApp.Run(() => QueryProfileData(updates, updates.Length));
        }

        async Task UpdateProfileData(ProfileDataResultEvent arg)
        {
            foreach (var row in _updates)
            {
                var info = row.Tag as SubscriptionInfo;
                if (info.AccountId == arg.AccountId)
                {
                    var profileData = await ProfileManager.Current.GetProfileData(info.AccountId, ProfileDownloadType.DownloadIfNotAvailable, false);
                    ProfilePageSections.UpdateProfileRow(row, info.AccountId, profileData, info.Profile);
                    row.Tag = info;

                    break;
                }
            }
        }

        async Task QueryProfileData(SubscriptionInfo[] updates, int count)
        {
            for (var i = 0; i < count; i++)
            {
                var info = updates[i];
                await ProfileManager.Current.GetProfileData(info.AccountId, ProfileDownloadType.DownloadIfNotAvailable, true);
            }
        }
    }
}
