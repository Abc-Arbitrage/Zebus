using System;
using Abc.Zebus.Lotus;

namespace Abc.Zebus
{
    public interface IBusConfiguration
    {
        /// <summary>
        /// The list of directories that can be used by the Bus to register.
        /// The syntax is "tcp://hostname:port"
        /// </summary>
        string[] DirectoryServiceEndPoints { get; }

        /// <summary>
        /// The time to for when registering to a Directory, once this time is over,
        /// the next directory in the list will be used.
        /// </summary>
        TimeSpan RegistrationTimeout { get; }

        /// <summary>
        /// The time to wait for when trying to replay messages from the persistence on startup.
        /// Failing to get a response from the Persistence in the allotted time causes the Peer to stop.
        /// </summary>
        TimeSpan StartReplayTimeout { get; }

        /// <summary>
        /// A peer marked as persistent will benefit from the persistence mechanism
        /// (https://github.com/Abc-Arbitrage/Zebus/wiki/Persistence)
        /// </summary>
        bool IsPersistent { get; }
        
        /// <summary>
        /// Mainly a debugging setting, setting it to false will prevent the Bus from connecting
        /// to a random Directory when needed
        /// </summary>
        bool IsDirectoryPickedRandomly { get; }

        /// <summary>
        ///  Indicates whether <see cref="MessageProcessingFailed"/> should be published on handler errors.
        /// </summary>
        bool IsErrorPublicationEnabled { get; }
    }
}