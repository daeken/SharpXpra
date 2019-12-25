using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SharpXpra {
	static class Extensions {
		public static int NestedCount(this List<object> list) =>
			list.Select(x => x is ITuple tuple ? tuple.Length : 1).Sum();
	}
}