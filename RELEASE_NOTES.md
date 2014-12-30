## Upcoming features
### Future
 - Persistence service
 - Reconsider ZMQ as a transport library

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