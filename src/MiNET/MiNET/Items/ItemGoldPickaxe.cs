namespace MiNET.Items
{
	public class ItemGoldPickaxe : ItemTool
	{
		public ItemGoldPickaxe() : base(285)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Gold;
			ItemType = ItemType.PickAxe;
		}
	}
}