using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
#if NETFWK
        [Test]
        public void should_not_generate_identical_MessageIds_when_multiple_buses_are_started_in_different_app_domains_simultaneously()
        {
            const int appDomainsToGenerate = 100;
            var appDomainProxies = Enumerable.Range(0, appDomainsToGenerate).Select(i => CreateMessageIdCallerFromNewAppDomain()).ToList();
            var proxiesTasks = appDomainProxies.Select(proxy => new Task<Guid>(proxy.GetNextId)).ToArray();

            foreach (var task in proxiesTasks)
            {
                task.Start();
            }

            var generatedMessageIds = proxiesTasks.Select(task => task.Result).ToList();

            generatedMessageIds.Distinct().Count().ShouldEqual(appDomainsToGenerate);
        }

        private static MessageIdProxy CreateMessageIdCallerFromNewAppDomain()
        {
            var appDomainInfo = new AppDomainSetup
            {
                ApplicationBase = Path.GetDirectoryName(new Uri(typeof(MessageIdProxy).Assembly.CodeBase).LocalPath),
                ShadowCopyFiles = "true"
            };

            var newAppDomain = AppDomain.CreateDomain("MyAppDomain", null, appDomainInfo);
            return (MessageIdProxy)newAppDomain.CreateInstanceAndUnwrap(typeof(MessageIdProxy).Assembly.FullName, "Abc.Zebus.Tests.MessageIdTests+MessageIdProxy");
        }
#endif

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
        [Repeat(20)]
        public void should_pause_id_generation_at_given_date()
        {
            MessageId.ResetLastTimestamp();
            MessageId pausedId;
            var dateInThePast = DateTime.UtcNow.Date.AddDays(-10);
            using (MessageId.PauseIdGenerationAtDate(dateInThePast))
            {
                pausedId = MessageId.NextId();
                pausedId.GetDateTime().ShouldEqual(dateInThePast);
                MessageId.NextId().Value.ShouldEqual(pausedId.Value);
            }

            var unpausedId = MessageId.NextId();
            unpausedId.Value.ShouldNotEqual(pausedId.Value);
            SystemDateTime.Today.ShouldEqual(DateTime.Today);
        }

        [Test, Repeat(20)]
        public void should_convert_message_id_to_DateTime()
        {
            MessageId.ResetLastTimestamp();
            using (SystemDateTime.PauseTime())
            {
                var id = MessageId.NextId();
                var date = id.GetDateTime();
                date.ShouldEqual(SystemDateTime.UtcNow);
            }
        }

        [Test, Explicit("Manual test")]
        public void MesurePerformances()
        {
            Measure.Execution(x =>
            {
                x.Iteration = 200000;
                x.WarmUpIteration = 10;
                x.Action = _ =>
                {
                    for (var i = 0; i < 10; ++i)
                    {
                        MessageId.NextId();
                    }
                };
            });
        }

        public class MessageIdProxy : MarshalByRefObject
        {
            public Guid GetNextId()
            {
                return MessageId.NextId().Value;
            }
        }
    }
}
