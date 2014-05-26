#About

Zebus is a lightweight peer to peer service bus, built with [CQRS](http://martinfowler.com/bliki/CQRS.html) principles in mind. It allows applications to communicate with each other in a fast and easy manner. Most of the complexity is hidden in the library and you can focus on writing code that matters to you, not debugging messaging code.

# Introduction

Zebus is **peer to peer**, so it does not depend on a broker to dispatch messages between the peers. This allows it to reach a throughput of 80Kmgs/s and a roundtrip latency under 500µs. It is **resilient** thanks to the absence of a broker and an optional persistence feature that ensures that messages are not lost if a peer is down or disconnected. It is **stable**, since we have been using it on a production environment at [Abc Arbitrage](http://www.abc-arbitrage.com/) for more than a year.

## Key concepts

### Peer

We call a peer any program that is connected to the bus, a peer is identified by a unique identifier called a PeerId that looks like this: `MyAmazingPeer.0` (we use this convention to identify different instances of the same service).

### Event

An event is sent by a peer to notify everyone who is interested that something happened (ex: `MyBusinessObjectWasSaved`, `AlertTriggered`...).

### Command

A command is sent to a peer asking for an action to be performed (ex: `SaveMyBusinessObjectCommand`).

### Bus

The piece of code where the magic happens, the methods that you will use the most are `Publish(IEvent)` and `Send(ICommand)`.

## A quick demo

On startup, the bus will scan your assemblies for message handlers and notify the other peers that you are interested by those messages. When a peer publishes a message, it will know who handles them and send the messages directly to the correct recipients.

### Receiver
```csharp
public class MyHandler : IMessageHandler<MyEvent>
{
    public void Handle(MyEvent myEvent)
    {
        Console.WriteLine(myEvent.Value);
    }
}
```

### Sender
```csharp
public class MyClient
{
    private IBus _bus;
    
    public MyClient (IBus bus)
    {
        _bus = bus;
    }
    
    public void MethodThatSends()
    {
        _bus.Publish(new MyEvent { Value = 42 });
    }
}
```
### Event description
```csharp
    [ProtoContract]
    public class MyEvent : IEvent
    {
        [ProtoMember(1, IsRequired = true)]
        public int Value { get;set; }
        
        public MyEvent (int value)
        {
            Value = value;
        }
    }
```
And you are set ! This is all the code you need to send an event from one machine to the other. If you want to read more about how the magic happens, have a look at the [wiki](https://github.com/Abc-Arbitrage/Zebus/wiki).

# Release notes
We try to stick to the [semantic versioning](http://semver.org/) principles and keep the [release notes](https://github.com/Abc-Arbitrage/Zebus/blob/master/RELEASE_NOTES.md) up to date.

# Copyright

Copyright © 2014 Abc Arbitrage Asset Management

# License

Zebus is licensed under MIT, refer to [LICENSE.md](https://github.com/Abc-Arbitrage/Zebus/blob/master/LICENSE.md) for more information.