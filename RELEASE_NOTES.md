## 1.3.1
### Bug fixes
 - Error codes of locally handled commands are returned properly
 - Empty subscription updates are not sent to the Directory
 
## 1.3.0
### Requirements
 - A client using this version needs its Directory server to be at least in 1.1 for dynamic subscriptions to work properly

### Features
 - Subscription updates are made by message type instead of as a big atomic change to improve subscriptions performance

## 1.2.13
Republished the NuGet because the version number was not incremented in 1.2.12
 
## 1.2.12
### Bug fixes
 - Pulled the new `MessageDispatcher` filter feature up to `IMessageDispatcher`
 
## 1.2.11
### Bug fixes
 - Fixed NuGet dependencies
 
## 1.2.10
### Features
 - The inbound port is no longer sticky, since the feature was not relevant anymore (we are no longer relying on ZMQ's buffers)
 - The `MessageDispatcher` can be provided with a message type filter
 
## 1.2.9
### Features
 - Scanning ".exe" files for handlers as well as ".dlls" at startup

### Bug fixes
 - The `TestBus` now publishes a `PeerStopped` event when unregistering 
 
## 1.2.8
### Features 
 - `MessageId` can now be paused at a given date
 - Added new "Handling" methods to the `TestBus`
 
## 1.2.7
### Bug fixes
 - Fix the Period setter on `PeriodicActionHostInitializer`
 
## 1.2.6
### Features 
 - Added `PeriodicActionHostInitializer` / `HostInitializerHelper` to provide an easy way to run code Before/After the Bus is Starting/Stopping and at given intervals.
 - `DomainException`s can now be thrown using an Enum, and the error message can be retrieved by the sender using Description attributes on the Enum.
 
## 1.2.5
### Bug fixes
 - Transient Infrastructure Commands now ignore the "IsResponding" state of the target Peer, as they should (Some Commands like `PingCommand` should be sent no matter what)

## 1.2.4
### Features 
 - Deserialization errors now produce a `MessageProcessingFailed` instead of a `CustomProcessingFailed` since it makes more sense to handle it like a conventional handler error
 - NuGet dependencies updated

### Bug fixes
 - The handler being run while the Bus shutdown is initiated could not send messages because the Bus was signaled as "Stopped" too early
 
## 1.2.3
### Features 
 - Added `Abc.Zebus.Persistence.Tests` to the InternalsVisibleTo list to prepare the release of the Persistence

## 1.2.2
### Features 
 - Zebus.Testing: The default object comparer now ignores static fields/properties
 - Added `Abc.Zebus.Persistence` to the InternalsVisibleTo list to prepare the release of the Persistence

### Bug fixes
 - Sending a message with a `null` Routing Key now throws an explicit exception (instead of `NullReferenceException`) 

## 1.2.1
### Features 
 - `Abc.Zebus.Lotus.CustomProcessingFailed` is now mutable, allowing users to pool it

## 1.2.0
### Features
 - The Pipes move from `Scan\Pipes` to `Dispatch\Pipes` (theoretically a breaking change, but the API is quite internal)
 - Removed `RoutingType` since it wasn't used
 - The Bus will now throw if you try to use it before it is started
 - Moq, ProtoBuf-Net, AutoFixture, Json.Net, NUnit and CompareNetObjects are now referenced as NuGets
 - The new `MarkPeerAsRespondingCommand`/ `MarkPeerAsNotRespondingCommand` commands allow to mark a Peer as (not) responding (NOT a standard operation, use with care)

### Bug fixes
 - The Persistence is now acked when a message cannot be deserialized, to prevent the Persistence from sending it over and over
 - A race condition could prevent the Bus from starting properly

## 1.1.8
### Features 
 - When sending a transient command, `Send()` will throw if the target Peer is not responding

## 1.1.7
### Features 
 - A message that cannot be deserialized is now dumped on disk
 - IProvideQueueLength now exposes a `Purge()` method, that is called when the queue length provider exceeds queue thresholds

### Bug fixes
 - Fixed thread-safety issue in MessageDispatch.SetHandled

## 1.1.6
### Features
 - log4net is now referenced as a NuGet package

## 1.1.5
### Features
 - The repository is split, from now on Zebus.Directory has its own repository
 - The MessageContext can be injected in the constructor of a handler
 - The new SubscriptionModeAttribute allows to control automatic subscriptions more explicitly

### Bug fixes
 - The "HANDLE" log is now accurate for async

## 1.1.4 
### Features
 - Split the "HANDLE" log into "RECV" and "HANDLE", making the distinction between the time a message is received and the time it is handled by user code

### Bug fixes
 - Directories don't decommission other Directories/self
 - Starting multiple Buses on the same machine simultaneously could result in identical message ids

## 1.1.3
### Features
 - MessageExecutionCompleted now logs the MessageId of the corresponding command

## 1.1.2
### Features
 - Now using Cassandra driver 2.0.3 in Directory.Cassandra

## 1.1.1
### Features
 - Now using libZmq 4.0.4 and providing the matching pdbs in the Zebus repository

## 1.1.0
### Features 
 - The Cassandra backed Directory server is fully operational
 - The tree-backed local Directory cache is now fully operational (routing performance improvement, faster routing rules updates, smaller memory footprint, etc.)

### Bug fixes
 - Dynamic subscriptions for outgoing messages can be disabled on the Cassandra Directory implementation to handle massive dynamic subscriptions (not recommended)
 - The SocketConnected/SocketDisconnected feature was removed (it was largely undocumented / unused, so it made to a minor)
 - The local Directory cache doesn't lose subscriptions when a Peer is decommissioned
 - Reduced the Directory cache memory footprint
 - Fixed a bug in the Directory cache that prevented multiple Peers from receiving the same messages
 - Messages received from the Directory during the Registration procedure could be lost
 - The Directory server now deletes existing dynamic subscriptions when a Peer registers
 - The Directory server now handles PeerSubscriptionsForTypesUpdated with "null" BindingKeys

## 1.0.10
### Features
 - The local Directory cache now handles the new dynamic subscriptions. We will release a 1.1 after thorough testing / benchmarking.

## 1.0.9
### Features
 - The incremental subscriptions support was revamped to work on a MessageType level instead of subscription level (it couldn't handle the required load).
 - The Directory server Cassandra implementation was modified to support the new dynamic subscriptions efficiently.
 - All packages are now supporting SymbolSource.

## 1.0.8
### Features
 - CustomDelegatedProcessingFailed was removed (it should never have been in the public API).

## 1.0.7
### Bug fixes
 - The Cassandra Directory server implementation was ignoring some updates because the DateTime.Kind was not set and some timestamps where erroneously converted to Utc.

## 1.0.6
### Bug fixes
 - Added some logging in the Directory events to ease debugging

## 1.0.5
### Features
 - Added support for incremental subscription updates in the client Directory cache using a tree structure that allows to keep throughput stable with huge volumes of routings.
 - Added a Cassandra implementation of the Directory server repository (WIP, not production ready yet)

## 1.0.4
### Features
 - Added SymbolSource support, you can now browse sources and debug from Visual Studio.

## 1.0.3
### Features
 - IMultiEventHandler is replaced by Bus.Subscribe(Subscription[], Action<IMessage>) (Should have been in a major release since it is a Core breaking change, but given that it was not even documented we just changed it in a patch release).
 - The project is now built/tested on [AppVeyor](https://ci.appveyor.com/project/alprema/zebus)

### Bug fixes
 - When creating two identical dynamic subscriptions, disposing one does not dispose the other anymore.

## 1.0.2
### Bug fixes
 - Embedding libZmq in the Zebus DLL so it is packaged in the Nuget

## 1.0.1
### Features
 - All core features (Including [Events](https://github.com/Abc-Arbitrage/Zebus/wiki/Event), [Commands](https://github.com/Abc-Arbitrage/Zebus/wiki/Command), [Dynamic subscriptions](https://github.com/Abc-Arbitrage/Zebus/wiki/Command), etc.)
 - In-memory Directory for testing purposes, but **should NOT be used in production**
