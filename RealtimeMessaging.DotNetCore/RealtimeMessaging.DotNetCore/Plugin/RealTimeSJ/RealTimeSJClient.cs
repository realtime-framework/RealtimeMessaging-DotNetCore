using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization.Formatters;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using RealtimeMessaging.DotNetCore.Extensibility;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.Threading.Timeout;
using RealtimeMessaging.DotNetCore.Plugin.WS;


namespace RealtimeMessaging.DotNetCore.Plugin.RealTimeSJ
{
    /// <summary>
    /// Real Time SJ type client.
    /// </summary>
    public class RealTimeSJClient : OrtcClient
    {
        #region Constants (11)

        // REGEX patterns
        private const string OPERATION_PATTERN = @"^a\[""{""op"":""(?<op>[^\""]+)"",(?<args>.*)}""\]$";
        private const string CLOSE_PATTERN = @"^c\[?(?<code>[^""]+),?""?(?<message>.*)""?\]?$";
        private const string VALIDATED_PATTERN = @"^(""up"":){1}(?<up>.*)?,""set"":(?<set>.*)$";
        private const string CHANNEL_PATTERN = @"^""ch"":""(?<channel>.*)""$";
        private const string EXCEPTION_PATTERN = @"^""ex"":{(""op"":""(?<op>[^""]+)"",)?(""ch"":""(?<channel>.*)"",)?""ex"":""(?<error>.*)""}$";
        private const string RECEIVED_PATTERN = @"^a\[""{""ch"":""(?<channel>.*)"",""m"":""(?<message>[\s\S]*)""}""\]$";
        private const string JSON_PATTERN = @"^a\["".*""\]$";
        private const string MULTI_PART_MESSAGE_PATTERN = @"^(?<messageId>.[^_]*)_(?<messageCurrentPart>.[^-]*)-(?<messageTotalPart>.[^_]*)_(?<message>[\s\S]*)$";
        private const string PERMISSIONS_PATTERN = @"""(?<key>[^""]+)"":{1}""(?<value>[^,""]+)"",?";
        private const string CLUSTER_RESPONSE_PATTERN = @"var SOCKET_SERVER = ""(?<host>.*)"";";

        #endregion

        #region Attributes (14)

        private string _applicationKey;
        private string _authenticationToken;
        private bool _isConnecting;
        private bool _alreadyConnectedFirstTime;
        private bool _stopReconnecting;
        private bool _callDisconnectedCallback;
        private bool _waitingServerResponse;
        private List<KeyValuePair<string, string>> _permissions;
        private ConcurrentDictionary<string, ChannelSubscription> _subscribedChannels;
        private ConcurrentDictionary<string, ConcurrentDictionary<int, BufferedMessage>> _multiPartMessagesBuffer;
        private WebSocketConnection _webSocketConnection;
        private DateTime? _reconnectStartedAt;
        private DateTime? _lastKeepAlive; // Holds the time of the last keep alive received from the server
        private SynchronizationContext _synchContext; // To synchronize different contexts, preventing cross-thread operation errors (Windows Application and WPF Application))
        private Task _reconnectTimer; // Timer to reconnect
        private int _sessionExpirationTime; // minutes
        private System.Threading.Timer _heartbeatTimer;
        private AutoResetEvent _autoEvent;

        #endregion

        #region Constructor (1)

        /// <summary>
        /// Initializes a new instance of the <see cref="RTRealTimeSClient"/> class.
        /// </summary>
        public RealTimeSJClient()
        {
            ConnectionTimeout = 5000;
            _sessionExpirationTime = 30;

            IsConnected = false;
            IsCluster = false;
            _isConnecting = false;
            _alreadyConnectedFirstTime = false;
            _stopReconnecting = false;
            _callDisconnectedCallback = false;
            _waitingServerResponse = false;

            HeartbeatActive = false;
            HeartbeatFails = 3;
            HeartbeatTime = 15;

            _autoEvent = new AutoResetEvent(false);


            _permissions = new List<KeyValuePair<string, string>>();

            _lastKeepAlive = null;
            _reconnectStartedAt = null;
            _reconnectTimer = null;

            _subscribedChannels = new ConcurrentDictionary<string, ChannelSubscription>();
            _multiPartMessagesBuffer = new ConcurrentDictionary<string, ConcurrentDictionary<int, BufferedMessage>>();

            // To catch unobserved exceptions
            TaskScheduler.UnobservedTaskException += new EventHandler<UnobservedTaskExceptionEventArgs>(TaskScheduler_UnobservedTaskException);

            // To use the same context inside the tasks and prevent cross-thread operation errors (Windows Application and WPF Application)
            _synchContext = System.Threading.SynchronizationContext.Current;

            _webSocketConnection = new WebSocketConnection();

            _webSocketConnection.OnOpened += new WebSocketConnection.onOpenedDelegate(_webSocketConnection_OnOpened);
            _webSocketConnection.OnClosed += new WebSocketConnection.onClosedDelegate(_webSocketConnection_OnClosedAsync);
            _webSocketConnection.OnError += new WebSocketConnection.onErrorDelegate(_webSocketConnection_OnErrorAsync);
            _webSocketConnection.OnMessageReceived += new WebSocketConnection.onMessageReceivedDelegate(_webSocketConnection_OnMessageReceivedAsync);
        }

        #endregion

        #region Public Methods (6)

        /// <summary>
        /// Connects to the gateway with the application key and authentication token. The gateway must be set before using this method.
        /// </summary>
        /// <param name="appKey">Your application key to use ORTC.</param>
        /// <param name="authToken">Authentication token that identifies your permissions.</param>
        /// <example>
        ///   <code>
        /// ortcClient.Connect("myApplicationKey", "myAuthenticationToken");
        ///   </code>
        ///   </example>
        public override async Task Connect(string appKey, string authToken)
        {
            #region Sanity Checks

            if (IsConnected)
            {
                DelegateExceptionCallback(new OrtcAlreadyConnectedException("Already connected"));
            }
            else if (String.IsNullOrEmpty(ClusterUrl) && String.IsNullOrEmpty(Url))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("URL and Cluster URL are null or empty"));
            }
            else if (String.IsNullOrEmpty(appKey))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Application Key is null or empty"));
            }
            else if (String.IsNullOrEmpty(authToken))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Authentication ToKen is null or empty"));
            }
            else if (!IsCluster && !Url.OrtcIsValidUrl())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Invalid URL"));
            }
            else if (IsCluster && !ClusterUrl.OrtcIsValidUrl())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Invalid Cluster URL"));
            }
            else if (!appKey.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Application Key has invalid characters"));
            }
            else if (!authToken.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Authentication Token has invalid characters"));
            }
            else if (AnnouncementSubChannel != null && !AnnouncementSubChannel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Announcement Subchannel has invalid characters"));
            }
            else if (!String.IsNullOrEmpty(ConnectionMetadata) && ConnectionMetadata.Length > MAX_CONNECTION_METADATA_SIZE)
            {
                DelegateExceptionCallback(new OrtcMaxLengthException(String.Format("Connection metadata size exceeds the limit of {0} characters", MAX_CONNECTION_METADATA_SIZE)));
            }
            else if (_isConnecting || _reconnectStartedAt != null)
            {
                DelegateExceptionCallback(new OrtcAlreadyConnectedException("Already trying to connect"));
            }
            else

            #endregion
            {
                _stopReconnecting = false;

                _authenticationToken = authToken;
                _applicationKey = appKey;

                await DoConnectAsync();
            }
        }


		/// <summary>
		/// Publish a message to a channel.
		/// </summary>
		/// <param name="channel">Channel name.</param>
		/// <param name="message">Message to be sent.</param>
		/// <param name="ttl">The message expiration time in seconds (0 for maximum allowed ttl).</param>
		/// <param name="callback">Returns error if message publish was not successful or published message unique id (seqId) if sucessfully published</param>
		/// <example>
		///   <code>
		/// ortcClient.publish("channelName", "messageToSend");
		///   </code>
		///   </example>
		public override void publish(string channel, string message, int ttl, OnPublishResultDelegate callback){
            #region Sanity Checks

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcNotConnectedException("Not connected"));
            }
            else if (String.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Channel is null or empty"));
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Channel has invalid characters"));
            }
            else if (String.IsNullOrEmpty(message))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Message is null or empty"));
            }
            else

            #endregion
            { 
                byte[] channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    DelegateExceptionCallback(new OrtcMaxLengthException(String.Format("Channel size exceeds the limit of {0} characters", MAX_CHANNEL_SIZE)));
                }
                else
                {
                    var domainChannelCharacterIndex = channel.IndexOf(':');
                    var channelToValidate = channel;

                    if (domainChannelCharacterIndex > 0)
                    {
                        channelToValidate = channel.Substring(0, domainChannelCharacterIndex + 1) + "*";
                    }

                    string hash = _permissions.Where(c => c.Key == channel || c.Key == channelToValidate).FirstOrDefault().Value;

                    if (_permissions != null && _permissions.Count > 0 && String.IsNullOrEmpty(hash))
                    {
                        DelegateExceptionCallback(new OrtcNotConnectedException(String.Format("No permission found to send to the channel '{0}'", channel)));
                    }
                    else
                    {
                        message = message.Replace(Environment.NewLine, "\n");

                        if (channel != String.Empty && message != String.Empty)
                        {
                            try
                            {
                                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                                ArrayList messageParts = new ArrayList();
                                int pos = 0;
                                int remaining;
                                string messageId = Strings.GenerateId(8);

                                // Multi part
                                while ((remaining = messageBytes.Length - pos) > 0)
                                {
                                    byte[] messagePart;

                                    if (remaining >= MAX_MESSAGE_SIZE - channelBytes.Length)
                                    {
                                        messagePart = new byte[MAX_MESSAGE_SIZE - channelBytes.Length];
                                    }
                                    else
                                    {
                                        messagePart = new byte[remaining];
                                    }

                                    Array.Copy(messageBytes, pos, messagePart, 0, messagePart.Length);

                                    messageParts.Add(Encoding.UTF8.GetString((byte[])messagePart));

                                    pos += messagePart.Length;
                                }


                                Task ackTimeout = Task.Run(async () =>
                                {
                                    await Task.Delay(this.publishTimeout);
                                    if (pendingPublishMessages.ContainsKey(messageId))
                                    {
                                        String err = String.Format("Message publish timeout after {0} seconds", publishTimeout);
                                        if (pendingPublishMessages != null && ((Dictionary<string, object>)pendingPublishMessages[messageId]).ContainsKey("callback"))
                                        {
                                            OnPublishResultDelegate callbackP = (OnPublishResultDelegate)((Dictionary<string, object>)pendingPublishMessages[messageId])["callback"];
                                            callbackP(err, null);
                                            pendingPublishMessages.Remove(messageId);
                                        }
                                        pendingPublishMessages.Remove(messageId);
                                    }
                                });

                                Dictionary<string, object> pendingMsg = new Dictionary<string, object>();
                                pendingMsg.Add("totalNumOfParts", messageParts.Count);
                                pendingMsg.Add("callback", callback);
                                pendingMsg.Add("timeout", ackTimeout);

                                this.pendingPublishMessages.Add(messageId, pendingMsg);

                                if (messageParts.Count < 20)
                                {
                                    this.flushMessages(channel, ttl, messageParts, 0);
                                }else{
                                    this.flushMessages(channel, ttl, messageParts, 100);
                                }
                            }
                            catch (Exception ex)
                            {
                                string exName = null;

                                if (ex.InnerException != null)
                                {
                                    exName = ex.InnerException.GetType().Name;
                                }

                                switch (exName)
                                {
                                    case "OrtcNotConnectedException":
                                        // Server went down
                                        if (IsConnected)
                                        {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                            DoDisconnectAsync();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                                        }
                                        break;
                                    default:
                                        DelegateExceptionCallback(new OrtcGenericException(String.Format("Unable to send: {0}", ex)));
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        private void flushMessages(string channel, int ttl, ArrayList messageParts, int delay){
            this.partSendInterval = Task.Run(async () =>
            {
                foreach (KeyValuePair<String, String> messageToSend in messageParts)
                {
                    await Task.Delay(delay);
                    string escapedMessage = new StringContent(messageToSend.Value, System.Text.Encoding.UTF8, "application/json").ToString();

                    String messageParsed = String.Format("publish;%s;%s;%s;%s;%s;%s",
                                                         this._applicationKey, this._authenticationToken, channel,
                                                         ttl,
                                                         _permissions,
                                                         String.Format("{0}_{1}", messageToSend.Key, escapedMessage));
                    await DoSendAsync(messageParsed);
                }
            });
        }

        /// <summary>
        /// Sends a message to a channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <param name="message">Message to be sent.</param>
        /// <example>
        ///   <code>
        /// ortcClient.Send("channelName", "messageToSend");
        ///   </code>
        ///   </example>
        public override async Task Send(string channel, string message)
        {
            #region Sanity Checks

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcNotConnectedException("Not connected"));
            }
            else if (String.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Channel is null or empty"));
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Channel has invalid characters"));
            }
            else if (String.IsNullOrEmpty(message))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Message is null or empty"));
            }
            else

            #endregion
            {
                byte[] channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    DelegateExceptionCallback(new OrtcMaxLengthException(String.Format("Channel size exceeds the limit of {0} characters", MAX_CHANNEL_SIZE)));
                }
                else
                {
                    var domainChannelCharacterIndex = channel.IndexOf(':');
                    var channelToValidate = channel;

                    if (domainChannelCharacterIndex > 0)
                    {
                        channelToValidate = channel.Substring(0, domainChannelCharacterIndex + 1) + "*";
                    }

                    string hash = _permissions.Where(c => c.Key == channel || c.Key == channelToValidate).FirstOrDefault().Value;

                    if (_permissions != null && _permissions.Count > 0 && String.IsNullOrEmpty(hash))
                    {
                        DelegateExceptionCallback(new OrtcNotConnectedException(String.Format("No permission found to send to the channel '{0}'", channel)));
                    }
                    else
                    {
                        message = message.Replace(Environment.NewLine, "\n");

                        if (channel != String.Empty && message != String.Empty)
                        {
                            try
                            {
                                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                                ArrayList messageParts = new ArrayList();
                                int pos = 0;
                                int remaining;
                                string messageId = Strings.GenerateId(8);

                                // Multi part
                                while ((remaining = messageBytes.Length - pos) > 0)
                                {
                                    byte[] messagePart;

                                    if (remaining >= MAX_MESSAGE_SIZE - channelBytes.Length)
                                    {
                                        messagePart = new byte[MAX_MESSAGE_SIZE - channelBytes.Length];
                                    }
                                    else
                                    {
                                        messagePart = new byte[remaining];
                                    }

                                    Array.Copy(messageBytes, pos, messagePart, 0, messagePart.Length);

                                    messageParts.Add(Encoding.UTF8.GetString((byte[])messagePart));

                                    pos += messagePart.Length;
                                }

                                for (int i = 0; i < messageParts.Count; i++)
                                {
                                    string s = String.Format("send;{0};{1};{2};{3};{4}", _applicationKey, _authenticationToken, channel, hash, String.Format("{0}_{1}-{2}_{3}", messageId, i + 1, messageParts.Count, messageParts[i]));

                                    await DoSendAsync(s);
                                }
                            }
                            catch (Exception ex)
                            {
                                string exName = null;

                                if (ex.InnerException != null)
                                {
                                    exName = ex.InnerException.GetType().Name;
                                }

                                switch (exName)
                                {
                                    case "OrtcNotConnectedException":
                                        // Server went down
                                        if (IsConnected)
                                        {
                                            await DoDisconnectAsync();
                                        }
                                        break;
                                    default:
                                        DelegateExceptionCallback(new OrtcGenericException(String.Format("Unable to send: {0}", ex)));
                                        break;
                                }
                            }
                        }
                    }
                }
            }
        }

        public override async Task SendProxyAsync(string applicationKey, string privateKey, string channel, string message)
        {
            #region Sanity Checks

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcNotConnectedException("Not connected"));
            }
            else if (String.IsNullOrEmpty(applicationKey))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Application Key is null or empty"));
            }
            else if (String.IsNullOrEmpty(privateKey))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Private Key is null or empty"));
            }
            else if (String.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Channel is null or empty"));
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Channel has invalid characters"));
            }
            else if (String.IsNullOrEmpty(message))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Message is null or empty"));
            }
            else

            #endregion
            {
                byte[] channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    DelegateExceptionCallback(new OrtcMaxLengthException(String.Format("Channel size exceeds the limit of {0} characters", MAX_CHANNEL_SIZE)));
                }
                else
                {

                    message = message.Replace(Environment.NewLine, "\n");

                    if (channel != String.Empty && message != String.Empty)
                    {
                        try
                        {
                            byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                            ArrayList messageParts = new ArrayList();
                            int pos = 0;
                            int remaining;
                            string messageId = Strings.GenerateId(8);

                            // Multi part
                            while ((remaining = messageBytes.Length - pos) > 0)
                            {
                                byte[] messagePart;

                                if (remaining >= MAX_MESSAGE_SIZE - channelBytes.Length)
                                {
                                    messagePart = new byte[MAX_MESSAGE_SIZE - channelBytes.Length];
                                }
                                else
                                {
                                    messagePart = new byte[remaining];
                                }

                                Array.Copy(messageBytes, pos, messagePart, 0, messagePart.Length);

                                messageParts.Add(Encoding.UTF8.GetString((byte[])messagePart));

                                pos += messagePart.Length;
                            }

                            for (int i = 0; i < messageParts.Count; i++)
                            {
                                string s = String.Format("sendproxy;{0};{1};{2};{3}", applicationKey, privateKey, channel, String.Format("{0}_{1}-{2}_{3}", messageId, i + 1, messageParts.Count, messageParts[i]));

                                await DoSendAsync(s);
                            }
                        }
                        catch (Exception ex)
                        {
                            string exName = null;

                            if (ex.InnerException != null)
                            {
                                exName = ex.InnerException.GetType().Name;
                            }

                            switch (exName)
                            {
                                case "OrtcNotConnectedException":
                                    // Server went down
                                    if (IsConnected)
                                    {
                                        await DoDisconnectAsync();
                                    }
                                    break;
                                default:
                                    DelegateExceptionCallback(new OrtcGenericException(String.Format("Unable to send: {0}", ex)));
                                    break;
                            }
                        }
                    }
                }

            }
        }

        private async Task _SubscribeWithOptionsAsync(Dictionary<string, object> options, OnMessageWithOptionsDelegate onMessage)
        {
            #region Sanity Checks
            string channel = null;
            if (options.ContainsKey("channel"))
                channel = (string)options["channel"];
            
            bool subscribeOnReconnected = true;
            if (options.ContainsKey("subscribeOnReconnected"))
                subscribeOnReconnected = (Boolean)options["subscribeOnReconnected"];
            
            bool withFilter = false;
            if (options.ContainsKey("withFilter"))
                withFilter = (Boolean)options["withFilter"];
            
            string filter = null;
            if (options.ContainsKey("filter"))
                filter = (string)options["filter"];
            
            string subscriberId = null;
            if (options.ContainsKey("subscriberId"))
                subscriberId = (string)options["subscriberId"];

            bool sanityChecked = true;

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcNotConnectedException("Not connected"));
                sanityChecked = false;
            }
            else if (String.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Channel is null or empty"));
                sanityChecked = false;
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Channel has invalid characters"));
                sanityChecked = false;
            }
            else if (_subscribedChannels.ContainsKey(channel))
            {
                ChannelSubscription channelSubscription = null;
                _subscribedChannels.TryGetValue(channel, out channelSubscription);

                if (channelSubscription != null)
                {
                    if (channelSubscription.IsSubscribing)
                    {
                        DelegateExceptionCallback(new OrtcSubscribedException(String.Format("Already subscribing to the channel {0}", channel)));
                        sanityChecked = false;
                    }
                    else if (channelSubscription.IsSubscribed)
                    {
                        DelegateExceptionCallback(new OrtcSubscribedException(String.Format("Already subscribed to the channel {0}", channel)));
                        sanityChecked = false;
                    }
                }
            }
            else
            {
                byte[] channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    if (_subscribedChannels.ContainsKey(channel))
                    {
                        ChannelSubscription channelSubscription = null;
                        _subscribedChannels.TryGetValue(channel, out channelSubscription);

                        if (channelSubscription != null)
                        {
                            channelSubscription.IsSubscribing = false;
                        }
                    }

                    DelegateExceptionCallback(new OrtcMaxLengthException(String.Format("Channel size exceeds the limit of {0} characters", MAX_CHANNEL_SIZE)));
                    sanityChecked = false;
                }
            }

            #endregion

            if (sanityChecked)
            {
                var domainChannelCharacterIndex = channel.IndexOf(':');
                var channelToValidate = channel;

                if (domainChannelCharacterIndex > 0)
                {
                    channelToValidate = channel.Substring(0, domainChannelCharacterIndex + 1) + "*";
                }

                string hash = _permissions.Where(c => c.Key == channel || c.Key == channelToValidate).FirstOrDefault().Value;

                if (_permissions != null && _permissions.Count > 0 && String.IsNullOrEmpty(hash))
                {
                    DelegateExceptionCallback(new OrtcNotConnectedException(String.Format("No permission found to subscribe to the channel '{0}'", channel)));
                }
                else
                {
                    if (!_subscribedChannels.ContainsKey(channel))
                    {
                        _subscribedChannels.TryAdd(channel,
                            new ChannelSubscription
                            {
                                IsSubscribing = true,
                                IsSubscribed = false,
                                SubscribeOnReconnected = subscribeOnReconnected,
                                OnMessage = onMessage
                            });
                    }

                    try
                    {
                        if (_subscribedChannels.ContainsKey(channel))
                        {
                            ChannelSubscription channelSubscription = null;
                            _subscribedChannels.TryGetValue(channel, out channelSubscription);

                            channelSubscription.IsSubscribing = true;
                            channelSubscription.IsSubscribed = false;
                            channelSubscription.SubscribeOnReconnected = subscribeOnReconnected;
                            channelSubscription.OnMessage = onMessage;
                            channelSubscription.withFilter = withFilter;
                            channelSubscription.filter = filter;
                            channelSubscription.subscriberId = subscriberId;
                        }
                        string s = string.Format("subscribeoptions;{0};{1};{2};{3};;{4};{5};{6}",
                                                 this._applicationKey, 
                                                 this._authenticationToken, 
                                                 channel, 
                                                 subscriberId,
                                                 "",
                                                 hash,
                                                 String.Format("{0}", (filter == null ? "" : filter)));

                        await DoSendAsync(s);
                    }
                    catch (Exception ex)
                    {
                        string exName = null;

                        if (ex.InnerException != null)
                        {
                            exName = ex.InnerException.GetType().Name;
                        }

                        switch (exName)
                        {
                            case "OrtcNotConnectedException":
                                // Server went down
                                if (IsConnected)
                                {
                                    await DoDisconnectAsync();
                                }
                                break;
                            default:
                                DelegateExceptionCallback(new OrtcGenericException(String.Format("Unable to subscribe: {0}", ex)));
                                break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Subscribes to a channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <param name="subscribeOnReconnected">Subscribe to the specified channel on reconnect.</param>
        /// <param name="onMessage"><see cref="OnMessageDelegate"/> callback.</param>
        /// <example>
        ///   <code>
        /// ortcClient.Subscribe("channelName", true, OnMessageCallback);
        /// private void OnMessageCallback(object sender, string channel, string message)
        /// {
        /// // Do something
        /// }
        ///   </code>
        ///   </example>

        public override async Task Subscribe(string channel, bool subscribeOnReconnected, OnMessageDelegate onMessage)
        {
            Dictionary<string, object> options = new Dictionary<string, object>();
            options.Add("channel", channel);
            options.Add("subscribeOnReconnected", subscribeOnReconnected);
            await this._SubscribeWithOptionsAsync(options, new OnMessageWithOptionsDelegate((object sender, Dictionary<string, object> msgOptions) =>
            {
                onMessage(sender, (string)msgOptions["channel"], (string)msgOptions["message"]);
            }));
        }

		/// <summary>
		/// Subscribe the specified channel in order to receive messages in that channel
		/// </summary>
		/// <param name="channel">Channel name.</param>
		/// <param name="subscribeOnReconnected">Subscribe to the specified channel on reconnect.</param>
		/// <param name="filter">Indicates the filter for this channel</param>
		/// <param name="onMessage"><see cref="OnMessageWithFilterDelegate"/> callback.</param>
		/// <example>
		///   <code>
		/// ortcClient.SubscribeWithFilter("channelName", true, "message.a = 10", OnMessageCallback);
		/// private void OnMessageCallback(object sender, string channel, bool filtered, string message)
		/// {
		/// // Do something
		/// }
		///   </code>
		///   </example>
		public override async Task SubscribeWithFilter(String channel, Boolean subscribeOnReconnect, String filter, OnMessageWithFilterDelegate onMessage)
        {
            Dictionary<string, object> options = new Dictionary<string, object>();
            options.Add("channel", channel);
            options.Add("subscribeOnReconnected", subscribeOnReconnect);
            options.Add("withFilter", true);
            options.Add("filter", filter);
            await this._SubscribeWithOptionsAsync(options, new OnMessageWithOptionsDelegate((object sender, Dictionary<string, object> msgOptions) =>
            {
                onMessage(sender, (string)msgOptions["channel"], (bool)msgOptions["filtered"], (string)msgOptions["message"]);
            }));
        }

		/// <summary>
		/// Subscribes to a channel with given options.
		/// </summary>
		/// <param name="options">Channel subscription options</param>
		/// <param name="subscribeOnReconnected">Subscribe to the specified channel on reconnect.</param>
		/// <param name="onMessage"><see cref="OnMessageWithOptionsDelegate"/> callback.</param>
		/// <example>
		///   <code>
		/// 
		/// "options = {
		///          channel,
		///          subscribeOnReconnected, // optional, default = true,
		///          withNotifications(Bool), // optional, default = false, use push notifications as in subscribeWithNotifications
		///          filter, // optional, default = "", the subscription filter as in subscribeWithFilter
		///          subscriberId // optional, default = "", the subscriberId as in subscribeWithBuffer
		///         }"
		/// 
		/// ortcClient.SubscribeWithOptions(options, OnMessageCallback);
		/// private void OnMessageCallback(object sender, Dictionary<string, object> msgOptions)
		/// {
		/// // Do something
		/// }
		///   </code>
		///   </example>
		public override async Task SubscribeWithOptions(Dictionary<string, object> options, OnMessageWithOptionsDelegate onMessage){
            
            await this._SubscribeWithOptionsAsync(options, onMessage);
        }


		/// <summary>
		/// Subscribes to a channel with buffer.
		/// </summary>
		/// <param name="channel">Channel name.</param>
		/// <param name="subscriberId">The subscriber client identifier</param>
		/// <param name="onMessage"><see cref="OnMessageWithBufferDelegate"/> callback.</param>
		/// <example>
		///   <code>
		/// ortcClient.subscribeWithBuffer("channelName", "SOME_ID", OnMessageCallback);
		/// private void OnMessageCallback(object ortc, string channel, string seqId, string message)
		/// {
		/// // Do something
		/// }
		///   </code>
		///   </example>
		public override async Task subscribeWithBuffer(String channel, String subscriberId, OnMessageWithBufferDelegate onMessage){
            Dictionary<string, object> options = new Dictionary<string, object>();
            options.Add("channel", channel);
            options.Add("subscribeOnReconnected", true);
            options.Add("subscriberId", subscriberId);
            await this._SubscribeWithOptionsAsync(options, new OnMessageWithOptionsDelegate((object sender, Dictionary<string, object> msgOptions) => {
                string seqId = null;
                if (msgOptions.ContainsKey("seqId"))
                    seqId = (string)msgOptions["seqId"];
                onMessage(sender, (string)msgOptions["channel"], seqId, (string)msgOptions["message"]);
            }));
        }

        /// <summary>
        /// Unsubscribes from a channel.
        /// </summary>
        /// <param name="channel">Channel name.</param>
        /// <example>
        ///   <code>
        /// ortcClient.Unsubscribe("channelName");
        ///   </code>
        ///   </example>
        public override async Task Unsubscribe(string channel)
        {
            #region Sanity Checks

            bool sanityChecked = true;

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcNotConnectedException("Not connected"));
                sanityChecked = false;
            }
            else if (String.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Channel is null or empty"));
                sanityChecked = false;
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Channel has invalid characters"));
                sanityChecked = false;
            }
            else if (!_subscribedChannels.ContainsKey(channel))
            {
                DelegateExceptionCallback(new OrtcNotSubscribedException(String.Format("Not subscribed to the channel {0}", channel)));
                sanityChecked = false;
            }
            else if (_subscribedChannels.ContainsKey(channel))
            {
                ChannelSubscription channelSubscription = null;
                _subscribedChannels.TryGetValue(channel, out channelSubscription);

                if (channelSubscription != null && !channelSubscription.IsSubscribed)
                {
                    DelegateExceptionCallback(new OrtcNotSubscribedException(String.Format("Not subscribed to the channel {0}", channel)));
                    sanityChecked = false;
                }
            }
            else
            {
                byte[] channelBytes = Encoding.UTF8.GetBytes(channel);

                if (channelBytes.Length > MAX_CHANNEL_SIZE)
                {
                    DelegateExceptionCallback(new OrtcMaxLengthException(String.Format("Channel size exceeds the limit of {0} characters", MAX_CHANNEL_SIZE)));
                    sanityChecked = false;
                }
            }

            #endregion

            if (sanityChecked)
            {
                try
                {
                    string s = String.Format("unsubscribe;{0};{1}", _applicationKey, channel);

                    await DoSendAsync(s);
                }
                catch (Exception ex)
                {
                    string exName = null;

                    if (ex.InnerException != null)
                    {
                        exName = ex.InnerException.GetType().Name;
                    }

                    switch (exName)
                    {
                        case "OrtcNotConnectedException":
                            // Server went down
                            if (IsConnected)
                            {
                                await DoDisconnectAsync();
                            }
                            break;
                        default:
                            DelegateExceptionCallback(new OrtcGenericException(String.Format("Unable to unsubscribe: {0}", ex)));
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// Disconnects from the gateway.
        /// </summary>
        /// <example>
        ///   <code>
        /// ortcClient.Disconnect();
        ///   </code>
        ///   </example>
        public override async void Disconnect()
        {
            DoStopReconnecting();

            // Clear subscribed channels
            _subscribedChannels.Clear();

            #region Sanity Checks

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcNotConnectedException("Not connected"));
            }
            else

            #endregion
            {
                await DoDisconnectAsync();
            }
        }

        /// <summary>
        /// Indicates whether is subscribed to a channel.
        /// </summary>
        /// <param name="channel">The channel name.</param>
        /// <returns>
        ///   <c>true</c> if subscribed to the channel; otherwise, <c>false</c>.
        /// </returns>
        public override bool IsSubscribed(string channel)
        {
            bool result = false;

            #region Sanity Checks

            if (!IsConnected)
            {
                DelegateExceptionCallback(new OrtcNotConnectedException("Not connected"));
            }
            else if (String.IsNullOrEmpty(channel))
            {
                DelegateExceptionCallback(new OrtcEmptyFieldException("Channel is null or empty"));
            }
            else if (!channel.OrtcIsValidInput())
            {
                DelegateExceptionCallback(new OrtcInvalidCharactersException("Channel has invalid characters"));
            }
            else

            #endregion
            {
                result = false;

                if (_subscribedChannels.ContainsKey(channel))
                {
                    ChannelSubscription channelSubscription = null;
                    _subscribedChannels.TryGetValue(channel, out channelSubscription);

                    if (channelSubscription != null && channelSubscription.IsSubscribed)
                    {
                        result = true;
                    }
                }
            }

            return result;
        }

        public override void Presence(String channel, OnPresenceDelegate callback)
        {
            var isCluster = !String.IsNullOrEmpty(this.ClusterUrl);
            var url = String.IsNullOrEmpty(this.ClusterUrl) ? this.Url : this.ClusterUrl;

            Extensibility.Presence.GetPresence(url, isCluster, this._applicationKey, this._authenticationToken, channel, callback);
        }

        public override void EnablePresence(String privateKey, String channel, bool metadata, OnEnablePresenceDelegate callback)
        {
            var isCluster = !String.IsNullOrEmpty(this.ClusterUrl);
            var url = String.IsNullOrEmpty(this.ClusterUrl) ? this.Url : this.ClusterUrl;

            Extensibility.Presence.EnablePresence(url, isCluster, this._applicationKey, privateKey, channel, metadata, callback);
        }

        public override void DisablePresence(String privateKey, String channel, OnDisablePresenceDelegate callback)
        {
            var isCluster = !String.IsNullOrEmpty(this.ClusterUrl);
            var url = String.IsNullOrEmpty(this.ClusterUrl) ? this.Url : this.ClusterUrl;

            Extensibility.Presence.DisablePresence(url, isCluster, this._applicationKey, privateKey, channel, callback);
        }

        #endregion

        #region Private Methods (13)

        /// <summary>
        /// Processes the operation validated.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        private async Task ProcessOperationValidatedAsync(string arguments)
        {
            if (!String.IsNullOrEmpty(arguments))
            {
                _reconnectStartedAt = null;

                bool isValid = false;

                // Try to match with authentication
                Match validatedAuthMatch = Regex.Match(arguments, VALIDATED_PATTERN);

                if (validatedAuthMatch.Success)
                {
                    isValid = true;

                    string userPermissions = String.Empty;

                    if (validatedAuthMatch.Groups["up"].Length > 0)
                    {
                        userPermissions = validatedAuthMatch.Groups["up"].Value;
                    }

                    if (validatedAuthMatch.Groups["set"].Length > 0)
                    {
                        _sessionExpirationTime = int.Parse(validatedAuthMatch.Groups["set"].Value);
                    }

                    if (String.IsNullOrEmpty(ReadLocalStorage(_applicationKey, _sessionExpirationTime)))
                    {
                        CreateLocalStorage(_applicationKey);
                    }

                    if (!String.IsNullOrEmpty(userPermissions) && userPermissions != "null")
                    {
                        MatchCollection matchCollection = Regex.Matches(userPermissions, PERMISSIONS_PATTERN);

                        var permissions = new List<KeyValuePair<string, string>>();

                        foreach (Match match in matchCollection)
                        {
                            string channel = match.Groups["key"].Value;
                            string hash = match.Groups["value"].Value;

                            permissions.Add(new KeyValuePair<string, string>(channel, hash));
                        }

                        _permissions = new List<KeyValuePair<string, string>>(permissions);
                    }
                }

                if (isValid)
                {
                    _isConnecting = false;
                    IsConnected = true;
                    if (HeartbeatActive)
                    {
                        _heartbeatTimer = new Timer(_heartbeatTimer_ElapsedAsync, _autoEvent, HeartbeatTime * 1000, HeartbeatTime * 1000);
                    }
                    if (_alreadyConnectedFirstTime)
                    {
                        ArrayList channelsToRemove = new ArrayList();

                        // Subscribe to the previously subscribed channels
                        foreach (KeyValuePair<string, ChannelSubscription> item in _subscribedChannels)
                        {
                            string channel = item.Key;
                            ChannelSubscription channelSubscription = item.Value;

                            // Subscribe again
                            if (channelSubscription.SubscribeOnReconnected && (channelSubscription.IsSubscribing || channelSubscription.IsSubscribed))
                            {
                                channelSubscription.IsSubscribing = true;
                                channelSubscription.IsSubscribed = false;

                                var domainChannelCharacterIndex = channel.IndexOf(':');
                                var channelToValidate = channel;

                                if (domainChannelCharacterIndex > 0)
                                {
                                    channelToValidate = channel.Substring(0, domainChannelCharacterIndex + 1) + "*";
                                }

                                string hash = _permissions.Where(c => c.Key == channel || c.Key == channelToValidate).FirstOrDefault().Value;

                                string s = String.Format("subscribe;{0};{1};{2};{3}", _applicationKey, _authenticationToken, channel, hash);

                                await DoSendAsync(s);
                            }
                            else
                            {
                                channelsToRemove.Add(channel);
                            }
                        }

                        for (int i = 0; i < channelsToRemove.Count; i++)
                        {
                            ChannelSubscription removeResult = null;
                            _subscribedChannels.TryRemove(channelsToRemove[i].ToString(), out removeResult);
                        }

                        // Clean messages buffer (can have lost message parts in memory)
                        _multiPartMessagesBuffer.Clear();

                        DelegateReconnectedCallback();
                    }
                    else
                    {
                        _alreadyConnectedFirstTime = true;

                        // Clear subscribed channels
                        _subscribedChannels.Clear();

                        DelegateConnectedCallback();
                    }

                    //if (arguments.IndexOf("busy", StringComparison.CurrentCulture) < 0)
                    //{
                    //    if (_reconnectTimer != null)
                    //    {
                    //        _reconnectTimer.Dispose();
                    //    }
                    //}

                    _callDisconnectedCallback = true;
                }
            }
        }

        /// <summary>
        /// Processes the operation subscribed.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        private void ProcessOperationSubscribed(string arguments)
        {
            if (!String.IsNullOrEmpty(arguments))
            {
                Match subscribedMatch = Regex.Match(arguments, CHANNEL_PATTERN);

                if (subscribedMatch.Success)
                {
                    string channelSubscribed = String.Empty;

                    if (subscribedMatch.Groups["channel"].Length > 0)
                    {
                        channelSubscribed = subscribedMatch.Groups["channel"].Value;
                    }

                    if (!String.IsNullOrEmpty(channelSubscribed))
                    {
                        ChannelSubscription channelSubscription = null;
                        _subscribedChannels.TryGetValue(channelSubscribed, out channelSubscription);

                        if (channelSubscription != null)
                        {
                            channelSubscription.IsSubscribing = false;
                            channelSubscription.IsSubscribed = true;
                        }

                        DelegateSubscribedCallback(channelSubscribed);
                    }
                }
            }
        }

        /// <summary>
        /// Processes the operation unsubscribed.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        private void ProcessOperationUnsubscribed(string arguments)
        {
            if (!String.IsNullOrEmpty(arguments))
            {
                Match unsubscribedMatch = Regex.Match(arguments, CHANNEL_PATTERN);

                if (unsubscribedMatch.Success)
                {
                    string channelUnsubscribed = String.Empty;

                    if (unsubscribedMatch.Groups["channel"].Length > 0)
                    {
                        channelUnsubscribed = unsubscribedMatch.Groups["channel"].Value;
                    }

                    if (!String.IsNullOrEmpty(channelUnsubscribed))
                    {
                        ChannelSubscription channelSubscription = null;
                        _subscribedChannels.TryGetValue(channelUnsubscribed, out channelSubscription);

                        if (channelSubscription != null)
                        {
                            channelSubscription.IsSubscribed = false;
                        }

                        DelegateUnsubscribedCallback(channelUnsubscribed);
                    }
                }
            }
        }

        /// <summary>
        /// Processes the operation error.
        /// </summary>
        /// <param name="arguments">The arguments.</param>
        private async Task ProcessOperationErrorAsync(string arguments)
        {
            if (!String.IsNullOrEmpty(arguments))
            {
                Match errorMatch = Regex.Match(arguments, EXCEPTION_PATTERN);

                if (errorMatch.Success)
                {
                    string op = String.Empty;
                    string error = String.Empty;
                    string channel = String.Empty;

                    if (errorMatch.Groups["op"].Length > 0)
                    {
                        op = errorMatch.Groups["op"].Value;
                    }

                    if (errorMatch.Groups["error"].Length > 0)
                    {
                        error = errorMatch.Groups["error"].Value;
                    }

                    if (errorMatch.Groups["channel"].Length > 0)
                    {
                        channel = errorMatch.Groups["channel"].Value;
                    }

                    if (!String.IsNullOrEmpty(error))
                    {
                        DelegateExceptionCallback(new OrtcGenericException(error));
                    }

                    if (!String.IsNullOrEmpty(op))
                    {
                        switch (op)
                        {
                            case "validate":
                                if (!String.IsNullOrEmpty(error) && (error.Contains("Unable to connect") || error.Contains("Server is too busy")))
                                {
                                    IsConnected = false;
                                    DoReconnectAsync();
                                }
                                else
                                {
                                    DoStopReconnecting();
                                }
                                break;
                            case "subscribe":
                                if (!String.IsNullOrEmpty(channel))
                                {
                                    ChannelSubscription channelSubscription = null;
                                    _subscribedChannels.TryGetValue(channel, out channelSubscription);

                                    if (channelSubscription != null)
                                    {
                                        channelSubscription.IsSubscribing = false;
                                    }
                                }
                                break;
                            case "subscribe_maxsize":
                            case "unsubscribe_maxsize":
                            case "send_maxsize":
                                if (!String.IsNullOrEmpty(channel))
                                {
                                    ChannelSubscription channelSubscription = null;
                                    _subscribedChannels.TryGetValue(channel, out channelSubscription);

                                    if (channelSubscription != null)
                                    {
                                        channelSubscription.IsSubscribing = false;
                                    }
                                }

                                DoStopReconnecting();
                                await DoDisconnectAsync();
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }

        private void ProcessOperationReceived(string message)
        {
            string[] parts2;
            Dictionary<string, object> json = new Dictionary<string, object>();
            try
            {
                string[] parts = message.Split(new string[] { "a[\"" }, StringSplitOptions.None);
                parts2 = parts[1].Split(new string[] { "\"]" }, StringSplitOptions.None);
                string transformMessage = parts2[0].Replace("\\\"","\"");

                json = JsonConvert.DeserializeObject<Dictionary<string, object>>(transformMessage);
            }catch(Exception){
                return;
            }
            // Received
            if (json != null)
            {
                string channelReceived = String.Empty;
                string messageReceived = String.Empty;
                bool filtered = false;
                string seqId = String.Empty;

                if (json.ContainsKey("ch"))
                {
                    channelReceived = (string)json["ch"];
                }
                if (json.ContainsKey("m"))
                {
                    messageReceived = (string)json["m"];
                }
                if (json.ContainsKey("f"))
                {
                    filtered = (bool)json["f"];
                }
                if (json.ContainsKey("s"))
                {
                    seqId = (string)json["s"];
                }


                if (!String.IsNullOrEmpty(channelReceived) && !String.IsNullOrEmpty(messageReceived) && _subscribedChannels.ContainsKey(channelReceived))
                {
                    messageReceived = messageReceived.Replace(@"\\n", Environment.NewLine).Replace("\\\\\"", @"""").Replace("\\\\\\\\", @"\");

                    // Multi part
                    Match multiPartMatch = Regex.Match(messageReceived, MULTI_PART_MESSAGE_PATTERN);

                    string messageId = String.Empty;
                    int messageCurrentPart = 1;
                    int messageTotalPart = 1;
                    bool lastPart = false;
                    ConcurrentDictionary<int, BufferedMessage> messageParts = null;

                    if (multiPartMatch.Success)
                    {
                        if (multiPartMatch.Groups["messageId"].Length > 0)
                        {
                            messageId = multiPartMatch.Groups["messageId"].Value;
                        }

                        if (multiPartMatch.Groups["messageCurrentPart"].Length > 0)
                        {
                            messageCurrentPart = Int32.Parse(multiPartMatch.Groups["messageCurrentPart"].Value);
                        }

                        if (multiPartMatch.Groups["messageTotalPart"].Length > 0)
                        {
                            messageTotalPart = Int32.Parse(multiPartMatch.Groups["messageTotalPart"].Value);
                        }

                        if (multiPartMatch.Groups["message"].Length > 0)
                        {
                            messageReceived = multiPartMatch.Groups["message"].Value;
                        }
                    }

                    lock (_multiPartMessagesBuffer)
                    {
                        // Is a message part
                        if (!String.IsNullOrEmpty(messageId))
                        {
                            if (!_multiPartMessagesBuffer.ContainsKey(messageId))
                            {
                                _multiPartMessagesBuffer.TryAdd(messageId, new ConcurrentDictionary<int, BufferedMessage>());
                            }


                            _multiPartMessagesBuffer.TryGetValue(messageId, out messageParts);

                            if (messageParts != null)
                            {
                                lock (messageParts)
                                {
                                    messageParts.TryAdd(messageCurrentPart, new BufferedMessage(messageCurrentPart, messageReceived));
                                    // Last message part
                                    if (messageParts.Count == messageTotalPart)
                                    {
                                        //messageParts.Sort();

                                        lastPart = true;
                                    }
                                    if(!seqId.Equals(""))
                                        sendAckAsync(channelReceived, messageId, seqId, (lastPart?"1":"0"));
                                }
                            }
                        }
                        // Message does not have multipart, like the messages received at announcement channels
                        else
                        {
                            lastPart = true;
                        }

                        if (lastPart)
                        {
                            if (_subscribedChannels.ContainsKey(channelReceived))
                            {
                                ChannelSubscription channelSubscription = null;
                                _subscribedChannels.TryGetValue(channelReceived, out channelSubscription);

                                if (channelSubscription != null)
                                {
                                    var ev = channelSubscription.OnMessage;

                                    if (ev != null)
                                    {
                                        if (!String.IsNullOrEmpty(messageId) && _multiPartMessagesBuffer.ContainsKey(messageId))
                                        {
                                            messageReceived = String.Empty;
                                            //lock (messageParts)
                                            //{
                                            var bufferedMultiPartMessages = new List<BufferedMessage>();

                                            foreach (var part in messageParts.Keys)
                                            {
                                                bufferedMultiPartMessages.Add(messageParts[part]);
                                            }

                                            bufferedMultiPartMessages.Sort();

                                            foreach (var part in bufferedMultiPartMessages)
                                            {
                                                if (part != null)
                                                {
                                                    messageReceived = String.Format("{0}{1}", messageReceived, part.Message);
                                                }
                                            }
                                            //}

                                            // Remove from messages buffer
                                            ConcurrentDictionary<int, BufferedMessage> removeResult = null;
                                            _multiPartMessagesBuffer.TryRemove(messageId, out removeResult);
                                        }

                                        if (!String.IsNullOrEmpty(messageReceived))
                                        {
                                            Dictionary<string, object> msgOptions = new Dictionary<string, object>();
                                            msgOptions.Add("channel", channelReceived);
                                            msgOptions.Add("message", messageReceived);

                                            if(!seqId.Equals(""))
                                                msgOptions.Add("seqId", seqId);
                                            if (!filtered.Equals(""))
                                                msgOptions.Add("filtered", filtered);

                                            if (_synchContext != null)
                                            {
                                                _synchContext.Post(obj => ev(obj, msgOptions), this);
                                            }
                                            else
                                            {
                                                ev(this, msgOptions);
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                // Unknown
                DelegateExceptionCallback(new OrtcGenericException(String.Format("Unknown message received: {0}", message)));

                //DoDisconnect();
            }
        }

        protected async void sendAckAsync(String channel, String messageId, String seqId, String asAllParts)
        {
            String subscribeMessage = String.Format("ack;{0};{1};{2};{3};{4}",
                    this._applicationKey, channel, messageId, seqId, asAllParts);
            await DoSendAsync(subscribeMessage);
        }


        /// <summary>
        /// Do the Connect Task
        /// </summary>
        private async Task DoConnectAsync()
        {
            _isConnecting = true;
            _callDisconnectedCallback = false;

            if (IsCluster)
            {
                try
                {
                    Url = GetUrlFromCluster();

                    IsCluster = true;

                    if (String.IsNullOrEmpty(Url))
                    {
                        DelegateExceptionCallback(new OrtcEmptyFieldException("Unable to get URL from cluster"));
                        DoReconnectAsync();
                    }
                }
                catch (Exception ex)
                {
                    if (!_stopReconnecting)
                    {
                        DelegateExceptionCallback(new OrtcNotConnectedException(ex.Message));
                        DoReconnectAsync();
                    }
                }
            }

            if (!String.IsNullOrEmpty(Url))
            {
                try
                {
                    await _webSocketConnection.ConnectAsync(Url);

                    // Just in case the server does not respond
                    //
                    _waitingServerResponse = true;

                    StartReconnectTimer();
                }
                catch (OrtcEmptyFieldException ex)
                {
                    DelegateExceptionCallback(new OrtcNotConnectedException(ex.Message));
                    DoStopReconnecting();
                }
                catch (Exception ex)
                {
                    DelegateExceptionCallback(new OrtcNotConnectedException(ex.Message));
                    _isConnecting = false;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>

        private async void DoReconnectAsync()
        {
            if (_reconnectStartedAt != null)
            {
                StartReconnectTimer();
            }
            else
            {
                _reconnectStartedAt = DateTime.Now;

                DelegateReconnectingCallback();
                await DoConnectAsync();
            }
        }

        private static Semaphore ReconnectTimerPool = new Semaphore(0, 1);
        private void StartReconnectTimer()
        {
            if (this._reconnectTimer != null)
                return;

            this._reconnectTimer = Task.Run(async () =>
            {
                while (!this._stopReconnecting)
                {
                    await Task.Delay(this.ConnectionTimeout);
                    await reconnectTimerCodeAsync();
                }
            });
        }

        private async Task reconnectTimerCodeAsync()
        {

            if (!_stopReconnecting && !IsConnected)
            {
                if (_waitingServerResponse)
                {
                    _waitingServerResponse = false;
                    DelegateExceptionCallback(new OrtcNotConnectedException("Unable to connect"));
                }

                _reconnectStartedAt = DateTime.Now;

                DelegateReconnectingCallback();

                await DoConnectAsync();

            }
        }


        /// <summary>
        /// 
        /// </summary>
        private void DoStopReconnecting()
        {
            _isConnecting = false;
            if (_alreadyConnectedFirstTime == true)
                DelegateReconnectedCallback();

            _alreadyConnectedFirstTime = false;

            // Stop the connecting/reconnecting process


            _stopReconnecting = true;

            _reconnectStartedAt = null;


            if (_reconnectTimer != null)
            {
                _reconnectTimer = null;
            }
        }

        /// <summary>
        /// Disconnect the TCP client.
        /// </summary>
        private async Task DoDisconnectAsync()
        {
            _reconnectStartedAt = null;
            if (_heartbeatTimer != null)
                _heartbeatTimer.Dispose();
            try
            {
                await _webSocketConnection.CloseAsync();
            }
            catch (Exception ex)
            {
                DelegateExceptionCallback(new OrtcGenericException(String.Format("Error disconnecting: {0}", ex)));
            }
        }

        /// <summary>
        /// Sends a message through the TCP client.
        /// </summary>
        /// <param name="message">The message to be sent.</param>
        private async Task DoSendAsync(string message)
        {
            try
            {
                await _webSocketConnection.SendAsync(message);
            }
            catch (Exception ex)
            {
                DelegateExceptionCallback(new OrtcGenericException(String.Format("Unable to send: {0}", ex)));
            }
        }


        private static Semaphore _pool = new Semaphore(0, 1);
        private static int flag = 0;
        private int publishTimeout { get; set; } 
        private Dictionary<string, object> pendingPublishMessages = new Dictionary<string, object>();
        private Task partSendInterval;

        /// <summary>
        /// Gets the URL from cluster.
        /// </summary>
        /// <returns></returns>
        private string GetUrlFromCluster()
        {
            if (flag == 1)
                return "";
            flag = 1;
            String newUrl = "";
            Balancer.GetServerFromBalancerAsync(ClusterUrl, _applicationKey, (string server, Exception ex) =>
            {
                if (server != null)
                    newUrl = server;
                _pool.Release(1);
                flag = 0;
            });
            _pool.WaitOne();
            return newUrl;
        }

        #endregion

        #region Events (7)

        /// <summary>
        /// Occurs when a connection attempt was successful.
        /// </summary>
        public override event OnConnectedDelegate OnConnected;

        /// <summary>
        /// Occurs when the client connection terminated. 
        /// </summary>
        public override event OnDisconnectedDelegate OnDisconnected;

        /// <summary>
        /// Occurs when the client subscribed to a channel.
        /// </summary>
        public override event OnSubscribedDelegate OnSubscribed;

        /// <summary>
        /// Occurs when the client unsubscribed from a channel.
        /// </summary>
        public override event OnUnsubscribedDelegate OnUnsubscribed;

        /// <summary>
        /// Occurs when there is an error.
        /// </summary>
        public override event OnExceptionDelegate OnException;

        /// <summary>
        /// Occurs when a client attempts to reconnect.
        /// </summary>
        public override event OnReconnectingDelegate OnReconnecting;

        /// <summary>
        /// Occurs when a client reconnected.
        /// </summary>
        public override event OnReconnectedDelegate OnReconnected;


        #endregion

        #region Events handlers (6)

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            e.SetObserved();
        }



        private async void _heartbeatTimer_ElapsedAsync(Object stateInfo)
        {
            if (IsConnected)
            {
                await DoSendAsync("b");
            }
        }

        /// <summary>
        /// 
        /// </summary>
        private void _webSocketConnection_OnOpened()
        {
            DoStopReconnecting();
        }

        /// <summary>
        /// 
        /// </summary>
        private void _webSocketConnection_OnClosedAsync()
        {
            // Clear user permissions
            _permissions.Clear();

            _isConnecting = false;
            IsConnected = false;
            _stopReconnecting = false;
            if (_callDisconnectedCallback)
            {
                DelegateDisconnectedCallback();
                DoReconnectAsync();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="error"></param>
        private void _webSocketConnection_OnErrorAsync(Exception error)
        {
            if (!_stopReconnecting)
            {
                if (_isConnecting)
                {
                    DelegateExceptionCallback(new OrtcGenericException(error.Message));

                    DoReconnectAsync();
                }
                else
                {
                    DelegateExceptionCallback(new OrtcGenericException(String.Format("WebSocketConnection exception: {0}", error)));
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        private async void _webSocketConnection_OnMessageReceivedAsync(string message)
        {
            if (!String.IsNullOrEmpty(message))
            {
                // Open
                if (message == "o")
                {
                    try
                    {
                        if (String.IsNullOrEmpty(ReadLocalStorage(_applicationKey, _sessionExpirationTime)))
                        {
                            SessionId = Strings.GenerateId(16);
                        }

                        string s;
                        if (HeartbeatActive)
                        {
                            s = String.Format("validate;{0};{1};{2};{3};{4};{5};{6}", _applicationKey, _authenticationToken, AnnouncementSubChannel, SessionId,
                                ConnectionMetadata, HeartbeatTime, HeartbeatFails);
                        }
                        else
                        {
                            s = String.Format("validate;{0};{1};{2};{3};{4}", _applicationKey, _authenticationToken, AnnouncementSubChannel, SessionId, ConnectionMetadata);
                        }
                        await DoSendAsync(s);
                    }
                    catch (Exception ex)
                    {
                        DelegateExceptionCallback(new OrtcGenericException(String.Format("Exception sending validate: {0}", ex)));
                    }
                }
                // Heartbeat
                else if (message == "h")
                {
                    // Do nothing
                }
                else
                {
                    message = message.Replace("\\\"", @"""");

                    // Update last keep alive time
                    _lastKeepAlive = DateTime.Now;

                    // Operation
                    Match operationMatch = Regex.Match(message, OPERATION_PATTERN);

                    if (operationMatch.Success)
                    {
                        string operation = operationMatch.Groups["op"].Value;
                        string arguments = operationMatch.Groups["args"].Value;

                        switch (operation)
                        {
                            case "ortc-validated":
                                await ProcessOperationValidatedAsync(arguments);
                                break;
                            case "ortc-subscribed":
                                ProcessOperationSubscribed(arguments);
                                break;
                            case "ortc-unsubscribed":
                                ProcessOperationUnsubscribed(arguments);
                                break;
                            case "ortc-error":
                                await ProcessOperationErrorAsync(arguments);
                                break;
                            default:
                                // Unknown operation
                                DelegateExceptionCallback(new OrtcGenericException(String.Format("Unknown operation \"{0}\" for the message \"{1}\"", operation, message)));

                                await DoDisconnectAsync();
                                break;
                        }
                    }
                    else
                    {
                        // Close
                        Match closeOperationMatch = Regex.Match(message, CLOSE_PATTERN);

                        if (!closeOperationMatch.Success)
                        {
                            ProcessOperationReceived(message);
                        }
                    }
                }
            }
        }

        #endregion

        #region Events calls (7)

        private void DelegateConnectedCallback()
        {
            var ev = OnConnected;

            if (ev != null)
            {
                if (_synchContext != null)
                {
                    _synchContext.Post(obj => ev(obj), this);
                }
                else
                {
                    Task.Factory.StartNew(() => ev(this));
                }
            }
        }

        private void DelegateDisconnectedCallback()
        {
            var ev = OnDisconnected;

            if (ev != null)
            {
                if (_synchContext != null)
                {
                    _synchContext.Post(obj => ev(obj), this);
                }
                else
                {
                    Task.Factory.StartNew(() => ev(this));
                }
            }
        }

        private void DelegateSubscribedCallback(string channel)
        {
            var ev = OnSubscribed;

            if (ev != null)
            {
                if (_synchContext != null)
                {
                    _synchContext.Post(obj => ev(obj, channel), this);
                }
                else
                {
                    Task.Factory.StartNew(() => ev(this, channel));
                }
            }
        }

        private void DelegateUnsubscribedCallback(string channel)
        {
            var ev = OnUnsubscribed;

            if (ev != null)
            {
                if (_synchContext != null)
                {
                    _synchContext.Post(obj => ev(obj, channel), this);
                }
                else
                {
                    Task.Factory.StartNew(() => ev(this, channel));
                }
            }
        }

        private void DelegateExceptionCallback(Exception ex)
        {
            var ev = OnException;

            if (ev != null)
            {
                if (_synchContext != null)
                {
                    _synchContext.Post(obj => ev(obj, ex), this);
                }
                else
                {
                    Task.Factory.StartNew(() => ev(this, ex));
                }
            }
        }

        private void DelegateReconnectingCallback()
        {
            var ev = OnReconnecting;

            if (ev != null)
            {
                if (_synchContext != null)
                {
                    _synchContext.Post(obj => ev(obj), this);
                }
                else
                {
                    Task.Factory.StartNew(() => ev(this));
                }
            }
        }

        private void DelegateReconnectedCallback()
        {
            var ev = OnReconnected;

            if (ev != null)
            {
                if (_synchContext != null)
                {
                    _synchContext.Post(obj => ev(obj), this);
                }
                else
                {
                    Task.Factory.StartNew(() => ev(this));
                }
            }
        }

        #endregion
    }
}
