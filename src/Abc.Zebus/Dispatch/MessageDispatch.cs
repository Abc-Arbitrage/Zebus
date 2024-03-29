﻿using System;
using System.Collections.Generic;
using System.Threading;
using Abc.Zebus.Serialization;

namespace Abc.Zebus.Dispatch;

public class MessageDispatch
{
    private static readonly object _exceptionsLock = new();

    private readonly IMessageSerializer _messageSerializer;
    private readonly Action<MessageDispatch, DispatchResult> _continuation;
    private Dictionary<Type, Exception>? _exceptions;
    private int _remainingHandlerCount;
    private bool _isCloned;

    public MessageDispatch(MessageContext context, IMessage message, IMessageSerializer messageSerializer, Action<MessageDispatch, DispatchResult> continuation, bool shouldRunSynchronously = false)
    {
        _messageSerializer = messageSerializer;
        _continuation = continuation;

        ShouldRunSynchronously = shouldRunSynchronously;
        Context = context;
        Message = message;
    }

    public bool IsLocal { get; set; }
    public bool ShouldRunSynchronously { get; }
    public MessageContext Context { get; }
    public IMessage Message { get; private set; }

    public void SetIgnored()
    {
        _continuation(this, new DispatchResult(null));
    }

    public void SetHandled(IMessageHandlerInvoker invoker, Exception? error)
    {
        if (error != null)
            AddException(invoker.MessageHandlerType, error);

        if (Interlocked.Decrement(ref _remainingHandlerCount) == 0)
            _continuation(this, new DispatchResult(_exceptions));
    }

    private void AddException(Type messageHandlerType, Exception error)
    {
        lock (_exceptionsLock)
        {
            if (_exceptions == null)
                _exceptions = new Dictionary<Type, Exception>();

            _exceptions[messageHandlerType] = error;
        }
    }

    public void SetHandlerCount(int handlerCount)
    {
        _remainingHandlerCount = handlerCount;
    }

    internal void BeforeEnqueue()
    {
        if (!IsLocal || _isCloned)
            return;

        if (_messageSerializer.TryClone(Message, out var clone))
            Message = clone;

        _isCloned = true;
    }
}
