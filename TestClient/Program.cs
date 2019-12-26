using System.Collections.Generic;
using System.Threading;
using SharpXpra;

namespace TestClient {
	class Program {
		public static void Main(string[] args) {
			var compositor = new TestCompositor();
			var client = new Client<TestCompositor, TestWindow>("10.0.0.200", 10000, compositor);
			while(true) {
				client.Update();
				Thread.Sleep(10);
			}
		}
	}
}