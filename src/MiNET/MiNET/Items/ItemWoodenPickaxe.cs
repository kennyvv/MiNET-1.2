﻿namespace MiNET.Items
{
	public class ItemWoodenPickaxe : ItemTool
	{
		public ItemWoodenPickaxe() : base(270)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Wood;
			ItemType = ItemType.PickAxe;
			FuelEfficiency = 10;
		}
	}
}