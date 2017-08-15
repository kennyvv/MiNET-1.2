using System.Collections.Generic;
using System.Linq;
using log4net;
using MiNET.Items;
using MiNET.Net;
using MiNET.Utils;
using MiNET.Worlds;

namespace MiNET
{
	public class PlayerInventory
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (PlayerInventory));

		public const int HotbarSize = 9;
		public const int InventorySize = HotbarSize + 36;
		public Player Player { get; private set; }

		public Item[] Slots { get; private set; }
		//public int[] ItemHotbar { get; private set; }
		public int InHandSlot { get; set; } = 36;
		public int Cursor { get; set; } = -1;
		public Item CursorItem { get; set; } 
		public int SelectedHotbarSlot { get; private set; }

		// Armour
		public Item Boots { get; set; }
		public Item Leggings { get; set; }
		public Item Chest { get; set; }
		public Item Helmet { get; set; }

		public PlayerInventory(Player player)
		{
			Player = player;

			int idx = 0;
			Slots = Enumerable.Repeat((Item) new ItemAir(), InventorySize).ToArray();
			Slots[idx++] = new ItemCompass(); // test with y=-1
		//	Slots[idx++] = new ItemSpawnEgg(EntityType.Wither);
			//Slots[idx++] = new ItemSpawnEgg(EntityType.Wolf);
			//Slots[idx++] = new ItemSpawnEgg(EntityType.Pig);
			//Slots[idx++] = new ItemSpawnEgg(EntityType.Horse);
			//Slots[idx++] = new ItemSpawnEgg(EntityType.SkeletonHorse);
			//Slots[idx++] = new ItemSpawnEgg(EntityType.Npc);
			//Slots[idx++] = new ItemSpawnEgg(EntityType.Zombie);
			//Slots[idx++] = new ItemSpawnEgg(EntityType.IronGolem);
			Slots[idx++] = new ItemSnowball();
			Slots[idx++] = new ItemBow();
			Slots[idx++] = new ItemArrow() {Count = 64};

			Slots[35] = new ItemApple();
			Slots[9] = new ItemDiamond();

			Boots = new ItemDiamondBoots();
			Leggings = new ItemDiamondLeggings();
			Chest = new ItemDiamondChestplate();
			Helmet = new ItemDiamondHelmet();
			CursorItem = new ItemAir();
		}

		public virtual Item GetItemInHand()
		{
			//var index = ItemHotbar[InHandSlot];
			var index = InHandSlot;
			if (index == -1 || index >= Slots.Length) return new ItemAir();

			return Slots[index] ?? new ItemAir();
		}

		[Wired]
		public void SetInventorySlot(int slot, Item item)
		{
			Slots[slot] = item;

			SendSetSlot(slot);
		}

		public void UpdateInventorySlot(int slot, Item item)
		{
			var existing = Slots[slot];
			if (existing.Id != item.Id)
			{
				Slots[slot] = item;
				existing = item;
			}

			existing.Count = item.Count;
			existing.Metadata = item.Metadata;
			existing.ExtraData = item.ExtraData;
		}

		public ItemStacks GetSlots()
		{
			ItemStacks slotData = new ItemStacks();
			for (int i = 0; i < Slots.Length; i++)
			{
				if (Slots[i].Count == 0) Slots[i] = new ItemAir();
				slotData.Add(Slots[i]);
			}

			return slotData;
		}

		public ItemStacks GetArmor()
		{
			return new ItemStacks
			{
				Helmet ?? new ItemAir(),
				Chest ?? new ItemAir(),
				Leggings ?? new ItemAir(),
				Boots ?? new ItemAir(),
			};
		}

		public bool SetFirstEmptySlot(Item item, bool update, bool reverseOrder)
		{
			if (reverseOrder)
			{
				for (int si = Slots.Length; si > 9; si--)
				{
					if (FirstEmptySlot(item, update, si - 1)) return true;
				}
			}
			else
			{
				for (int si = 9; si < Slots.Length; si++)
				{
					if (FirstEmptySlot(item, update, si)) return true;
				}
			}

			return false;
		}

		private bool FirstEmptySlot(Item item, bool update, int si)
		{
			Item existingItem = Slots[si];

			if (existingItem.Id == item.Id && existingItem.Metadata == item.Metadata && existingItem.Count + item.Count <= item.MaxStackSize)
			{
				Slots[si].Count += item.Count;
				//if (update) Player.SendPlayerInventory();
				if (update) SendSetSlot(si);
				return true;
			}
			else if (existingItem is ItemAir || existingItem.Id == -1)
			{
				Slots[si] = item;
				//if (update) Player.SendPlayerInventory();
				if (update) SendSetSlot(si);
				return true;
			}

			return false;
		}

		public void SetHeldItemSlot(int selectedHotbarSlot, bool sendToPlayer = true)
		{
			SelectedHotbarSlot = selectedHotbarSlot;
			InHandSlot = selectedHotbarSlot;
			if (selectedHotbarSlot >= 0 && selectedHotbarSlot <= 8)
			{
				InHandSlot = 36 + selectedHotbarSlot;
			}

			if (sendToPlayer)
			{
				McpeMobEquipment order = McpeMobEquipment.CreateObject();
				order.runtimeEntityId = EntityManager.EntityIdSelf;
				order.item = GetItemInHand();
				order.selectedSlot = (byte) selectedHotbarSlot;
				order.slot = (byte) InHandSlot; //(byte) ItemHotbar[InHandSlot];
				Player.SendPackage(order);
			}

			McpeMobEquipment broadcast = McpeMobEquipment.CreateObject();
			broadcast.runtimeEntityId = Player.EntityId;
			broadcast.item = GetItemInHand();
			broadcast.selectedSlot = (byte) selectedHotbarSlot;
			broadcast.slot = (byte) InHandSlot;//(byte) ItemHotbar[InHandSlot];
			Player.Level?.RelayBroadcast(Player, broadcast);
		}

		/// <summary>
		///     Empty the specified slot
		/// </summary>
		/// <param name="slot">The slot to empty.</param>
		public void ClearInventorySlot(byte slot)
		{
			SetInventorySlot(slot, new ItemAir());
		}

		public bool HasItem(Item item)
		{
			for (byte i = 0; i < Slots.Length; i++)
			{
				if ((Slots[i]).Id == item.Id && (Slots[i]).Metadata == item.Metadata)
				{
					return true;
				}
			}
			return false;
		}

		public void RemoveItems(short id, byte count)
		{
			for (byte i = 0; i < Slots.Length; i++)
			{
				var slot = Slots[i];
				if (slot.Id == id)
				{
					slot.Count--;
					if (slot.Count == 0)
					{
						Slots[i] = new ItemAir();
					}

					SendSetSlot(i);
					return;
				}
			}
		}

		public void SendSetSlot(int slot)
		{
			Log.Warn("!!! Send set slot: " + slot);
			McpeInventorySlot ssendSlot = McpeInventorySlot.CreateObject();
			ssendSlot.inventoryId = 0;
			ssendSlot.slot = slot;
			ssendSlot.item = Slots[slot];
			Player.SendPackage(ssendSlot);
			return;
			if (slot < HotbarSize || (slot >= 36 && slot <= 45)/* && (ItemHotbar[slot] == -1 || ItemHotbar[slot] == slot)*/)
			{
			//	ItemHotbar[slot] = slot;
				//Player.SendPlayerInventory();

				McpeMobEquipment order = McpeMobEquipment.CreateObject();
				order.runtimeEntityId = EntityManager.EntityIdSelf;
				order.item = Slots[slot];
				order.slot = (byte) slot;
				order.selectedSlot = (byte) (slot); // Selected hotbar slot
				Player.SendPackage(order);
			}
			else
			{
				McpeInventorySlot sendSlot = McpeInventorySlot.CreateObject();
				sendSlot.inventoryId = 0;
				sendSlot.slot = slot;
				sendSlot.item = Slots[slot];
				Player.SendPackage(sendSlot);
			}
		}

		public void Clear()
		{
			for (int i = 0; i < Slots.Length; ++i)
			{
				if (Slots[i] == null || Slots[i].Id != 0) Slots[i] = new ItemAir();
			}

			if (Helmet.Id != 0) Helmet = new ItemAir();
			if (Chest.Id != 0) Chest = new ItemAir();
			if (Leggings.Id != 0) Leggings = new ItemAir();
			if (Boots.Id != 0) Boots = new ItemAir();

			Player.SendPlayerInventory();
		}
	}
}