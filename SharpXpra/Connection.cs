using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Sockets;
using System.Threading;

namespace SharpXpra {
	public class Connection {
		readonly Stream Stream;
		readonly CancellationTokenSource Canceller = new CancellationTokenSource();
		readonly BlockingCollection<List<object>> IncomingPackets = new BlockingCollection<List<object>>();
		readonly BlockingCollection<List<object>> OutgoingPackets = new BlockingCollection<List<object>>();

		[Flags]
		enum ProtocolFlags {
			Bencode = 0, 
			Rencode = 1, 
			Cipher = 2, 
			Yaml = 4
		}

		[Flags]
		enum CompressionFlags {
			Zlib = 0, 
			Lz4 = 0x10, 
			Lzo = 0x20, 
			Brotli = 0x40
		}
		
		public Connection(string hostname, int port) {
			var tcpClient = new TcpClient(hostname, port);
			Stream = tcpClient.GetStream();
			new Thread(() => {
				try {
					var header = new byte[8];
					var aux = new Dictionary<byte, byte[]>();
					while(true) {
						ReadAll(header);
						if(header[0] != 'P')
							throw new Exception();
						var protoflags = (ProtocolFlags) header[1];
						var compressionLevel = header[2];
						var index = header[3];
						var dataSize = ((uint) header[4] << 24) | ((uint) header[5] << 16) | 
						               ((uint) header[6] << 8) | header[7];
						if(dataSize > 256 * 1024 * 1024) // 256MB max message
							throw new Exception();
						var buf = new byte[dataSize];
						ReadAll(buf);
						if(compressionLevel != 0) {
							if(((CompressionFlags) compressionLevel).HasFlag(CompressionFlags.Lz4)) {
								throw new NotImplementedException();
							} else {
								using var ts = new MemoryStream();
								using var ms = new MemoryStream(buf, 2, (int) dataSize - 6);
								using var ds = new DeflateStream(ms, CompressionMode.Decompress);
								ds.CopyTo(ts);
								buf = ts.GetBuffer();
							}
						}

						if(index == 0 && protoflags != ProtocolFlags.Rencode)
							throw new NotSupportedException();
						if(index != 0) {
							aux[index] = buf;
							continue;
						}
						if(!(Rencode.Decode(buf) is List<object> packet))
							throw new NotSupportedException();
						foreach(var kv in aux)
							packet[kv.Key] = kv.Value;
						aux.Clear();
						IncomingPackets.Add(packet);
					}
				} catch(OperationCanceledException) { }
			}).Start();
			new Thread(() => {
				try {
					var header = new byte[8];
					header[0] = (byte) 'P';
					header[1] = (byte) ProtocolFlags.Rencode;
					header[2] = 0;
					header[3] = 0;
					foreach(var packet in OutgoingPackets.GetConsumingEnumerable(Canceller.Token)) {
						var enc = Rencode.Encode(packet);
						var len = (uint) enc.Length;
						unchecked {
							header[4] = (byte) (len >> 24);
							header[5] = (byte) (len >> 16);
							header[6] = (byte) (len >> 8);
							header[7] = (byte) len;
						}

						WriteAll(header);
						WriteAll(enc);
					}
				} catch(OperationCanceledException) { }
			}).Start();
		}

		public void Disconnect() {
			Canceller.Cancel();
			Stream.Close();
		}

		void ReadAll(byte[] buffer) {
			var off = 0;
			var size = buffer.Length;
			while(size > 0) {
				var task = Stream.ReadAsync(buffer, off, size);
				task.Wait(Canceller.Token);
				size -= task.Result;
				off += task.Result;
			}
		}

		void WriteAll(byte[] buffer) => Stream.WriteAsync(buffer, 0, buffer.Length).Wait(Canceller.Token);

		public void Send(List<object> packet) => OutgoingPackets.Add(packet);
		public bool TryGetIncoming(out List<object> packet) => IncomingPackets.TryTake(out packet);
	}
}