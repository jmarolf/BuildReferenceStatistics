using System.Collections.Generic;
using System.CommandLine.Rendering;

namespace BuildReferenceStatistics {
    internal static class Extensions {
        public static TextSpan Underline(this string value) =>
            new ContainerSpan(StyleSpan.UnderlinedOn(),
                              new ContentSpan(value),
                              StyleSpan.UnderlinedOff());

        internal static void AddRange<TKey>(this Dictionary<TKey, int> dictionary, params TKey[] items)
            where TKey : notnull {
            foreach (var item in items) {
                if (dictionary.TryGetValue(item, out var i)) {
                    i++;
                    dictionary[item] = i;
                } else {
                    dictionary[item] = 1;
                }
            }
        }
    }
}
