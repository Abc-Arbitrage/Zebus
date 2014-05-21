using System;
using System.Linq;
using KellermanSoftware.CompareNetObjects;

namespace Abc.Zebus.Testing.Comparison
{
    public static class ComparisonExtensions
    {
        public static bool DeepCompare<T>(this T firstObj, T secondObj, params string[] elementsToIgnore)
        {
            var comparer = CreateComparer();
            comparer.ElementsToIgnore.AddRange(elementsToIgnore);
            return comparer.Compare(firstObj, secondObj);
        }

        public static CompareObjects CreateComparer()
        {
            return new CompareObjects
                {
                    IsUseCustomTypeComparer = type => type.GetInterfaces().Any(itf => itf.IsGenericType && itf.GetGenericTypeDefinition() == typeof(IEquatable<>)),
                    CustomComparer = (compareObjects, left, right, breadCrumbs) =>
                        {
                            if (!left.Equals(right))
                                compareObjects.Differences.Add(string.Format("object1{0} != object2{0} ({1},{2})", breadCrumbs, left, right));
                        }
                };
        }
    }
}