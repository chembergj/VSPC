using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Serialization;

namespace VSPC.Common
{
	public class AIModelRuleRepository
	{
		private const string filename = "aimodelrules.xml";

		public List<AIModelRule> GetAllRules()
		{
			try
			{
				var xmlSerializer = new XmlSerializer(typeof(List<AIModelRule>));
				var rules = (List<AIModelRule>)xmlSerializer.Deserialize(XmlReader.Create(filename));

				return rules;
			}
			catch (FileNotFoundException)
			{
				return new List<AIModelRule>();
			}
		}
	}
}
