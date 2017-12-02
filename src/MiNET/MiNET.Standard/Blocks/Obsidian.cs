using MiNET.Items;

namespace MiNET.Blocks
{
	public class Obsidian : Block
	{
		public Obsidian() : base(49)
		{
			BlastResistance = 6000;
			Hardness = 50;

			Tool = ItemType.PickAxe;
			ToolMaterial = ItemMaterial.Diamond;
		}
	}
}