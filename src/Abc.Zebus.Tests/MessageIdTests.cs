using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Testing.Measurements;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class MessageIdTests
    {
        [Test]
        public void should_generate_unique_ids()
        {
            var messageIds = new List<MessageId>(200000);
            for (var i = 0; i < messageIds.Capacity; ++i)
            {
                messageIds.Add(MessageId.NextId());
            }

            var duplicatedMessageIds = messageIds.GroupBy(x => x.Value).Where(x => x.Count() != 1).ToList();
            duplicatedMessageIds.ShouldBeEmpty();
        }

        [Test]
        public void should_generate_unique_ids_from_multiple_threads()
        {
            var messageIds = new ConcurrentQueue<MessageId>();

            Action taskAction = () =>
            {
                for (var i = 0; i < 100000; ++i)
                {
                    messageIds.Enqueue(MessageId.NextId());
                }
            };

            var task1 = Task.Factory.StartNew(taskAction);
            var task2 = Task.Factory.StartNew(taskAction);

            Task.WaitAll(task1, task2);

            var duplicatedMessageIds = messageIds.GroupBy(x => x.Value).Where(x => x.Count() != 1).ToList();
            duplicatedMessageIds.ShouldBeEmpty();
        }

        [Test]
        public void should_pause_id_generation()
        {
            MessageId pausedId;
            using (MessageId.PauseIdGeneration())
            {
                pausedId = MessageId.NextId();
                MessageId.NextId().Value.ShouldEqual(pausedId.Value);
            }

            var unpausedId = MessageId.NextId();
            unpausedId.Value.ShouldNotEqual(pausedId.Value);
        }

        [Test]
        public void should_convert_message_id_to_DateTime()
        {
            Thread.Sleep(1); // To ensure that noone called NextId() in this millisecond, because MessageId has a static state and would increment its value
            using (SystemDateTime.PauseTime())
            {
                var id = MessageId.NextId();
                var date = id.GetDateTime();
                date.ShouldEqual(SystemDateTime.UtcNow);
            }
        }

        [Test, Ignore("Manual test")]
        public void MesurePerformances()
        {
            Measure.Execution(x =>
            {
                x.Iteration = 200000;
                x.WarmUpIteration = 10;
            }, x =>
            {
                for (var i = 0; i < 10; ++i)
                {
                    MessageId.NextId();
                }
            });
        }
    }
}