using System;
using MiNET;
using Newtonsoft.Json.Linq;

namespace MiNET.UI.Elements
{
	public class Input : IElement
	{
		public string Text { get; set; }
		public string Placeholder { get; set; }

		public Input(string placeholder = "", string text = "")
		{
			Text = text;
			Placeholder = placeholder;
		}

		public JObject GetData()
		{
			var j = new JObject
			{
				{ "type", "input" },
				{ "text", Text },
				{ "placeholder", Placeholder },
				{ "default", Text }
			};
			return j;
		}

		public void Process(Player player, object value)
		{
			if (value == null)
			{
				return;
			}

			Text = value.ToString();
		}
	}
}
