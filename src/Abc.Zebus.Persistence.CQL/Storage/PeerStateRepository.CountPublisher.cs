using System;
using System.Linq;
using Abc.Zebus.Persistence.Messages;
using Abc.Zebus.Persistence.Util;

namespace Abc.Zebus.Persistence.CQL.Storage
{
    public partial class PeerStateRepository
    {
        private DateTime _lastPublicationDate;

        public void Handle(PublishNonAckMessagesCountCommand message)
        {
            _bus.Publish(new NonAckMessagesCountChanged(_statesByPeerId.Values
                                                                    .Where(x => x.LastNonAckedMessageCountChanged > _lastPublicationDate)
                                                                    .Select(x => new NonAckMessage(x.PeerId.ToString(), x.NonAckedMessageCount))
                                                                    .ToArray()));
           
            _lastPublicationDate = SystemDateTime.UtcNow;
        }

        private void PublishMessageCountForPurgedPeer(PeerState peerState)
        {
            _bus.Publish(new NonAckMessagesCountChanged(new[] { new NonAckMessage(peerState.PeerId.ToString(), 0) }));
        }
    }
}