using MiNET;
using Newtonsoft.Json.Linq;

namespace MiNET.UI.Elements
{
	public interface IElement
	{
		string Text { get; set; }
		JObject GetData();
		void Process(Player player, object value);
	}
}
