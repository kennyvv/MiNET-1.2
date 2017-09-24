namespace MiNET.Items
{
	public class ItemDiamondSword : ItemTool
	{
		public ItemDiamondSword() : base(276)
		{
			MaxStackSize = 1;
			ItemType = ItemType.Sword;
			ItemMaterial = ItemMaterial.Diamond;
		}
	}
}