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

namespace RealtimeMessaging.DotNetCore.Extensibility
{
    internal delegate void OnResponseDelegate(OrtcPresenceException ex, string result);

    internal static class RestWebservice
    {
        internal static async Task GetAsync(string url, OnResponseDelegate callback)
        {
            await RestWebservice.RequestAsync(url, "GET", null, callback);
        }

        internal static async Task PostAsync(string url, string content, OnResponseDelegate callback)
        {
            await RestWebservice.RequestAsync(url, "POST", content, callback);
        }

        internal static async Task<string> GetAsync(string url)
        {
            return await RestWebservice.RequestAsync(url, "GET");
        }

        internal static async Task<string> PostAsync(string url, string content)
        {
            return await RestWebservice.RequestAsync(url, "POST");
        }

        private static async Task<string> RequestAsync(string url, string method, string content = null)
        {
            using (var client = new HttpClient())
            {
                var httpBody = new StringContent(content, Encoding.UTF8, "application/x-www-form-urlencoded");
                HttpResponseMessage res;
                switch (method.ToLower())
                {
                    case "post":
                        res = await client.PostAsync(url, httpBody);
                        break;
                    case "put":
                        res = await client.PutAsync(url, httpBody);
                        break;
                    case "delete":
                        res = await client.DeleteAsync(url);
                        break;
                    default:
                        res = await client.GetAsync(url);
                        break;
                }

                using (HttpContent httpContent = res.Content)
                {
                    return await httpContent.ReadAsStringAsync();
                }
            }
        }

        private static async Task RequestAsync(string url, string method, string content, OnResponseDelegate callback)
        {

            var request = (HttpWebRequest)WebRequest.Create(new Uri(url));

            request.Proxy = null;
            request.ContinueTimeout = 10000;
            request.Method = method;

            if (string.Compare(method, "POST") == 0 && !string.IsNullOrEmpty(content))
            {
                byte[] postBytes = Encoding.UTF8.GetBytes(content);

                request.ContentType = "application/x-www-form-urlencoded";

                var requestStream = await request.GetRequestStreamAsync();

                requestStream.Write(postBytes, 0, postBytes.Length);
                requestStream.Dispose();
            }

            request.BeginGetResponse(new AsyncCallback((asynchronousResult) =>
            {
                var server = string.Empty;

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
                    string errorMessage = string.Empty;
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
