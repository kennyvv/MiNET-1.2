namespace MiNET.Items
{
	public class ItemStonePickaxe : ItemTool
	{
		public ItemStonePickaxe() : base(274)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Stone;
			ItemType = ItemType.PickAxe;
		}
	}
}