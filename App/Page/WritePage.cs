using System;
using System.IO;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.StatusService;
using Heleus.Transactions;
using Xamarin.Forms;

namespace Heleus.Apps.Status
{
    public class WritePage : StackPage
    {
        EditorRow _message;
        ButtonRow _imageButton;
        ButtonRow _sendButton;
        ServiceNodeButtonRow _serviceNode;
        Status _status => StatusApp.Current.GetStatus(_serviceNode?.ServiceNode);

        async Task Authorize(ButtonRow button)
        {
            await UIApp.Current.ShowPage(typeof(StatusPage));
        }

        async Task SelectImage(ButtonRow button)
        {
            if (!(button.Tag is byte[] imageData))
            {
                await ImageSelectionPage.OpenImagePicker(ImageSelected);
            }
            else
            {
                var remove = Tr.Get("RemoveImage");
                var select = Tr.Get("ChooseImage");

                var result = await DisplayActionSheet("ImageAction", Tr.Get("Common.Cancel"), null, select, remove);

                if (result == remove)
                {
                    button.Tag = null;
                    var view = button.DetailView as ExtImage;
                    if (view != null)
                        view.SizeChanged -= ImgView_SizeChanged;

                    button.RemoveDetailView();
                }
                else if (result == select)
                {
                    await ImageSelectionPage.OpenImagePicker(ImageSelected);
                }
            }
        }

        async Task ImageSelected(ImageHandler img)
        {
            byte[] imageData;
            if (img.Width > StatusServiceInfo.ImageDimension || img.Height > StatusServiceInfo.ImageDimension)
            {
                using (var resize = await img.Resize(StatusServiceInfo.ImageDimension))
                {
                    imageData = await resize.Save(60);
                }
            }
            else
            {
                imageData = await img.Save(60);
            }

            _imageButton.Tag = imageData;
            _imageButton.RemoveDetailView();

            var imgView = new ExtImage { Source = ImageSource.FromStream(() => new MemoryStream(imageData)) };
            imgView.SizeChanged += ImgView_SizeChanged;

            _imageButton.SetDetailView(imgView);

            imgView.Margin = new Thickness(0);
            AbsoluteLayout.SetLayoutFlags(imgView, AbsoluteLayoutFlags.HeightProportional);
            AbsoluteLayout.SetLayoutBounds(imgView, new Rectangle(0, 0, AbsoluteLayout.AutoSize, 1));
        }

        void ImgView_SizeChanged(object sender, EventArgs e)
        {
            var img = (sender as ExtImage);
            var height = (int)img.Height;
            if(height > 0)
            {
                if ((int)img.WidthRequest != height)
                    img.WidthRequest = height;
            }
        }

        void Edit_TextChanged(object sender, TextChangedEventArgs e)
        {
            _sendButton.IsEnabled = !string.IsNullOrWhiteSpace(e.NewTextValue) && e.NewTextValue.Length >= 2;
        }

        async Task MessageSent(MessageSentEvent sentEvent)
        {
            IsBusy = false;

            var result = sentEvent.Result;

            if (result.TransactionResult == TransactionResultTypes.Ok)
            {
                await MessageAsync("MessageSent");

                _message.Edit.Text = string.Empty;
                _imageButton.Tag = null;
                _imageButton.RemoveDetailView();
                _sendButton.IsEnabled = false;
            }
            else
            {
                await ErrorTextAsync(result.GetErrorMessage());
            }
        }

        async Task SendMessage(ButtonRow button)
        {
            var status = _status;

            if (status != null)
            {
                if (await ConfirmAsync("ConfirmSend"))
                {
                    IsBusy = true;
                    UIApp.Run(() => _status.SendMessage(_message.Edit.Text, null, _imageButton.Tag as byte[]));
                }
            }
        }

        async Task Messages(ButtonRow arg)
        {
            var status = _status;
            if (status != null)
                await StatusAccountPage.OpenStatusAccountPage(this, status.ServiceNode, status.AccountId, StatusAccountProfileType.None);
        }

        Task ProfileDownloaded(ProfileDataResultEvent profileEvent)
        {
            var status = _status;
            if (_status != null)
            {
                if (profileEvent.AccountId == status.AccountId)
                    ProfilePageSections.UpdateProfileSections(this, profileEvent.ProfileData);
            }
            return Task.CompletedTask;
        }

        Task Resume(ResumeEvent arg)
        {
            if (_serviceNode != null && _serviceNode.ServiceNode == null)
            {
                _serviceNode.ServiceNode = StatusApp.Current.GetLastUsedServiceNode("me");
                SetupPage();
            }

            return Task.CompletedTask;
        }

        Task AccountImported(ServiceAccountImportEvent arg)
        {
            var status = StatusApp.Current.GetStatus(arg.ServiceNode);
            if (status != null)
            {
                if (_serviceNode != null && _serviceNode.ServiceNode == null)
                    _serviceNode.ServiceNode = status.ServiceNode;
                UIApp.Run(() => ProfileManager.Current.GetProfileData(status.AccountId, ProfileDownloadType.ForceDownload, true));
            }

            SetupPage();
            return Task.CompletedTask;
        }

        Task AccountAuthorized(ServiceAccountAuthorizedEvent arg)
        {
            var status = StatusApp.Current.GetStatus(arg.ServiceNode);
            if (status != null)
            {
                if(_serviceNode != null && _serviceNode.ServiceNode == null)
                    _serviceNode.ServiceNode = status.ServiceNode;
                UIApp.Run(() => ProfileManager.Current.GetProfileData(status.AccountId, ProfileDownloadType.ForceDownload, true));
            }

            SetupPage();
            return Task.CompletedTask;
        }

        Task Loaded(ServiceNodesLoadedEvent arg)
        {
            if (_serviceNode != null && _serviceNode.ServiceNode == null)
                _serviceNode.ServiceNode = AppBase.Current.GetLastUsedServiceNode("me");
            return Task.CompletedTask;
        }

        public WritePage() : base("WritePage")
        {
            Subscribe<MessageSentEvent>(MessageSent);
            Subscribe<ResumeEvent>(Resume);
            Subscribe<ProfileDataResultEvent>(ProfileDownloaded);
            Subscribe<ServiceAccountAuthorizedEvent>(AccountAuthorized);
            Subscribe<ServiceAccountImportEvent>(AccountImported);
            Subscribe<ServiceNodesLoadedEvent>(Loaded);

            SetupPage();
        }

        public override void OnOpen()
        {
            var status = _status;
            if(status != null)
            {
                var profileData = ProfileManager.Current.GetCachedProfileData(status.AccountId);
                if (profileData == null)
                {
                    UIApp.Run(() => ProfileManager.Current.GetProfileData(status.AccountId, ProfileDownloadType.ForceDownload, true));
                }
                else
                {
                    ProfilePageSections.UpdateProfileSections(this, profileData);
                }
            }
        }

        Task ServiceNodeChanged(ServiceNodeButtonRow obj)
        {
            ProfilePageSections.UpdateProfileSections(this, null);
            OnOpen();

            return Task.CompletedTask;
        }

        void SetupPage()
        {
            StackLayout.Children.Clear();

            AddTitleRow("Title");

            if (!ServiceNodeManager.Current.HadUnlockedServiceNode)
            {
                AddHeaderRow("Auth");
                AddButtonRow("Authorize", Authorize);
                AddInfoRow("AutorhizeInfo");
                AddFooterRow();
            }
            else
            {
                AddHeaderRow("NewMessage");
                _message = AddEditorRow("", "Message");
                _message.Edit.TextChanged += Edit_TextChanged;
                _imageButton = AddButtonRow("ChooseImage", SelectImage);
                AddFooterRow();

                _sendButton = AddSubmitRow("SendMessage", SendMessage);
                _sendButton.IsEnabled = false;


                AddHeaderRow("Misc");
                AddButtonRow("Messages", Messages);
                AddButtonRow("Profile", Profile);
                if (UIApp.CanShare)
                    AddButtonRow("Share", Share);
                AddButtonRow("Copy", Copy);
                AddFooterRow();


                AddHeaderRow("Common.ServiceNode");
                _serviceNode = AddRow(new ServiceNodeButtonRow(this, ServiceNodesPageSelectionFlags.ActiveRequired | ServiceNodesPageSelectionFlags.UnlockedAccountRequired, "me"));
                _serviceNode.SelectionChanged = ServiceNodeChanged;
                AddInfoRow("Common.ServiceNodeInfo");
                AddFooterRow();
            }
        }

        Task Copy(ButtonRow arg)
        {
            var sn = _serviceNode.ServiceNode;
            if (sn != null)
            {
                var code = StatusApp.Current.GetRequestCode(sn, StatusServiceInfo.StatusDataChainIndex, ViewAccountSchemeAction.ActionName, sn.AccountId);
                UIApp.CopyToClipboard(code);
                UIApp.Toast(Tr.Get("Common.CopiedToClipboard"));
            }

            return Task.CompletedTask;
        }

        Task Share(ButtonRow arg)
        {
            var sn = _serviceNode.ServiceNode;
            if (sn != null)
            {
                var code = StatusApp.Current.GetRequestCode(sn, StatusServiceInfo.StatusDataChainIndex, ViewAccountSchemeAction.ActionName, sn.AccountId);
                UIApp.Share(code);
            }

            return Task.CompletedTask;
        }

        async Task Profile(ButtonRow arg)
        {
            var sn = _serviceNode.ServiceNode;
            if(sn != null)
            {
                await Navigation.PushAsync(new ViewProfilePage(sn.AccountId, true));
            }
        }
    }
}
