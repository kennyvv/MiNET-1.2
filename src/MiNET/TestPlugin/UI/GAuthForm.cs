using System;
using MiNET;
using MiNET.UI.Elements;
using MiNET.UI.Forms;
using MiNET.Utils;

namespace TestPlugin
{
	public class GAuthForm : CustomForm
	{
		private Label MOTD { get; }
		private Input TwoFactorAuth { get; }
		private Dropdown Drop { get; }
		public GAuthForm() : base("Two Factor Authentication")
		{
			MOTD = new Label("");
			TwoFactorAuth = new Input("123 456");

			//Drop = new Dropdown("Mode");
		//	Drop.AddOption("Casual");
		//	Drop.AddOption("Hard");
		//	Drop.AddOption("Extreme");

			//Buttons.Add(new Button("Test image", "https://i.vimeocdn.com/portrait/58832_300x300"));
			Elements.Add(MOTD);
			Elements.Add(TwoFactorAuth);
		//	Elements.Add(Drop);
		//	Elements.Add(new Toggle("Remember session"));
			//Elements.Add(new Slider("Minutes", 5f, 60f, 1f, 30f));
		}

		public override void OnShow(Player player)
		{
			MOTD.Text =
				$"{ChatColors.White}Welcome {ChatColors.Blue}{player.Username}{ChatColors.White}!\nPlease authenticate!";
		}

		public override void OnClose(Player player)
		{
			player.Disconnect($"{ChatColors.Red}Authentication failed!");
		}

		public override void OnSubmit(Player player)
		{
			if (TwoFactorAuth.Text == "123456")
			{
				player.SendMessage($"{ChatColors.Yellow}Welcome to the server!", MessageType.Raw);
				//player.SendMessage($"{ChatColors.Yellow}Selected mode: " + Drop.SelectedValue, MessageType.Raw);
			}
			else
			{
				OnClose(player);
			}
		}
	}
}
