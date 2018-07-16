using System;
using Abc.Zebus.Testing.Extensions;
using NUnit.Framework;

namespace Abc.Zebus.Tests
{
    [TestFixture]
    public class MessageContextTests
    {
        [Test]
        public void should_override_username()
        {
            var context = MessageContext.CreateTest("original");
            using (MessageContext.SetCurrent(context))
            {
                MessageContext.Current.InitiatorUserName.ShouldEqual("original");

                using (MessageContext.OverrideInitiatorUsername("override"))
                    MessageContext.Current.InitiatorUserName.ShouldEqual("override");

                MessageContext.Current.InitiatorUserName.ShouldEqual("original");
            }
        }

        [Test]
        public void should_override_username_outside_of_a_context()
        {
            var realName = Environment.UserName;

            MessageContext.GetInitiatorUserName().ShouldEqual(realName);

            using (MessageContext.OverrideInitiatorUsername("lol"))
            {
                MessageContext.GetInitiatorUserName().ShouldEqual("lol");
            }

            MessageContext.GetInitiatorUserName().ShouldEqual(realName);
        }
    }
}
