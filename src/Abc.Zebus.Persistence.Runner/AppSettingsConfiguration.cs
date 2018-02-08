using System;

namespace Abc.Zebus.Persistence.Runner
{
    internal class AppSettingsConfiguration : IBusConfiguration, IPersistenceConfiguration
    {
        public AppSettingsConfiguration()
        {
            DirectoryServiceEndPoints = AppSettings.GetArray("Bus.Directory.EndPoints");
            RegistrationTimeout = AppSettings.Get("Bus.Directory.RegistrationTimeout", TimeSpan.FromSeconds(30));
            StartReplayTimeout = AppSettings.Get("Bus.Persistence.StartReplayTimeout", TimeSpan.FromSeconds(30));
            IsDirectoryPickedRandomly = AppSettings.Get("Bus.Directory.PickRandom", true);
            MessagesBatchSize = AppSettings.Get("Bus.Persistence.MessagesBatchSize", 200);

            PersisterBatchSize = AppSettings.Get("Persister.BatchSize", 500);
            PersisterDelay = AppSettings.Get("Persister.Delay", TimeSpan.FromSeconds(30));
            SafetyPhaseDuration = AppSettings.Get("Replayer.SafetyPhaseDuration", TimeSpan.FromSeconds(30));
            QueuingTransportStopTimeout = AppSettings.Get("Transport.StopTimeout", TimeSpan.FromSeconds(15));
            PeerIdsToInvestigate = AppSettings.GetArray("PeerIdsToInvestigate");
            ReplayBatchSize = AppSettings.Get("MessageReplayer.BatchSize", 2000);
            ReplayUnackedMessageCountThatReleasesNextBatch = AppSettings.Get("MessageReplayer.ReplayUnackedMessageCountThatReleasesNextBatch", 200);
            UseInMemoryStorage = AppSettings.Get("UseInMemoryStorage", true);
        }

        public string[] DirectoryServiceEndPoints { get; }
        public bool IsPersistent => false;
        public TimeSpan RegistrationTimeout { get; }
        public TimeSpan StartReplayTimeout { get; }
        public bool IsDirectoryPickedRandomly { get; }
        public bool IsErrorPublicationEnabled => false;
        public int MessagesBatchSize { get; }

        public int PersisterBatchSize { get; }
        public TimeSpan? PersisterDelay { get; }
        public TimeSpan SafetyPhaseDuration { get; }
        public TimeSpan QueuingTransportStopTimeout { get; }
        public string[] PeerIdsToInvestigate { get; }
        public int ReplayBatchSize { get; }
        public int ReplayUnackedMessageCountThatReleasesNextBatch { get; }
        public bool UseInMemoryStorage { get; }
    }
}