using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Timer = System.Timers.Timer;

namespace SharpXpra {
	public class XrcServer {
		readonly TcpListener Listener;
		readonly List<XrcClient> Clients = new List<XrcClient>();
		bool Alive = true;

		public XrcServer(CompositorBehavior behavior) {
			Listener = TcpListener.Create(31337);
			Listener.Start();
			var broadcastTimer = new Timer { AutoReset = true, Interval = 100 };
			var broadcastClient = new UdpClient { EnableBroadcast = true };
			var ep = new IPEndPoint(IPAddress.Broadcast, 31337);
			var nameBytes = Encoding.UTF8.GetBytes("My XrCompositor");
			var packet = new byte[6 + nameBytes.Length];
			Array.Copy(BitConverter.GetBytes((ushort) 31337), 0, packet, 0, 2);
			Array.Copy(BitConverter.GetBytes((uint) nameBytes.Length), 0, packet, 2, 4);
			Array.Copy(nameBytes, 0, packet, 6, nameBytes.Length);
			broadcastTimer.Elapsed += (_, __) => {
				if(!Alive)
					broadcastTimer.Stop();
				else
					broadcastClient.Send(packet, packet.Length, ep);
			};
			broadcastTimer.Start();
			new Thread(() => {
				while(Alive) {
					if(Listener.Pending())
						Clients.Add(new XrcClient(Listener.AcceptSocket(), behavior));
					else
						Thread.Sleep(50);
				}
			}).Start();
		}

		public void Stop() {
			Alive = false;
			foreach(var client in Clients)
				client.Stop();
			Listener.Stop();
		}
	}
}