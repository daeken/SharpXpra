using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace SharpXpra {
	[AttributeUsage(AttributeTargets.Method)]
	public class PrettyPrinterAttribute : Attribute {
	}

	public interface IPrettyPrintable {
		string ToPrettyString();
	}
	
	public static class Extensions {
		static readonly IReadOnlyDictionary<Type, MethodInfo> Printers;
		static Extensions() {
			var printers = new Dictionary<Type, MethodInfo>();
			foreach(var asm in AppDomain.CurrentDomain.GetAssemblies()) {
				foreach(var type in asm.GetTypes()) {
					if(type.GetInterfaces().Contains(typeof(IPrettyPrintable)))
						printers[type] = type.GetMethod("ToPrettyString");
					else
						foreach(var method in type.GetMethods(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)) {
							if(method.GetCustomAttribute<PrettyPrinterAttribute>() != null)
								printers[method.GetParameters()[0].ParameterType] = method;
						}
				}
			}
			Printers = printers;
		}
		
		public static void Print<T>(this T obj) => Console.WriteLine(obj.ToPrettyString());
		public static void PrettyPrint<T>(this T obj) => Console.WriteLine(obj.ToPrettyString());

		public static string ToPrettyString<T>(this T obj) => ToPrettyString((object) obj);

		static string Indentify(string value) => 
			value.Contains("\n")
				? string.Join("\n", value.Trim().Split('\n').Select(x => "\t" + x))
				: "\t" + value;

		public static IEnumerable<object> Enumeratorable(IEnumerator e) {
			while(e.MoveNext())
				yield return e.Current;
		}

		static string ToPrettyString(object obj) {
			if(obj == null) return "null";
			var type = obj.GetType();
			if(obj is Type) type = typeof(Type);
			if(!Printers.ContainsKey(type)) {
				if(!type.GetInterfaces().Contains(typeof(IEnumerable))) return GenericPretty(obj);
				var temp = Enumeratorable(((IEnumerable) obj).GetEnumerator()).ToList();
				var prefix = $"{ToPrettyString(type.IsArray ? type.GetElementType() : type)}[{temp.Count}]";
				switch(temp.Count) {
					case 0: return prefix;
					case 1: return prefix + $" {{ {ToPrettyString(temp[0])} }}";
					default:
						return prefix + " {\n" + string.Join(", \n", temp.Select(x => Indentify(ToPrettyString(x)))) + "\n}";
				}
			}

			var printer = Printers[type];
			return printer.IsStatic
				? (string) printer.Invoke(null, new[] { obj })
				: (string) printer.Invoke(obj, null);
		}

		static string GenericPretty(object obj) {
			var type = obj.GetType();
			if(type.GetMethods(BindingFlags.Instance | BindingFlags.Public).Count(x => x.Name == "ToString" && x.DeclaringType != typeof(object)) != 0) return obj.ToString();

			var fields = type.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
			var prefix = type.FullName + " {";
			switch(fields.Length) {
				case 0: return prefix;
				case 1: return prefix + $" {fields[0].Name} = {fields[0].GetValue(obj).ToPrettyString()} }}";
				default:
					return prefix + "\n" + string.Join(", \n", fields.Select(field => Indentify(field.Name + " = " + field.GetValue(obj).ToPrettyString()))) + "\n}";
			}
		}
		
		[PrettyPrinter]
		static string PrettyPrintThis(string value) {
			var ret = "\"";
			foreach(var c in value) {
				switch(c) {
					case '\t': ret += "\\t"; break;
					case '\n': ret += "\\n"; break;
					case '\r': ret += "\\r"; break;
					case '\\': ret += "\\\\"; break;
					case '"': ret += "\\\""; break;
					case { } _ when char.IsControl(c) || c < ' ' || c >= 0x7f && c <= 0xff: ret += $"\\x{(int) c:x02}"; break;
					case { } _ when c > 0xff: ret += "\\u..."; break; // TODO: Implement algo from unicodeescape_string: https://svn.python.org/projects/python/trunk/Objects/unicodeobject.c
					default: ret += c; break;
				}
			}
			return ret + "\"";
		}

		[PrettyPrinter]
		static string PrettyPrintThis(Type type) => 
			type.IsGenericType
				? $"{type.FullName.Split('`')[0]}<{string.Join(", ", type.GetGenericArguments().Select(PrettyPrintThis))}>"
				: type.ToString();
	}
}