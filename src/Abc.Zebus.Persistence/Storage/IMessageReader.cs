using System;
using System.Collections.Generic;
using Abc.Zebus.Transport;

namespace Abc.Zebus.Persistence.Storage
{
    public interface IMessageReader : IDisposable
    {
        IEnumerable<byte[]> GetUnackedMessages();
    }
}
