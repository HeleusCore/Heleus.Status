using System;
using Xamarin.Forms;
using Heleus.Base;
using SkiaSharp;
using Heleus.Apps.Status;
using Heleus.Network.Client;
using Heleus.StatusService;
using Heleus.ProfileService;
using System.Threading.Tasks;
#if !(GTK || CLI)
using SkiaSharp.Views.Forms;
#endif

namespace Heleus.Apps.Shared
{
    public class ViewAccountSchemeAction : ServiceNodeSchemeAction
    {
        public const string ActionName = "viewaccount";

        public readonly long AccountId;

        public override bool IsValid => base.IsValid && AccountId > 0;

        public ViewAccountSchemeAction(SchemeData schemeData) : base(schemeData)
        {
            GetLong(StartIndex, out AccountId);
        }

        public override async Task Run()
        {
            if (!IsValid)
                return;

            var serviceNode = await GetServiceNode();
            if (serviceNode == null)
                return;

            var app = UIApp.Current;
            if (app != null)
            {
                app.MainTabbedPage?.ShowPage(typeof(StatusPage));
                await StatusAccountPage.OpenStatusAccountPage(app.CurrentPage, serviceNode, AccountId, StatusAccountProfileType.Big);
            }
        }
    }

    public class ViewMessageSchemeAction : ServiceNodeSchemeAction
    {
        public const string ActionName = "viewmessage";

        public readonly long AccountId;
        public readonly long TransactionId;

        public override bool IsValid => base.IsValid && AccountId > 0 && TransactionId > 0;

        public ViewMessageSchemeAction(SchemeData schemeData) : base(schemeData)
        {
            GetLong(StartIndex, out AccountId);
            GetLong(StartIndex + 1, out TransactionId);
        }

        public override async Task Run()
        {
            if (!IsValid)
                return;

            var serviceNode = await GetServiceNode();
            if (serviceNode == null)
                return;

            var app = UIApp.Current;
            if(app != null)
            {
                app.MainTabbedPage?.ShowPage(typeof(StatusPage));
                await StatusAccountPage.OpenStatusAccountPage(app.CurrentPage, serviceNode, AccountId,  StatusAccountProfileType.Big, TransactionId);
            }
        }
    }

    partial class UIApp : Application
    {
        public static void NewContentPage(ExtContentPage contentPage)
        {
            if (IsGTK)
                return;

            if (!(contentPage is UWPMenuPage || contentPage is DesktopMenuPage))
                contentPage.EnableSkiaBackground();
        }

        public static void UpdateBackgroundCanvas(SKCanvas canvas, int width, int height)
        {
            try
            {
#if !(GTK || CLI)
                var colors = new SKColor[] { Theme.PrimaryColor.Color.ToSKColor(), Theme.SecondaryColor.Color.ToSKColor() };
                var positions = new float[] { 0.0f, 1.0f };

                var gradient = SKShader.CreateLinearGradient(new SKPoint(0, height / 2), new SKPoint(width, height / 2), colors, positions, SKShaderTileMode.Mirror);
                var paint = new SKPaint { Shader = gradient, IsAntialias = true };

                canvas.DrawPaint(paint);
#endif
            }
            catch (Exception ex)
            {
                Log.IgnoreException(ex);
            }
        }

        public static bool UIAppUsesPushNotifications = true;

        void Init()
        {
            SchemeAction.SchemeParser = (host, segments) =>
            {
                var action = string.Empty;
                var startIndex = 0;

                if (host == "heleuscore.com" && segments[1] == "status/")
                {
                    if (segments[2] == "request/")
                    {
                        action = SchemeAction.GetString(segments, 3);
                        startIndex = 4;
                    }
                }

                return new Tuple<string, int>(action, startIndex);
            };

            SchemeAction.RegisterSchemeAction<ViewMessageSchemeAction>();
            SchemeAction.RegisterSchemeAction<ViewAccountSchemeAction>();

            var sem = new ServiceNodeManager(StatusServiceInfo.ChainId, StatusServiceInfo.EndPoint, StatusServiceInfo.Version, StatusServiceInfo.Name, _currentSettings, _currentSettings, PubSub);
            _ = new ProfileManager(new ClientBase(sem.HasDebugEndPoint ? sem.DefaultEndPoint : ProfileServiceInfo.EndPoint, ProfileServiceInfo.ChainId), sem.CacheStorage, PubSub);
            StatusApp.Current.Init();

            if (IsAndroid || IsUWP || IsDesktop)
            {
                var masterDetail = new ExtMasterDetailPage();
                var navigation = new ExtNavigationPage(new StatusPage());
                MenuPage menu = null;

                if (IsAndroid)
                    menu = new AndroidMenuPage(masterDetail, navigation);
                else if (IsUWP)
                    menu = new UWPMenuPage(masterDetail, navigation);
                else if (IsDesktop)
                    menu = new DesktopMenuPage(masterDetail, navigation);

                menu.AddPage(typeof(StatusPage), "StatusPage.Title", Icons.ListUl);
                menu.AddPage(typeof(WritePage), "WritePage.Title", Icons.Pencil);
                menu.AddPage(typeof(SubscriptionsPage), "SubscriptionsPage.Title", Icons.Check);
                menu.AddPage(typeof(ExplorePage), "ExplorePage.Title", Icons.Search);
                menu.AddPage(typeof(SettingsPage), "SettingsPage.Title", Icons.Slider);

                masterDetail.Master = menu;
                masterDetail.Detail = navigation;

                MainPage = MainMasterDetailPage = masterDetail;
            }
            else if (IsIOS)
            {
                var tabbed = new ExtTabbedPage();

                tabbed.AddPage(typeof(StatusPage), "StatusPage.Title", "icons/list-ul.png");
                tabbed.AddPage(typeof(WritePage), "WritePage.Title", "icons/pencil.png");
                tabbed.AddPage(typeof(SubscriptionsPage), "SubscriptionsPage.Title", "icons/list-ul.png");
                tabbed.AddPage(typeof(ExplorePage), "ExplorePage.Title", "icons/search.png");
                tabbed.AddPage(typeof(SettingsPage), "SettingsPage.Title", "icons/sliders.png");

                MainPage = MainTabbedPage = tabbed;
            }
        }

        void Start()
        {
        }

        void Resume()
        {
        }

        void Sleep()
        {

        }

        void RestoreSettings(ChunkReader reader)
        {

        }

        void StoreSettings(ChunkWriter writer)
        {

        }
    }
}
