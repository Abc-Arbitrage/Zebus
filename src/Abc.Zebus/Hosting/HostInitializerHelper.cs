using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Logging;
using StructureMap;

namespace Abc.Zebus.Hosting
{
    public static class HostInitializerHelper
    {
        private static readonly ILogger _log = ZebusLogManager.GetLogger(typeof(HostInitializerHelper));

        public static void CallActionOnInitializers(this Container container, Expression<Action<HostInitializer>> actionToCall, bool invertPriority = false)
        {
            var initializers = container.GetAllInstances<HostInitializer>();
            var orderedInitializers = invertPriority ? initializers.OrderBy(x => x.Priority)
                                                     : initializers.OrderByDescending(x => x.Priority);

            var methodInfo = GetMethodInfo(actionToCall);

            foreach (var hostInitializer in orderedInitializers)
            {
                var hostMethodInfo = hostInitializer.GetType().GetMethod(methodInfo.Name, BindingFlags.Instance | BindingFlags.Public);
                if (hostMethodInfo == null || hostMethodInfo.DeclaringType == typeof(HostInitializer))
                    continue;

                _log.LogInformation("Calling " + methodInfo.Name + " on initializer: " + hostInitializer.GetType().Name);
                actionToCall.Compile()(hostInitializer);
            }

            _log.LogInformation(methodInfo.Name + " on initializers executed");
        }

        private static MethodInfo GetMethodInfo<T>(Expression<Action<T>> expression)
        {
            var methodCall = (MethodCallExpression)expression.Body;
            return methodCall.Method;
        }
    }
}
