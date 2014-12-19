using System;
using KellermanSoftware.CompareNetObjects;
using KellermanSoftware.CompareNetObjects.TypeComparers;

namespace Abc.Zebus.Testing.Comparison
{
    public static class ComparisonExtensions
    {
        public static bool DeepCompare<T>(this T firstObj, T secondObj, params string[] elementsToIgnore)
        {
            var comparer = CreateComparer();
            comparer.Config.MembersToIgnore.AddRange(elementsToIgnore);

            return comparer.Compare(firstObj, secondObj).AreEqual;
        }

        public static CompareLogic CreateComparer()
        {
            return new CompareLogic
            {
                Config =
                {
                    CompareStaticProperties = false,
                    CompareStaticFields = false,
                    CustomComparers =
                    {
                        // TODO : Is this still used?
                        new EquatableComparer()
                    }
                }
            };
        }

        private class EquatableComparer : BaseTypeComparer
        {
            public EquatableComparer()
                : base(RootComparerFactory.GetRootComparer())
            {
            }

            public override bool IsTypeMatch(Type type1, Type type2)
            {
                if (type1 != type2)
                    return false;

                return typeof(IEquatable<>).MakeGenericType(type1).IsAssignableFrom(type1);
            }

            public override void CompareType(CompareParms parms)
            {
                if (!Equals(parms.Object1, parms.Object2))
                    AddDifference(parms);
            }
        }
    }
}
