using Newtonsoft.Json.Linq;

namespace MiNET.UI.Forms
{
	public interface IForm
	{
		string Title { get; set; }
		string GetData();
		void Process(Player player, string response);
		void OnClose(Player player);
		void OnShow(Player player);
	}
}
