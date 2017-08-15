using System;
using MiNET;
using Newtonsoft.Json.Linq;
using MiNET.UI.Elements;
using System.Collections.Generic;

namespace MiNET.UI.Forms
{
	public class ModalForm : IForm
	{
		public string Title { get; set; }
		public string Content { get; set; }
		public List<Button> Buttons { get; set; }
		public List<IElement> Elements { get; set; }

		public ModalForm(string title, string content = "")
		{
			Title = title;
			Buttons = new List<Button>(2);
			Elements = new List<IElement>();
		}

		public JArray GetElements()
		{
			var j = new JArray();
			foreach (var element in Elements)
				j.Add(element.GetData());
			return j;
		}

		public string GetData()
		{
			var j = new JObject
			{
				{ "type", "modal" },
				{ "title", Title },
				{ "content", GetElements().ToString() },
				{ "buttons", GetButtons() }
			};
			return j.ToString(Newtonsoft.Json.Formatting.None);
		}

		public JArray GetButtons()
		{
			var j = new JArray();
			foreach (var button in Buttons)
			{
				j.Add(button);
			}
			return j;
		}

		public void Process(Player player, JArray response)
		{

		}

		public virtual void OnClose(Player player)
		{
			
		}

		public virtual void OnShow(Player player)
		{
			
		}
	}
}
