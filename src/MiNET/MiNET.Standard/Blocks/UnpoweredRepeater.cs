using System;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Blocks
{
	public class UnpoweredRepeater : RedstoneComponent
	{
		public UnpoweredRepeater() : this(93)
		{
		}

		public UnpoweredRepeater(byte id) : base(id)
		{
			IsTransparent = true;
		}

		public override void BlockUpdate(Level level, BlockCoordinates blockCoordinates)
		{
			if (!RedstoneEnabled) return;

			int rotation = Metadata << 2;
			var target = Coordinates;
			switch (rotation)
			{
				case 0: //Facing north
					if (blockCoordinates != Coordinates + BlockCoordinates.South)
					{
						return;
					}
					target = Coordinates + BlockCoordinates.North;
					break;
				case 1: //Facing east
					if (blockCoordinates != Coordinates + BlockCoordinates.West)
					{
						return;
					}
					target = Coordinates + BlockCoordinates.East;
					break;
				case 2: //Facing south
					if (blockCoordinates != Coordinates + BlockCoordinates.North)
					{
						return;
					}
					target = Coordinates + BlockCoordinates.South;
					break;
				case 3: //Facing west
					if (blockCoordinates != Coordinates + BlockCoordinates.East)
					{
						return;
					}
					target = Coordinates + BlockCoordinates.West;
					break;
			}

			Block sourceBlock = level.GetBlock(blockCoordinates);
			if (sourceBlock is PoweredRepeater pr)
			{
				//Set to blocking repeater mode.
			}
			else if (sourceBlock is UnpoweredRepeater ur)
			{
				//Set to normal repeater mode.
			}
			else if (sourceBlock is RedstoneComponent red)
			{
				if (this is PoweredRepeater)
				{
					if (red.Power == 0)
					{
						level.SetBlock(new UnpoweredRepeater()
						{
							Coordinates = Coordinates
						});
					}
				}
				else
				{
					if (red.Power > 0)
					{
						level.SetBlock(new PoweredRepeater()
						{
							Coordinates = Coordinates
						});
					}
				}
			}

			level.ScheduleBlockTick(level.GetBlock(target), 2);
		}
	}
}