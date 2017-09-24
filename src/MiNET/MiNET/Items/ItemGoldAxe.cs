namespace MiNET.Items
{
	public class ItemGoldAxe : ItemTool
	{
		public ItemGoldAxe() : base(286)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Gold;
			ItemType = ItemType.Axe;
		}
	}
}