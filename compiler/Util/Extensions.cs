using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Caique.Util
{
    public static class Extensions
    {
        public static IEnumerable<(T item, int index)> WithIndex<T>(this IEnumerable<T> self)
            => self.Select((item, index) => (item, index));

        public unsafe static sbyte* ToCString(this string str)
        {
            if (str == "")
            {
                fixed (sbyte* ptr = new[] { (sbyte)'\0' })
                    return ptr;
            }

            fixed (byte* ptr = &Encoding.Default.GetBytes(str)[0])
                return (sbyte*)ptr;
        }
    }
}