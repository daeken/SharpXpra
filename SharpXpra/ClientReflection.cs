using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace SharpXpra {
	[AttributeUsage(AttributeTargets.Method)]
	public class HandlerAttribute : Attribute {
		public readonly string Name;
		public HandlerAttribute(string name) => Name = name;
	}
	
	public partial class Client {
		void SetupHandlers() {
			foreach(var method in GetType()
				.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)) {
				var handler = method.GetCustomAttribute<HandlerAttribute>();
				if(handler == null) continue;
				var client = Expression.Parameter(typeof(Client));
				var param = Expression.Parameter(typeof(List<object>));
				var body = Expression.Block(typeof(void),
					Expression.Call(typeof(Client).GetMethod("AssertEqual"),
						Expression.MakeMemberAccess(param, typeof(List<object>).GetMember("Count")[0]),
						Expression.Constant(method.GetParameters().Length + 1)),
					Expression.Call(client, method,
						method.GetParameters()
							.Select((x, i) =>
								Expression.Convert(
									Expression.Property(param, param.Type.GetProperty("Item"),
										Expression.Constant(i + 1)),
									x.ParameterType))));
				Handlers[handler.Name] = (Action<Client, List<object>>) Expression.Lambda(body, client, param).Compile();
			}
		}

		public static void AssertEqual(int got, int expected) {
			if(got != expected)
				throw new Exception($"Assertion failed; got {got} expected {expected}");
		}
	}
}