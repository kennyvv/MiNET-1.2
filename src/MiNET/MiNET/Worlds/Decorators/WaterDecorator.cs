using System;
using MiNET.Worlds.Generators.Survival;

namespace MiNET.Worlds.Decorators
{
	public class WaterDecorator : ChunkDecorator
	{
		protected override void InitSeed(int seed)
		{
			
		}

		public override void Decorate(ChunkColumn column, Biome biome, float[] thresholdMap, int x, int y, int z, bool surface, bool isBelowMaxHeight)
		{
			if (y > OverworldGenerator.WaterLevel && y > 32) return;

			//var density = thresholdMap[x + 16*(y + 256*z)];
			/*var bid = column.GetBlock(x, y, z);
			if (bid == 0 /* || density < 0) //If this block is supposed to be air.
			{
				if (surface && biome.Temperature <= 0f)
				{
					column.SetBlock(x, y, z, 79);
				}
				else
				{
					column.SetBlock(x, y, z, 8);
				}
			}
			else if (surface)
			{
				column.SetBlock(x,y,z, 12);
			}*/

		//	if (y >= SurvivalWorldProvider.WaterLevel) return;

			var block = column.GetBlock(x, y, z);
			if (surface)
			{
				column.SetBlock(x, y, z, 12); //Sand
			}
			else if (y <= OverworldGenerator.WaterLevel && block == 0)
			{
				if (biome.Temperature <= 0f && y == OverworldGenerator.WaterLevel - 1)
				{
					column.SetBlock(x, y, z, 79); //Ice
				}
				else
				{
					column.SetBlock(x, y, z, 8); //Water
				}
			}

		}
	}
}
