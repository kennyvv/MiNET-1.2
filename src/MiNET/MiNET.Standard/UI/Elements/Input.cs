using System;
using MiNET;
using Newtonsoft.Json.Linq;

namespace MiNET.UI.Elements
{
	public class Input : IElement
	{
		public string Text { get; set; }
		public string Title { get; set; }
		public string Placeholder { get; set; }

		public Input(string placeholder = "", string title = "", string text = "")
		{
			Text = text;
			Title = title;
			Placeholder = placeholder;
		}

		public JObject GetData()
		{
			var j = new JObject
			{
				{ "type", "input" },
				{ "text", Title },
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
