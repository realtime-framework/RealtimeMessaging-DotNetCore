using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Net.Http;
using System.Net.NetworkInformation;

namespace RT.Ortc.Api.Extensibility
{
    internal delegate void OnResponseDelegate(OrtcPresenceException ex, String result);

    internal static class RestWebservice
    {
        internal static async Task GetAsync(String url, OnResponseDelegate callback)
        {
            await RestWebservice.RequestAsync(url, "GET", null, callback);
        }

        internal static async Task PostAsync(String url, String content, OnResponseDelegate callback)
        {
            await RestWebservice.RequestAsync(url, "POST", content, callback);
        }

        private static async Task RequestAsync(String url, String method, String content, OnResponseDelegate callback)
        {
          
            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));

            request.Proxy = null;
            request.ContinueTimeout = 10000;
            request.Method = method;

            if (String.Compare(method, "POST") == 0 && !String.IsNullOrEmpty(content))
            {
                byte[] postBytes = Encoding.UTF8.GetBytes(content);

                request.ContentType = "application/x-www-form-urlencoded";

                var requestStream = await request.GetRequestStreamAsync();

				requestStream.Write(postBytes, 0, postBytes.Length);
                requestStream.Dispose();
            }

            request.BeginGetResponse(new AsyncCallback((asynchronousResult) =>
            {
                var server = String.Empty;

                var synchContext = System.Threading.SynchronizationContext.Current;

                try
                {
                    HttpWebRequest asyncRequest = (HttpWebRequest)asynchronousResult.AsyncState;

                    HttpWebResponse response = (HttpWebResponse)asyncRequest.EndGetResponse(asynchronousResult);
                    Stream streamResponse = response.GetResponseStream();
                    StreamReader streamReader = new StreamReader(streamResponse);

                    var responseBody = streamReader.ReadToEnd();

                    if (callback != null)
                    {
                        if (synchContext != null)
                        {
                            synchContext.Post(obj => callback(null, responseBody), null);
                        }
                        else
                        {
                            Task.Factory.StartNew(() => callback(null, responseBody));
                        }
                    }
                }
                catch (WebException wex)
                {
                    String errorMessage = String.Empty;
                    if (wex.Response == null)
                    {
                        errorMessage = "Uknown request error";
                        if (synchContext != null)
                        {
                            synchContext.Post(obj => callback(new OrtcPresenceException(errorMessage), null), null);
                        }
                        else
                        {
                            Task.Factory.StartNew(() => callback(new OrtcPresenceException(errorMessage), null));
                        }
                    }
                    else
                    {
                        using (var stream = wex.Response.GetResponseStream())
                        {
                            using (var reader = new StreamReader(stream))
                            {
                                errorMessage = reader.ReadToEnd();
                            }

                            if (synchContext != null)
                            {
                                synchContext.Post(obj => callback(new OrtcPresenceException(errorMessage), null), null);
                            }
                            else
                            {
                                Task.Factory.StartNew(() => callback(new OrtcPresenceException(errorMessage), null));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                   if (synchContext != null)
                        {
                            synchContext.Post(obj => callback(new OrtcPresenceException(ex.Message), null), null);
                        }
                        else
                        {
                            Task.Factory.StartNew(() => callback(new OrtcPresenceException(ex.Message), null));
                        }
                    }
            }), request);
        }
    }
}
