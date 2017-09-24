using System;
using System.Numerics;
using MiNET.BlockEntities;
using MiNET.Blocks;
using MiNET.Entities;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET.Items
{
	public class ItemTool : Item
	{
		protected internal ItemTool(short id, short metadata = 0, int count = 1) : base(id, metadata, count)
		{
		}

		private static int GetDurability(ItemType itemType, ItemMaterial material)
		{
			int durability = 0;
			switch (material)
			{
				case ItemMaterial.Leather:
					break;
				case ItemMaterial.Chain:
					break;
				case ItemMaterial.None:
					break;

				case ItemMaterial.Wood:
					durability = 60;
					break;
				case ItemMaterial.Stone:
					durability = 132;
					break;
				case ItemMaterial.Gold:
					durability = 33;
					break;
				case ItemMaterial.Iron:
					durability = 351;
					break;
				case ItemMaterial.Diamond:
					durability = 1562;
					break;
			}

			return durability;
		}

		private void UseItem(Player player, bool useOnEntity, bool foreceUpdate = true)
		{
			short updatedMetadata = Metadata;
			int maxDurability = GetDurability(ItemType, ItemMaterial);
			bool sendUpdate = false;

			switch (ItemType)
			{
				case ItemType.Sword:
					if (useOnEntity)
					{
						updatedMetadata++;
					}
					else
					{
						updatedMetadata += 2;
					}
					break;
				case ItemType.Shovel:
				case ItemType.Axe:
				case ItemType.PickAxe:
					if (useOnEntity)
					{
						updatedMetadata += 2;
					}
					else
					{
						updatedMetadata++;
					}
					break;

				case ItemType.Hoe:
					updatedMetadata++;
					break;
				case ItemType.Shears:

					break;
				case ItemType.Item:

					break;
				case ItemType.Helmet:
					break;
				case ItemType.Chestplate:
					break;
				case ItemType.Leggings:
					break;
				case ItemType.Boots:
					break;
			}

			if (updatedMetadata != Metadata)
			{
				if (updatedMetadata >= maxDurability)
				{
					player.Inventory.SetInventorySlot(player.Inventory.InHandSlot, new ItemAir());
					return;
				}

				Metadata = updatedMetadata;
			}

			if (foreceUpdate || sendUpdate)
			{
				player.Inventory.SetInventorySlot(player.Inventory.InHandSlot, this);
			}
		}

		public void UseItem(Level world, Entity target, Entity source)
		{
			if (source is Player player)
			{
				UseItem(player, true, false);
			}
		}

		public override void UseItem(Level world, Player player, BlockCoordinates blockCoordinates, BlockFace face,
			Vector3 faceCoords)
		{
			UseItem(player, false, false);
		}

		public override bool BreakBlock(Level world, Player player, Block block, BlockEntity blockEntity)
		{
			if (player.GameMode == GameMode.Creative) return true;

			short updatedMetadata = Metadata;
			int maxDurability = GetDurability(ItemType, ItemMaterial);
			bool instantBreak = block.GetBreakTime(this) <= 0.05 || block.Hardness == 0F;
			bool forceUpdate = false;

			/*if ((block.Tool & ItemType.Any) != ItemType.Any)
			{
				
			}*/

			switch (ItemType)
			{
				case ItemType.Sword:
					if (!instantBreak)
					{
						updatedMetadata += 2;
					}
					break;
				case ItemType.Axe:
				case ItemType.Shovel:
				case ItemType.PickAxe:
					//bool canHarvest = block.GetDrops(this)?.Length > 0;
					if (!instantBreak 
						&& (((block.Tool & ItemType) != 0 && (block.ToolMaterial & ItemMaterial) != 0) 
						|| (block.Tool.HasFlag(ItemType) && block.ToolMaterial.HasFlag(ItemMaterial))))
					{
						updatedMetadata++; //Use tool.
					}
					else if (!instantBreak) //Tool not supported.
					{
						return false;
					}
					break;

				case ItemType.Hoe:
					//No use at all :O
					break;
				case ItemType.Shears:

					break;
				case ItemType.Item:

					break;
				case ItemType.Helmet:
					break;
				case ItemType.Chestplate:
					break;
				case ItemType.Leggings:
					break;
				case ItemType.Boots:
					break;
			}

			if (updatedMetadata != Metadata)
			{
				if (updatedMetadata >= maxDurability)
				{
					player.Inventory.SetInventorySlot(player.Inventory.InHandSlot, new ItemAir());
					return true;
				}

				Metadata = updatedMetadata;
			}

			if (forceUpdate)
			{
				player.Inventory.SetInventorySlot(player.Inventory.InHandSlot, this);
			}

			return true;
		}
	}
}
