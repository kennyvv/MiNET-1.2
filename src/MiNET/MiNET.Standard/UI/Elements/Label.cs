using MiNET;
using Newtonsoft.Json.Linq;

namespace MiNET.UI.Elements
{
	public class Label : IElement
	{
		public string Text { get; set; }

		public Label(string text = "")
		{
			Text = text;
		}

		public JObject GetData()
		{
			var j = new JObject
			{
				{ "type", "label" },
				{ "text", Text }
			};
			return j;
		}

		public virtual void Process(Player player, object value)
		{
			// Label has no input so no need to process it
			// Anyway if you want... it's virtual, so override it :D
		}
	}
}
