﻿using System;
using Abc.Zebus.Transport;
using Abc.Zebus.Util;

namespace Abc.Zebus
{
    public class MessageContext
    {
        internal static readonly string CurrentMachineName = Environment.MachineName;
        internal static readonly string CurrentUserName = Environment.UserName;

        [ThreadStatic]
        private static MessageContext _current;

        [ThreadStatic]
        private static string _outboundInitiatorOverride;

        public virtual int ReplyCode { get; internal set; }
        public virtual string ReplyMessage { get; internal set; }
        public virtual IMessage ReplyResponse { get; internal set; }
        public virtual MessageId MessageId { get; private set; }
        public virtual OriginatorInfo Originator { get; private set; }
        public string DispatchQueueName { get; private set; }
        public DateTime? ReceptionTimeUtc { get; private set; }

        public PeerId SenderId => Originator.SenderId;
        public string SenderEndPoint => Originator.SenderEndPoint;
        public string InitiatorUserName => Originator.InitiatorUserName;

        public Peer GetSender()
        {
            return new Peer(SenderId, SenderEndPoint);
        }

        public MessageContext WithDispatchQueueName(string dispatchQueueName)
        {
            return new MessageContextWithDispatchQueueName(this, dispatchQueueName);
        }

        public static MessageContext Current => _current;

        public static IDisposable SetCurrent(MessageContext context)
        {
            var previous = _current;
            _current = context;
            return new DisposableAction(() => _current = previous);
        }

        public static IDisposable OverrideInitiatorUsername(string username)
        {
            string currentInitiatorUsername = null,
                previousOverride = _outboundInitiatorOverride;

            var current = Current;
            if (current != null)
            {
                currentInitiatorUsername = current.Originator.InitiatorUserName;
                current.Originator.InitiatorUserName = username;
            }

            _outboundInitiatorOverride = string.IsNullOrEmpty(username) ? null : username;

            return new DisposableAction(() =>
            {
                _outboundInitiatorOverride = previousOverride;

                if (current != null)
                    current.Originator.InitiatorUserName = currentInitiatorUsername;
            });
        }

        public static MessageContext CreateNew(TransportMessage transportMessage)
        {
            return new MessageContext
            {
                MessageId = transportMessage.Id,
                Originator = transportMessage.Originator,
                ReceptionTimeUtc = transportMessage.ReceptionTimeUtc
            };
        }


        public static MessageContext CreateOverride(PeerId peerId, string peerEndPoint)
        {
            var currentContext = Current;
            var initiatorUserName = GetInitiatorUserName();

            var originator = new OriginatorInfo(peerId, peerEndPoint, CurrentMachineName, initiatorUserName);

            return new MessageContext
            {
                MessageId = MessageId.NextId(),
                Originator = originator,
                DispatchQueueName = currentContext?.DispatchQueueName,
                ReceptionTimeUtc = currentContext?.ReceptionTimeUtc
            };
        }

        public static MessageContext CreateTest(string initiatorUserName = "t.test")
        {
            return CreateTest(new OriginatorInfo(new PeerId("Abc.Testing.999"), "tcp://abctest:1234", "abctest", initiatorUserName));
        }

        public static MessageContext CreateTest(OriginatorInfo originator)
        {
            return new MessageContext
            {
                MessageId = MessageId.NextId(),
                Originator = originator,
            };
        }

        internal static string GetInitiatorUserName()
        {
            var current = Current;
            return current != null ? current.Originator.InitiatorUserName : (_outboundInitiatorOverride ?? CurrentUserName);
        }

        internal ErrorStatus GetErrorStatus()
        {
            if (ReplyCode == 0 && string.IsNullOrEmpty(ReplyMessage))
                return ErrorStatus.NoError;

            return new ErrorStatus(ReplyCode, ReplyMessage);
        }

        private class MessageContextWithDispatchQueueName : MessageContext
        {
            private readonly MessageContext _context;

            public MessageContextWithDispatchQueueName(MessageContext context, string dispatchQueueName)
            {
                _context = context;
                DispatchQueueName = dispatchQueueName;
                ReceptionTimeUtc = context.ReceptionTimeUtc;
            }

            public override MessageId MessageId => _context.MessageId;
            public override OriginatorInfo Originator => _context.Originator;

            public override int ReplyCode
            {
                get { return _context.ReplyCode; }
                internal set { _context.ReplyCode = value; }
            }

            public override string ReplyMessage
            {
                get { return _context.ReplyMessage; }
                internal set { _context.ReplyMessage = value; }
            }

            public override IMessage ReplyResponse
            {
                get { return _context.ReplyResponse; }
                internal set { _context.ReplyResponse = value; }
            }
        }
    }
}
