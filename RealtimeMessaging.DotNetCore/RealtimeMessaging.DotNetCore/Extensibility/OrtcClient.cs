using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RealtimeMessaging.DotNetCore.Extensibility
{
    #region Delegates

    /// <summary>
    /// Occurs when the client connects to the gateway.
    /// </summary>
    /// <exclude/>
    public delegate void OnConnectedDelegate(object sender);

    /// <summary>
    /// Occurs when the client disconnects from the gateway.
    /// </summary>
    /// <exclude/>
    public delegate void OnDisconnectedDelegate(object sender);

    /// <summary>
    /// Occurs when the client subscribed to a channel.
    /// </summary>
    /// <exclude/>
    public delegate void OnSubscribedDelegate(object sender, string channel);

    /// <summary>
    /// Occurs when the client unsubscribed from a channel.
    /// </summary>
    /// <exclude/>
    public delegate void OnUnsubscribedDelegate(object sender, string channel);

    /// <summary>
    /// Occurs when there is an exception.
    /// </summary>
    /// <exclude/>
    public delegate void OnExceptionDelegate(object sender, Exception ex);

    /// <summary>
    /// Occurs when the client attempts to reconnect to the gateway.
    /// </summary>
    /// <exclude/>
    public delegate void OnReconnectingDelegate(object sender);

    /// <summary>
    /// Occurs when the client reconnected to the gateway.
    /// </summary>
    /// <exclude/>
    public delegate void OnReconnectedDelegate(object sender);

    /// <summary>
    /// Occurs when the client receives a message in the specified channel.
    /// </summary>
    /// <exclude/>
    public delegate void OnMessageDelegate(object sender, string channel, string message);


	/// <summary>
	/// Occurs when the client receives a message in the specified channel that was subscribed with filter.
	/// </summary>
	/// <exclude/>
	public delegate void OnMessageWithFilterDelegate(object sender, string channel, Boolean filtered, string message);

	/// <summary>
	/// Occurs when the client receives a message in the specified channel that was subscribed with options.
	/// </summary>
	/// <exclude/>
    public delegate void OnMessageWithOptionsDelegate(object sender, Dictionary<string, object> msgOptions);


    /// <summary>
    /// Occurs when the client receives a message in the specified channel that was subscribed with buffer.
    /// </summary>
    /// <exclude/>
    public delegate void OnMessageWithBufferDelegate(object sender, string channel, string seqId, string message);

	/// <summary>
	/// Occurs when the client receives a message in the specified channel that was subscribed with buffer.
	/// </summary>
	/// <exclude/>
	public delegate void OnPublishResultDelegate(string error, string seqId);

    #endregion

    /// <summary>
    /// Represents a <see cref="OrtcClient"/> that connects to a specified gateway.
    /// </summary>
    /// <example>
    /// <code>
    /// public partial class OrtcUsageForm : Form
    /// {
    ///     public OrtcUsageForm()
    ///     {
    ///         string applicationKey = "myApplicationKey";
    ///         string authenticationToken = "myAuthenticationToken"; 
    ///         
    ///         // Permissions
    ///         Dictionary{string, ChannelPermissions} permissions = new Dictionary{string, ChannelPermissions}();
    /// 
    ///         permissions.Add("channel1", ChannelPermissions.Read);
    ///         permissions.Add("channel2", ChannelPermissions.Write);
    /// 
    ///         string url = "http://ortc_server";
    ///         bool isCluster = false;
    ///         string authenticationToken = "myAuthenticationToken";
    ///         bool authenticationTokenIsPrivate = true;
    ///         string applicationKey = "myApplicationKey";
    ///         int timeToLive = 1800; // 30 minutes
    ///         string privateKey = "myPrivateKey";
    /// 
    ///         // Save authentication
    ///         bool authSaved = RealtimeMessaging.DotNetCore.Ortc.SaveAuthentication(url, isCluster, authenticationToken, authenticationTokenIsPrivate, applicationKey, timeToLive, privateKey, permissions)) 
    ///         
    ///         // Load factory
    ///         var api = new Api.Ortc("Plugins");
    ///         
    ///         IOrtcFactory factory = api.LoadOrtcFactory("RealTimeSJ");
    ///         
    ///         if (factory != null)
    ///         {
    ///             // Create ORTC client
    ///             OrtcClient ortcClient = factory.CreateClient();
    /// 
    ///             if (ortcClient != null)
    ///             {
    ///                 // ORTC client parameters
    ///                 ortcClient.Id = "myId";
    ///                 // You can use a cluster server URL
    ///                 ortcClient.ClusterUrl = "http://ortc_cluster_server";
    ///                 // Or just a server URL
    ///                 // ortcClient.Url = "http://ortc_server";
    ///                 ortcClient.ConnectionMetadata = "myConnectionMetadata";
    ///                 
    ///                 // Ortc client handlers
    ///                 ortcClient.OnConnected += new OnConnectedDelegate(ortc_OnConnected);
    ///                 ortcClient.OnDisconnected += new OnDisconnectedDelegate(ortc_OnDisconnected);
    ///                 ortcClient.OnReconnecting += new OnReconnectingDelegate(ortc_OnReconnecting);
    ///                 ortcClient.OnReconnected += new OnReconnectedDelegate(ortc_OnReconnected);
    ///                 ortcClient.OnSubscribed += new OnSubscribedDelegate(ortc_OnSubscribed);
    ///                 ortcClient.OnUnsubscribed += new OnUnsubscribedDelegate(ortc_OnUnsubscribed);
    ///                 ortcClient.OnException += new OnExceptionDelegate(ortc_OnException);
    ///                 
    ///                 ortcClient.connect(applicationKey, authenticationToken);
    ///             }
    ///             else
    ///             {
    ///                 // Error creating client
    ///             }
    ///         }
    ///         else
    ///         {
    ///             // Error loading factory
    ///         }
    ///     }
    ///     
    ///     private void ortc_OnConnected(object sender)
    ///     {
    ///         // If the client reconnects it will automatically subscribe to the channel “channel1”
    ///         ortcClient.Subscribe("channel1", true, OnMessageCallback1);
    ///         
    ///         // If the client reconnects it will not automatically subscribe to the channel “channel2”
    ///         ortcClient.Subscribe("channel2", false, OnMessageCallback2);
    ///     }
    ///     
    ///     private void OnMessageCallback1(object sender, string channel, string message)
    ///     {
    ///         // Handle the message received event
    ///     }
    ///     
    ///     private void OnMessageCallback2(object sender, string channel, string message)
    ///     {
    ///         // Handle the message received event
    ///     }
    /// 
    ///     private void ortc_OnDisconnected(object sender)
    ///     {
    ///         // Handle the ortc client disconnected event
    ///     }
    ///     
    ///     private void ortc_OnReconnecting(object sender)
    ///     {
    ///         // Handle the ortc client reconnecting event
    ///     }
    ///     
    ///     private void ortc_OnReconnected(object sender)
    ///     {
    ///         // Handle the ortc client reconnected event
    ///     }
    ///     
    ///     private void ortc_OnSubscribed(object sender, string channel)
    ///     {
    ///         // Handle the ortc client subscribed event
    ///     }
    ///     
    ///     private void ortc_OnUnsubscribed(object sender, string channel)
    ///     {
    ///         // Handle the ortc client unsubscribed event
    ///     }
    ///     
    ///     private void ortc_OnException(object sender, Exception ex)
    ///     {
    ///         // Handle the ortc client exception event
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class OrtcClient
    {
        #region Constants (4)

        /// <summary>
        /// Message maximum size in bytes
        /// </summary>
        /// <exclude/>
        public const int MAX_MESSAGE_SIZE = 700;

        /// <summary>
        /// Channel maximum size in bytes
        /// </summary>
        /// <exclude/>
        public const int MAX_CHANNEL_SIZE = 100;

        /// <summary>
        /// Connection Metadata maximum size in bytes
        /// </summary>
        /// <exclude/>
        public const int MAX_CONNECTION_METADATA_SIZE = 256;

        /// <summary>
        /// Session storage name
        /// </summary>
        public const string SESSION_STORAGE_NAME = "ortcsession-";

        private const int HEARTBEAT_MAX_TIME = 60;
        private const int HEARTBEAT_MIN_TIME = 10;
        private const int HEARTBEAT_MAX_FAIL = 6;
        private const int HEARTBEAT_MIN_FAIL = 1;

        #endregion

        #region Attributes (3)

        private string _url;
        private string _clusterUrl;
        private bool _isCluster;
        private int _heartbeatTime;
        private int _heartbeatFail;

        #endregion

        #region Properties (9)

        /// <summary>
        /// Gets or sets the client object identifier.
        /// </summary>
        /// <value>Object identifier.</value>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the gateway URL.
        /// </summary>
        /// <value>Gateway URL where the socket is going to connect.</value>
        public string Url
        {
            get
            {
                return _url;
            }
            set
            {
                _isCluster = false;
                _url = String.IsNullOrEmpty(value) ? String.Empty : value.Trim();
            }
        }

        /// <summary>
        /// Gets or sets the cluster gateway URL.
        /// </summary>
        public string ClusterUrl
        {
            get
            {
                return _clusterUrl;
            }
            set
            {
                _isCluster = true;
                _clusterUrl = String.IsNullOrEmpty(value) ? String.Empty : value.Trim();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this instance is cluster.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is cluster; otherwise, <c>false</c>.
        /// </value>
        public bool IsCluster
        {
            get { return _isCluster; }
            set { _isCluster = value; }
        }

        /// <summary>
        /// Gets or sets the connection timeout. Default value is 5000 miliseconds.
        /// </summary>
        public int ConnectionTimeout { get; set; }

        /// <summary>
        /// Gets a value indicating whether this client object is connected.
        /// </summary>
        /// <value>
        /// <c>true</c> if this client is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected { get; set; }

        /// <summary>
        /// Gets or sets the client connection metadata.
        /// </summary>
        /// <value>
        /// Connection metadata.
        /// </value>
        public string ConnectionMetadata { get; set; }

        /// <summary>
        /// Gets or sets the client announcement subchannel.
        /// </summary>
        /// /// <value>
        /// Announcement subchannel.
        /// </value>
        public string AnnouncementSubChannel { get; set; }

        /// <summary>
        /// Gets or sets the session id.
        /// </summary>
        /// <value>
        /// The session id.
        /// </value>
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this client has a heartbeat activated.
        /// </summary>
        /// <value>
        /// <c>true</c> if the heartbeat is active; otherwise, <c>false</c>.
        /// </value>
        public bool HeartbeatActive { get; set; }

        /// <summary>
        /// Gets or sets a value indicating how many times can the client fail the heartbeat.
        /// </summary>
        /// <value>
        /// Failure limit.
        /// </value>
        public int HeartbeatFails
        {
            get { return _heartbeatFail; }
            set { _heartbeatFail = value > HEARTBEAT_MAX_FAIL ? HEARTBEAT_MAX_FAIL : (value < HEARTBEAT_MIN_FAIL ? HEARTBEAT_MIN_FAIL : value); }
        }

        /// <summary>
        /// Gets or sets the heartbeat interval.
        /// </summary>
        /// <value>
        /// Interval in seconds between heartbeats.
        /// </value>
        public int HeartbeatTime {
            get { return _heartbeatTime; }
            set { _heartbeatTime = value > HEARTBEAT_MAX_TIME ? HEARTBEAT_MAX_TIME : (value < HEARTBEAT_MIN_TIME ? HEARTBEAT_MIN_TIME : value); }
        }

        #endregion
        
        #region Methods (8)

        /// <summary>
        /// Connects to the gateway with the application key and authentication token. The gateway must be set before using this method.
        /// </summary>
        /// <param name="applicationKey">Your application key to use ORTC.</param>
        /// <param name="authenticationToken">Authentication token that identifies your permissions.</param>
        /// <example>
        ///   <code>
        /// ortcClient.Connect("myApplicationKey", "myAuthenticationToken");
        ///   </code>
        ///   </example>
        public virtual Task Connect(string applicationKey, string authenticationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Disconnects from the gateway.
        /// </summary>
        /// <example>
        ///   <code>
        /// ortcClient.Disconnect();
        ///   </code>
        ///   </example>
        public virtual void Disconnect()
        {
            throw new NotImplementedException();
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
        public virtual Task Subscribe(string channel, bool subscribeOnReconnected, OnMessageDelegate onMessage)
        {
            throw new NotImplementedException();
        }

		/// <summary>
		/// Subscribes to a channel with a message filter.
		/// </summary>
		/// <param name="channel">Channel name.</param>
		/// <param name="subscribeOnReconnected">Subscribe to the specified channel on reconnect.</param>
		/// <param name="filter">Message filter</param>
		/// <param name="onMessage"><see cref="OnMessageWithFilterDelegate"/> callback.</param>
		/// <example>
		///   <code>
		/// ortcClient.subscribeWithFilter("channelName", true, "a = 1", OnMessageCallback);
		/// private void OnMessageCallback(object sender, string channel, Boolean filtered, string message)
		/// {
		/// // Do something
		/// }
		///   </code>
		///   </example>
		public virtual Task SubscribeWithFilter(String channel, Boolean subscribeOnReconnect,
						  String filter, OnMessageWithFilterDelegate onMessage)
		{
			throw new NotImplementedException();
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
		public virtual Task SubscribeWithOptions(Dictionary<string, object> options, OnMessageWithOptionsDelegate onMessage)
        { 
            throw new NotImplementedException();
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
		public virtual Task subscribeWithBuffer(String channel, String subscriberId, OnMessageWithBufferDelegate onMessage)
        { 
            throw new NotImplementedException();
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
        public virtual Task Unsubscribe(string channel)
        {
            throw new NotImplementedException();
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
        public virtual Task Send(string channel, string message)
        {
            throw new NotImplementedException();
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
		public virtual void publish(string channel, string message, int ttl, OnPublishResultDelegate callback){
            throw new NotImplementedException();
        }

        public abstract Task SendProxyAsync(string applicationKey,string privateKey,string channel,string message);

        /// <summary>
        /// Indicates whether is subscribed to a channel.
        /// </summary>
        /// <param name="channel">The channel name.</param>
        /// <returns>
        ///   <c>true</c> if subscribed to the channel; otherwise, <c>false</c>.
        /// </returns>
        public virtual bool IsSubscribed(string channel)
        {
            throw new NotImplementedException();
        }
        
        /// <summary>
        /// Gets the subscriptions in the specified channel and if active the first 100 unique metadata.
        /// </summary>
        /// <param name="channel">Channel with presence data active.</param>
        /// <param name="callback"><see cref="OnPresenceDelegate"/>Callback with error <see cref="OrtcPresenceException"/> and result <see cref="Presence"/>.</param>
        /// <example>
        /// <code>
        /// client.Presence("presence-channel", (error, result) =>
        /// {
        ///     if (error != null)
        ///     {
        ///         Console.WriteLine(error.Message);
        ///     }
        ///     else
        ///     {
        ///         if (result != null)
        ///         {
        ///             Console.WriteLine(result.Subscriptions);
        /// 
        ///             if (result.Metadata != null)
        ///             {
        ///                 foreach (var metadata in result.Metadata)
        ///                 {
        ///                     Console.WriteLine(metadata.Key + " - " + metadata.Value);
        ///                 }
        ///             }
        ///         }
        ///         else
        ///         {
        ///             Console.WriteLine("There is no presence data");
        ///         }
        ///     }
        /// });
        /// </code>
        /// </example>
        public abstract void Presence(String channel, OnPresenceDelegate callback);

        /// <summary>
        /// Enables presence for the specified channel with first 100 unique metadata if metadata is set to true.
        /// </summary>
        /// <param name="privateKey">The private key provided when the ORTC service is purchased.</param>
        /// <param name="channel">Channel to activate presence.</param>
        /// <param name="metadata">Defines if to collect first 100 unique metadata.</param>
        /// <param name="callback">Callback with error <see cref="OrtcPresenceException"/> and result.</param>
        /// /// <example>
        /// <code>
        /// client.EnablePresence("myPrivateKey", "presence-channel", false, (error, result) =>
        /// {
        ///     if (error != null)
        ///     {
        ///         Console.WriteLine(error.Message);
        ///     }
        ///     else
        ///     {
        ///         Console.WriteLine(result);
        ///     }
        /// });
        /// </code>
        /// </example>
        public abstract void EnablePresence(String privateKey, String channel, bool metadata, OnEnablePresenceDelegate callback);

        /// <summary>
        /// Disables presence for the specified channel.
        /// </summary>
        /// <param name="privateKey">The private key provided when the ORTC service is purchased.</param>
        /// <param name="channel">Channel to disable presence.</param>
        /// <param name="callback">Callback with error <see cref="OrtcPresenceException"/> and result.</param>
        public abstract void DisablePresence(String privateKey, String channel, OnDisablePresenceDelegate callback);




        /// <summary>
        /// Reads the local storage.
        /// </summary>
        /// <param name="applicationKey">The application key.</param>
        /// <param name="sessionExpirationTime">The session expiration time.</param>
        /// <returns></returns>
        public string ReadLocalStorage(string applicationKey, int sessionExpirationTime)
        {
            //IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null);
            //string isolatedFileName = String.Format("{0}{1}", SESSION_STORAGE_NAME, applicationKey);
            //string[] fileNames = isoStore.GetFileNames(isolatedFileName);
            //DateTime sessionCreatedAt = DateTime.MinValue;
            string sessionId = "";

            //foreach (string file in fileNames)
            //{
            //    if (file == isolatedFileName)
            //    {
            //        IsolatedStorageFileStream iStream = new IsolatedStorageFileStream(isolatedFileName, FileMode.Open, isoStore);
            //        StreamReader reader = new StreamReader(iStream);
            //        string line;
            //        int lineCount = 0;

            //        while ((line = reader.ReadLine()) != null)
            //        {
            //            if (lineCount == 0)
            //            {
            //                sessionId = line;
            //            }
            //            else
            //            {
            //                sessionCreatedAt = DateTime.Parse(line);
            //            }

            //            lineCount++;
            //        }

            //        reader.Close();

            //        break;
            //    }
            //}

            //DateTime currentDateTime = DateTime.Now;
            //TimeSpan interval = currentDateTime.Subtract(sessionCreatedAt);

            //if (sessionCreatedAt != DateTime.MinValue && interval.TotalMinutes >= sessionExpirationTime)
            //{
            //    sessionId = "";
            //}
            //else if (!String.IsNullOrEmpty(sessionId))
            //{
            //    SessionId = sessionId;
            //}

            return sessionId;
        }
        
        /// <summary>
        /// Creates the local storage.
        /// </summary>
        /// <param name="applicationKey">The application key.</param>
        public void CreateLocalStorage(string applicationKey)
        {
            //IsolatedStorageFile isoStore = IsolatedStorageFile.GetStore(IsolatedStorageScope.User | IsolatedStorageScope.Assembly, null, null);
            //string isolatedFileName = String.Format("{0}{1}", SESSION_STORAGE_NAME, applicationKey);
            //IsolatedStorageFileStream oStream = new IsolatedStorageFileStream(isolatedFileName, FileMode.Create, isoStore);
            //StreamWriter writer = new StreamWriter(oStream);

            //writer.WriteLine(String.Format("{0}\n{1}", SessionId, DateTime.Now));
            //writer.Close();
        }

        #endregion

        #region Events (7)

        /// <summary>
        /// Occurs when a connection attempt was successful.
        /// </summary>
        public abstract event OnConnectedDelegate OnConnected;

        /// <summary>
        /// Occurs when the client connection terminated. 
        /// </summary>
        public abstract event OnDisconnectedDelegate OnDisconnected;

        /// <summary>
        /// Occurs when the client subscribed to a channel.
        /// </summary>
        public abstract event OnSubscribedDelegate OnSubscribed;

        /// <summary>
        /// Occurs when the client unsubscribed from a channel.
        /// </summary>
        public abstract event OnUnsubscribedDelegate OnUnsubscribed;

        /// <summary>
        /// Occurs when there is an error.
        /// </summary>
        public abstract event OnExceptionDelegate OnException;

        /// <summary>
        /// Occurs when a client attempts to reconnect.
        /// </summary>
        public abstract event OnReconnectingDelegate OnReconnecting;

        /// <summary>
        /// Occurs when a client reconnected.
        /// </summary>
        public abstract event OnReconnectedDelegate OnReconnected;

        #endregion

    }
}
