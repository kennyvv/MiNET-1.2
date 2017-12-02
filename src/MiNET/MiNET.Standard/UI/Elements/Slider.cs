using MiNET;
using Newtonsoft.Json.Linq;

namespace MiNET.UI.Elements
{
	public class Slider : IElement
	{
		public string Text { get; set; }
		public float Min { get; set; }
		public float Max { get; set; }
		public float Step { get; set; }
		public float Value { get; set; }

		public Slider(string text, float min = 0, float max = 100, float step = 0, float defaultValue = 0)
		{
			Text = text;
			Value = defaultValue;
			Min = min;
			Max = max;
			Step = step;
		}
		public JObject GetData()
		{
			var j = new JObject
			{
				{ "type", "slider" },
				{ "text", Text },
				{ "min", Min },
				{ "max", Max },
				{ "default", Value }
			};
			if(Step != 0)
			{
				j.Add("step", Step);
			}
			return j;
		}

		public void Process(Player player, object value)
		{
			
		}
	}
}
