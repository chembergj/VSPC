 using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace VSPC.Common
{
	/// <summary>
	/// Class describing a rule used for AI model matching
	/// </summary>
	[Serializable]
	public class AIModelRule
	{
		public string Airline { get; set; }					// Airline code (NAX, SAS, DLH etc.)
		public string PlaneType { get; set; }				// Plane type (B738)
		public string Model { get; set; }					// Model name from aircraft.cfg

		public string RuleDescription
		{
			get
			{
				string description = "";

				if (!string.IsNullOrEmpty(Airline))
				{
					description = string.Format("When airline is {0}", Airline);
				}

				if (!string.IsNullOrEmpty(PlaneType))
				{
					if (!string.IsNullOrEmpty(description))
						description += " and ";

					description += string.Format("plane type is {0}", PlaneType);
					description = description.Substring(0, 1).ToUpper() + description.Substring(1);
				}

				if (string.IsNullOrEmpty(description))
				{
					description = "For all airlines and plane types";
				}

				description += string.Format(", model name is {0}", Model);

				return description;
			}
		}
	}
}
