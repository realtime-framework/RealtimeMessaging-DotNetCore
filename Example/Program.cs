using System;
using System.Threading.Tasks;
using RealtimeMessaging.DotNetCore;
using RealtimeMessaging.DotNetCore.Extensibility;

namespace RTMCore
{
	class Program
	{
		static void Main(string[] args)
		{

			var p = new Program();
			p.Start().Wait();
			while (true) { }
		}


		private OrtcClient ortcClient;


		public async Task Start()
		{
			var api = new Ortc();
			IOrtcFactory factory = api.LoadOrtcFactory("RealTimeSJ");
			ortcClient = factory.CreateClient();

			ortcClient.ClusterUrl = "http://ortc-developers.realtime.co/server/2.1/";
			ortcClient.ConnectionMetadata = "myConnectionMetadata";

			ortcClient.OnConnected += new OnConnectedDelegate(ortc_OnConnected);
			ortcClient.OnDisconnected += new OnDisconnectedDelegate(ortc_OnDisconnected);
			ortcClient.OnSubscribed += new OnSubscribedDelegate(ortc_OnSubscribed);
			ortcClient.OnUnsubscribed += new OnUnsubscribedDelegate(ortc_OnUnsubscribed);
			ortcClient.OnException += new OnExceptionDelegate(ortc_OnException);
			ortcClient.OnReconnected += new OnReconnectedDelegate(ortc_OnReconnected);
			ortcClient.OnReconnecting += new OnReconnectingDelegate(ortc_OnReconnecting);


			Console.Out.WriteLine("connecting");
			await ortcClient.Connect("[YOUR_APPLICATION_KEY]", "myToken");
		}


		private void ortc_OnReconnecting(object sender)
		{
			Console.Out.WriteLine(string.Format("OnReconnecting"));
		}

		private void ortc_OnException(object sender, Exception ex)
		{
			Console.Out.WriteLine(string.Format("OnException:{0}", ex.ToString()));
		}

		private void ortc_OnReconnected(object sender)
		{
			Console.Out.WriteLine(string.Format("OnReconnected"));
		}

		private void ortc_OnUnsubscribed(object sender, string channel)
		{
			Console.Out.WriteLine(string.Format("OnUnsubscribed"));
		}

		private void ortc_OnSubscribed(object sender, string channel)
		{
			Console.Out.WriteLine(string.Format("subscribe channel:{0}", channel));
			ortcClient.Send(channel, "Hello world!!!");
		}

		private void ortc_OnDisconnected(object sender)
		{
			Console.Out.WriteLine(string.Format("OnDisconnect"));
		}

		private void ortc_OnConnected(object sender)
		{
			ortcClient.Subscribe("myChannel", true, (object ortc, string channel, string message) =>
			{
				Console.Out.WriteLine(string.Format("message: {0}, on channel: {1}", message, channel));
			});
		}
	}
}
