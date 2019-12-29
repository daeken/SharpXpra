using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SharpXpra {
	public partial class Client<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT> {
		readonly Dictionary<string, Action<Client<CompositorT, WindowT>, List<object>>> Handlers =
			new Dictionary<string, Action<Client<CompositorT, WindowT>, List<object>>>();
		readonly Connection Connection;
		readonly CompositorT Compositor;

		public Client(string hostname, int port, CompositorT compositor) {
			Compositor = compositor;
			Compositor.Client = this;
			SetupHandlers();
			Connection = new Connection(hostname, port);
			SendHello();
		}

		public void Update() {
			while(Connection.TryGetIncoming(out var packet)) {
				if(!Handlers.TryGetValue((string) packet[0], out var handler))
					throw new NotImplementedException($"Unhandled packet: {packet.ToPrettyString()}");
				handler(this, packet);
			}
		}

		public void Disconnect() => Connection.Disconnect();

		[Handler("hello")]
		void HandleHello(Dictionary<object, object> capabilities) {
			//foreach(var kv in capabilities)
			//	Compositor.Log($"{kv.Key.ToPrettyString()} -- {kv.Value.ToPrettyString()}");
		}

		// TODO: We really should just be keeping a free list around and only rearranging when a new window won't fit
		internal void RearrangeWindows() {
			if(Compositor.TrueWindows.Count(window => window.Position.X == -1 && window.Position.Y == -1) == 0)
				return;
			// TODO: Remove hard-coding here; get max_desktop_size from hello
			var free = new List<(int Area, int X, int Y, int W, int H)> { (8192 * 4096, 0, 0, 8192, 4096) };
			foreach(var window in Compositor.TrueWindows.OrderByDescending(window => window.BufferSize.W * window.BufferSize.H)) {
				var (bw, bh) = window.BufferSize;
				var area = bw * bh;
				var found = false;
				foreach(var bin in free) {
					if(bin.Area < area || bin.W < bw || bin.H < bh) continue;
					found = true;
					free.Remove(bin);
					var nw = bin.W - bw;
					var nh = bin.H - bh;
					if(bin.W > bw && bin.H > bh) {
						free.Add((bw * nh, bin.X, bin.Y + bh, bw, nh));
						free.Add((nw * bin.H, bin.X + bw, bin.Y, nw, bin.H));
					} else if(bin.W > bw)
						free.Add((nw * bin.H, bin.X + bw, bin.Y, nw, bh));
					else if(bin.H > bh)
						free.Add((nh * bin.W, bin.X, bin.Y + bh, bw, nh));

					if(window.Position.X == bin.X && window.Position.Y == bin.Y) break;
					window.Position = (bin.X, bin.Y);
					Send("map-window", window.Id, window.Position, window.BufferSize);
					break;
				}
				if(!found)
					throw new Exception("Could not find open bin for window!");
			}
		}

		[Handler("new-window")]
		void HandleNewWindow(int wid, int x, int y, int w, int h, Dictionary<object, object> metadata,
			Dictionary<object, object> clientProperties) {
			Compositor.Log($"New window! {wid} @ {x}x{y} ({w}x{h}) {metadata.ToPrettyString()}");
			var window = Compositor.CreateWindow(wid);
			window.BufferSize = (w, h);
			if(metadata.ContainsKey("title"))
				window.Title = metadata["title"] as string;
			RearrangeWindows();
			Send("focus", wid);
		}

		[Handler("new-override-redirect")]
		void HandleNewOverrideRedirect(int wid, int x, int y, int w, int h, Dictionary<object, object> metadata,
			Dictionary<object, object> clientProperties) {
			Compositor.Log($"New popup? {wid} @ {x}x{y} ({w}x{h}) {metadata.ToPrettyString()}");
			WindowT parent = null;
			if(metadata.ContainsKey("transient-for")) {
				var transientFor = (int) metadata["transient-for"];
				parent = Compositor.Windows.First(window => window.Id == transientFor);
			} else
				foreach(var win in Compositor.Windows)
					if(win.Position.X >= x && win.Position.Y >= y &&
					   win.Position.X + win.BufferSize.W > x && win.Position.Y + win.BufferSize.H > y) {
						parent = win;
						break;
					}

			if(parent == null)
				parent = Compositor.TrueWindows.Select(win => (win, win.Position.X - x, win.Position.Y - y))
					.Select(t => (t.win, t.Item2 * t.Item2 + t.Item3 * t.Item3)).OrderBy(t => t.Item2).First().win;
			
			var window = Compositor.CreatePopup(wid, parent, x - parent.Position.X, y - parent.Position.Y);
			window.BufferSize = (w, h);
			window.Position = (x, y);
			Send("focus", wid);
		}

		[Handler("lost-window")]
		void HandleLostWindow(int wid) {
			Compositor.Log($"Lost window {wid}");
			var window = Compositor.Windows.FirstOrDefault(window => window.Id == wid);
			if(window == null) return;
			window.Closing();
			Compositor.Windows.Remove(window);
		}

		[Handler("startup-complete")]
		void HandleStartupComplete() { }

		[Handler("ping")]
		void HandlePing(long time, long uuid) =>
			Send("ping_echo", time, 0, 0, 0, -1);

		[Handler("draw")]
		void HandleDraw(int wid, int x, int y, int w, int h, string coding, byte[] data, int packet_sequence,
			int rowstride, Dictionary<object, object> options) {
			Compositor.Log($"Got draw! {wid} {x}x{y} {w}x{h} {coding.ToPrettyString()} {packet_sequence} {rowstride}");
			if(rowstride != w * 3)
				throw new NotImplementedException($"Rowstride != w * 3: {rowstride} vs {w} ({h} {coding} {x} {y})");
			var encoding = coding switch {
				"rgb24" => PixelEncoding.Rgb24, 
				"rgb32" => PixelEncoding.Rgb32, 
				_ => throw new NotImplementedException($"Unknown pixel encoding {coding}")
			};
			Compositor.Windows.First(window => window.Id == wid).Damage(x, y, w, h, encoding, data);
			Send("damage-sequence", packet_sequence, wid, w, h, 0, "");
		}
		
		[Handler("raise-window")]
		void HandleRaiseWindow(int wid) { }

		[Handler("window-metadata")]
		void HandleWindowMetadata(int wid, Dictionary<object, object> metadata) {
			Compositor.Log($"Window metadata {wid} {metadata.ToPrettyString()}");
			var window = Compositor.Windows.First(window => window.Id == wid);
			if(metadata.TryGetValue("title", out var title))
				window.Title = (string) title;
		}

		[Handler("window-move-resize")]
		void HandleWindowMoveResize(int wid, int x, int y, int w, int h, int resize_counter) {
			Compositor.Log($"Window moved/resized {wid} {x}x{y} {w}x{h}");
			var window = Compositor.Windows.First(window => window.Id == wid);
			window.BufferSize = (w, h);
			RearrangeWindows();
		}

		[Handler("configure-override-redirect")]
		void HandleConfigureOverrideRedirect(int wid, int x, int y, int w, int h) {
			Compositor.Log($"Popup moved/resized {wid} {x}x{y} {w}x{h}");
			var window = Compositor.Windows.First(window => window.Id == wid);
			window.Position = (x, y);
			window.BufferSize = (w, h);
		}

		public void SendMouseMove(int wid, int x, int y, bool[] buttons) {
			var window = Compositor.Windows.First(window => window.Id == wid);
			var (px, py) = window.Position;
			//Compositor.Log($"Sending mouse move: {wid} {px + x} {py + y}");
			Send("pointer-position", wid, new List<object> { px + x, py + y }, new List<object>(),
				buttons.Select(x => (object) x).ToList());
		}

		public void SendMouseButton(int wid, int x, int y, int button, bool pressed) {
			var window = Compositor.Windows.First(window => window.Id == wid);
			var (px, py) = window.Position;
			Send("button-action", wid, button, pressed, new List<object> { px + x, py + y }, new List<object>());
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