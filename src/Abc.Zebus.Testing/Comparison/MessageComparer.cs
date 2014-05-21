using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Abc.Zebus.EventSourcing;
using KellermanSoftware.CompareNetObjects;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using NUnit.Framework;

namespace Abc.Zebus.Testing.Comparison
{
    public class MessageComparer
    {
        private readonly CompareObjects _comparer = ComparisonExtensions.CreateComparer();

        public MessageComparer()
        {
            _comparer.ElementsToIgnore.Add("Sourcing");
        }

        public void CheckExpectations(IEnumerable<object> actualMessages, IEnumerable<object> expectedMessages, bool exactMatch)
        {
            var differences = GetListsDiff(actualMessages, expectedMessages);

            var errorMsg = CreateErrorMessage(differences, exactMatch);

            if (!string.IsNullOrEmpty(errorMsg))
                Assert.Fail(errorMsg);
        }


        private string CreateErrorMessage<TItem>(Differences<TItem> differences, bool exactMatch) where TItem : class
        {
            string errorMsg = string.Empty;

            foreach (var missing in differences.Missings)
            {
                errorMsg += string.Format("Missing: {0} {1} ", missing.GetType().Name, SerializeJsonSerializer(missing)) + Environment.NewLine;
                if (!exactMatch)
                {
                    var candidate = differences.PossibleCandidates.FirstOrDefault(evt => evt.GetType() == missing.GetType());
                    if (candidate != null)
                        errorMsg += string.Format("Possible match: {0} {1}", candidate.GetType().Name, SerializeJsonSerializer(candidate)) + Environment.NewLine;
                }
            }
                

            if (exactMatch)
            {
                foreach (var notExpected in differences.NotExpected)
                {
                    var notExpectedEvent = notExpected as IDomainEvent;
                    if (notExpectedEvent != null)
                        notExpectedEvent.Sourcing = null;
                    errorMsg += string.Format("Not Expected: {0} {1} ", notExpected.GetType().Name, SerializeJsonSerializer(notExpected)) + Environment.NewLine;
                }
            }
            return errorMsg;
        }


        private Differences<TItem> GetListsDiff<TItem>(IEnumerable<TItem> actualItems, IEnumerable<TItem> expectedItems) where TItem : class
        {
            var notExpectedEvents = new List<TItem>(actualItems);
            var missingEvents = new List<TItem>();
            var possibleCandidates = new List<TItem>();

            foreach (var expected in expectedItems)
            {
                var foundEvent = notExpectedEvents.FirstOrDefault(evt => _comparer.Compare(evt, expected));
                if (foundEvent != null)
                    notExpectedEvents.Remove(foundEvent);
                else
                {
                    missingEvents.Add(expected);
                    var eventOfSameType = notExpectedEvents.FirstOrDefault(evt => evt.GetType() == expected.GetType());
                    if (eventOfSameType != null)
                        possibleCandidates.Add(eventOfSameType);
                }
            }
            return new Differences<TItem>(missingEvents, notExpectedEvents, possibleCandidates);
        }


        private string SerializeJsonSerializer(object value)
        {
            var stringWriter = new StringWriter();
            using (var jsonWriter = new JsonTextWriter(stringWriter))
            {
                jsonWriter.QuoteName = false;
                jsonWriter.Formatting = Formatting.Indented;
                var serializerSettings = new JsonSerializerSettings
                    {
                        Converters = new JsonConverter[] { new IsoDateTimeConverter() },
                        NullValueHandling = NullValueHandling.Ignore,
                    };
                JsonSerializer.Create(serializerSettings).Serialize(jsonWriter, value);
            }
            return stringWriter.ToString();
        }

        private class Differences<TItem>
        {
            public IEnumerable<TItem> PossibleCandidates { get; private set; }
            public IEnumerable<TItem> Missings { get; private set; }
            public IEnumerable<TItem> NotExpected { get; private set; }

            public Differences(IEnumerable<TItem> missings, IEnumerable<TItem> notExpected, IEnumerable<TItem> possibleCandidates)
            {
                PossibleCandidates = possibleCandidates;
                Missings = missings;
                NotExpected = notExpected;
            }
        }
    }
}