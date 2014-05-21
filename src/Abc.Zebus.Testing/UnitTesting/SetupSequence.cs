using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Abc.Zebus.Testing.UnitTesting
{
    /// <summary>
    /// Usage:
    /// <code>
    /// var sequence = new SetupSequence();
    /// mock.Setup(x => x.Foo()).InSequence(sequence);
    /// mock.Setup(x => x.Bar()).InSequence(sequence);
    /// //...
    /// sequence.Verify();
    /// </code>
    /// </summary>
    public class SetupSequence
    {
        private readonly List<string> _errorMessages = new List<string>();
        private int _expectedOrderSequence;
        private int _order;

        public Action GetCallback(string expression = null)
        {
            var expectedOrder = _expectedOrderSequence;
            ++_expectedOrderSequence;
            return () =>
            {
                if (expectedOrder == _order)
                    ++_order;
                else if (expression != null)
                    _errorMessages.Add("Expected sequence index " + expectedOrder + " but was " + _order + ": " + expression);
            };
        }

        public void Verify()
        {
            var messageText = _errorMessages.Count == 0 ? "Sequence is not in order" : string.Join(Environment.NewLine, _errorMessages);
            Assert.That(_order, Is.EqualTo(_expectedOrderSequence), messageText);
        }
    }
}