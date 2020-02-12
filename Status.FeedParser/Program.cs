using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CodeHollow.FeedReader;
using Heleus.Cryptography;
using Heleus.Messages;
using Heleus.Network.Client;
using Heleus.Operations;
using Heleus.StatusService;
using Heleus.Transactions;
using Heleus.Transactions.Features;
using Newtonsoft.Json;

namespace Status.FeedParser
{
    public class FeedProcessor
    {
        readonly string _url;
        readonly HeleusClient _client;
        readonly List<string> _history = new List<string>();

        public FeedProcessor(string url, HeleusClient client)
        {
            _url = url;
            _client = client;
        }

        public async Task CheckFeed()
        {
            var feed = await FeedReader.ReadAsync(_url);

            if (_history.Count == 0)
            {
                foreach (var item in feed.Items)
                {
                    _history.Add(item.Link);
                }

                if (_history.Count > 0)
                    _history.RemoveAt(0);
            }

            var newItems = new List<FeedItem>();
            foreach (var item in feed.Items)
            {
                var link = item.Link;
                if (!_history.Contains(link))
                {
                    _history.Insert(newItems.Count, link);
                    newItems.Insert(0, item);
                }
            }

            while (_history.Count > 250)
                _history.RemoveAt(_history.Count - 1);

            var tasks = new List<Task>();
            foreach(var newItem in newItems)
            {
                tasks.Add(SendMessage(newItem));
            }

            await Task.WhenAll(tasks);

            await _client.CloseConnection(DisconnectReasons.Graceful);
        }

        async Task SendMessage(FeedItem item)
        {
            var tries = 1;

            while(true)
            {
                var result = await SendMessage(item.Description, item.Link, null);
                if (result.ResultType != HeleusClientResultTypes.Ok)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5 * tries));
                    tries++;
                    if (tries > 5)
                        break;
                }
                else
                {
                    break;
                }
            }
        }

        async Task<HeleusClientResponse> SendMessage(string message, string link, byte[] imageData)
        {
            HeleusClientResponse response;

            if (string.IsNullOrWhiteSpace(message) || message.Length < 2)
            {
                response = new HeleusClientResponse(HeleusClientResultTypes.Ok, (long)ServiceUserCodes.InvalidStatusMessageLength);
                goto end;
            }

            message = message.Trim();
            var idx = message.IndexOf('<');
            if (idx > 0)
                message = message.Substring(0, idx);

            if (!link.IsValdiUrl())
            {
                response = new HeleusClientResponse(HeleusClientResultTypes.Ok, (long)ServiceUserCodes.InvalidStatusLink);
                goto end;
            }

            var attachements = _client.NewAttachements();

            var json = JsonConvert.SerializeObject(new StatusJson { m = message, l = link });
            attachements.AddStringAttachement(StatusServiceInfo.StatusJsonFileName, json);

            if (imageData != null)
                attachements.AddBinaryAttachement(StatusServiceInfo.ImageFileName, imageData);

            response = await _client.UploadAttachements(attachements, (transaction) =>
            {
                transaction.PrivacyType = DataTransactionPrivacyType.PublicData;
                transaction.EnableFeature<AccountIndex>(AccountIndex.FeatureId).Index = StatusServiceInfo.MessageIndex;
            });

            var transactionId = Operation.InvalidTransactionId;
            if (response.Transaction != null)
                transactionId = response.Transaction.OperationId;

            Console.WriteLine($"SendMessage Transaction Result: {response.TransactionResult}, Client Response: {response.ResultType}, TransactionId : {transactionId}, \"{message}\"");

        end:

            return response;
        }
    }

    public class Config
    {
        public string nodeurl;
        public string account;
        public string password;

        public string feedurl;
    }

    public class Program
    {
        static int Main(string[] args)
        {
            return Task.Run(async () =>
            {
                try
                {
                    var file = new FileInfo(args[0]);
                    if(file.Exists)
                    {
                        var json = await File.ReadAllTextAsync(file.FullName);
                        var config = JsonConvert.DeserializeObject<Config>(json);

                        if (config != null)
                        {
                            var keyStore = KeyStore.Restore<PublicSignedKeyStore>(config.account);
                            if (await keyStore.DecryptKeyAsync(config.password, true))
                            {
                                var client = new HeleusClient(new Uri(config.nodeurl));
                                if (await client.SetServiceAccount(keyStore, config.password, true))
                                {
                                    // test
                                    await FeedReader.ReadAsync(config.feedurl);
                                    var processor = new FeedProcessor(config.feedurl, client);

                                    while (true)
                                    {
                                        await processor.CheckFeed();
                                        await Task.Delay(TimeSpan.FromMinutes(1));
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        Console.WriteLine($"File does not exist {file.FullName}.");
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }

                return 0;
            }).Result;
        }
    }
}
