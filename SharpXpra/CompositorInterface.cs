using System.Collections.Generic;

namespace SharpXpra {
	public abstract class BaseCompositor<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT>, new() {
		public readonly List<WindowT> Windows = new List<WindowT>();

		public WindowT CreateWindow(int wid) {
			var window = new WindowT {
				Compositor = (CompositorT) this, 
				WindowId = wid
			};
			Windows.Add(window);
			window.Create();
			return window;
		}
		
		public virtual void Log(string message) {}
		public virtual void Error(string message) {}
	}

	public abstract class BaseWindow<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT>, new() {
		public CompositorT Compositor { get; internal set; }
		public virtual string Title { get; set; }
		public int WindowId;
		internal (int, int) Position => (WindowId % 100 * 10000, WindowId / 100 * 10000);
		public virtual (int W, int H) BufferSize { get; set; }
		public virtual void Create() {}
	}
}