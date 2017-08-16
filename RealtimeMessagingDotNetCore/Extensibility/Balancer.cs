using System;
using System.Net;
using System.IO;
//using System.Security.Policy;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;

namespace RT.Ortc.Api.Extensibility
{
    /// <summary>
    /// Callback delegate type raised after resolving a cluster server from balancer
    /// </summary>
    /// <param name="server">The server.</param>
    /// <remarks></remarks>
    public delegate void OnBalancerUrlResolvedDelegate(string server, Exception ex);

    public delegate void OnGetServerUrlDelegate(Exception ex, String server);

    /// <summary>
    /// A static class containing all the methods to communicate with the Ortc Balancer 
    /// </summary>
    /// <example>
    /// <code>
    /// String url = "http://ortc-developers.realtime.co/server/2.1/"";
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

        private const String BALANCER_SERVER_PATTERN = "^var SOCKET_SERVER = \"(?<server>http.*)\";$";

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
        /// String url = "http://ortc-developers.realtime.co/server/2.1/";
        /// Balancer.GetServerFromBalancer(url, applicationKey, (server) =>
        ///		{
        ///			//Do something with the returned server      
        ///		});
        /// </code>
        /// </example>
        /// <remarks></remarks>
        public static string lastBalancerUrl = "";
        public static void GetServerFromBalancerAsync(String balancerUrl, String applicationKey, OnBalancerUrlResolvedDelegate onClusterUrlResolved)
        {
            lastBalancerUrl = String.IsNullOrEmpty(applicationKey) ? balancerUrl : balancerUrl + "?appkey=" + applicationKey;

            GetServerFromBalancerAsync(lastBalancerUrl, onClusterUrlResolved);
        }

        public static void GetServerFromBalancerAsync(String parsedUrl, OnBalancerUrlResolvedDelegate onClusterUrlResolved){
			var request = (HttpWebRequest)WebRequest.Create(new Uri(parsedUrl));

			//ServicePointManager.SecurityProtocol = SecurityProtocolType.Ssl3;
			//.SecurityProtocol = SecurityProtocolType.Tls;

			request.Proxy = null;
			request.ContinueTimeout = 1000;
			//request.ProtocolVersion = HttpVersion.Version11;
			request.Method = "GET";

			request.BeginGetResponse(new AsyncCallback((asynchronousResult) =>
			{
				var server = String.Empty;

				try
				{
					HttpWebRequest asyncRequest = (HttpWebRequest)asynchronousResult.AsyncState;

					HttpWebResponse response = (HttpWebResponse)asyncRequest.EndGetResponse(asynchronousResult);
					Stream streamResponse = response.GetResponseStream();
					StreamReader streamReader = new StreamReader(streamResponse);

					server = ParseBalancerResponse(streamReader);

					if (!IsWellFormedBalancerUrl(server))
					{
						throw new Exception("Balancer server url is not valid (" + server + ")");
					}

					if (onClusterUrlResolved != null)
					{
						onClusterUrlResolved(server, null);
					}
				}
				catch (Exception ex)
				{
					if (onClusterUrlResolved != null)
					{
                        onClusterUrlResolved(null, ex);
					}
				}
			}), request);
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

        private static String ParseBalancerResponse(StreamReader response)
        {
            var responseBody = response.ReadToEnd();

            String server = "";

            var match = Regex.Match(responseBody, BALANCER_SERVER_PATTERN);

            if (match.Success)
            {
                server = match.Groups["server"].Value;
            }

            return server;
        }


        public static void GetServerUrl(String url, bool isCluster, String applicationKey, OnGetServerUrlDelegate callback)
        {
            if (!String.IsNullOrEmpty(url) && isCluster)
            {
                GetServerFromBalancerAsync(url, applicationKey, (server, error) =>
                {
                    callback(error, server);
                });
            }
            else
            {
                callback(null, url);
            }
        }

        #endregion Methods
    }
}
