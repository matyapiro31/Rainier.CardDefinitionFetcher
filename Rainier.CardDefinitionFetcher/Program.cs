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

//using Platform.Sdk;
//using Platform.Sdk.Models.Account;
using ClientNetworking;
using ClientNetworking.Models.Account;
using Omukade.Tools.RainierCardDefinitionFetcher;
using Spectre.Console;
using static TPCI.PTCS.PTCSUtils;
using Omukade.Tools.RainierCardDefinitionFetcher.Model;
using Newtonsoft.Json;
using Omukade.AutoPAR;
using Omukade.AutoPAR.Rainier;
using TPCI.PTCS;
using System.Reflection;
using System.Collections.Generic;

internal class Program
{
    const string ARG_FETCH_ITEMDB = "--fetch-itemdb";
    const string ARG_FETCH_CARDACTIONS = "--fetch-cardactions";
    const string ARG_FETCH_CARDDB = "--fetch-carddb";
    const string ARG_FETCH_CARDDEFINITIONS = "--fetch-carddefinitions";
    const string ARG_FETCH_CARDDEFINITIONS_SHORTER = "--fetch-carddefs";
    const string ARG_CARDDEFS_IGNORE_INVALID_CARDS_FILE = "--ignore-invalid-ids-file";
    const string ARG_FETCH_RULES = "--fetch-rules";
    const string ARG_FETCH_ALL_QUESTS = "--fetch-allquests";
    const string ARG_FETCH_CURRENT_QUESTS = "--fetch-currentquests";
    const string ARG_FETCH_AIDECKS = "--fetch-aidecks";
    const string ARG_FETCH_OTHERDB = "--fetch-otherdb";
    const string ARG_OUTPUT_FOLDER = "--output-folder";
    const string ARG_INPUT_TOKEN = "--input-token";
    const string ARG_REFRESH_TOKEN = "--refresh-token";
    const string ARG_HELP_LONG = "--help";
    const string ARG_HELP_SHORT = "-h";
    // Default is User's AppData/Local/omukade/rainier-shared
    static internal string outputFolder = RainierSharedDataHelper.GetSharedDataDirectory();
    private static void Main(string[] args)
    {
        // Log in to the Pokemon TCG API with Browser.
        Console.WriteLine("Checking for Rainier updates...");
        UpdaterManifest updaterManifest = RainierFetcher.GetUpdateManifestAsync().Result;
        if(RainierFetcher.DoesNeedUpdate(updaterManifest))
        {
            Console.WriteLine($"Update detected; downloading...");
            RainierFetcher.DownloadUpdateFile(updaterManifest).Wait();
            Console.WriteLine("Update downloaded; extracting...");
            RainierFetcher.ExtractUpdateFile(deleteExistingUpdateFolder: true);
            Console.WriteLine("Update complete and extracted.");
        }
        else
        {
            Console.WriteLine("Rainier client is up-to-date");
        }

        Console.WriteLine("Initializing AutoPAR...");
        AssemblyLoadInterceptor.Initialize(RainierFetcher.UpdateDirectory);

        PostParMain(args);
    }

    private static void PostParMain(string[] args)
    {
        Console.WriteLine("Rainer Card Definition Fetcher");

        if(args.Length == 0 || args.Contains(ARG_HELP_SHORT) || args.Contains(ARG_HELP_LONG))
        {
            ShowHelpText();
            return;
        }

        // Check if login with refresh token is enabled.
        if (!Directory.Exists(RainierSharedDataHelper.GetSharedDataDirectory()) || !File.Exists(Path.Combine(RainierSharedDataHelper.GetSharedDataDirectory(), "config-rcd.json")))
        {
            Console.WriteLine("Reading from Environment Variables PTCGL_FETCHER_REFRESH_TOKEN...");
            if (Environment.GetEnvironmentVariable("PTCGL_FETCHER_REFRESH_TOKEN") != null)
            {
                args = args.Prepend(Environment.GetEnvironmentVariable("PTCGL_FETCHER_REFRESH_TOKEN")).Prepend(ARG_REFRESH_TOKEN).ToArray()!;
                RainierSharedDataHelper.CreateRegistryFile(Environment.GetEnvironmentVariable("PTCGL_FETCHER_REFRESH_TOKEN")!);
            }
            else
            {
                Console.WriteLine("No refresh token found in Environment Variables. Creating registry file...");
            }
        }
        else
        {
            SecretsConfig secrets = JsonConvert.DeserializeObject<SecretsConfig>(File.ReadAllText(Path.Combine(RainierSharedDataHelper.GetSharedDataDirectory(), "config-rcd.json")));
            args = args.Prepend(secrets.RefreshToken).Prepend(ARG_REFRESH_TOKEN).ToArray()!;
        }

            // Parse output folder if needed
            outputFolder = GetOptionDirectory(args, ARG_OUTPUT_FOLDER);

        Console.WriteLine("Logging in...");
        TokenData tokenData;
        if (args.Contains(ARG_INPUT_TOKEN))
        {
            Console.WriteLine("Input token JSON here.");
            string? TokenJson = Console.ReadLine();
            if (TokenJson != null)
            {
                tokenData = JsonConvert.DeserializeObject<PTCSUtils.TokenData>(TokenJson);
            }
            else
            {
                tokenData = AccessHelper.GetTokenForUsernameAndPassword();
            }
        }
        else if (args.Contains(ARG_REFRESH_TOKEN))
        {
            //Console.WriteLine("Input refresh token here.");
            string? RefreshToken = args.SkipWhile(arg => arg != ARG_REFRESH_TOKEN).Skip(1).FirstOrDefault();
            if (RefreshToken!=null && !RefreshToken.StartsWith("--"))
            {
                tokenData = AccessHelper.GetTokenByOAuth(RefreshToken);
            }
            else
            {
                tokenData = AccessHelper.GetTokenForUsernameAndPassword();
            }
        }
        else
        {
            tokenData = AccessHelper.GetTokenForUsernameAndPassword();
        }
        Console.WriteLine("Logged in Successfully");

        // Access Key is hardcoded in Client.Setup
        const string ACCESS_KEY = "421d8904-0236-4ab4-94f5-a8a84aeb3f7b";

        // DEVICE_ID is derived from, in preference:
        // * UnityEngine.SystemInfo.deviceUniqueIdentifier
        // * PlayerPrefs.GetString("GameVersionInfo:Identifier")
        // * Guid.NewGuid()
        string DEVICE_ID = Guid.NewGuid().ToString(); //"1047b8069bcaa0358004cb88aad57f5cc7dc4759";

        // CLIENT_ID is derived from, in perference:
        // * tokenData.id_token
        // * If PlayerPrefs.GetInt("prefs-random-user") > 0, Guid.NewGuid()
        // * UnityEngine.SystemInfo.deviceUniqueIdentifier
        // * PlayerPrefs.GetString("GameVersionInfo:Identifier")
        // * Guid.NewGuid()
        string CLIENT_ID = tokenData.id_token ?? Guid.NewGuid().ToString(); // "6a9d54403b2ba18414990995b57b2632";

        Client client = (Client)new ClientBuilder()
            .WithStage(Stages.PROD)
            .WithAccessKey(ACCESS_KEY)
            .WithDeviceInfo(deviceId: DEVICE_ID, deviceType: null, "Windows")
            .Build(clientId: CLIENT_ID);
        HashSet<string> updatedApis = new HashSet<string> {
            "/account/v1/external/token/login",
            "/account/v1/external/token/register",
            "/commerce/v1/external/webccr/can_redeem",
            "/commerce/v1/external/webccr/redeem",
            "/commerce/v1/external/webccr/verify",
            "/user/v1/external/routing/route"
        };
        // Update to new APIs. (never used)
        // Use dnspy to change Platform.Sdk.HttpRouter.RouteQuery(). 
        typeof(Client)?.GetField("AnonymousApis", BindingFlags.Instance | BindingFlags.Public)?.SetValue(client, updatedApis);
        client.RegisterAsync().Wait();
        client.MakeSyncCall<string, string, TokenResponse>(client.AuthAsync, tokenData.access_token, "PJWT");

        bool anyArgWasSpecified = false;
        if (args.Contains(ARG_FETCH_ITEMDB))
        {
            anyArgWasSpecified = true;
            Fetchers.FetchAndSaveItemDatabase(client);
        }

        if (args.Contains(ARG_FETCH_CARDACTIONS))
        {
            anyArgWasSpecified = true;
            Fetchers.FetchAndSaveCardActions(client);
        }

        if (args.Contains(ARG_FETCH_CARDDB))
        {
            anyArgWasSpecified = true;
            Fetchers.FetchAndSaveCardDatabase(client);
        }

        if (args.Contains(ARG_FETCH_CARDDEFINITIONS) || args.Contains(ARG_FETCH_CARDDEFINITIONS_SHORTER))
        {
            anyArgWasSpecified = true;
            Fetchers.FetchAndSaveCardDefinitions(client, leveragePreviousInvalidCardIds: !args.Contains(ARG_CARDDEFS_IGNORE_INVALID_CARDS_FILE));
        }

        if (args.Contains(ARG_FETCH_RULES))
        {
            anyArgWasSpecified = true;
            Fetchers.FetchAndSaveAllGamemodeData(client);
        }

        if (args.Contains(ARG_FETCH_AIDECKS))
        {
            anyArgWasSpecified = true;
            for (int i = 0; i < 5; i++)
            {
                Fetchers.FetchAndSaveAiCustomizationData(client);
            }
        }

        if(args.Contains(ARG_FETCH_ALL_QUESTS))
        {
            anyArgWasSpecified = true;
            Fetchers.FetchQuestData(client);
        }

        if (args.Contains(ARG_FETCH_OTHERDB))
        {
            anyArgWasSpecified = true;
            Fetchers.FetchAndSaveOtherDatabase(client);
        }

        if (!anyArgWasSpecified)
        {
            Console.WriteLine("No fetch argument was specified.");
            ShowHelpText();
        }
    }

    private static string GetOptionDirectory(string[] args, string option)
    {
        string? outputFolder;
        if (args.Contains(option))
        {
            outputFolder = args.SkipWhile(arg => arg != option).Skip(1).FirstOrDefault();
            if (outputFolder == null)
            {
                string err = option + " specified, but no output folder provided";
                Console.Error.WriteLine(err);
                Environment.Exit(1);
                throw new ArgumentException(err);
            }
            else if (outputFolder.StartsWith("--"))
            {
                string err = option + " specified, but output folder looks like another argument";
                Console.Error.WriteLine(err);
                Environment.Exit(1);
                throw new ArgumentException(err);
            }

            if (!Directory.Exists(outputFolder))
            {
                try
                {
                    Directory.CreateDirectory(outputFolder);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine("Couldn't create output directory because:");
                    AnsiConsole.WriteException(e);
                    Environment.Exit(1);
                    throw new ArgumentException("Couldn't create output directory");
                }
            }
        }
        else
        {
            outputFolder = RainierSharedDataHelper.GetSharedDataDirectory();
        }

        return outputFolder;
    }

    private static void ShowHelpText()
    {
        Console.WriteLine
        (
"""
Fetch arguments (as many as desired can be specified):
--fetch-itemdb          Fetches the database of items.
--fetch-cardactions     Fetches the localized list of card actions.
--fetch-carddb          Fetches the list of cards for display in-game (not implementations, see --fetch-carddefinitions)
--fetch-carddefinitions / --fetch-carddefs Fetches all card implementations.
--fetch-otherdb         Fetches all other configs.
--ignore-invalid-ids-file   By default, --fetch-carddefinitions will create a file of known-bad card IDs that can be skipped
                            for significantly faster performance on subsequent executions (minutes vs hours).
                            These entries may become stale as skipped cards become implemented; --fetch-carddefinitions should
                            probably be run monthly with this flag.

                            If the invalid IDs file doesn't already exist, this flag will have no effect.

--fetch-rules           Fetches the game rules.
--fetch-aidecks         Fetches a selection of decks used by the AI.

Omukade Cheyenne servers typically only need the results of --fetch-carddefinitions --fetch-rules

Output arguments:
--output-folder (/foo/bar)  DEPRECATED: Writes all fetched data to this folder. By default, the Common Omukade Datastore is used for output.
                            Directories will be created under this folder with all retrieved information.

Other arguments:
--refresh-token (token)  Login with OAuth2.0 refresh token. Youcan use Refresh token only once.
--input-token      Read Token text from stdin.
-h / --help             This help text. Will also appear automatically if run with no fetch arguments, or no arguments at all.
"""
        );
    }
}