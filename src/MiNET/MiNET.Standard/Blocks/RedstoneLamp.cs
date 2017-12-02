using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Blocks
{
	public class RedstoneLamp : RedstoneComponent
	{
		public RedstoneLamp() : this(123)
		{
			
		}

		public RedstoneLamp(byte id) : base(id)
		{
			BlastResistance = 1.5f;
			Hardness = 0.3f;
		}

		public override void BlockUpdate(Level level, BlockCoordinates blockCoordinates)
		{
			if (!RedstoneEnabled) return;

			var neighbor = GetPoweredNeighbor(level, Coordinates);
			if (this is LitRedstoneLamp)
			{
				if (neighbor == null || neighbor.Power <= 0)
				{
					level.SetBlock(new RedstoneLamp()
					{
						Coordinates = Coordinates
					});
				}
			}
			else
			{
				if (neighbor != null && neighbor.Power > 0)
				{
					level.SetBlock(new LitRedstoneLamp()
					{
						Coordinates = Coordinates
					});
				}
			}
		}
	}
}