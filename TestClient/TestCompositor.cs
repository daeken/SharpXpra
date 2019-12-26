using System;
using SharpXpra;

namespace TestClient {
	public class TestCompositor : BaseCompositor<TestCompositor, TestWindow> {
		protected override TestWindow ConstructWindow(int wid) => new TestWindow(this, wid);
	}

	public class TestWindow : BaseWindow<TestCompositor, TestWindow> {
		protected override void UpdateTitle() => Console.WriteLine($"Window {Id} title updated to {Title}");
		protected override void UpdateBufferSize() =>
			Console.WriteLine($"Window {Id} buffer size updated to {BufferSize.ToPrettyString()}");

		public TestWindow(TestCompositor compositor, int id) : base(compositor, id) { }

		public override void Damage(int x, int y, int w, int h, PixelEncoding encoding, byte[] data) {
			throw new NotImplementedException();
		}
	}
}