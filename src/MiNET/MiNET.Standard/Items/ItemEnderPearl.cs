using MiNET.Entities.Projectiles;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Items
{
	public class ItemEnderPearl : Item
	{
		public ItemEnderPearl() : base(368)
		{
			MaxStackSize = 16;
		}

		public override void UseItem(Level world, Player player, BlockCoordinates blockCoordinates)
		{
			var slotId = player.Inventory.InHandSlot;
			var slot = player.Inventory.Slots[slotId];
			if (slot.Count - 1 == 0)
			{
				player.Inventory.SetInventorySlot(slotId, new ItemAir());
			}
			else
			{
				slot.Count--;
				player.Inventory.SetInventorySlot(slotId, slot);
			}

			const float force = 1.5f;

			EnderPearl enderPearl = new EnderPearl(player, world);
			enderPearl.KnownPosition = (PlayerLocation) player.KnownPosition.Clone();
			//snowBall.KnownPosition.Y += 1.62f;
			enderPearl.Velocity = enderPearl.KnownPosition.GetDirection()*(force);
			enderPearl.BroadcastMovement = true;
			enderPearl.DespawnOnImpact = true;
			enderPearl.SpawnEntity();
		}
	}
}
