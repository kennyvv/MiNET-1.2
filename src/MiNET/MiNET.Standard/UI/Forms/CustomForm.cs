using System;
using Newtonsoft.Json.Linq;
using MiNET.UI.Elements;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace MiNET.UI.Forms
{
	public class CustomForm : IForm
	{
		public List<IElement> Elements { get; set; }
		public string Title { get; set; }

		public CustomForm(string title)
		{
			Title = title;
			Elements = new List<IElement>();
		}

		public void AddElement(IElement element)
		{
			//if(element is Button)
			//{
			//	throw new UiException("Button can't be added to CustomForm!");
			//}
			Elements.Add(element);
		}

		public JArray GetElements()
		{
			return new JArray(Elements.Where(x => !(x is Button)).Select(x => x.GetData()));
		}

		public string GetData()
		{
			var j = new JObject
			{
				{ "type", "custom_form" },
				{ "title", Title },
				{ "content", GetElements() }
		};
			return j.ToString(Newtonsoft.Json.Formatting.None);
		}

		public void Process(Player player, string r)
		{
			var response = JArray.Parse(r);
			for (var i = 0; i < response.Count; i++)
			{
				Elements[i].Process(player, response[i]);
			}

			OnSubmit(player);
		}

		public virtual void OnShow(Player player)
		{
			
		}

		public virtual void OnClose(Player player)
		{
			
		}

		public virtual void OnSubmit(Player player)
		{
			
		}
	}
}
