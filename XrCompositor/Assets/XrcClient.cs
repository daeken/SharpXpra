using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using Timer = System.Timers.Timer;

namespace SharpXpra {
	public class XrcClient {
		bool Alive = true;
		readonly CompositorBehavior Behavior;
		readonly Socket Socket;
		
		public XrcClient(Socket socket, CompositorBehavior behavior) {
			Socket = socket;
			Behavior = behavior;
			var pingCount = 0;
			var pingTimer = new Timer { AutoReset = true, Interval = 5000 };
			pingTimer.Elapsed += (_, __) => {
				lock(this) {
					if(pingCount >= 12 || !Alive) {
						// Client timed out
						Alive = false;
						pingTimer.Stop();
						return;
					}
					pingCount++;
					Send(1);
				}
			};
			pingTimer.Start();
			
			new Thread(() => {
				var minibuf = new byte[128];
				while(Alive) {
					try {
						var off = 0;
						while(off < 8)
							off += socket.Receive(minibuf, off, 8 - off, SocketFlags.None);
						var len = BitConverter.ToInt32(minibuf, 0);
						Debug.Assert(len >= 0);
						var opcode = BitConverter.ToUInt32(minibuf, 4);
						Behavior.Log($"Got message with opcode {opcode} and length {len}");
						byte[] data = null;
						if(len > 128)
							data = new byte[len];
						else if(len > 0)
							data = minibuf;
						if(data != null) {
							off = 0;
							while(off < len)
								off += socket.Receive(data, off, len - off, SocketFlags.None);
						}

						// Reset ping timer
						pingTimer.Stop();
						pingTimer.Start();

						switch(opcode) {
							case 1: // Ping
								Send(2);
								break;
							case 2: // Pong
								pingCount--;
								break;
							case 1001: { // Run script
								Behavior.Log("Attempting to run script...");
								var code = Encoding.UTF8.GetString(data, 0, len);
								Behavior.Log($"Running script: {code.ToPrettyString()}");
								Behavior.JobQueue.Enqueue(() => Behavior.RunScriptCode(code));
								break;
							}
							case 1002: { // Load script
								var fnlen = BitConverter.ToInt32(data, 0);
								var fn = Encoding.UTF8.GetString(data, 4, fnlen);
								Behavior.JobQueue.Enqueue(() => {
									var path = Path.Combine(Application.persistentDataPath, fn);
									Behavior.Log($"Writing script to {path.ToPrettyString()}");
									try {
										File.Delete(path);
									} catch(Exception) { }

									using(var fp = File.OpenWrite(path))
										fp.Write(data, 4 + fnlen, len - 4 - fnlen);
									Behavior.LoadScript(path);
								});
								break;
							}
							case 1004: // Request logs
								Behavior.LogMessage += (isError, message) =>
									Send(2001,
										new[] { (byte) (isError ? 1 : 0) }.Concat(Encoding.UTF8.GetBytes(message))
											.ToArray());
								break;
							default:
								Behavior.Log($"Got message with unknown opcode {opcode} and length {len}");
								break;
						}
					} catch(Exception e) {
						Alive = false;
						Behavior.Log(e.ToString());
						break;
					}
				}
			}).Start();
		}

		public void Stop() {
			Alive = false;
			Socket.Close();
		}

		readonly byte[] SendMinibuf = new byte[128];
		void Send(uint opcode, byte[] data = null) {
			lock(this) {
				var packet = data == null || data.Length < 120 ? SendMinibuf : new byte[8 + data.Length];
				Array.Copy(BitConverter.GetBytes(data?.Length ?? 0), 0, packet, 0, 4);
				Array.Copy(BitConverter.GetBytes(opcode), 0, packet, 4, 4);
				if(data != null)
					Array.Copy(data, 0, packet, 8, data.Length);
				try {
					Socket.Send(packet, (data?.Length + 8) ?? 8, SocketFlags.None);
				} catch(Exception) {
					Alive = false;
				}
			}
		}
	}
}