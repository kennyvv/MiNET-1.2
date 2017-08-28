using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using log4net;
using MiNET.UI.Elements;

namespace MiNET.UI.Forms
{
	public class ButtonsForm : IForm
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (ButtonsForm));
		
		public string Title { get; set; }
		public string Content { get; set; }
		public List<Button> Buttons { get; set; }

		public ButtonsForm(string title, string content = "")
		{
			Title = title;
			Content = content;
			Buttons = new List<Button>();
		}

		public string GetData()
		{
			var ourButtons = GetButtons();
			var j = new JObject
			{
				{ "type", "form" },
				{ "title", Title },
				{ "content", Content },
				{ "buttons", ourButtons }
			};
		/*	if(buttons.Count > 0)
			{
				j.Add("button1", buttons[0]);
			}
			if(buttons.Count > 1)
			{
				j.Add("button2", buttons[1]);
			}*/
			return j.ToString(Newtonsoft.Json.Formatting.None);
		}

		public JArray GetButtons()
		{
			var j = new JArray();
			foreach(var button in Buttons)
			{
				j.Add(button.GetData());
			}
			return j;
		}

		public void Process(Player player, string response)
		{
			int indx;
			if (int.TryParse(response, out indx))
			{
				if (indx < Buttons.Count)
				{
					Buttons[indx].Process(player, indx);
				}
			}
			//for (var i = 0; i < response.Count; i++)
			//{
			//	Buttons[i].Process(player, response[i]);
			//}
		}

		public virtual void OnClose(Player player)
		{
			
		}

		public virtual void OnShow(Player player)
		{
			
		}
	}
}
