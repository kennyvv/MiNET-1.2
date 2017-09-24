using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using MiNET.Items;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Blocks
{
	public class RedstoneWire : RedstoneComponent
	{
		public RedstoneWire() : base(55)
		{
			IsTransparent = true;
			Metadata = 0;
		}

		public override Item[] GetDrops(Item tool)
		{
			return new[] {ItemFactory.GetItem(331)};
		}

		public override byte Power
		{
			get { return Metadata; }
			set { Metadata = value; }
		}

		protected override bool CanPlace(Level world, BlockCoordinates blockCoordinates, BlockCoordinates targetCoordinates, BlockFace face)
		{
			//world.BroadcastMessage("Trying place redstone...");

			var newCoordinates = GetNewCoordinatesFromFace(targetCoordinates, face);
			var currentBlock = world.GetBlock(newCoordinates);
			if (!currentBlock.IsReplacible || currentBlock.IsSolid) return false;

			//world.BroadcastMessage("Checking block down");

			var blockDown = newCoordinates + BlockCoordinates.Down;
			var b = world.GetBlock(blockDown);
			if (b.IsTransparent || !b.IsSolid) return false;

//			world.BroadcastMessage("Can place!");
			return true;
		}


		public override void OnTick(Level world, bool isRandom)
		{
			//world.BroadcastMessage("OnTick: " + Coordinates + "\n----");
			if (RedstoneEnabled)
				DoLogic(world, BlockCoordinates.Zero);
		}

		public override void BlockUpdate(Level world, BlockCoordinates blockCoordinates)
		{
			//world.BroadcastMessage("Update: " + Coordinates + " Source: " + blockCoordinates + "\n----");
			if (RedstoneEnabled)
				DoLogic(world, blockCoordinates);
		}

		private void DoLogic(Level world, BlockCoordinates except)
		{
			if (!RedstoneEnabled) return;

			var oldPower = Power;

			var neighbor = GetPoweredNeighbor(world, Coordinates);
			if (neighbor == null)
			{
				Metadata = 0;
				//world.SetBlock(this);
				world.SetData(Coordinates, Metadata);
			}
			else if (neighbor != null)
			{
				if (neighbor.Power < Power)
				{
					Metadata = 0;
					/*if (base.IsPowered(world)) //Direct power.
					{
						Metadata = 15;
					}*/
					//world.SetBlock(this);
					world.SetData(Coordinates, Metadata);
				}
				else if (neighbor.Power > Power)
				{
					Metadata = (byte) (neighbor.Power - 1);
					//world.SetBlock(this);
					world.SetData(Coordinates, Metadata);
				}
				else if (neighbor.Power == Power)
				{
					Metadata = 0;

					world.SetData(Coordinates, Metadata);
					//world.SetBlock(this);
					return;
				}
			}


			//if (Power != oldPower)
			{
				foreach (var b in GetNeighbors(world, Coordinates))
				{
					if (b.Coordinates == except || b.Power > oldPower) continue;
					b.BlockUpdate(world, Coordinates);
					//world.ScheduleBlockTick(b, 0);
				}
			}
		}
	}
}
