using System;
using System.Collections.Generic;
using System.Linq;

namespace SharpXpra {
	public enum PixelEncoding {
		Rgb24, 
		Rgb32
	}
	
	public abstract class BaseCompositor<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT> {
		public readonly List<WindowT> Windows = new List<WindowT>();
		public List<WindowT> TrueWindows => Windows.Where(x => !x.IsPopup).ToList();
		public List<WindowT> PopupWindows => Windows.Where(x => x.IsPopup).ToList();
		public Client<CompositorT, WindowT> Client;

		public WindowT CreateWindow(int wid) {
			var window = ConstructWindow(wid);
			Windows.Add(window);
			return window;
		}

		public WindowT CreatePopup(int wid, WindowT parent, int x, int y) {
			var window = ConstructPopup(wid, parent, x, y);
			Windows.Add(window);
			return window;
		}

		protected abstract WindowT ConstructWindow(int wid);
		protected abstract WindowT ConstructPopup(int wid, WindowT parent, int x, int y);
		
		public virtual void Log(string message) {}
		public virtual void Error(string message) {}
	}

	public abstract class BaseWindow<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT> {
		public readonly CompositorT Compositor;
		public readonly int Id;
		public readonly bool IsPopup;

		string _Title;
		public string Title {
			get => _Title;
			set {
				if(_Title == value) return;
				_Title = value;
				UpdateTitle();
			}
		}

		public (int X, int Y) Position = (-1, -1);
		(int W, int H) _BufferSize;

		public (int W, int H) BufferSize {
			get => _BufferSize;
			set {
				if(_BufferSize.W == value.W && _BufferSize.H == value.H) return;
				_BufferSize = value;
				UpdateBufferSize();
			}
		}

		public void MouseMove(int x, int y, bool[] buttons) => Compositor.Client.SendMouseMove(Id, x, y, buttons);
		public void MouseButton(int x, int y, int button, bool pressed) =>
			Compositor.Client.SendMouseButton(Id, x, y, button, pressed);

		protected BaseWindow(CompositorT compositor, int id, bool isPopup) {
			Compositor = compositor;
			Id = id;
			IsPopup = isPopup;
		}
		
		protected virtual void UpdateTitle() {}
		protected virtual void UpdateBufferSize() {}
		public virtual void Closing() {}
		
		public abstract void Damage(int x, int y, int w, int h, PixelEncoding encoding, byte[] data);
	}
}