using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpXpra {
	public partial class Client {
		readonly Dictionary<string, Action<Client, List<object>>> Handlers =
			new Dictionary<string, Action<Client, List<object>>>();
		readonly Connection Connection;

		public Client() {
			SetupHandlers();
			Connection = new Connection();
			SendHello();
		}

		public void Update() {
			while(Connection.TryGetIncoming(out var packet)) {
				packet[0].Print();
				if(!Handlers.TryGetValue((string) packet[0], out var handler))
					throw new NotImplementedException($"Unhandled packet: {packet.ToPrettyString()}");
				packet.Print();
				handler(this, packet);
			}
		}

		[Handler("hello")]
		void HandleHello(Dictionary<object, dynamic> capabilities) {
			capabilities.Print();
		}

		[Handler("new-window")]
		void HandleNewWindow(int wid, int x, int y, int w, int h, Dictionary<object, object> metadata,
			Dictionary<object, object> clientProperties) {
			Console.WriteLine($"New window! {wid} @ {x}x{y} ({w}x{h}) {metadata.ToPrettyString()}");
		}

		[Handler("startup-complete")]
		void HandleStartupComplete() {
		}

		[Handler("ping")]
		void HandlePing(int time, object uuid) =>
			Send("ping_echo", time, 0, 0, 0, -1);

		void Send(params object[] args) => Connection.Send(args.ToList());

		void SendHello() =>
			Connection.Send(new List<object> {
				"hello", new Dictionary<object, object> {
					["version"] = "4.0",
					["encodings"] = new List<object> { "rgb32", "rgb24" },
					["rencode"] = true,
					["client_type"] = "SharpXpra XR",
				}
			});
	}
}