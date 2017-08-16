using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.IO;

namespace RT.Ortc.Api.Extensibility
{
    public delegate void OnPresenceDelegate(OrtcPresenceException ex, Presence presence);
    public delegate void OnDisablePresenceDelegate(OrtcPresenceException ex, String result);
    public delegate void OnEnablePresenceDelegate(OrtcPresenceException ex, String result);

    /// <summary>
    /// Presence info, such as total subscriptions and metadata.
    /// </summary>
    public class Presence
    {
        private const string SUBSCRIPTIONS_PATTERN = "^{\"subscriptions\":(?<subscriptions>\\d*),\"metadata\":{(?<metadata>.*)}}$";
        private const string METADATA_PATTERN = "\"([^\"]*|[^:,]*)*\":(\\d*)";
        private const string METADATA_DETAIL_PATTERN = "\"(.*)\":(\\d*)";
        
        /// <summary>
        /// Gets the subscriptions value.
        /// </summary>
        public long Subscriptions { get; private set; }
        
        /// <summary>
        /// Gets the first 100 unique metadata.
        /// </summary>
        public Dictionary<String,long> Metadata { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Presence"/> class.
        /// </summary>
        public Presence()
        {
            this.Subscriptions = 0;
            this.Metadata = new Dictionary<String, long>();
        }

        /// <summary>
        /// Deserializes the specified json string to a presence object.
        /// </summary>
        /// <param name="message">Json string to deserialize.</param>
        /// <returns></returns>
        public static Presence Deserialize(string message)
        {
            Presence result = new Presence();
            
            if (!String.IsNullOrEmpty(message))
            {
                var json = message.Replace("\\\\\"", @"""");
                json = Regex.Unescape(json);

                Match presenceMatch = Regex.Match(json, SUBSCRIPTIONS_PATTERN, RegexOptions.Compiled);

                var subscriptions = 0;

                if (int.TryParse(presenceMatch.Groups["subscriptions"].Value, out subscriptions))
                {
                    var metadataContent = presenceMatch.Groups["metadata"].Value;

                    var metadataRegex = new Regex(METADATA_PATTERN, RegexOptions.Compiled);
                    foreach (Match metadata in metadataRegex.Matches(metadataContent))
                    {
                        if (metadata.Groups.Count > 1)
                        {
                            var metadataDetailMatch = Regex.Match(metadata.Groups[0].Value, METADATA_DETAIL_PATTERN, RegexOptions.Compiled);

                            var metadataSubscriptions = 0;
                            if (int.TryParse(metadataDetailMatch.Groups[2].Value, out metadataSubscriptions))
                            {
                                result.Metadata.Add(metadataDetailMatch.Groups[1].Value, metadataSubscriptions);
                            }
                        }
                    }
                }

                result.Subscriptions = subscriptions;
               
            }

            return result;
        }

        /// <summary>
        /// Gets the subscriptions in the specified channel and if active the first 100 unique metadata.
        /// </summary>
        /// <param name="url">Server containing the presence service.</param>
        /// <param name="isCluster">Specifies if url is cluster.</param>
        /// <param name="applicationKey">Application key with access to presence service.</param>
        /// <param name="authenticationToken">Authentication token with access to presence service.</param>
        /// <param name="channel">Channel with presence data active.</param>
        /// <param name="callback"><see cref="OnPresenceDelegate"/>Callback with error <see cref="OrtcPresenceException"/> and result <see cref="Presence"/>.</param>
        /// <example>
        /// <code>
        /// client.Presence("http://ortc-developers.realtime.co/server/2.1", true, "myApplicationKey", "myAuthenticationToken", "presence-channel", (error, result) =>
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
        public static void GetPresence(String url, bool isCluster, String applicationKey, String authenticationToken, String channel, OnPresenceDelegate callback)
        {
            Balancer.GetServerUrl(url, isCluster, applicationKey, (error, server) =>
            {
                if (error == null)
                {
                    var presenceUrl = String.IsNullOrEmpty(server) ? server : server[server.Length - 1] == '/' ? server : server + "/";
                    presenceUrl = String.Format("{0}presence/{1}/{2}/{3}", presenceUrl, applicationKey, authenticationToken, channel);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    RestWebservice.GetAsync(presenceUrl, (responseError, result) =>

#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    {
                        if (responseError != null)
                        {
                            callback(responseError, null);
                        }
                        else
                        {
                            Presence presenceData = new Presence();
                            if (!String.IsNullOrEmpty(result))
                            {
                                presenceData = Extensibility.Presence.Deserialize(result);
                            }
                            callback(null, presenceData);
                        }
                    });  
                }
                else
                {
                    callback(new OrtcPresenceException(error.Message), null);
                }
            });
        }

        /// <summary>
        /// Enables presence for the specified channel with first 100 unique metadata if metadata is set to true.
        /// </summary>
        /// <param name="url">Server containing the presence service.</param>
        /// <param name="isCluster">Specifies if url is cluster.</param>
        /// <param name="applicationKey">Application key with access to presence service.</param>
        /// <param name="privateKey">The private key provided when the ORTC service is purchased.</param>
        /// <param name="channel">Channel to activate presence.</param>
        /// <param name="metadata">Defines if to collect first 100 unique metadata.</param>
        /// <param name="callback">Callback with error <see cref="OrtcPresenceException"/> and result.</param>
        /// <example>
        /// <code>
        /// client.EnablePresence("http://ortc-developers.realtime.co/server/2.1", true, "myApplicationKey", "myPrivateKey", "presence-channel", false, (error, result) =>
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
        public static void EnablePresence(String url, bool isCluster, String applicationKey, String privateKey, String channel, bool metadata, OnEnablePresenceDelegate callback)
        {
            Balancer.GetServerUrl(url, isCluster, applicationKey, (error, server) =>
            {
                if (error == null)
                {
                    var presenceUrl = String.IsNullOrEmpty(server)
                        ? server
                        : server[server.Length - 1] == '/' ? server : server + "/";
                    presenceUrl = String.Format("{0}presence/enable/{1}/{2}", presenceUrl, applicationKey, channel);

                    var content = String.Format("privatekey={0}", privateKey);

                    if (metadata)
                    {
                        content = String.Format("{0}&metadata=1", content);
                    }

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    RestWebservice.PostAsync(presenceUrl, content, (responseError, result) =>

#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    {
                        if (responseError != null)
                        {
                            callback(responseError, null);
                        }
                        else
                        {
                            callback(null, result);
                        }
                    });
                }
                else
                {
                    callback(new OrtcPresenceException(error.Message), null);
                }
            });
        }

        /// <summary>
        /// Disables presence for the specified channel.
        /// </summary>
        /// <param name="url">Server containing the presence service.</param>
        /// <param name="isCluster">Specifies if url is cluster.</param>
        /// <param name="applicationKey">Application key with access to presence service.</param>
        /// <param name="privateKey">The private key provided when the ORTC service is purchased.</param>
        /// <param name="channel">Channel to disable presence.</param>
        /// <param name="callback">Callback with error <see cref="OrtcPresenceException"/> and result.</param>
        public static void DisablePresence(String url, bool isCluster, String applicationKey, String privateKey, String channel, OnDisablePresenceDelegate callback)
        {
            Balancer.GetServerUrl(url, isCluster, applicationKey, (error, server) =>
            {
                if (error == null)
                {
                    var presenceUrl = String.IsNullOrEmpty(server) ? server : server[server.Length - 1] == '/' ? server : server + "/";
                    presenceUrl = String.Format("{0}presence/disable/{1}/{2}", presenceUrl, applicationKey, channel);

                    var content = String.Format("privatekey={0}", privateKey);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    RestWebservice.PostAsync(presenceUrl, content, (responseError, result) =>

#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    {
                        if (responseError != null)
                        {
                            callback(responseError, null);
                        }
                        else
                        {
                            callback(null, result);
                        }
                    });
                }
                else
                {
                    callback(new OrtcPresenceException(error.Message), null);
                }
            });
        }
    }
}
