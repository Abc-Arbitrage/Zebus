// Copyright 2011 ThoughtWorks, Inc.
// https://github.com/duelinmarkers/FluentTime
// Licensed under the Apache License, Version 2.0 (the "License");

using System;

namespace Abc.Zebus.Util
{
    internal static class TimeSpanCreation
    {
        public static TimeSpan Weeks(this double i) { return TimeSpan.FromDays(i * 7); }
        public static TimeSpan Days(this double i) { return TimeSpan.FromDays(i); }
        public static TimeSpan Hours(this double i) { return TimeSpan.FromHours(i); }
        public static TimeSpan Minutes(this double i) { return TimeSpan.FromMinutes(i); }
        public static TimeSpan Seconds(this double i) { return TimeSpan.FromSeconds(i); }
        public static TimeSpan Milliseconds(this double i) { return new TimeSpan((long)(TimeSpan.TicksPerMillisecond * i)); }
        public static TimeSpan Ticks(this long i) { return TimeSpan.FromTicks(i); }

        public static TimeSpan Week(this double i) { return Weeks(i); }
        public static TimeSpan Day(this double i) { return Days(i); }
        public static TimeSpan Hour(this double i) { return Hours(i); }
        public static TimeSpan Minute(this double i) { return Minutes(i); }
        public static TimeSpan Second(this double i) { return Seconds(i); }
        public static TimeSpan Millisecond(this double i) { return Milliseconds(i); }
        public static TimeSpan Tick(this long i) { return Ticks(i); }

        public static TimeSpan Weeks(this double i, TimeSpan otherTime) { return Weeks(i) + otherTime; }
        public static TimeSpan Days(this double i, TimeSpan otherTime) { return Days(i) + otherTime; }
        public static TimeSpan Hours(this double i, TimeSpan otherTime) { return Hours(i) + otherTime; }
        public static TimeSpan Minutes(this double i, TimeSpan otherTime) { return Minutes(i) + otherTime; }
        public static TimeSpan Seconds(this double i, TimeSpan otherTime) { return Seconds(i) + otherTime; }
        public static TimeSpan Milliseconds(this double i, TimeSpan otherTime) { return Milliseconds(i) + otherTime; }
        public static TimeSpan Ticks(this long i, TimeSpan otherTime) { return Ticks(i) + otherTime; }

        public static TimeSpan Week(this double i, TimeSpan otherTime) { return Weeks(i, otherTime); }
        public static TimeSpan Day(this double i, TimeSpan otherTime) { return Days(i, otherTime); }
        public static TimeSpan Hour(this double i, TimeSpan otherTime) { return Hours(i, otherTime); }
        public static TimeSpan Minute(this double i, TimeSpan otherTime) { return Minutes(i, otherTime); }
        public static TimeSpan Second(this double i, TimeSpan otherTime) { return Seconds(i, otherTime); }
        public static TimeSpan Millisecond(this double i, TimeSpan otherTime) { return Milliseconds(i, otherTime); }
        public static TimeSpan Tick(this long i, TimeSpan otherTime) { return Ticks(i, otherTime); }


        public static TimeSpan Weeks(this int i) { return Weeks((double)i); }
        public static TimeSpan Days(this int i) { return Days((double)i); }
        public static TimeSpan Hours(this int i) { return Hours((double)i); }
        public static TimeSpan Minutes(this int i) { return Minutes((double)i); }
        public static TimeSpan Seconds(this int i) { return Seconds((double)i); }
        public static TimeSpan Milliseconds(this int i) { return Milliseconds((double)i); }
        public static TimeSpan Ticks(this int i) { return Ticks((long)i); }

        public static TimeSpan Week(this int i) { return Week((double)i); }
        public static TimeSpan Day(this int i) { return Day((double)i); }
        public static TimeSpan Hour(this int i) { return Hour((double)i); }
        public static TimeSpan Minute(this int i) { return Minute((double)i); }
        public static TimeSpan Second(this int i) { return Second((double)i); }
        public static TimeSpan Millisecond(this int i) { return Millisecond((double)i); }
        public static TimeSpan Tick(this int i) { return Tick((long)i); }

        public static TimeSpan Weeks(this int i, TimeSpan otherTime) { return Weeks((double)i, otherTime); }
        public static TimeSpan Days(this int i, TimeSpan otherTime) { return Days((double)i, otherTime); }
        public static TimeSpan Hours(this int i, TimeSpan otherTime) { return Hours((double)i, otherTime); }
        public static TimeSpan Minutes(this int i, TimeSpan otherTime) { return Minutes((double)i, otherTime); }
        public static TimeSpan Seconds(this int i, TimeSpan otherTime) { return Seconds((double)i, otherTime); }
        public static TimeSpan Milliseconds(this int i, TimeSpan otherTime) { return Milliseconds((double)i, otherTime); }
        public static TimeSpan Ticks(this int i, TimeSpan otherTime) { return Ticks((long)i, otherTime); }

        public static TimeSpan Week(this int i, TimeSpan otherTime) { return Weeks((double)i, otherTime); }
        public static TimeSpan Day(this int i, TimeSpan otherTime) { return Days((double)i, otherTime); }
        public static TimeSpan Hour(this int i, TimeSpan otherTime) { return Hours((double)i, otherTime); }
        public static TimeSpan Minute(this int i, TimeSpan otherTime) { return Minutes((double)i, otherTime); }
        public static TimeSpan Second(this int i, TimeSpan otherTime) { return Seconds((double)i, otherTime); }
        public static TimeSpan Millisecond(this int i, TimeSpan otherTime) { return Milliseconds((double)i, otherTime); }
        public static TimeSpan Tick(this int i, TimeSpan otherTime) { return Ticks(i, otherTime); }
    }
}