using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace VSPC.Loader.Config
{
    public class ModuleConfiguration : ConfigurationSection
    {
        private static string sConfigurationSectionConst = "ModuleConfiguration";

        /// <summary>
        /// Returns an shiConfiguration instance
        /// </summary>
        public static ModuleConfiguration GetConfig()
        {

            return (ModuleConfiguration)System.Configuration.ConfigurationManager.
               GetSection(ModuleConfiguration.sConfigurationSectionConst) ??
               new ModuleConfiguration();

        }
        [System.Configuration.ConfigurationProperty("modules")]
        public ModuleElementCollection Modules
        {
            get
            {
                return (ModuleElementCollection)this["modules"] ??
                   new ModuleElementCollection();
            }
        }
    }
}
