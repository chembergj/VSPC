using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace VSPC.Loader.Config
{
    public class ModuleElement : ConfigurationElement
    {
        [ConfigurationProperty("type", IsRequired=true)]
        public string TypeName { get { return this["type"] as string; } }
    }
}
