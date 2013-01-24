using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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

		private static ObservableCollection<AIModelRule> rules;

		public static ObservableCollection<AIModelRule> AllRules
		{
			get { return rules ?? (rules = LoadRules()); }
		}

		private static ObservableCollection<AIModelRule> LoadRules()
		{
			try
			{
				var xmlSerializer = new XmlSerializer(typeof (AIModelRuleCollection));
				return
					((AIModelRuleCollection) xmlSerializer.Deserialize(XmlReader.Create(filename))).Rules;
			}
			catch (FileNotFoundException)
			{
				return new ObservableCollection<AIModelRule>();
			}
		}

		public static void Delete(AIModelRule r)
		{
			AllRules.Remove(r);
		}

		public static void SaveRules()
		{
			if (rules != null)
			{
				var xmlSerializer = new XmlSerializer(typeof(AIModelRuleCollection));
				xmlSerializer.Serialize(XmlWriter.Create(filename), new AIModelRuleCollection() { Rules = rules });
			}
		}

		public static AIModelRule FindNext(int from, string search)
		{
			return
				AllRules.Skip(from).FirstOrDefault(
					rule => rule.Airline.Contains(search) || rule.PlaneType.Contains(search) || rule.Model.Contains(search));
		}
	}

	[Serializable]
	public class AIModelRuleCollection
	{
		public ObservableCollection<AIModelRule> Rules { get; set; }
	}
}
