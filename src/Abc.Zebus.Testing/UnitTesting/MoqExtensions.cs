using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Abc.Zebus.Util.Extensions;
using Moq;
using Moq.Language.Flow;

namespace Abc.Zebus.Testing.UnitTesting
{
    public static class MoqExtensions
    {
        public static void CaptureEnumerable<TMock, TArg, TArg0>(this Mock<TMock> mock, TArg0 arg0, Expression<Action<TMock, TArg0, IEnumerable<TArg>>> action, ICollection<TArg> target) where TMock : class
        {
            var mockParam = Expression.Parameter(typeof(TMock));
            var arg0Param = Expression.Constant(arg0);
            var enumerableParam = Expression.Call(typeof(It).GetMethod("IsAny").MakeGenericMethod(typeof(IEnumerable<TArg>)));
            var methodCall = (MethodCallExpression)action.Body;
            var methodExpression = Expression.Call(mockParam, methodCall.Method, arg0Param, enumerableParam);
            var expression = Expression.Lambda<Action<TMock>>(methodExpression, mockParam);

            mock.Setup(expression).Callback<TArg0, IEnumerable<TArg>>((x, items) => items.ForEach(target.Add));
        }

        public static ICallbackResult InSequence<TMock>(this ISetup<TMock> setup, SetupSequence sequence) where TMock : class
        {
            return setup.Callback(sequence.GetCallback(setup.ToString()));
        }
    }
}