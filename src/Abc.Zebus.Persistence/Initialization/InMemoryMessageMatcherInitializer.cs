using Abc.Zebus.Hosting;
using Abc.Zebus.Persistence.Matching;
using Abc.Zebus.Persistence.Storage;

namespace Abc.Zebus.Persistence.Initialization
{
    public class InMemoryMessageMatcherInitializer : HostInitializer
    {
        private readonly IStorage _storage;
        private readonly IInMemoryMessageMatcher _persister;

        public InMemoryMessageMatcherInitializer(IStorage storage, IInMemoryMessageMatcher persister)
        {
            _storage = storage;
            _persister = persister;
        }

        public override void BeforeStart()
        {
            base.BeforeStart();
            _storage.Start();
            _persister.Start();
        }

        public override void AfterStop()
        {
            base.AfterStop();
            _persister.Stop();
            _storage.Stop();
        }
    }
}