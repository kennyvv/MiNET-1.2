using System;
using System.Reflection;
using MiNET;
using MiNET.Net;
using MiNET.UI.Elements;
using MiNET.UI.Forms;
using MiNET.Utils;
using MiNET.Worlds;

namespace TestPlugin.UI
{
	public class PlayerPropertiesForm : CustomForm
	{
		private Input Displayname { get; }
		private Dropdown PermissionLevelDropdown { get; }
		private Dropdown DifficultyDropdown { get; }
		private Dropdown GamemodeDropdown { get; }
		private Toggle MuteToggle { get; }
		private Player Target { get; }
		public PlayerPropertiesForm(Player player) : base($"{player.Username}'s Properties")
		{
			Target = player;

			Displayname = new Input(player.Username, "Displayname");

			GamemodeDropdown = new PlayerEnumDropdown<GameMode>(player, "GameMode", (int)player.GameMode, SetAsChanged);
			DifficultyDropdown = new PlayerEnumDropdown<Actionpermissions>(player, "Permissions", (int)player.ActionPermissions, SetAsChanged);
			PermissionLevelDropdown = new PlayerEnumDropdown<UserPermission>(player, "PermissionLevel", (int)player.PermissionLevel, SetAsChanged);

			MuteToggle = new Toggle("Mute", player.IsMuted);

			AddElement(Displayname);

			AddElement(GamemodeDropdown);
			AddElement(DifficultyDropdown);
			AddElement(PermissionLevelDropdown);

			AddElement(MuteToggle);
		}

		private bool Changed { get; set; } = false;
		private void SetAsChanged()
		{
			Changed = true;
		}

		public override void OnSubmit(Player player)
		{
			//bool changed = false;
		/*	if (GamemodeDropdown.SelectedIndex != (int) Target.GameMode)
			{
				Target.SetGameMode((GameMode) GamemodeDropdown.SelectedIndex);
				Changed = true;
			}*/

			if (MuteToggle.Value != Target.IsMuted)
			{
				Target.IsMuted = MuteToggle.Value;
				Changed = true;
			}

			if (Changed)
			{
				player.SendMessage($"{Target.Username}'s properties were changed!");
				player.SendAdventureSettings();
				player.SendSetDificulty();
			}
		}
	}

	internal class PlayerEnumDropdown<TEnumType> : Dropdown where TEnumType : struct
	{
		private Action ChangedAction { get; }
		private Player Target { get; }
		private PropertyInfo PropInfo { get; }
		public PlayerEnumDropdown(Player player, string property, int selectedIndex = 0, Action setChanged= null) : base(property, selectedIndex)
		{
			Target = player;
			ChangedAction = setChanged;

			Type type = player.GetType();
			PropInfo = type.GetProperty(property);
			if (PropInfo == null || !PropInfo.PropertyType.IsEnum) throw new Exception("Invalid property!");

			string[] propertieValues = Enum.GetNames(PropInfo.PropertyType);
			for (int i = 0; i < propertieValues.Length; i++)
			{
				AddOption(propertieValues[i]);
			}
		}

		public override void Process(Player player, object value)
		{
			int previous = SelectedIndex;
			base.Process(player, value);
			if (SelectedIndex != previous)
			{
				PropInfo.SetValue(Target, Enum.ToObject(typeof (TEnumType), (int)SelectedIndex), null);
				ChangedAction?.Invoke();
			}
		}
	}
}
