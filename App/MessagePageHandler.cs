using System;
using System.Collections.Generic;
using System.Linq;
using Heleus.Apps.Shared;
using Heleus.Network.Client;
using Heleus.StatusService;
using Heleus.Transactions;

namespace Heleus.Apps.Status
{
    public class MessagePageHandler
    {
        readonly HeaderRow _header;
        readonly StackPage _page;
        readonly List<StackRow> _messageRows = new List<StackRow>();

        public MessagePageHandler(StackPage page, HeaderRow header)
        {
            _page = page;
            _header = header;
        }

        StackRow AddMessageRow(TransactionDownloadData<Transaction> transaction)
        {
            var att = transaction.Transaction as AttachementDataTransaction;
            var hasImage = att.Items.Any((i) => i.Name == StatusServiceInfo.ImageFileName);
            ButtonLayoutRow row = null;

            if (!hasImage)
            {
                var view = new TextMessageView(transaction, _page);
                row = new ButtonLayoutRow(Icons.RowMore, view.Button, view);
                row.Tag = view;
            }
            else
            {
                var view = new ImageMessageView(transaction, _page);
                row = new ButtonLayoutRow(Icons.RowMore, view.Button, view);

                row.OnColorStyleChange = (colorStyle) => view.LabelFrame.ColorStyle = colorStyle;
                row.Tag = view;
            }

            //row.RowStyle = Theme.MessageRowButton;
            _page.AddView(row);
            return row;
        }

        public void Clear()
        {
            foreach(var row in _messageRows)
            {
                _page.RemoveView(row);
            }
            _messageRows.Clear();
        }

        public void HandleTransactions(TransactionDownloadResult<Transaction> download)
        {
            if (download.Transactions.Count == 0)
            {
                if (_page.GetRow("NoMessageFound") == null)
                {
                    _page.AddIndex = _header;
                    _page.AddInfoRow("NoMessageFound");
                }
            }
            else
            {
                _page.RemoveView(_page.GetRow("NoMessageFound"));

                var diff = ListDiff.Compute(_messageRows, download.Transactions, (a, b) => (a.Tag as MessageViewBase).Transaction.TransactionId == b.TransactionId);

                diff.Process(_messageRows, download.Transactions,
                (message) =>
                {
                    _page.RemoveView(message);
                    return true;
                },
                (idx, item) =>
                {
                    _page.AddIndexBefore = false;
                    if (idx == 0)
                        _page.AddIndex = _header;
                    else
                        _page.AddIndex = _messageRows[idx - 1];

                    var r = AddMessageRow(item);
                    _messageRows.Insert(idx, r);
                });

                _page.AddIndex = null;
            }
        }
    }
}
