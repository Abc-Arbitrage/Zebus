// Copyright (c) Lokad 2009 
// https://github.com/Lokad/lokad-shared-libraries
// This code is released under the terms of the new BSD licence

namespace Abc.Zebus.Util
{
	/// <summary>
	/// Utility class to manipulate arrays
	/// </summary>
	internal static class ArrayUtil
	{
		/// <summary>
		/// Returns empty array instance
		/// </summary>
		/// <typeparam name="T">type of the item for the array</typeparam>
		/// <returns>empty array singleton</returns>
		public static T[] Empty<T>()
		{
            return EmptyArray<T>.Value;
		}

	    public static T[] Copy<T>(T[] array)
	    {
	        if (array == null)
	            return null;

	        var clone = new T[array.Length];
            array.CopyTo(clone, 0);
	        return clone;
	    }

        private class EmptyArray<T>
        {
            internal static readonly T[] Value = new T[0];
        }
	}
}