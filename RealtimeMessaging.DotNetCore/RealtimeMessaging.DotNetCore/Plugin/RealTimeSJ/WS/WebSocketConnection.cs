using System;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using RealtimeMessaging.DotNetCore.Extensibility;
using System.Net.WebSockets;
using System.Collections.Generic;
using System.Threading;
using System.Linq;

namespace RealtimeMessaging.DotNetCore.Plugin.WS
{
    class WebSocketConnection
    {
        #region Attributes (1)

        private ClientWebSocket _websocket = null;
        private const int receiveChunkSize = 2048;
        private const int getStateTime = 5000;

        #endregion

        #region Methods - Public (3)

        public async Task ConnectAsync(string url)
        {
            if (!IsAvailableNetworkActive())
            {
                websocket_Closed();
                return;
            }

            Uri uri = null;

            string connectionId = Strings.RandomString(8);
            int serverId = Strings.RandomNumber(1, 1000);

            try
            {
                uri = new Uri(url);
            }
            catch (Exception)
            {
                throw new OrtcEmptyFieldException(String.Format("Invalid URL: {0}", url));
            }

            string prefix = uri != null && "https".Equals(uri.Scheme) ? "wss" : "ws";

            Uri connectionUrl = new Uri(String.Format("{0}://{1}:{2}/broadcast/{3}/{4}/websocket", prefix, uri.DnsSafeHost, uri.Port, serverId, connectionId));

            _websocket = new ClientWebSocket();
            try
            {
                await _websocket.ConnectAsync(connectionUrl, new CancellationTokenSource().Token);
                stateTimerAction(null);
                await ReceiveMessage(_websocket);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Exception: {0}", ex);
            }
            finally
            {
                if (_websocket != null)
                {
                    _websocket.Dispose();
                    _websocket = null;
                    websocket_Closed();
                }
            }

        }

        private void stateTimerAction(Object stateInfo)
        {
            GetState();

            if (_stateTimer != null)
            {
                _stateTimer.Dispose();
                _stateTimer = null;
            }

            _stateTimer = new Timer(stateTimerAction, AutoEvent, getStateTime, getStateTime);
        }

        private static WebSocketState prev;

        private void GetState()
        {
            if(!IsAvailableNetworkActive()){
                if(_websocket != null)
                    _websocket.Dispose();
                websocket_Closed();
                if(_stateTimer != null){
					_stateTimer.Dispose();
					_stateTimer = null;
                }
                return;
            }

            if (_websocket == null ||_websocket.State == prev)
            {
                return;
            }

            prev = _websocket.State;

            switch (prev)
            {
                case WebSocketState.Open:
                    websocket_Opened();
                    break;
                case WebSocketState.CloseReceived:
                case WebSocketState.Closed:
                    websocket_Closed();
                    break;
                case WebSocketState.Aborted:
                    websocket_Error();
                    break;
            }
        }

        private static Semaphore _pool;
		public static bool IsAvailableNetworkActive()
		{
			_pool = new Semaphore(0, 3);
            bool _IsAvailableNetworkActive = false;
            try
            {
                Balancer.GetServerFromBalancerAsync(Balancer.lastBalancerUrl, (server, ex) =>
                {
                    if (server != null && !server.Equals(""))
                        _IsAvailableNetworkActive = true;
                    _pool.Release(1);
                });
                _pool.WaitOne();
                _pool.Dispose();

            }catch(Exception e){
                e.ToString();
            }
            _pool = null;
            return _IsAvailableNetworkActive;
		}

        public async Task CloseAsync()
        {
            if (_websocket != null)
            {
                if (_websocket.State != WebSocketState.Connecting && _websocket.State != WebSocketState.Closed)
                {
                    await _websocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye bye", new CancellationTokenSource().Token);
                }
            }
        }

        public async Task SendAsync(string message)
        {
            if (_websocket != null)
            {
                await SendMessage(_websocket, Serialize(message));
            }
        }

        #endregion

        #region Methods - Private (1)


        static UTF8Encoding encoder = new UTF8Encoding();
        private Timer _stateTimer;
        private AutoResetEvent _autoEvent;

        public AutoResetEvent AutoEvent { get => _autoEvent; set => _autoEvent = value; }

        private async Task SendMessage(ClientWebSocket webSocket, String message)
        {
            try{
                byte[] buffer = encoder.GetBytes(message);
                await webSocket.SendAsync(new ArraySegment<byte>(buffer), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (Exception e)
			{
				e.ToString();
			}
        }
        private StringBuilder rcvMsg = new StringBuilder();

        private async Task ReceiveMessage(ClientWebSocket webSocket)
        {
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var rcvBytes = new byte[receiveChunkSize];
                    var rcvBuffer = new ArraySegment<byte>(rcvBytes);
                    WebSocketReceiveResult rcvResult = await _websocket.ReceiveAsync(rcvBuffer, new CancellationTokenSource().Token);
                    byte[] msgBytes = rcvBuffer.Skip(rcvBuffer.Offset).Take(rcvResult.Count).ToArray();
                    string temp = Encoding.UTF8.GetString(msgBytes);

                    if (temp.Length > 1)
                    {

                        if (temp.ToCharArray()[0] == 'a' && temp.ToCharArray()[1] == '[' && temp.ToCharArray()[temp.Length - 1] == ']')
                        {
                            websocket_MessageReceived(temp);
                        }
                        else if (temp.ToCharArray()[0] == 'a' && temp.ToCharArray()[1] == '[' && temp.ToCharArray()[temp.Length - 1] != ']')
                        {
                            rcvMsg.Append(temp);
                        }
                        else if (temp.ToCharArray()[0] != 'a' && temp.ToCharArray()[1] != '[' && temp.ToCharArray()[temp.Length - 1] != ']')
                        {
                            rcvMsg.Append(temp);
                        }
                        else if (temp.ToCharArray()[0] != 'a' && temp.ToCharArray()[1] != '[' && temp.ToCharArray()[temp.Length - 1] == ']')
                        {                        
                            rcvMsg.Append(temp);
                            websocket_MessageReceived(rcvMsg.ToString());
                            rcvMsg.Clear();
                        }
                    }else{
                        if(temp.ToCharArray()[0] == 'o' || temp.ToCharArray()[0] == 'h')
                            websocket_MessageReceived(temp);
                        else
                            rcvMsg.Append(temp);
                    }
                }
            }
            catch (Exception e)
            {
                e.ToString();
            }
        }


        /// <summary>
        /// Serializes the specified data.
        /// </summary>
        /// <param name="data">The data.</param>
        /// <returns></returns>
        private string Serialize(object data)
        {
            string result = "";

            try
            {
                DataContractJsonSerializer serializer = new DataContractJsonSerializer(data.GetType());
                var stream = new System.IO.MemoryStream();
                serializer.WriteObject(stream, data);
                string jsonData = Encoding.UTF8.GetString(stream.ToArray(), 0, (int)stream.Length);
                stream.Dispose();
                result = jsonData;
            }
            catch (Exception e)
            {
                e.ToString();
            }

            return result;
        }

        #endregion

        #region Delegates (4)

        public delegate void onOpenedDelegate();
        public delegate void onClosedDelegate();
        public delegate void onErrorDelegate(Exception error);
        public delegate void onMessageReceivedDelegate(string message);

        #endregion

        #region Events (4)

        public event onOpenedDelegate OnOpened;
        public event onClosedDelegate OnClosed;
        public event onErrorDelegate OnError;
        public event onMessageReceivedDelegate OnMessageReceived;

        #endregion

        #region Events Handles (4)
        Semaphore _semReceivedMsg = new Semaphore(1, 1);
        private void websocket_MessageReceived(String message)
        {
            _semReceivedMsg.WaitOne();
            var ev = OnMessageReceived;

            if (ev != null)
            {
                string msgCpy = new string(message.ToCharArray());
                Task.Factory.StartNew(() => {
                    ev(msgCpy);
                    _semReceivedMsg.Release(1);
                });
            }
        }

        private void websocket_Opened()
        {
            var ev = OnOpened;

            if (ev != null)
            {
                Task.Factory.StartNew(() => ev());
            }
        }

        private void websocket_Closed()
        {
            var ev = OnClosed;

            if (ev != null)
            {
                Task.Factory.StartNew(() => ev());
            }
        }

        private void websocket_Error()
        {
            var ev = OnError;

            if (ev != null)
            {
                Task.Factory.StartNew(() => ev(new Exception()));
            }
        }

        #endregion
    }
}
