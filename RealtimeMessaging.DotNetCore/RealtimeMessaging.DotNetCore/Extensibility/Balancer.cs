using System;
using System.Net;
using System.IO;
//using System.Security.Policy;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace RealtimeMessaging.DotNetCore.Extensibility
{
    /// <summary>
    /// Callback delegate type raised after resolving a cluster server from balancer
    /// </summary>
    /// <param name="server">The server.</param>
    /// <remarks></remarks>
    public delegate void OnBalancerUrlResolvedDelegate(string server, Exception ex);

    public delegate void OnGetServerUrlDelegate(Exception ex, string server);

    /// <summary>
    /// A static class containing all the methods to communicate with the Ortc Balancer 
    /// </summary>
    /// <example>
    /// <code>
    /// string url = "http://ortc-developers.realtime.co/server/2.1/"";
    /// Balancer.GetServerFromBalancer(url, applicationKey, (server) =>
    ///		{
    ///			//Do something with the returned server      
    ///		});
    /// </code>
    /// </example>
    /// <remarks></remarks>
    public static class Balancer
    {
        #region Fields (1)

        private const string BALANCER_SERVER_PATTERN = "^var SOCKET_SERVER = \"(?<server>http.*)\";$";

        #endregion Fields

        #region Methods (2)

        // Public Methods (1) 

        /// <summary>
        /// Retrieves an Ortc Server url from the Ortc Balancer
        /// </summary>
        /// <param name="balancerUrl">The Ortc Balancer url.</param>
        /// <param name="onClusterUrlResolved">Callback that is raised after an Ortc server url have been retrieved from the Ortc balancer.</param>
        /// <example>
        /// <code>
        /// string url = "http://ortc-developers.realtime.co/server/2.1/";
        /// Balancer.GetServerFromBalancer(url, applicationKey, (server) =>
        ///		{
        ///			//Do something with the returned server      
        ///		});
        /// </code>
        /// </example>
        /// <remarks></remarks>
        public static string lastBalancerUrl = "";
        public static async Task<string> GetServerFromBalancerAsync(string balancerUrl, string applicationKey)
        {
            lastBalancerUrl = string.IsNullOrEmpty(applicationKey) ? balancerUrl : balancerUrl + "?appkey=" + applicationKey;
            return await GetServerFromBalancerAsync(lastBalancerUrl);
        }

        public static async Task<string> GetServerFromBalancerAsync(string parsedUrl)
        {
            var request = (HttpWebRequest)WebRequest.Create(new Uri(parsedUrl));

            //ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;
            //.SecurityProtocol = SecurityProtocolType.Tls;

            request.Proxy = null;
            request.ContinueTimeout = 1000;
            //request.ProtocolVersion = HttpVersion.Version11;
            request.Method = "GET";

            var server = string.Empty;
            var response = await request.GetResponseAsync();

            var streamResponse = response.GetResponseStream();
            var streamReader = new StreamReader(streamResponse);

            server = ParseBalancerResponse(streamReader);

            if (!IsWellFormedBalancerUrl(server))
            {
                throw new Exception("Balancer server url is not valid (" + server + ")");
            }

            return server;
        }

        // Private Methods (1) 

        private static bool IsWellFormedBalancerUrl(string url)
        {
            var valid = true;

            //if (Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
            //{
            //    var uri = new Uri(url);
            //    valid = uri.Scheme == Uri. || uri.Scheme == Uri.UriSchemeHttps;
            //}

            return valid;
        }

        private static string ParseBalancerResponse(StreamReader response)
        {
            var responseBody = response.ReadToEnd();

            string server = "";

            var match = Regex.Match(responseBody, BALANCER_SERVER_PATTERN);

            if (match.Success)
            {
                server = match.Groups["server"].Value;
            }

            return server;
        }


        public static async Task<string> GetServerUrlAsync(string url, bool isCluster, string applicationKey)
        {
            if (string.IsNullOrEmpty(url) || !isCluster)
                return null;

            return await GetServerFromBalancerAsync(url, applicationKey);
        }

        #endregion Methods
    }
}
