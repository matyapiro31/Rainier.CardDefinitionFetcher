/*************************************************************************
* Rainier Card Definition Fetcher
* (c) 2022 Hastwell/Electrosheep Networks 
* 
* This program is free software: you can redistribute it and/or modify
* it under the terms of the GNU Affero General Public License as published
* by the Free Software Foundation, either version 3 of the License, or
* (at your option) any later version.
* 
* This program is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
* GNU Affero General Public License for more details.
* 
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see <http://www.gnu.org/licenses/>.
**************************************************************************/

using Newtonsoft.Json;
using Omukade.AutoPAR.Rainier;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TPCI.PTCS;
using UnityEngine;
using static Omukade.Tools.RainierCardDefinitionFetcher.Manipulators;

namespace Omukade.Tools.RainierCardDefinitionFetcher
{
    internal static class AccessHelper
    {
        public static PTCSUtils.TokenData GetTokenForUsernameAndPassword()
        {
            ClientData clientData = newWithoutConstructor<ClientData>();
            clientData.clientID = "tpci-tcg-app";
            clientData.redirectURI = "https://tpcitcgapp/callback";
            clientData.scope = new string[] { "offline", "screen_name", "openid", "friends" };

            const string AUTH_STAGE_1_PREFIX = "https://access.pokemon.com/oauth2/auth";
            const string AUDIENCE_VALUE = "https://op-core.pokemon.com+https://api.friends.pokemon.com";
            const string SELECTED_LANGUAGE = "en";
            string stage1url = TPCI.PTCS.PTCSUtils.GetAuthRequest(AUTH_STAGE_1_PREFIX, AUDIENCE_VALUE, clientData, SELECTED_LANGUAGE, out string challenge, out string verifier);
            Console.WriteLine("Bot-based login is not allowed now, so you must log in by using Web Browser.");
            // if OS is Windows, open the URL in the default browser
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string encodedUrl = stage1url.Replace("&", "\"&\"");
                Process.Start(new ProcessStartInfo("cmd", $"/c start {encodedUrl}") { CreateNoWindow = true });
            }
            else
            {
                Console.WriteLine("Login url is:");
                Console.WriteLine(stage1url);
            }
            Console.WriteLine();
            HttpClient httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            httpClient.DefaultRequestHeaders.Add("User-Agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36");
            
            // Stage 7 - POST /oauth2/token
            Console.WriteLine("Insert url start with tpcitcgapp:");
            string? hoge = Console.ReadLine();
            if (hoge == String.Empty)
            {
                Environment.Exit(-1);
            }
            Uri? stage7url = new Uri(hoge!) ;
            System.Collections.Specialized.NameValueCollection stage7queryparams = System.Web.HttpUtility.ParseQueryString(stage7url.Query.TrimStart('?'));
            FormUrlEncodedContent stage6postbody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"code", stage7queryparams["code"]! },
                {"grant_type", "authorization_code" },
                {"client_id", clientData.clientID },
                {"code_verifier", verifier },
                {"redirect_uri", clientData.redirectURI},
                {"state", challenge }
            });

            HttpResponseMessage stage7result = httpClient.PostAsync("https://access.pokemon.com/oauth2/token", stage6postbody).Result;
            string stage7body = stage7result.Content.ReadAsStringAsync().Result;
            Console.WriteLine(stage7body);
            var tokenObject = JsonConvert.DeserializeObject<PTCSUtils.TokenData>(stage7body);
            Console.WriteLine("Refresh Token is: ");
            Console.WriteLine(tokenObject.refresh_token);
            RainierSharedDataHelper.CreateRegistryFile(tokenObject.refresh_token, tokenObject.expires_in);

            return tokenObject;
        }
        public static PTCSUtils.TokenData GetTokenByOAuth(string refreshToken)
        {
            string clientID = "tpci-tcg-app";
            HttpClient httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false });
            httpClient.DefaultRequestHeaders.Add("User-Agent", @"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/81.0.4044.138 Safari/537.36");
            FormUrlEncodedContent postbody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                {"grant_type", "refresh_token" },
                {"client_id", clientID },
                {"redirect_url", "https://tpcitcgapp/callback" },
                {"refresh_token", refreshToken }
            });
            HttpResponseMessage result = httpClient.PostAsync("https://access.pokemon.com/oauth2/token", postbody).Result;
            if (!result.IsSuccessStatusCode)
            {
                Console.WriteLine("Failed to get token by refresh token.");
                File.Delete(Path.Combine(RainierSharedDataHelper.GetSharedDataDirectory(), "config-rcd.json"));
                Environment.Exit(-1);
            }
            string tokenJson = result.Content.ReadAsStringAsync().Result;
            Console.WriteLine(tokenJson);
            var tokenObject = JsonConvert.DeserializeObject<PTCSUtils.TokenData>(tokenJson);
            Console.WriteLine("Refresh Token is: ");
            Console.WriteLine(tokenObject.refresh_token);
            RainierSharedDataHelper.CreateRegistryFile(tokenObject.refresh_token, tokenObject.expires_in);
            return tokenObject;
        }
    }
}
