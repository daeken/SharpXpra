using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace SharpXpra {
	[AttributeUsage(AttributeTargets.Method)]
	public class HandlerAttribute : Attribute {
		public readonly string Name;
		public HandlerAttribute(string name) => Name = name;
	}
	
	public partial class Client<CompositorT, WindowT> {
		void SetupHandlers() {
			foreach(var method in GetType()
				.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
				var handler = method.GetCustomAttribute<HandlerAttribute>();
				if(handler == null) continue;
				var client = Expression.Parameter(typeof(Client<CompositorT, WindowT>));
				var param = Expression.Parameter(typeof(List<object>));
				var body = Expression.Block(typeof(void),
					Expression.Call(typeof(Client<CompositorT, WindowT>).GetMethod("AssertEqual"),
						Expression.MakeMemberAccess(param, typeof(List<object>).GetMember("Count")[0]),
						Expression.Constant(method.GetParameters().Length + 1)),
					Expression.Call(client, method,
						method.GetParameters()
							.Select((x, i) =>
								Expression.Call(
									typeof(Client<CompositorT, WindowT>).GetMethod("ConvertParam")
										.MakeGenericMethod(x.ParameterType), param,
									Expression.Constant(i)))));
				Handlers[handler.Name] =
					(Action<Client<CompositorT, WindowT>, List<object>>) Expression.Lambda(body, client, param)
						.Compile();
			}
		}

		public static void AssertEqual(int got, int expected) {
			if(got != expected)
				throw new Exception($"Assertion failed; got {got} expected {expected}");
		}

		public static OutT ConvertParam<OutT>(List<object> packet, int i) {
			var obj = packet[i + 1];
			if(obj is OutT match) return match;
			var itype = obj.GetType();
			var otype = typeof(OutT);
			return obj switch {
				//string str when otype == typeof(byte[]) => (OutT) (object) Encoding.UTF8.GetBytes(str),
				byte[] bytes when otype == typeof(string) => (OutT) (object) Encoding.UTF8.GetString(bytes), 
				int iv when otype == typeof(long) => (OutT) (object) (long) iv,
				_ => throw new NotSupportedException(
					$"Handler for {packet[0].ToPrettyString()} wanted {otype.ToPrettyString()} but got {itype.ToPrettyString()} in parameter {i}; no conversion available")
			};
		}
	}
}