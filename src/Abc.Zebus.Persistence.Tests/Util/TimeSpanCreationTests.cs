// Copyright 2011 ThoughtWorks, Inc.
// https://github.com/duelinmarkers/FluentTime
// Licensed under the Apache License, Version 2.0 (the "License");

using System;
using Abc.Zebus.Persistence.Util;
using NUnit.Framework;

namespace Abc.Zebus.Persistence.Tests.Util
{
    [TestFixture]
    public class TimeSpanCreationTests
    {
        [Test]
        public void Creates_TimeSpans_readably_with_ints()
        {
            int two = 2;
            Assert.That(two.Weeks(), Is.EqualTo(TimeSpan.FromDays(14)));
            Assert.That(two.Days(), Is.EqualTo(TimeSpan.FromDays(2)));
            Assert.That(two.Hours(), Is.EqualTo(TimeSpan.FromHours(2)));
            Assert.That(two.Minutes(), Is.EqualTo(TimeSpan.FromMinutes(2)));
            Assert.That(two.Seconds(), Is.EqualTo(TimeSpan.FromSeconds(2)));
            Assert.That(two.Milliseconds(), Is.EqualTo(TimeSpan.FromMilliseconds(2)));
            Assert.That(two.Ticks(), Is.EqualTo(TimeSpan.FromTicks(2)));
        }

        [Test]
        public void Creates_TimeSpans_readably_with_doubles()
        {
            double oneAndAHalf = 1.5;
            Assert.That(oneAndAHalf.Weeks(), Is.EqualTo(new TimeSpan(days: 10, hours: 12, minutes: 0, seconds: 0)));
            Assert.That(oneAndAHalf.Days(), Is.EqualTo(new TimeSpan(days: 1, hours: 12, minutes: 0, seconds: 0)));
            Assert.That(oneAndAHalf.Hours(), Is.EqualTo(new TimeSpan(hours: 1, minutes: 30, seconds: 0)));
            Assert.That(oneAndAHalf.Minutes(), Is.EqualTo(new TimeSpan(hours: 0, minutes: 1, seconds: 30)));
            Assert.That(oneAndAHalf.Seconds(), Is.EqualTo(new TimeSpan(days: 0, hours: 0, minutes: 0, seconds: 1, milliseconds: 500)));
            Assert.That(oneAndAHalf.Milliseconds(), Is.EqualTo(TimeSpan.FromTicks((long)(TimeSpan.TicksPerMillisecond * 1.5))));
        }

        [Test]
        public void Note_difference_with_TimeSpanFromMilliseconds_which_accepts_double_but_is_only_millisecond_precise()
        {
            Assert.That(TimeSpan.FromMilliseconds(1.5),
                        Is.EqualTo(TimeSpan.FromMilliseconds(2)));
            // ... which is unexpected to most. Therefore ...
            Assert.That(1.5.Milliseconds(),
                        Is.Not.EqualTo(TimeSpan.FromMilliseconds(1.5)));
            // It's too bad, but I think this follows the principle of least surprise.
        }

        [Test]
        public void Creates_TimeSpans_readably_with_singular_aliases()
        {
            Assert.That(1.Week(), Is.EqualTo(TimeSpan.FromDays(7)));
            Assert.That(1.Day(), Is.EqualTo(TimeSpan.FromDays(1)));
            Assert.That(1.Hour(), Is.EqualTo(TimeSpan.FromHours(1)));
            Assert.That(1.Minute(), Is.EqualTo(TimeSpan.FromMinutes(1)));
            Assert.That(1.Second(), Is.EqualTo(TimeSpan.FromSeconds(1)));
            Assert.That(1.Millisecond(), Is.EqualTo(TimeSpan.FromMilliseconds(1)));
            Assert.That(1.Tick(), Is.EqualTo(TimeSpan.FromTicks(1)));
        }

        [Test]
        public void Stacks_TimeSpans_readably_in_plural_and_singular()
        {
            Assert.That(2.Weeks(2.Days(3.Hours(4.Minutes(15.Seconds(5.Milliseconds()))))),
                        Is.EqualTo(new TimeSpan(days: 16, hours: 3, minutes: 4, seconds: 15, milliseconds: 5)));

            Assert.That(2.Milliseconds(3.Ticks(4.Ticks())),
                        Is.EqualTo(TimeSpan.FromTicks((2 * TimeSpan.TicksPerMillisecond) + 7)));

            Assert.That(1.Week(1.Day(1.Hour(1.Minute(1.Second(1.Millisecond(1.Tick())))))),
                        Is.EqualTo(7.Days(24.Hours(60.Minutes(60.Seconds(1001.Milliseconds()))) + 1.Tick())));
        }
    }
}