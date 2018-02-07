using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence.Runner.Storage
{
    public class InMemoryStorage : IStorage
    {
        private readonly Dictionary<PeerId, List<MatcherEntry>> _entriesByPeerId = new Dictionary<PeerId, List<MatcherEntry>>();

        public Task Write(IList<MatcherEntry> entriesToPersist)
        {
            foreach (var groupedEntries in entriesToPersist.GroupBy(x=>x.PeerId))
            {
                if (!_entriesByPeerId.TryGetValue(groupedEntries.Key, out var entries))
                {
                    entries = new List<MatcherEntry>();
                    _entriesByPeerId.Add(groupedEntries.Key, entries);
                }
                entries.AddRange(groupedEntries);
            }

            return Task.FromResult(0);
        }

        public IMessageReader CreateMessageReader(PeerId peerId)
        {
            return _entriesByPeerId.TryGetValue(peerId, out var entries) ? new MessageReader(entries) : null;
        }

        public void PurgeMessagesAndAcksForPeer(PeerId peerId)
        {
            _entriesByPeerId.Remove(peerId);
        }

        public void Start()
        {
        }

        public void Stop()
        {
        }

        public int PersistenceQueueSize { get; } = 0;

        private class MessageReader : IMessageReader
        {
            private readonly IEnumerable<MatcherEntry> _entriesToReplay;

            public MessageReader(IEnumerable<MatcherEntry> entriesToReplay)
            {
                _entriesToReplay = entriesToReplay;
            }

            public IEnumerable<TransportMessage> GetUnackedMessages()
            {
                return _entriesToReplay.Select(x => TransportMessageDeserializer.Deserialize(x.MessageBytes));
            }

            public void Dispose()
            {
            }
        }
    }
}