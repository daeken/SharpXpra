using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpXpra {
	public static class Rencode {
		enum TypeCode : byte {
			List = 59, 
			Dict = 60, 
			Int = 61, 
			Int1 = 62, 
			Int2 = 63, 
			Int4 = 64, 
			Int8 = 65, 
			Float32 = 66, 
			Float64 = 44, 
			True = 67, 
			False = 68, 
			None = 69, 
			Term = 127
		}

		public static object Decode(byte[] data) {
			var pos = 0;
			return Decode(data, ref pos);
		}
		
		public static object Decode(byte[] data, ref int pos) {
			if(pos >= data.Length)
				throw new Exception();
			unchecked {
				switch((TypeCode) data[pos++]) {
					case TypeCode.Int1:
						return (int) data[pos++];
					case TypeCode.Int2:
						return (int) (short) (ushort) (((uint) data[pos++] << 8) | data[pos++]);
					case TypeCode.Int4:
						return (int) (((uint) data[pos++] << 24) | ((uint) data[pos++] << 16) |
						              ((uint) data[pos++] << 8) | data[pos++]);
					case TypeCode.Int8:
						return (long) (((ulong) data[pos++] << 56) | ((ulong) data[pos++] << 48) |
						               ((ulong) data[pos++] << 40) | ((ulong) data[pos++] << 32) |
						               ((ulong) data[pos++] << 24) | ((ulong) data[pos++] << 16) |
						               ((ulong) data[pos++] << 8) | data[pos++]);
					case TypeCode v when (byte) v < 44:
						return (int) v;
					case TypeCode v when (byte) v >= 70 && (byte) v < 70 + 32:
						return -((int) v - 69);
					case TypeCode.Int:
						throw new NotImplementedException();
					case TypeCode.Float32: {
						var temp = new byte[4];
						for(var i = 0; i < 4; ++i)
							temp[3 - i] = data[pos++];
						return BitConverter.ToSingle(temp, 0);
					}
					case TypeCode.Float64: {
						var temp = new byte[8];
						for(var i = 0; i < 4; ++i)
							temp[7 - i] = data[pos++];
						return BitConverter.ToDouble(temp, 0);
					}
					case TypeCode v when (byte) v >= 128 && (byte) v < 128 + 64:
						var slen = (int) v - 128;
						pos += slen;
						return Encoding.UTF8.GetString(data, pos - slen, slen);
					case TypeCode v when (byte) v >= 49 && (byte) v <= 57: {
						var lstr = ((char) v).ToString();
						while(data[pos] != ':')
							lstr += (char) data[pos++];
						var len = int.Parse(lstr);
						pos += len + 1;
						return Encoding.UTF8.GetString(data, pos - len, len);
					}
					case TypeCode.None:
						return null;
					case TypeCode.True:
						return true;
					case TypeCode.False:
						return false;
					case TypeCode v when (byte) v >= 192: {
						var size = (int) v - 192;
						var list = new List<object>();
						for(var i = 0; i < size; ++i)
							list.Add(Decode(data, ref pos));
						return list;
					}
					case TypeCode.List: {
						var list = new List<object>();
						while((TypeCode) data[pos] != TypeCode.Term)
							list.Add(Decode(data, ref pos));
						pos++;
						return list;
					}
					case TypeCode v when (byte) v >= 102 && (byte) v < 102 + 25: {
						var size = (int) v - 102;
						var dict = new Dictionary<object, object>();
						for(var i = 0; i < size; ++i) {
							var key = Decode(data, ref pos);
							dict[key] = Decode(data, ref pos);
						}
						return dict;
					}
					case TypeCode.Dict: {
						var dict = new Dictionary<object, object>();
						while((TypeCode) data[pos] != TypeCode.Term) {
							var key = Decode(data, ref pos);
							dict[key] = Decode(data, ref pos);
						}
						pos++;
						return dict;
					}
					default:
						throw new NotSupportedException();
				}
			}
		}

		public static byte[] Encode(object obj) {
			var blist = new List<byte>();
			void SubEncode(object v) {
				unchecked {
					switch(v) {
						case true:
							blist.Add((byte) TypeCode.True);
							break;
						case false:
							blist.Add((byte) TypeCode.False);
							break;
						case null:
							blist.Add((byte) TypeCode.None);
							break;
						case int x when x >= 0 && x < 44:
							blist.Add((byte) x);
							break;
						case int x when x < 0 && x >= -32:
							blist.Add((byte) (-x + 69));
							break;
						case int x when x >= sbyte.MinValue && x <= sbyte.MaxValue:
							blist.Add((byte) TypeCode.Int1);
							blist.Add((byte) (sbyte) x);
							break;
						case int x when x >= short.MinValue && x <= short.MaxValue: {
							blist.Add((byte) TypeCode.Int2);
							var t = (ushort) (short) x;
							blist.Add((byte) (t >> 8));
							blist.Add((byte) t);
							break;
						}
						case int x: {
							blist.Add((byte) TypeCode.Int4);
							var t = (uint) x;
							blist.Add((byte) (t >> 24));
							blist.Add((byte) (t >> 16));
							blist.Add((byte) (t >> 8));
							blist.Add((byte) t);
							break;
						}
						case long x: {
							blist.Add((byte) TypeCode.Int8);
							var t = (ulong) x;
							blist.Add((byte) (t >> 56));
							blist.Add((byte) (t >> 48));
							blist.Add((byte) (t >> 40));
							blist.Add((byte) (t >> 32));
							blist.Add((byte) (t >> 24));
							blist.Add((byte) (t >> 16));
							blist.Add((byte) (t >> 8));
							blist.Add((byte) t);
							break;
						}
						case float x: {
							blist.Add((byte) TypeCode.Float32);
							var temp = BitConverter.GetBytes(x);
							for(var i = 3; i >= 0; i--)
								blist.Add(temp[i]);
							break;
						}
						case double x: {
							blist.Add((byte) TypeCode.Float64);
							var temp = BitConverter.GetBytes(x);
							for(var i = 7; i >= 0; i--)
								blist.Add(temp[i]);
							break;
						}
						case List<object> x when x.Count < 64: {
							blist.Add((byte) (192 + x.Count));
							foreach(var elem in x)
								SubEncode(elem);
							break;
						}
						case List<object> x: {
							blist.Add((byte) TypeCode.List);
							foreach(var elem in x)
								SubEncode(elem);
							blist.Add((byte) TypeCode.Term);
							break;
						}
						case Dictionary<object, object> x when x.Count < 25: {
							blist.Add((byte) (102 + x.Count));
							foreach(var kv in x) {
								SubEncode(kv.Key);
								SubEncode(kv.Value);
							}
							break;
						}
						case Dictionary<object, object> x: {
							blist.Add((byte) TypeCode.Dict);
							foreach(var kv in x) {
								SubEncode(kv.Key);
								SubEncode(kv.Value);
							}
							blist.Add((byte) TypeCode.Term);
							break;
						}
						case string x when x.Length < 64: {
							blist.Add((byte) (128 + x.Length));
							blist.AddRange(Encoding.UTF8.GetBytes(x));
							break;
						}
						case string x:
							blist.AddRange(Encoding.UTF8.GetBytes(x.Length.ToString()));
							blist.Add((byte) ':');
							blist.AddRange(Encoding.UTF8.GetBytes(x));
							break;
						default:
							throw new NotImplementedException($"Rencoding {v} ({v.GetType().FullName})");
					}
				}
			}
			SubEncode(obj);
			return blist.ToArray();
		}
	}
}