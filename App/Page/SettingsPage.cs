using System;

namespace Heleus.Apps.Shared
{
    class SettingsPage : SettingsPageBase
    {
        public SettingsPage()
        {
            AddTitleRow("Title");

            AddHeaderRow().Label.Text = Tr.Get("App.FullName");

            AddButtonRow(ServiceNodesPage.PageTitle, async (button) =>
            {
                if (!ServiceNodeManager.Current.Ready)
                {
                    await MessageAsync("ServiceNodeManagerNotReady");
                    return;
                }
                await Navigation.PushAsync(new ServiceNodesPage());
            }).SetDetailViewIcon(ServiceNodesPage.PageIcon);

            AddButtonRow(HandleRequestPage.HandleRequestTranslation, async (button) =>
            {
                await Navigation.PushAsync(new HandleRequestPage());
            }).SetDetailViewIcon(HandleRequestPage.HandleRequestIcon);

            AddButtonRow("About", async (button) =>
            {
                await Navigation.PushAsync(new AboutPage());
            }).SetDetailViewIcon(Icons.Info);

            AddFooterRow();

            AddAppInfoSection(AppInfoType.Status);

            AddPushNotificationSection();

            AddThemeSection();
            //AddNotificationSection();
#if DEBUG
            AddButtonRow("Icons", async (button) =>
            {
                await Navigation.PushAsync(new IconsPage());
            });
#endif
        }
    }
}
