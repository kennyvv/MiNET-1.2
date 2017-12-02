namespace MiNET.Items
{
	public class ItemIronPickaxe : ItemTool
	{
		public ItemIronPickaxe() : base(257)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Iron;
			ItemType = ItemType.PickAxe;
		}
	}
}