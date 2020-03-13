using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Text;
using WebUI.Models;
using Utils.Json;
using System.IO; 
using System.Text.RegularExpressions; 
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Table;
using Microsoft.Extensions.Logging;
using Common.Utils;

namespace WebUI.Azure
{
    public enum CloudProviderType : int { 
        Azure=0, 
        GCE=1, 
        AWS=2 
    };

    public class CloudProvider {
        public const String All = "*";
        public static String Azure = null;
        public static String GCE = null;
        public static String AWS = null;
        public static String LOCAL = null;
        public const String Any = null;
        public static String Default = null;
        public String Name = null;

        public virtual String[] GetURLs( string storage, string path, string location )
        {
            var typeName = this.GetType().FullName;
            throw new Exception($"GetURLs hasn't been implemented for provider {typeName}");
        }

        public virtual BlobContainer GetContainer( string storage, string path, string location ) {
            var typeName = this.GetType().FullName;
            throw new Exception($"GetContainer hasn't been implemented for provider {typeName}"); 
        }

        // Table, currently only support Azure 
        public virtual CloudTable GetTable( string storage, string path, string location) {
            var typeName = this.GetType().FullName;
            throw new Exception($"GetTable hasn't been implemented for provider {typeName}"); 
        }



        public virtual bool Ready()
        {
            return false; 
        }

        public static String urlCombine( String path1, String path2 )
        {
            if (String.IsNullOrEmpty(path2))
            {
                return path1; 
            } else if (String.IsNullOrEmpty(path1))
            {
                return path2;
            } else
            {
                if ( path2[0] == '/' )
                {
                    if ( path1[path1.Length-1] == '/')
                    {
                        return path1 + path2.Substring(1);
                    } else
                    {
                        return path1 + path2; 
                    }
                } else
                {
                    if (path1[path1.Length - 1] == '/')
                    {
                        return path1 + path2; 
                    } else
                    {
                        return path1 + "/" + path2; 
                    }
                }
            }
        }

        public static String urlCombine(String path1, String path2, String path3)
        {
            return urlCombine(urlCombine(path1, path2), path3); 
        }

        public static String urlCombine(String path1, String path2, String path3, String path4)
        {
            return urlCombine(urlCombine(path1, path2, path3), path4);
        }

    }

    public class CloudProviderSetting
    {
        public static CloudProviderSetting Current = new CloudProviderSetting(); 
        public Dictionary<String, CloudProvider> providers = null; 

        /// The name of the provider is part of filename. 
        /// Certain word, e.g., config, is removed from the file.
        /// If there is "_", only the 1st part before "_" is used as the provider name 
        private String GetProviderName( String filename )
        {
            var fname1 = Path.GetFileNameWithoutExtension(filename);
            // var fname1 = Regex.Replace(filename, ".json", "", RegexOptions.IgnoreCase);
            var fname2 = Regex.Replace(fname1, "config", "", RegexOptions.IgnoreCase);
            var fname3 = fname2.Split(new string[]{"_"}, StringSplitOptions.RemoveEmptyEntries);
            return fname3[0].Trim();
        }

        public void Setup(ILogger logger)
        {
            var configDir = WebUIConfig.ConfigDirectory; 
            var storageDir = Path.Combine( configDir, "storage");
            var allConfigFiles = Directory.GetFiles( storageDir ); 
            providers = new Dictionary<String, CloudProvider>(StringComparer.OrdinalIgnoreCase);
            foreach ( var oneConfigFile in allConfigFiles)
            {
                if ( oneConfigFile.EndsWith( ".json", StringComparison.OrdinalIgnoreCase))
                {
                    var providerName = GetProviderName(oneConfigFile);
                    CloudProvider provider = null;
                    //logger.LogInformation($"Setup storage provider {providerName}");
                    if ( providerName.IndexOf("azure", StringComparison.OrdinalIgnoreCase) >= 0 )
                    {
                        //logger.LogInformation($"Enter azure setup for {providerName}");
                        provider = new AzureCloudProvider(oneConfigFile, logger);
                        if (!Object.ReferenceEquals(provider, null))
                        {
                            CloudProvider.Azure = providerName; 
                        }
                    }
#if MULTI_PROVIDER
                    else if ( providerName.IndexOf("google", StringComparison.OrdinalIgnoreCase) >= 0 )
                    {
                        //logger.LogInformation($"Enter google setup for {providerName}");
                        provider = new GoogleCloudProvider(oneConfigFile, logger);
                        if (!Object.ReferenceEquals(provider, null))
                        {
                            CloudProvider.GCE = providerName; 
                        }
                    } else if ( providerName.IndexOf("aws", StringComparison.OrdinalIgnoreCase) >= 0 )
                    {
                        //logger.LogInformation($"Enter aws setup for {providerName}");
                        provider = new AWSCloudProvider(oneConfigFile, logger);
                        if (!Object.ReferenceEquals(provider, null))
                        {
                            CloudProvider.AWS = providerName; 
                        }
                    }
#endif
                    else if (providerName.IndexOf("local", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        //logger.LogInformation($"Enter local setup for {providerName}");
                        provider = new NFSCloudProvider(oneConfigFile, logger);
                        if (!Object.ReferenceEquals(provider, null))
                        {
                            CloudProvider.LOCAL = providerName;
                        }
                    }
                    if ( !String.IsNullOrEmpty(providerName) 
                        && !Object.ReferenceEquals(provider,null) 
                        && provider.Ready() )
                    {
                        logger.LogInformation($"Configure {providerName} ==== {oneConfigFile}");
                        providers[providerName] = provider;
                        provider.Name = providerName;
                    }
                }
            }

            if ( !Object.ReferenceEquals(LocalSetting.Current, null))
            {
                CloudProvider.Default = LocalSetting.Current.StorageProvider;
            }
            else {
                CloudProvider.Default = null; 
            }
            if ( !String.IsNullOrEmpty(CloudProvider.Default) ) 
            {
                if ( !CloudProviderSetting.Current.providers.ContainsKey(CloudProvider.Default))
                {
                    CloudProvider.Default = null; 
                }
            }
            GetAnyProvider();
        }

        // Rule for pick a provider 
        public String GetAnyProvider()
        {
            if ( !String.IsNullOrEmpty(CloudProvider.Default))
            {
                return CloudProvider.Default;
            } else {
                if (!String.IsNullOrEmpty(CloudProvider.Azure))
                {
                    CloudProvider.Default = CloudProvider.Azure; 
                } else if (!String.IsNullOrEmpty(CloudProvider.GCE))
                {
                    CloudProvider.Default = CloudProvider.GCE; 
                } else if (!String.IsNullOrEmpty(CloudProvider.AWS))
                {
                    CloudProvider.Default = CloudProvider.AWS;
                }
                else
                {
                    CloudProvider.Default = CloudProvider.LOCAL;
                }
                if ( String.IsNullOrEmpty(CloudProvider.Default))
                {
                    throw new Exception("No cloud provider in setup.");
                }
                return CloudProvider.Default;
            }
        }

        // Parse for information string
        public static String Parse( String info)
        {
            if ( String.IsNullOrEmpty(info))
            {
                return CloudProvider.All;
            }
            if ( CloudProviderSetting.Current.providers.ContainsKey(info))
            {
                return info; 
            }
            else if (info.Equals( "all", StringComparison.OrdinalIgnoreCase))
            {
                return CloudProvider.All;
            }
            else if (info.Equals("azure", StringComparison.OrdinalIgnoreCase))
            {
                return CloudProvider.Azure;
            }
            else if (info.Equals("gce", StringComparison.OrdinalIgnoreCase))
            {
                return CloudProvider.GCE;
            }
            else if (info.Equals("aws", StringComparison.OrdinalIgnoreCase))
            {
                return CloudProvider.AWS;
            }
            else if (info.Equals("any", StringComparison.OrdinalIgnoreCase))
            {
                return CloudProvider.Any;
            }
            return info;
        }

        

        public CloudProvider GetProvider( String providerName )
        {
            String matchProvider = providerName;
            // Console.WriteLine($"Match for provider {matchProvider}");
            if ( String.IsNullOrEmpty(providerName) )
            {
                matchProvider = GetAnyProvider();
            }
            if ( String.IsNullOrEmpty(matchProvider) )
            {
                throw new Exception("Unmatched Cloud provider");
            }
            else {
                if ( matchProvider == CloudProvider.All )
                {
                    var allProviders = new List<CloudProvider>();
                    foreach( var pair in providers )
                    {
                        if ( pair.Key.IndexOf("azure", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // First add all azure provider, this is important, as many of the other provider doesn't have 
                            // eTag property. 
                            allProviders.Add(pair.Value);
                        }
                    }
                    foreach (var pair in providers)
                    {
                        if (pair.Key.IndexOf("aws", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Then add all aws provider
                            allProviders.Add(pair.Value);
                        }
                    }
                    foreach (var pair in providers)
                    {
                        if (pair.Key.IndexOf("google", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            // Finally add all provider that is not google 
                            allProviders.Add(pair.Value);
                        }
                    }
                    if ( allProviders.Count > 0 )
                    {
#if MULTI_PROVIDER
                        return new MultiCloudProvider(allProviders);
#else
                        // Use 1st Provider. 
                        return allProviders[0];
#endif
                    } else {
                        throw new Exception("0 Cloud providers are configured, match fails. ");
                    }
                } else {
                    if ( providers.TryGetValue(matchProvider, out CloudProvider provider ))
                    {
                        return provider; 
                    } else {
                        throw new Exception($"Request unmatched cloud provider {providerName}.");
                    }
                }
            }
        }

    }

    public class LocalSetting {
        public static LocalSetting Current = new LocalSetting();
        public static String LocalFQDN = "/etc/hostname-fqdn"; 
        public String Location = Constant.DefaultLocation;
        public String Cluster = "";
        public String FQDN = ""; 
        public String StorageProvider = "";
        public static void Setup(ILogger logger)
        {

            var localConfigFile = WebUIConfig.GetConfigFile(WebUIConfig.LocalConfigFile);
            var config = Json.Read(localConfigFile);
            Current.Location = JsonUtils.GetString("region", config);
            Current.Cluster = JsonUtils.GetString("cluster", config);
            Current.FQDN = JsonUtils.GetString("fqdn", config);
            if ( File.Exists(LocalSetting.LocalFQDN))
            { 
                using (var file = new StreamReader(LocalSetting.LocalFQDN))
                {
                    String line = null; 
                    while((line = file.ReadLine()) != null) {
                        if ( !String.IsNullOrEmpty(line) && !line.StartsWith('#') )
                        {
                            Current.FQDN = line;
                            break;
                        }
                    }
                }
            } else
            {
                Current.FQDN = "localhost";
            }
            Current.StorageProvider = JsonUtils.GetString(Constant.JsontagStorageProvider, config);
            CloudProviderSetting.Current.Setup(logger);
        }
    }

}
