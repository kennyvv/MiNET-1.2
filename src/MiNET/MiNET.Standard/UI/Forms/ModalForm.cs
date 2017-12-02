using Newtonsoft.Json.Linq;

namespace MiNET.UI.Forms
{
	public class ModalForm : IForm
	{
		public string Title { get; set; }
		public string Content { get; set; }
		public string Button1Text { get; set; } = "Yes";
		public string Button2Text { get; set; } = "No";

		public ModalForm(string title, string content = "")
		{
			Title = title;
			Content = content;
		}

		public string GetData()
		{
			var j = new JObject
			{
				{ "type", "modal" },
				{ "title", Title },
				{ "content", Content},
				{ "button1", Button1Text },
				{ "button2", Button2Text }
			};
			return j.ToString(Newtonsoft.Json.Formatting.None);
		}

		private bool Processed = false;
		public void Process(Player player, string response)
		{
			Processed = true;
			if (bool.TryParse(response, out bool result))
			{
				if (result)
				{
					OnConfirm(player);
				}
				else
				{
					OnDeny(player);
				}
			}
			else
			{
				OnDeny(player);
			}
		}

		protected virtual void OnConfirm(Player player)
		{
			
		}

		protected virtual void OnDeny(Player player)
		{
			
		}

		public virtual void OnClose(Player player)
		{
			if (!Processed)
				OnDeny(player);
		}

		public virtual void OnShow(Player player)
		{
			
		}
	}
}
