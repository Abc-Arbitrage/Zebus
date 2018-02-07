using System;
using FluentDate;

namespace Abc.Zebus.Persistence.Runner
{
    internal class AppSettingsConfiguration : IBusConfiguration, IPersistenceConfiguration
    {
        public AppSettingsConfiguration()
        {
            DirectoryServiceEndPoints = AppSettings.GetArray("Bus.Directory.EndPoints");
            RegistrationTimeout = AppSettings.Get("Bus.Directory.RegistrationTimeout", 30.Seconds());
            StartReplayTimeout = AppSettings.Get("Bus.Persistence.StartReplayTimeout", 30.Seconds());
            IsDirectoryPickedRandomly = AppSettings.Get("Bus.Directory.PickRandom", true);
            MessagesBatchSize = AppSettings.Get("Bus.Persistence.MessagesBatchSize", 200);

            PersisterBatchSize = AppSettings.Get("Persister.BatchSize", 500);
            PersisterDelay = AppSettings.Get("Persister.Delay", 30.Seconds());
            SafetyPhaseDuration = AppSettings.Get("Replayer.SafetyPhaseDuration", 30.Seconds());
            QueuingTransportStopTimeout = AppSettings.Get("Transport.StopTimeout", 15.Seconds());
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