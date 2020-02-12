using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Heleus.Apps.Shared;
using Heleus.Base;
using Heleus.StatusService;

namespace Heleus.Apps.Status
{
    public static class IPackableExtenstion
    {
        public static byte[] ToByteArray(this IPackable packable)
        {
            using (var packer = new Packer())
            {
                packable.Pack(packer);
                return packer.ToByteArray();
            }
        }
    }

    public enum StatusQueryEventType
    {
        QueryStart,
        QueryEnd
    }

    public class StatusQueryEvent
    {
        public readonly StatusQueryEventType QueryEventType;

        public StatusQueryEvent(StatusQueryEventType queryEventType)
        {
            QueryEventType = queryEventType;
        }
    }

    public class StatusApp : AppBase<StatusApp>
    {
        readonly Dictionary<string, Status> _status = new Dictionary<string, Status>();

        public override void Init()
        {
            base.Init();
        }

        protected override async Task ServiceNodesLoaded(ServiceNodesLoadedEvent arg)
        {
            await base.ServiceNodesLoaded(arg);
            await UIApp.Current.SetFinishedLoading();

            UIApp.Run(Loop);
        }

        async Task Loop()
        {
            while(true)
            {
                await QueryStatusNodes();
                await Task.Delay(TimeSpan.FromMinutes(1));
            }
        }

        public Status GetStatus(ServiceNode serviceNode)
        {
            if (serviceNode == null)
                return null;

            if (serviceNode.Active)
            {
                if (serviceNode.HasUnlockedServiceAccount && serviceNode.Active)
                {
                    if (!_status.TryGetValue(serviceNode.Id, out var todo))
                    {
                        todo = new Status(serviceNode);
                        _status[serviceNode.Id] = todo;
                    }

                    return todo;
                }
            }

            return null;
        }

        public List<Status> GetAllStatus()
        {
            var result = new List<Status>();

            foreach (var serviceNode in ServiceNodeManager.Current.ServiceNodes)
            {
                var status = GetStatus(serviceNode);
                if (status != null)
                    result.Add(status);
            }

            return result;
        }

        public async Task QueryStatusNodes()
        {
            UIApp.PubSub.Publish(new StatusQueryEvent(StatusQueryEventType.QueryStart));

            foreach (var serviceNode in ServiceNodeManager.Current.ServiceNodes)
            {
                var status = GetStatus(serviceNode);
                if (status != null)
                {
                    await status.QuerySubscriptions();
                    var cached = ProfileManager.Current.GetCachedProfileData(serviceNode.AccountId);
                    if (cached == null)
                        await ProfileManager.Current.GetProfileData(serviceNode.AccountId, ProfileDownloadType.DownloadIfNotAvailable, false);
                }
            }

            foreach (var serviceNode in ServiceNodeManager.Current.ServiceNodes)
            {
                var status = GetStatus(serviceNode);
                if (status != null)
                    await status.DownloadLastStatus();
            }

            UIApp.PubSub.Publish(new StatusQueryEvent(StatusQueryEventType.QueryEnd));
        }

        protected override Task AccountAuthorized(ServiceAccountAuthorizedEvent evt)
        {
            UIApp.Run(QueryStatusNodes);
            return base.AccountAuthorized(evt);
        }

        protected override Task AccountImport(ServiceAccountImportEvent evt)
        {
            UIApp.Run(QueryStatusNodes);
            return base.AccountImport(evt);
        }

        public override void UpdateSubmitAccounts()
        {
            var index = StatusServiceInfo.StatusIndex;

            foreach (var serviceNode in ServiceNodeManager.Current.ServiceNodes)
            {
                foreach (var serviceAccount in serviceNode.ServiceAccounts.Values)
                {
                    var keyIndex = serviceAccount.KeyIndex;

                    if (!serviceNode.HasSubmitAccount(keyIndex, index))
                    {
                        serviceNode.AddSubmitAccount(new SubmitAccount(serviceNode, keyIndex, index, true));
                    }
                }
            }
        }

        public override ServiceNode GetLastUsedServiceNode(string key = "default")
        {
            var node = base.GetLastUsedServiceNode(key);
            if (node != null)
                return node;

            return ServiceNodeManager.Current.FirstServiceNode;
        }

        public override T GetLastUsedSubmitAccount<T>(string key = "default")
        {
            var account = base.GetLastUsedSubmitAccount<T>(key);
            if (account != null)
                return account;

            var node = GetLastUsedServiceNode();
            if (node != null)
                return node.GetSubmitAccounts<T>().FirstOrDefault();

            return null;
        }
    }
}
