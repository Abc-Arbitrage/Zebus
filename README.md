# About

Zebus is a lightweight peer to peer service bus, built with [CQRS](http://martinfowler.com/bliki/CQRS.html) principles in mind. It allows applications to communicate with each other in a fast and easy manner, most of the complexity is hidden in the library and you can focus on writing code that matters to you, not debugging messaging code.

# Introduction

## A few concepts

### Peer

We call a peer any program that is connected to the bus, a peer is identified by a unique identifier called a PeerId that looks like this: `MyAmazingPeer.0`

### Event

An event is sent by a peer to notify everyone who is interested that something happened (ex: `MyBusinessObjectWasSaved`, `AlertTriggered`...)

### Bus

The piece of code where the magic happens, the methods that you will use the most are `Publish(IEvent)` and `Send(ICommand)`.

### Event


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
    public void MethodThatSends()
    {
        _bus.Publish(new MyEvent { Value = 42 });
    }
}
```
# Copyright

Copyright © 2014 Abc Arbitrage Asset Management

# License

Zebus is licensed under MIT, refer to LICENSE.md for more information.


