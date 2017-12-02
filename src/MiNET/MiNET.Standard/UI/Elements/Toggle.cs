using MiNET;
using Newtonsoft.Json.Linq;

namespace MiNET.UI.Elements
{
	public class Toggle : IElement
	{
		public string Text { get; set; }
		public bool Value { get; set; }

		public Toggle(string text = "", bool defaultValue = false)
		{
			Text = text;
			Value = defaultValue;
		}
		public JObject GetData()
		{
			var j = new JObject
			{
				{ "type", "toggle" },
				{ "text", Text },
				{ "default", Value }
			};
			return j;
		}

		public void Process(Player player, object value)
		{
			Value = value.ToString()[0] == 'T';
		}
	}
}
