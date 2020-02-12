using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.Base;
using Heleus.Network.Client;
using Heleus.StatusService;
using Heleus.Transactions;
using TinyJson;
using Xamarin.Forms;

namespace Heleus.Apps.Status
{
    public abstract class MessageViewBase : AbsoluteLayout
    {
        public readonly TransactionDownloadData<Transaction> Transaction;
        public readonly ExtContentPage Page;

        protected readonly ExtLabel TextLabel = new ExtLabel();

        string _link;
        protected bool HasAllAttachements => Transaction.AttachementsState == TransactionAttachementsState.Ok;

        bool _downloading;

        protected MessageViewBase(ExtContentPage page, TransactionDownloadData<Transaction> transaction)
        {
            Transaction = transaction;
            Page = page;

            TextLabel.InputTransparent = true;
            //TextLabel.FontStyle = Theme.TextFont;
            //TextLabel.ColorStyle = Theme.TextColor;
            TextLabel.Margin = new Thickness(15, 10, 15, 10);

            SizeChanged += LayoutChangedd;
            LayoutChanged += LayoutChangedd;
        }

        void LayoutChangedd(object sender, EventArgs e)
        {
            Layouted((int)Width);
        }

        protected (string, string) GetMessage()
        {
            if (HasAllAttachements)
            {
                try
                {
                    var jsonData = Transaction.GetAttachementData(StatusServiceInfo.StatusJsonFileName);
                    var json = Encoding.UTF8.GetString(jsonData).FromJson<StatusJson>();

                    return (json.m, json.l);
                }
                catch { }
            }

            return (null, null);
        }

        protected byte[] GetImage()
        {
            if (HasAllAttachements)
            {
                try
                {
                    return Transaction.GetAttachementData(StatusServiceInfo.ImageFileName);
                }
                catch { }
            }

            return null;
        }

        public async Task Button(ButtonLayoutRow button)
        {
            var cancel = Tr.Get("Common.Cancel");

            var download = Tr.Get("Common.MessageDownload");
            var link = Tr.Get("Common.MessageLink");
            var share = Tr.Get("Common.Share");
            var copy = Tr.Get("Common.CopyShareLink");
            var items = new List<string>();

            if (!string.IsNullOrWhiteSpace(_link))
                items.Add(link);

            if (!HasAllAttachements)
                items.Add(download);

            if (UIApp.CanShare)
                items.Add(share);
            items.Add(copy);
            if (items.Count == 0)
                return;

            var result = await Page.DisplayActionSheet(null, cancel, null, items.ToArray());
            if(result == download)
            {
                await Transaction.TransactionManager.DownloadTransactionAttachement(Transaction);
                if (!HasAllAttachements)
                    await Page.ErrorAsync("MessageDownloadFailed");
                Update();
            }
            else if (result == link)
            {
                UIApp.OpenUrl(new Uri(_link));
            }
            else if (result == share)
            {
                UIApp.Share(StatusApp.Current.GetRequestCode(Transaction.Tag as ServiceNode, StatusServiceInfo.StatusDataChainIndex, ViewMessageSchemeAction.ActionName, Transaction.Transaction.AccountId, Transaction.Transaction.TransactionId));
            }
            else if (result == copy)
            {
                UIApp.CopyToClipboard(StatusApp.Current.GetRequestCode(Transaction.Tag as ServiceNode, StatusServiceInfo.StatusDataChainIndex, ViewMessageSchemeAction.ActionName, Transaction.Transaction.AccountId, Transaction.Transaction.TransactionId));
                UIApp.Toast(Tr.Get("Common.CopiedToClipboard"));
            }
        }

        protected virtual void Layouted(int width)
        {
            if (width > 0)
            {
                if ((int)TextLabel.WidthRequest != (width - 30))
                    TextLabel.WidthRequest = width - 30;
            }
        }

        protected virtual void Update()
        {
            (var text, var link) = GetMessage();
            _link = link;

            text = text ?? Tr.Get("Common.MessageMissing");

            var message = new Span
            {
                Text = text
            };
            message.SetStyle(Theme.TextFont, Theme.TextColor);

            var formattedString = new FormattedString
            {
                Spans =
                {
                    message
                }
            };

            if (HasAllAttachements)
            {
                var date = new Span
                {
                    Text = (_link != null) ? $"\n {Time.DateTimeString(Transaction.Transaction.Timestamp)}, {_link}" : $"\n {Time.DateTimeString(Transaction.Transaction.Timestamp)}"
                };
                date.SetStyle(Theme.DetailFont, Theme.TextColor);

                formattedString.Spans.Add(date);
            }

            TextLabel.FormattedText = formattedString;
        }

        protected async Task<bool> DownloadAttachements()
        {
            if (HasAllAttachements)
                return true;

            if (_downloading)
                return false;
            _downloading = true;

            await Transaction.TransactionManager.DownloadTransactionAttachement(Transaction);

            _downloading = false;
            return HasAllAttachements;
        }
    }

    public sealed class TextMessageView : MessageViewBase
    {
        public TextMessageView(TransactionDownloadData<Transaction> transaction, ExtContentPage page) : base(page, transaction)
        {
            Update();

            Children.Add(TextLabel);
        }
    }

    public sealed class ImageMessageView : MessageViewBase
    {
        public readonly ExtImage Image = new ExtImage();
        public readonly PointerFrame LabelFrame = new PointerFrame();

        public ImageMessageView(TransactionDownloadData<Transaction> transaction, ExtContentPage page) : base(page, transaction)
        {
            //LabelFrame.ColorStyle = Theme.MessageRowColor;
            LabelFrame.Content = TextLabel;
            LabelFrame.InputTransparent = true;

            Image.Aspect = Aspect.Fill;
            Image.InputTransparent = true;

            AbsoluteLayout.SetLayoutFlags(LabelFrame, AbsoluteLayoutFlags.WidthProportional | AbsoluteLayoutFlags.YProportional);
            AbsoluteLayout.SetLayoutBounds(LabelFrame, new Rectangle(0, 1, 1, AbsoluteLayout.AutoSize));

            AbsoluteLayout.SetLayoutFlags(Image, AbsoluteLayoutFlags.SizeProportional);
            AbsoluteLayout.SetLayoutBounds(Image, new Rectangle(0, 0, 1, 1));

            Update();

            Children.Add(Image);
            Children.Add(LabelFrame);
        }

        protected override void Update()
        {
            base.Update();

            var image = GetImage();
            if (image != null)
                Image.Source = ImageSource.FromStream(() => new MemoryStream(image));
        }

        protected override void Layouted(int width)
        {
            base.Layouted(width);

            if (width > 0)
            {
                if ((int)Image.WidthRequest != width)
                    Image.WidthRequest = width;
                if ((int)Image.HeightRequest != width)
                    Image.HeightRequest = width;
            }
        }
    }
}
