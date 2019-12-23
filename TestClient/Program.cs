using System.Collections.Generic;
using System.Threading;
using SharpXpra;

namespace TestClient {
	internal class Program {
		public static void Main(string[] args) {
			var client = new Client();
			while(true) {
				client.Update();
				Thread.Sleep(10);
			}
		}
	}
}