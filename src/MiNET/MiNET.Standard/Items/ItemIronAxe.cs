namespace MiNET.Items
{
	public class ItemIronAxe : ItemTool
	{
		public ItemIronAxe() : base(258)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Iron;
			ItemType = ItemType.Axe;
		}
	}
}