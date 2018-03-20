using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.Serialization;
using System.Text.RegularExpressions;
using System.IO;
using RealtimeMessaging.DotNetCore.Extensibility;
using System.Threading.Tasks;

namespace RealtimeMessaging.DotNetCore.Extensibility
{
    public delegate void OnPresenceDelegate(OrtcPresenceException ex, Presence presence);
    public delegate void OnDisablePresenceDelegate(OrtcPresenceException ex, string result);
    public delegate void OnEnablePresenceDelegate(OrtcPresenceException ex, string result);

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
        public Dictionary<string, long> Metadata { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Presence"/> class.
        /// </summary>
        public Presence()
        {
            this.Subscriptions = 0;
            this.Metadata = new Dictionary<string, long>();
        }

        /// <summary>
        /// Deserializes the specified json string to a presence object.
        /// </summary>
        /// <param name="message">Json string to deserialize.</param>
        /// <returns></returns>
        public static Presence Deserialize(string message)
        {
            Presence result = new Presence();

            if (!string.IsNullOrEmpty(message))
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
        public static async Task<Presence> GetPresenceAsync(string url, bool isCluster, string applicationKey, string authenticationToken, string channel)
        {
            var server = await Balancer.GetServerUrlAsync(url, isCluster, applicationKey);

            var presenceUrl = string.IsNullOrEmpty(server) ? server : server[server.Length - 1] == '/' ? server : server + "/";
            presenceUrl = string.Format("{0}presence/{1}/{2}/{3}", presenceUrl, applicationKey, authenticationToken, channel);

            var result = await RestWebservice.GetAsync(presenceUrl);

            var presenceData = new Presence();
            if (!string.IsNullOrEmpty(result))
                presenceData = Extensibility.Presence.Deserialize(result);

            return presenceData;
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
        public static async Task<string> EnablePresenceAsync(string url, bool isCluster, string applicationKey, string privateKey, string channel, bool metadata)
        {
            var server = await Balancer.GetServerUrlAsync(url, isCluster, applicationKey);

            var presenceUrl = string.IsNullOrEmpty(server)
                ? server
                : server[server.Length - 1] == '/' ? server : server + "/";
            presenceUrl = string.Format("{0}presence/enable/{1}/{2}", presenceUrl, applicationKey, channel);

            var content = string.Format("privatekey={0}", privateKey);

            if (metadata)
            {
                content = string.Format("{0}&metadata=1", content);
            }

            return await RestWebservice.PostAsync(presenceUrl, content);
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
        public static async Task<string> DisablePresenceAsync(string url, bool isCluster, string applicationKey, string privateKey, string channel)
        {
            var server = await Balancer.GetServerUrlAsync(url, isCluster, applicationKey);

            var presenceUrl = string.IsNullOrEmpty(server)
                ? server
                : server[server.Length - 1] == '/' ? server : server + "/";
            presenceUrl = string.Format("{0}presence/disable/{1}/{2}", presenceUrl, applicationKey, channel);

            var content = string.Format("privatekey={0}", privateKey);
            return await RestWebservice.PostAsync(presenceUrl, content);
        }
    }
}
