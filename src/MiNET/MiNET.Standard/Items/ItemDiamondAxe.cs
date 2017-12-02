namespace MiNET.Items
{
	public class ItemDiamondAxe : ItemTool
	{
		public ItemDiamondAxe() : base(279)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Diamond;
			ItemType = ItemType.Axe;
		}
	}
}