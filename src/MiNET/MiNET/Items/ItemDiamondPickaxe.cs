namespace MiNET.Items
{
	public class ItemDiamondPickaxe : ItemTool
	{
		public ItemDiamondPickaxe() : base(278)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Diamond;
			ItemType = ItemType.PickAxe;
		}
	}
}