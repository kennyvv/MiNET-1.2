namespace MiNET.Items
{
	public class ItemIronSword : ItemTool
	{
		public ItemIronSword() : base(267)
		{
			MaxStackSize = 1;
			ItemMaterial = ItemMaterial.Iron;
			ItemType = ItemType.Sword;
		}
	}
}