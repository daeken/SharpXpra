using SharpXpra;

namespace TestClient {
	public class TestCompositor : BaseCompositor<TestCompositor, TestWindow> {
	}

	public class TestWindow : BaseWindow<TestCompositor, TestWindow> {
	}
}