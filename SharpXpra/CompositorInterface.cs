using System.Collections.Generic;

namespace SharpXpra {
	public enum PixelEncoding {
		Rgb24, 
		Rgb32
	}
	
	public abstract class BaseCompositor<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT> {
		public readonly List<WindowT> Windows = new List<WindowT>();

		public WindowT CreateWindow(int wid) {
			var window = ConstructWindow(wid);
			Windows.Add(window);
			return window;
		}

		protected abstract WindowT ConstructWindow(int wid);
		
		public virtual void Log(string message) {}
		public virtual void Error(string message) {}
	}

	public abstract class BaseWindow<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT> {
		public readonly CompositorT Compositor;
		public readonly int Id;

		string _Title;
		public string Title {
			get => _Title;
			set {
				if(_Title == value) return;
				_Title = value;
				UpdateTitle();
			}
		}

		internal (int, int) Position => (Id % 100 * 10000, Id / 100 * 10000);
		(int W, int H) _BufferSize;

		public (int W, int H) BufferSize {
			get => _BufferSize;
			set {
				if(_BufferSize.W == value.W && _BufferSize.H == value.H) return;
				_BufferSize = value;
				UpdateBufferSize();
			}
		}

		protected BaseWindow(CompositorT compositor, int id) {
			Compositor = compositor;
			Id = id;
		}
		
		protected virtual void UpdateTitle() {}
		protected virtual void UpdateBufferSize() {}
		
		public abstract void Damage(int x, int y, int w, int h, PixelEncoding encoding, byte[] data);
	}
}