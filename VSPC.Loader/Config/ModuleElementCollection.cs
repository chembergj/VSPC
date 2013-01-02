using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Configuration;

namespace VSPC.Loader.Config
{
    [ConfigurationCollection(typeof(ModuleElement),
    CollectionType = ConfigurationElementCollectionType.BasicMap, AddItemName="module")]
    public class ModuleElementCollection: ConfigurationElementCollection
    {
        public ModuleElement this[int index]
        {
            get
            {
                return base.BaseGet(index) as ModuleElement;
            }
            set
            {
                if (base.BaseGet(index) != null)
                {
                    base.BaseRemoveAt(index);
                }
                this.BaseAdd(index, value);
            }
        }

        protected override ConfigurationElement CreateNewElement()
        {
            return new ModuleElement();
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((ModuleElement)element).TypeName;
        }

        protected override string ElementName
        {
            get { return "module"; }
        }
    }
}
