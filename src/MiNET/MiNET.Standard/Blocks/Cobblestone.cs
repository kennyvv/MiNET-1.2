using MiNET.Items;

namespace MiNET.Blocks
{
	public class Cobblestone : Block
	{
		public Cobblestone() : base(4)
		{
			BlastResistance = 30;
			Hardness = 2;
		}

		public override Item[] GetDrops(Item tool)
		{
			if (tool != null)
			{
				if (tool.ItemMaterial >= ItemMaterial.Wood && tool.ItemType == ItemType.PickAxe)
				{
					return base.GetDrops(tool);
				}

				return new Item[0];
			}

			return base.GetDrops(null);
		}

		public override Item GetSmelt()
		{
			return ItemFactory.GetItem(1, 0);
		}
	}
}