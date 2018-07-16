using Abc.Zebus.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Abc.Zebus.Tests.Routing
{
    public static class BindingKeyHelper
    {
        public static BindingKey CreateFromString(string s, char separator) => new BindingKey(s.Split(separator));
    }
}
