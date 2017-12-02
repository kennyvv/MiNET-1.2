namespace MiNET.Items
{
	public class ItemStoneAxe : ItemTool
	{
		public ItemStoneAxe() : base(275)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Stone;
			ItemType = ItemType.Axe;
		}
	}
}