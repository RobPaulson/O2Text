using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace O2TextLib
{
    public class O2Text
    {
        private const string O2InitialURL = "http://messaging.o2online.ie/ssomanager.osp?APIID=AUTH-WEBSSO&TargetApp=o2om_smscenter_new.osp%3FMsgContentID%3D-1%26SID%3D_";
        private const string O2PostLoginURL = "https://www.o2online.ie/oam/server/auth_cred_submit";
        private const string O2MessageBaseURL = "http://messaging.o2online.ie/";        
        private const string O2SendMsgPostURLA = "http://messaging.o2online.ie/smscenter_evaluate.osp";
        private const string O2SendMsgPostURLB = "http://messaging.o2online.ie/smscenter_send.osp";
        
        private readonly string _username;
        private readonly string _password;
        private readonly string _proxyAddress;

        private string O2MessageURL = string.Empty;
        private string PreSendHtml = string.Empty;
        private string _reqId = string.Empty;
        private readonly CookieContainer cc = new CookieContainer();
        
        private bool _loggedIn;

        /// <summary>
        /// Public class constructor - needs O2 username and password
        /// </summary>
        /// <param name="username">Your O2 account username</param>
        /// <param name="password">Your O2 account web password</param>
        /// <param name="proxyAddress">Optional.  If not supplied no proxy will be used.</param>
        public O2Text(string username, string password, string proxyAddress=null)
        {
            _username = username;
            _password = password;
            _proxyAddress = proxyAddress;

        }
        
        private HttpClient _client;
        private HttpClient Client
        {
            get
            {
                if (_client == null)
                {
                    var httpClientHandler = new HttpClientHandler
                    {
                        Proxy = string.IsNullOrEmpty(_proxyAddress) ? null : new WebProxy(_proxyAddress, false, null, CredentialCache.DefaultNetworkCredentials),
                        UseDefaultCredentials = true,
                        CookieContainer = cc,
                    };

                    _client = new HttpClient(httpClientHandler);
                    _client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 6.1; WOW64; rv:24.0) Gecko/20100101 Firefox/24.0");
                    _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "en-GB,en;q=0.8,en-US;q=0.6,es;q=0.4");
                    _client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
                }

                return _client;
            }
        }

        /// <summary>
        /// Send a free O2 webtext (block execution and wait for it to complete)
        /// </summary>
        /// <param name="recipient">The phone number to send to</param>
        /// <param name="message">The message you would like to send</param>
        /// <returns></returns>
        public int SendTextMessage(string recipient, string message)
        {
            return SendTextMessageAsync(recipient, message).Result;
        }

        /// <summary>
        /// Send a free O2 webtext using async/await
        /// </summary>
        /// <param name="recipient">The phone number to send to</param>
        /// <param name="message">The message you would like to send</param>
        /// <returns></returns>
        public async Task<int> SendTextMessageAsync(string recipient, string message)
        {
            if (string.IsNullOrEmpty(recipient))
                return -1;

            if (!_loggedIn)
                await LogIn();

            var valsToPost = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("SID", GetInput(PreSendHtml, "SID")),
                new KeyValuePair<string, string>("MsgContentID", GetInput(PreSendHtml, "MsgContentID")),
                new KeyValuePair<string, string>("SMSTo", recipient),
                new KeyValuePair<string, string>("SMSText", message),
                new KeyValuePair<string, string>("FlagDLR", GetInput(PreSendHtml, "FlagDLR")),
                new KeyValuePair<string, string>("RepeatStartDate", GetDateStr()),
                new KeyValuePair<string, string>("RepeatEndDate", GetDateStr()),
                new KeyValuePair<string, string>("RepeatType", "0"),
                new KeyValuePair<string, string>("RepeatEndType", "0"),
                new KeyValuePair<string, string>("REF", GetInput(PreSendHtml, "REF")),
                new KeyValuePair<string, string>("FID", GetInput(PreSendHtml, "FID")),
            };

            var postValues1 = new FormUrlEncodedContent(valsToPost);
            valsToPost.Add(new KeyValuePair<string, string>("RURL", GetInput(PreSendHtml, "RURL")));
            var postValues2 = new FormUrlEncodedContent(valsToPost);


            var post1 = await Client.PostAsync(O2SendMsgPostURLA, postValues1);
            var html1 = await post1.Content.ReadAsStringAsync();
            var x1 = html1.Split('\n').First(x => x.Contains("freeMessageCount : "));
            var left = int.Parse(x1.Split(':')[1].Trim().Split(',')[0]);

            var post2 = await Client.PostAsync(O2SendMsgPostURLB, postValues2);
            var html2 = await post2.Content.ReadAsStringAsync();

            return left;
        }

        private string GetDateStr()
        {
            return DateTime.Now.ToString("yyyy,MM,dd,30,00");
        }

        private async Task LogIn()
        {
            await GetAuthCookieAndReqId();
            Thread.Sleep(500);
            await PostUserNameAndPass();
            Thread.Sleep(500);
            UglyCookieHack();
            await LoadMessagePage();
            Thread.Sleep(500);
            _loggedIn = true;
        }

        private void UglyCookieHack()
        {
            var cookie = cc.GetCookies(new Uri(O2MessageBaseURL + "ssomanager.osp"))["o3sisCookie"];
            cc.Add(new Cookie("o3sisCookie", cookie.Value, "/", ".o2online.ie"));
        }

        private async Task LoadMessagePage()
        {
            if (!Client.DefaultRequestHeaders.Contains("Referer"))
                Client.DefaultRequestHeaders.Add("Referer", O2InitialURL);

            var result = await Client.GetAsync(new Uri(O2MessageURL));
            PreSendHtml = await result.Content.ReadAsStringAsync();

        }

        private async Task PostUserNameAndPass()
        {
            var postValues = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("username", _username),
                new KeyValuePair<string, string>("password", _password),
                new KeyValuePair<string, string>("request_id", _reqId),

            });

            var post = await Client.PostAsync(O2PostLoginURL, postValues);
            var html = await post.Content.ReadAsStringAsync();

            const string matchStr = "<frame name=\"frame_content\" src=\"";

            // Get the URL with Id
            if (O2MessageURL.Length == 0)
                O2MessageURL = O2MessageBaseURL + GetValue(html, matchStr);

        }

        private async Task GetAuthCookieAndReqId()
        {
            var result = await Client.GetAsync(new Uri(O2InitialURL));
            if (!result.IsSuccessStatusCode)
                throw new Exception("Could not get auth cookies from O2 website");

            const string matchStr = "name=\"request_id\" value=\"";

            var html = await result.Content.ReadAsStringAsync();
            _reqId = GetValue(html, matchStr);

        }

        private static string GetInput(string html, string key)
        {
            return GetValue(html, string.Format("name=\"{0}\" value=\"", key));
        }

        private static string GetValue(string html, string matchStr)
        {
            var start = html.IndexOf(matchStr) + matchStr.Length;
            if (start < 0)
                throw new Exception("Could not find matching string");

            var end = html.IndexOf("\"", start);

            return html.Substring(start, end - start);
        }
        
    }
}
