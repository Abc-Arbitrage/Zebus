using System;

namespace Abc.Zebus.Persistence
{
    public interface IPersistenceConfiguration
    {
        int PersisterBatchSize { get; }

        TimeSpan? PersisterDelay { get; }

        TimeSpan SafetyPhaseDuration { get; }

        TimeSpan QueuingTransportStopTimeout { get; }

        string[] PeerIdsToInvestigate { get; }

        int ReplayBatchSize { get; }

        int ReplayUnackedMessageCountThatReleasesNextBatch { get; }
    }
}