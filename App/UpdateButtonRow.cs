using System;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.ProfileService;
using Xamarin.Forms;

namespace Heleus.Apps.Status
{
    public class StatusProfileButtonRow : ProfileButtonRow
    {
        readonly ExtLabel _countLabel = new ExtLabel();

        public void UpdateMessagesCount(SubscriptionInfo info)
        {
            if (_countLabel != null)
            {
                var diff = info.CurrentTransactionInfo.Count - info.LastViewedTransactionInfo.Count;
                var text = diff <= 0 ? null : $"+{diff}";
                if (_countLabel.Text != text)
                    _countLabel.Text = text;

                RowLayout.SetAccentColor(info.Subscriptions.Status.ServiceNode.AccentColor);
            }
        }

        public StatusProfileButtonRow(ServiceNode serviceNode, long accountId, ProfileInfo profileInfo, ProfileDataResult profileData, Func<ProfileButtonRow, Task> action, bool showCount) : base(accountId, profileInfo, profileData, action, AccentColorExtenstion.DefaultAccentColorWith)
        {
            if (showCount)
            {
                RowLayout.Children.Remove(FontIcon);

                _countLabel.InputTransparent = true;
                _countLabel.FontStyle = Theme.DetailFont;
                _countLabel.ColorStyle = Theme.TextColor;

                _countLabel.Margin = new Thickness(0, 0, 10, 0);

                AbsoluteLayout.SetLayoutFlags(_countLabel, AbsoluteLayoutFlags.PositionProportional);
                RowLayout.Children.Add(_countLabel, new Point(1, 0.5));
            }

            RowLayout.SetAccentColor(serviceNode.AccentColor);
        }
    }
}
