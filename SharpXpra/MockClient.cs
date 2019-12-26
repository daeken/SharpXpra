namespace SharpXpra {
	public class MockClient<CompositorT, WindowT>
		where CompositorT : BaseCompositor<CompositorT, WindowT>
		where WindowT : BaseWindow<CompositorT, WindowT> {
		public readonly CompositorT Compositor;

		public int Frames;
		
		public MockClient(string hostname, int port, CompositorT compositor) {
			Compositor = compositor;
			Compositor.CreateWindow(1).BufferSize = (640, 480);
			Compositor.CreateWindow(2).BufferSize = (800, 600);
		}

		public void Update() {
			foreach(var window in Compositor.Windows) {
				window.Title = $"Window {window.Id} -- {Frames / 200}";
			}
			Frames++;
		}
	}
}