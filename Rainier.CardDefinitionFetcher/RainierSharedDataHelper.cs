using Omukade.Tools.RainierCardDefinitionFetcher.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Omukade.AutoPAR.Rainier
{
    public static class RainierSharedDataHelper
    {
        public static string GetSharedDataDirectory()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData, Environment.SpecialFolderOption.DoNotVerify), "omukade", "rainier-shared");
        }

        internal static void CreateRegistryFile(string envRefreshToken = "", int envExpiresIn = 0)
        {
            string configPath = Path.Combine(GetSharedDataDirectory(), "config-rcd.json");
            // check if the directory exists
            if (!Directory.Exists(GetSharedDataDirectory()))
            {
                Directory.CreateDirectory(GetSharedDataDirectory());
            }
            var secrets = new SecretsConfig
            {
                RefreshToken = envRefreshToken,
                ExpiresIn = envExpiresIn
            };
            string json = JsonConvert.SerializeObject(secrets, Formatting.Indented);
            File.WriteAllText(configPath, json);
        }
    }
}
