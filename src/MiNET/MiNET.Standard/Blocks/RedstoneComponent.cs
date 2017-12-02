using System.Collections.Generic;
using System.Numerics;
using MiNET.BlockEntities;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Blocks
{
	public class RedstoneComponent : Block
	{
		public virtual byte Power { get; set; } = 0;
		public RedstoneComponent(byte id) : base(id)
		{

		}

		public override bool PlaceBlock(Level world, Player player, BlockCoordinates targetCoordinates, BlockFace face, Vector3 faceCoords)
		{
			if (RedstoneEnabled)
			{
				Coordinates = GetNewCoordinatesFromFace(targetCoordinates, face);
				foreach (var n in GetNeighbors(world, Coordinates))
				{
					world.ScheduleBlockTick(n, 0);
					/*if (Power > 0 && n.Power < Power)
					{
						n.Power = (byte) (Power - 1);
					}
					if (n.Power == 16) //Power source
					{
						continue;
					}*/
				}
			}
			return base.PlaceBlock(world, player, targetCoordinates, face, faceCoords);
		}

		public override bool IsPowered(Level level)
		{
			if (RedstoneEnabled)
			{
				var left = Coordinates + BlockCoordinates.Left;
				var right = Coordinates + BlockCoordinates.Right;
				var forward = Coordinates + BlockCoordinates.Forwards;
				var backwards = Coordinates + BlockCoordinates.Backwards;
				var down = Coordinates + BlockCoordinates.Down;

				var bLeft = level.GetBlock(left);
				if (bLeft.IsDirectlyPowered(level)) return true;

				var bRight = level.GetBlock(right);
				if (bRight.IsDirectlyPowered(level)) return true;

				var bForward = level.GetBlock(forward);
				if (bForward.IsDirectlyPowered(level)) return true;

				var bBackward = level.GetBlock(backwards);
				if (bBackward.IsDirectlyPowered(level)) return true;

				var bDown = level.GetBlock(down);
				if (bDown.IsDirectlyPowered(level)) return true;
			}

			return false;
		}

		protected RedstoneComponent GetPoweredNeighbor(Level level, BlockCoordinates self)
		{
			var left = self + BlockCoordinates.Left;
			var right = self + BlockCoordinates.Right;
			var forward = self + BlockCoordinates.Forwards;
			var backwards = self + BlockCoordinates.Backwards;
			var down = self + BlockCoordinates.Down;

			RedstoneComponent highestPoweredBlock = null;

			var bLeft = level.GetBlock(left);
		//	level.BroadcastMessage("Left: " + bLeft);
			if (bLeft is RedstoneComponent rLeft)
			{
				if (highestPoweredBlock == null || rLeft.Power > highestPoweredBlock.Power)
				{
					highestPoweredBlock = rLeft;
				}
			}

			var bRight = level.GetBlock(right);
			//level.BroadcastMessage("Right: " + bRight);
			if (bRight is RedstoneComponent rRight)
			{
				if (highestPoweredBlock == null || rRight.Power > highestPoweredBlock.Power)
				{
					highestPoweredBlock = rRight;
				}
			}

			var bForward = level.GetBlock(forward);
		//	level.BroadcastMessage("Forward: " + bForward);
			if (bForward is RedstoneComponent rForward)
			{
				if (highestPoweredBlock == null || rForward.Power > highestPoweredBlock.Power)
				{
					highestPoweredBlock = rForward;
				}
			}

			var bBackward = level.GetBlock(backwards);
		//	level.BroadcastMessage("Backward: " + bBackward);
			if (bBackward is RedstoneComponent rBackward)
			{
				if (highestPoweredBlock == null || rBackward.Power > highestPoweredBlock.Power)
				{
					highestPoweredBlock = rBackward;
				}
			}

			var bDown = level.GetBlock(down);
			//	level.BroadcastMessage("Down: " + bDown);
			if (bDown is RedstoneComponent rDown)
			{
				if (rDown.Power == 16) //PowerSource
				{
					highestPoweredBlock = rDown;
				}
			}

			return highestPoweredBlock;
		}

		protected IEnumerable<RedstoneComponent> GetNeighbors(Level level, BlockCoordinates self)
		{
			var left = self + BlockCoordinates.Left;
			var right = self + BlockCoordinates.Right;
			var forward = self + BlockCoordinates.Forwards;
			var backwards = self + BlockCoordinates.Backwards;

			var bLeft = level.GetBlock(left);
			//	level.BroadcastMessage("Left: " + bLeft);
			if (bLeft is RedstoneComponent rLeft)
			{
				yield return rLeft;
			}

			var bRight = level.GetBlock(right);
			//level.BroadcastMessage("Right: " + bRight);
			if (bRight is RedstoneComponent rRight)
			{
				yield return rRight;
			}

			var bForward = level.GetBlock(forward);
			//	level.BroadcastMessage("Forward: " + bForward);
			if (bForward is RedstoneComponent rForward)
			{
				yield return rForward;
			}

			var bBackward = level.GetBlock(backwards);
			//	level.BroadcastMessage("Backward: " + bBackward);
			if (bBackward is RedstoneComponent rBackward)
			{
				yield return rBackward;
			}

		}

		//private RedstoneComponent GetLeft()
	}
}
