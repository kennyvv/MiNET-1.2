using System;
using MiNET;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace MiNET.UI.Elements
{
	public class StepSlider : IElement
	{
		public string Text { get; set; }
		public List<string> Steps { get; set; }
		public int Default { get; set; }

		public StepSlider(string text, int defaultValue = 0)
		{
			Text = text;
			Default = defaultValue;
			Steps = new List<string>();
		}

		public JObject GetData()
		{
			var j = new JObject
			{
				{ "type", "step_slider" },
				{ "text", Text },
				{ "steps", GetSteps() },
				{ "default", Default }
			};
			return j;
		}

		public JArray GetSteps()
		{
			var j = new JArray();
			foreach (var o in Steps)
			{
				j.Add(o);
			}
			return j;
		}

		public void Process(Player player, object value)
		{
			
		}
	}
}
