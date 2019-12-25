using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpXpra {
	public partial class Client<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT>, new() {
		readonly Dictionary<string, Action<Client<CompositorT, WindowT>, List<object>>> Handlers =
			new Dictionary<string, Action<Client<CompositorT, WindowT>, List<object>>>();
		readonly Connection Connection;
		readonly CompositorT Compositor;

		public Client(string hostname, int port, CompositorT compositor) {
			Compositor = compositor;
			SetupHandlers();
			Connection = new Connection(hostname, port);
			SendHello();
		}

		public void Update() {
			while(Connection.TryGetIncoming(out var packet)) {
				packet[0].Print();
				if(!Handlers.TryGetValue((string) packet[0], out var handler))
					throw new NotImplementedException($"Unhandled packet: {packet[0].ToPrettyString()}");
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
			var window = Compositor.CreateWindow(wid);
			window.BufferSize = (w, h);
			Send("map-window", wid, window.Position, w, h);
			Send("buffer-refresh", wid, null, 100);
		}

		[Handler("startup-complete")]
		void HandleStartupComplete() {
		}

		[Handler("ping")]
		void HandlePing(long time, long uuid) =>
			Send("ping_echo", time, 0, 0, 0, -1);

		[Handler("draw")]
		void HandleDraw(int wid, int x, int y, int w, int h, object coding, byte[] data, int packet_sequence,
			int rowstride, Dictionary<object, object> options) {
			Console.WriteLine(
				$"Got draw! {wid} {x}x{y} {w}x{h} {coding.ToPrettyString()} {packet_sequence} {rowstride}");
		}

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