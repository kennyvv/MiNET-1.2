using System.Numerics;
using MiNET.Blocks;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Items
{
	public class ItemRedstone : Item
	{
		public ItemRedstone() : base(331)
		{
		}

		public override void UseItem(Level world, Player player, BlockCoordinates blockCoordinates, BlockFace face, Vector3 faceCoords)
		{
			//var newCoordinates = GetNewCoordinatesFromFace(blockCoordinates, face);
			//var currentBlock = world.GetBlock(newCoordinates);

			RedstoneWire wire = new RedstoneWire();
			if (wire.CanPlace(world, blockCoordinates, face))
			{
				if (!wire.PlaceBlock(world, player, blockCoordinates, face, faceCoords))
				{
					var newCoordinates = GetNewCoordinatesFromFace(blockCoordinates, face);
					wire.Coordinates = newCoordinates;
					world.SetBlock(wire);
				}
			}
		}
	}
}