using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Diagnostics;

namespace MiNET.UI.Elements
{
	public class Dropdown : IElement
	{
		public string Text { get; set; }
		private List<string> Options { get; set; }
		public int SelectedIndex { get; set; }
		public string SelectedValue => Options[SelectedIndex];

		public Dropdown(string text, int selectedIndex = 0)
		{
			Text = text;
			Options = new List<string>();
			SelectedIndex = selectedIndex;
		}

		public JObject GetData()
		{
			var j = new JObject
			{
				{ "type", "dropdown" },
				{ "text", Text },
				{ "options", GetOptions() },
				{ "default", SelectedIndex }
			};
			return j;
		}

		public JToken GetOptions()
		{
			var j = new JArray();
			foreach(var o in Options)
			{
				j.Add(o);
			}
			return j;
		}

		public void AddOption(string option)
		{
			if (!Options.Contains(option))
				Options.Add(option);
		}

		public virtual void Process(Player player, object value)
		{
			int val;
			if (int.TryParse(value.ToString(), out val))
			{
				SelectedIndex = val;
			}
			else
			{
				SelectedIndex = 0;
			}
		}
	}
}
