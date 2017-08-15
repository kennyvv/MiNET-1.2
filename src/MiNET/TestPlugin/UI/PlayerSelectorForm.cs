using System.Linq;
using MiNET;
using MiNET.UI.Elements;
using MiNET.UI.Forms;
using MiNET.Utils;

namespace TestPlugin.UI
{
	public class PlayerSelectorForm : CustomForm
	{
		private Dropdown PlayerDropDown { get; }
		private Player[] Players { get; }
		public PlayerSelectorForm(Player[] players) : base("Player Manager")
		{
			PlayerDropDown = new Dropdown("Player");
			Players = players;

			for (var index = 0; index < Players.Length; index++)
			{
				var player = Players[index];
				PlayerDropDown.AddOption(player.Username);
			}

			AddElement(PlayerDropDown);
		}

		public override void OnShow(Player player)
		{
			
		}

		public override void OnClose(Player player)
		{
			
		}

		public override void OnSubmit(Player player)
		{
			if (PlayerDropDown.SelectedIndex >= Players.Length)
			{
				player.SendMessage($"{ChatColors.Red}Player not found!", MessageType.Raw);
				return;
			}

			Player target = Players[PlayerDropDown.SelectedIndex];
			PlayerPropertiesForm propertiesForm = new PlayerPropertiesForm(target);
			player.OpenForm(propertiesForm);
		}
	}
}
