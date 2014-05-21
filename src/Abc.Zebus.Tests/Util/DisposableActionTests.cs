using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util
{
    [TestFixture]
    public class DisposableActionTests
    {
        [Test]
        public void should_call_action_on_dispose()
        {
            var callCount = 0;
            var disposableObject = new DisposableAction(() => callCount++);
            disposableObject.Dispose();

            callCount.ShouldEqual(1);
        }

        [Test]
        public void should_not_call_action_multiple_times_on_multiple_dispose()
        {
            var callCount = 0;
            var disposableObject = new DisposableAction(() => callCount++);
            disposableObject.Dispose();
            disposableObject.Dispose();
            disposableObject.Dispose();

            callCount.ShouldEqual(1);   
        }
    }
}