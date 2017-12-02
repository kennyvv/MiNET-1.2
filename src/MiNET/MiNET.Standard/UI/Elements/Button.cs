using System;
using MiNET;
using Newtonsoft.Json.Linq;

namespace MiNET.UI.Elements
{
	public delegate void OnButtonSubmit(Player player);
	public class Button : IElement
	{
		public string Text { get; set; }
		public string Image { get; set; }

		private OnButtonSubmit OnSubmit { get; }
		public Button(string text = "", string image = null, OnButtonSubmit onSubmit = null)
		{
			Text = text;
			Image = image;

			OnSubmit = onSubmit;
		}
		public JObject GetData()
		{
			var j = new JObject
			{
				{
					"text", Text
				},
				{
					"type", ""
				}
			};
			if (Image != null) j.Add("image", new JObject
			{
				{ "type", "url" },
				{ "data", Image }
			});
			return j;
		}

		public virtual void Process(Player player, object value)
		{
			OnSubmit?.Invoke(player);
		}
	}
}
