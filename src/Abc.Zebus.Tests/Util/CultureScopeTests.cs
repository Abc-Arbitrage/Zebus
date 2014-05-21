using System.Globalization;
using System.Threading;
using Abc.Zebus.Testing.Extensions;
using Abc.Zebus.Util;
using NUnit.Framework;

namespace Abc.Zebus.Tests.Util
{
    [TestFixture]
    public class CultureScopeTests
    {
        [Test]
        public void should_set_invariant_culture_and_reset_previous_culture_on_dispose()
        {
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            var currentUICulture = Thread.CurrentThread.CurrentUICulture;

            using (CultureScope.Invariant())
            {
                Thread.CurrentThread.CurrentCulture.ShouldEqual(CultureInfo.InvariantCulture);
                Thread.CurrentThread.CurrentUICulture.ShouldEqual(CultureInfo.InvariantCulture);
            }

            Thread.CurrentThread.CurrentCulture.ShouldEqual(currentCulture);
            Thread.CurrentThread.CurrentUICulture.ShouldEqual(currentUICulture);
        }

        [Test]
        public void should_set_different_culture_and_reset_previous_culture_on_dispose()
        {
            var scopedCulture = CultureInfo.GetCultureInfo("lv-LV");
            var currentCulture = Thread.CurrentThread.CurrentCulture;
            var currentUICulture = Thread.CurrentThread.CurrentUICulture;

            using (new CultureScope(scopedCulture))
            {
                Thread.CurrentThread.CurrentCulture.ShouldEqual(scopedCulture);
                Thread.CurrentThread.CurrentUICulture.ShouldEqual(scopedCulture);
            }

            Thread.CurrentThread.CurrentCulture.ShouldEqual(currentCulture);
            Thread.CurrentThread.CurrentUICulture.ShouldEqual(currentUICulture);
        }
    }
}