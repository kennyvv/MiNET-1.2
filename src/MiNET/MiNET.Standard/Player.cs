﻿#region LICENSE

// The contents of this file are subject to the Common Public Attribution
// License Version 1.0. (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// https://github.com/NiclasOlofsson/MiNET/blob/master/LICENSE. 
// The License is based on the Mozilla Public License Version 1.1, but Sections 14 
// and 15 have been added to cover use of software over a computer network and 
// provide for limited attribution for the Original Developer. In addition, Exhibit A has 
// been modified to be consistent with Exhibit B.
// 
// Software distributed under the License is distributed on an "AS IS" basis,
// WITHOUT WARRANTY OF ANY KIND, either express or implied. See the License for
// the specific language governing rights and limitations under the License.
// 
// The Original Code is Niclas Olofsson.
// 
// The Original Developer is the Initial Developer.  The Initial Developer of
// the Original Code is Niclas Olofsson.
// 
// All portions of the code written by Niclas Olofsson are Copyright (c) 2014-2017 Niclas Olofsson. 
// All Rights Reserved.

#endregion

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Reflection;
using System.Threading;
using log4net;
using log4net.Appender;
using log4net.Core;
using MiNET.Blocks;
using MiNET.Crafting;
using MiNET.Effects;
using MiNET.Entities;
using MiNET.Entities.World;
using MiNET.Items;
using MiNET.Net;
using MiNET.Particles;
using MiNET.Plugins.Commands;
using MiNET.UI.Forms;
using MiNET.Utils;
using MiNET.Utils.Skins;
using MiNET.Worlds;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Button = MiNET.UI.Elements.Button;
using Level = MiNET.Worlds.Level;

namespace MiNET
{
	public class Player : Entity, IMcpeMessageHandler
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (Player));

		private MiNetServer Server { get; set; }
		public IPEndPoint EndPoint { get; private set; }
		public INetworkHandler NetworkHandler { get; set; }

		private Dictionary<Tuple<int, int>, McpeWrapper> _chunksUsed = new Dictionary<Tuple<int, int>, McpeWrapper>();
		private ChunkCoordinates _currentChunkPosition;

		private Inventory _openInventory;
		public PlayerInventory Inventory { get; set; }

		public PlayerLocation SpawnPosition { get; set; }

		public int MaxViewDistance { get; set; } = 22;
		public int MoveRenderDistance { get; set; } = 1;

		public GameMode GameMode { get; set; }
		public bool UseCreativeInventory { get; set; } = true;
		public bool IsConnected { get; set; }
		public CertificateData CertificateData { get; set; }
		public string Username { get; set; }
		public string DisplayName { get; set; }
		public long ClientId { get; set; }
		public UUID ClientUuid { get; set; }
		public string ServerAddress { get; set; }
		public PlayerInfo PlayerInfo { get; set; }

		public Skin Skin { get; set; }

		public float ExperienceLevel { get; set; } = 0f;
		public float Experience { get; set; } = 0f;
		public float MovementSpeed { get; set; } = 0.1f;
		public ConcurrentDictionary<EffectType, Effect> Effects { get; set; } = new ConcurrentDictionary<EffectType, Effect>();

		public HungerManager HungerManager { get; set; }

		public bool IsFalling { get; set; }
		public bool IsFlyingHorizontally { get; set; }

		public Entity LastAttackTarget { get; set; }

		public List<Popup> Popups { get; set; } = new List<Popup>();

		public Session Session { get; set; }

		public DamageCalculator DamageCalculator { get; set; } = new DamageCalculator();
		public ConcurrentDictionary<int, IForm> FormsOpened { get; set; } = new ConcurrentDictionary<int, IForm>();
		public Player(MiNetServer server, IPEndPoint endPoint) : base(-1, null)
		{
			Server = server;
			EndPoint = endPoint;

			Inventory = new PlayerInventory(this);
			HungerManager = new HungerManager(this);

			IsSpawned = false;
			IsConnected = endPoint != null; // Can't connect if there is no endpoint

			Width = 0.6f;
			Length = Width;
			Height = 1.80;

			HideNameTag = false;
			IsAlwaysShowName = true;
			CanClimb = true;
			HasCollision = true;
			IsAffectedByGravity = true;
		}

		public void HandleMcpeClientToServerHandshake(McpeClientToServerHandshake message)
		{
			// Beware that message might be null here.

			var serverInfo = Server.ServerInfo;
			Interlocked.Increment(ref serverInfo.ConnectionsInConnectPhase);

			SendPlayerStatus(0);

			{
				SendResourcePacksInfo();
			}

			//MiNetServer.FastThreadPool.QueueUserWorkItem(() => { Start(null); });
		}

		public void HandleMcpeCommandBlockUpdate(McpeCommandBlockUpdate message)
		{
		}

		public virtual void HandleMcpeResourcePackChunkRequest(McpeResourcePackChunkRequest message)
		{
			var jsonSerializerSettings = new JsonSerializerSettings
			{
				PreserveReferencesHandling = PreserveReferencesHandling.None,
				Formatting = Formatting.Indented,
			};

			string result = JsonConvert.SerializeObject(message, jsonSerializerSettings);
			Log.Debug($"{message.GetType().Name}\n{result}");

			var content = File.ReadAllBytes(@"D:\Temp\ResourcePackChunkData_8f760cf7-2ca4-44ab-ab60-9be2469b9777.zip");
			McpeResourcePackChunkData chunkData = McpeResourcePackChunkData.CreateObject();
			chunkData.packageId = "5abdb963-4f3f-4d97-8482-88e2049ab149";
			chunkData.chunkIndex = 0; // Package index ?
			chunkData.progress = 0; // Long, maybe timestamp?
			chunkData.length = (uint) content.Length;
			chunkData.payload = content;
			SendPackage(chunkData);
		}

		public void HandleMcpePurchaseReceipt(McpePurchaseReceipt message)
		{
			Log.Warn("TODO: Purchase receipt");
		}

		public virtual void HandleMcpeServerSettingsRequest(McpeServerSettingsRequest message)
		{
			//Do not do anything by default, this is up to the servers...
		}

		public void HandleMcpeModalFormResponse(McpeModalFormResponse message)
		{
			var id = message.formid;
			if (string.IsNullOrEmpty(message.data) ||
			    message.data.StartsWith("null", StringComparison.InvariantCultureIgnoreCase)) //Manually closed.
			{
				if (FormsOpened.TryRemove(id, out IForm form))
				{
					form.OnClose(this);
				}
			}
			else
			{
				try
				{
					if (FormsOpened.TryRemove(id, out IForm form))
					{
						form.Process(this, message.data);
					}
				}
				catch (Exception ex)
				{
					Log.Error("Handle Form Response Exception", ex);
					// ¯\_(ツ)_/¯
				}
			}
		}

		private int GetFormId()
		{
			int formId = -1;
			if (FormsOpened.Count > 0)
			{
				for (int i = 0; i < FormsOpened.Count; i++)
				{
					if (!FormsOpened.ContainsKey(i))
					{
						formId = i;
						break;
					}
				}

				if (formId != -1)
				{
					formId = FormsOpened.Max(x => x.Key) + 1;
				}
			}
			else
			{
				formId = 0;
			}
			return formId;
		}

		public void OpenForm(IForm form, bool settings = false)
		{
			int formId = GetFormId();

			if (FormsOpened.TryAdd(formId, form))
			{
				//FormsOpened.AddOrUpdate(0, form, (id, f) => { return f; });

				if (settings)
				{
					var pk = McpeServerSettingsResponse.CreateObject();
					pk.data = form.GetData();
					pk.formId = formId;
					SendPackage(pk);
				}
				else
				{
					form.OnShow(this);

					var pk = McpeModalFormRequest.CreateObject();
					pk.data = form.GetData();
					pk.formid = formId;
					SendPackage(pk);
				}
			}
			else
			{
				Log.Warn("Threading issue! Tried to add form, but ID was already taken.");
			}
		}

		public void CloseAllForms()
		{
			foreach (var form in FormsOpened)
			{
				IForm trash;
				FormsOpened.TryRemove(form.Key, out trash);
				trash.OnClose(this);

				/*var pk = McpeModalFormRequest.CreateObject();
				pk.data = null;
				pk.formid = form.Key;
				SendPackage(pk);*/
			}
		}

		public void HandleMcpeSetDifficulty(McpeSetDifficulty message)
		{
			if (PermissionLevel != UserPermission.Operator
			    || !ActionPermissions.HasFlag(Actionpermissions.Operator)) return;

			Level.SetDifficulty((Difficulty) message.difficulty);
		}

		public void HandleMcpeSimpleEvent(McpeSimpleEvent message)
		{
			switch (message.flags)
			{
				case 1:
					if (PermissionLevel != UserPermission.Operator
					    || !ActionPermissions.HasFlag(Actionpermissions.Operator)) return;
					EnableCommands = true;
					SendSetCommandsEnabled();
					break;
				case 2:
					if (PermissionLevel != UserPermission.Operator
					    || !ActionPermissions.HasFlag(Actionpermissions.Operator)) return;
					EnableCommands = message.value;
					SendSetCommandsEnabled();
					break;
				//case 1:
				//	if (PermissionLevel != UserPermission.Operator) return;
				//	EnableCommands = false;
				//	break;
				default:
					Log.Warn(
						$"Unknown flag in SimpleEvent (F: {message.flags} V: {message.value})");
					break;
			}
		}

		private bool _serverHaveResources = false;

		public virtual void HandleMcpeResourcePackClientResponse(McpeResourcePackClientResponse message)
		{
			if (Log.IsDebugEnabled) Log.Debug($"Handled package 0x{message.Id:X2}\n{Package.HexDump(message.Bytes)}");

			if (message.responseStatus == 2)
			{
				McpeResourcePackDataInfo dataInfo = McpeResourcePackDataInfo.CreateObject();
				dataInfo.packageId = "5abdb963-4f3f-4d97-8482-88e2049ab149";
				dataInfo.maxChunkSize = 1048576;
				dataInfo.chunkCount = 1;
				dataInfo.compressedPackageSize = 359901; // Lenght of data
				dataInfo.hash = "9&\r2'ëX•;\u001bð—Ð‹\u0006´6\u0007TÞ/[Üx…x*\u0005h\u0002à\u0012"; //TODO: Fix encoding for this. Right now, must be Default :-(
				SendPackage(dataInfo);
				return;
			}
			else if (message.responseStatus == 3)
			{
				//if (_serverHaveResources)
				{
					SendResourcePackStack();
				}
				//else
				//{
				//	MiNetServer.FastThreadPool.QueueUserWorkItem(() => { Start(null); });
				//}
				return;
			}
			else if (message.responseStatus == 4)
			{
				//if (_serverHaveResources)
				{
					MiNetServer.FastThreadPool.QueueUserWorkItem(() => { Start(null); });
				}
				return;
			}
		}

		private void SendResourcePacksInfo()
		{
			McpeResourcePacksInfo packInfo = McpeResourcePacksInfo.CreateObject();
			if (_serverHaveResources)
			{
				packInfo.mustAccept = false;
				packInfo.resourcepackinfos = new ResourcePackInfos
				{
					new ResourcePackInfo() {PackIdVersion = new PackIdVersion() {Id = "5abdb963-4f3f-4d97-8482-88e2049ab149", Version = "0.0.1"}, Size = 359901},
				};
			}
			SendPackage(packInfo);
		}

		private void SendResourcePackStack()
		{
			McpeResourcePackStack packStack = McpeResourcePackStack.CreateObject();
			if (_serverHaveResources)
			{
				packStack.mustAccept = false;
				packStack.resourcepackidversions = new ResourcePackIdVersions
				{
					new PackIdVersion() {Id = "5abdb963-4f3f-4d97-8482-88e2049ab149", Version = "0.0.1"},
				};
			}
			SendPackage(packStack);
		}

		public virtual void HandleMcpePlayerInput(McpePlayerInput message)
		{
			Log.Debug($"Player input: Motion X={message.motionX}, Motion Z={message.motionZ}, Flags=0x{message.motionX:X2}");
		}

		private object _mapInfoSync = new object();

		private Timer _mapSender;
		private ConcurrentQueue<McpeWrapper> _mapBatches = new ConcurrentQueue<McpeWrapper>();

		public virtual void HandleMcpeMapInfoRequest(McpeMapInfoRequest message)
		{
			lock (_mapInfoSync)
			{
				//if(_mapSender == null)
				//{
				//	_mapSender = new Timer(Callback);
				//}

				long mapId = message.mapId;

				//Log.Warn($"Requested map with ID: {mapId} 0x{mapId:X2}");

				if (mapId == 0)
				{
					// 2016-02-26 02:53:01,895 [17] INFO  MiNET.Player - Requested map with ID: 0xFFFFFFFFFFFFFFFF
					// Should not happen.
				}
				else
				{
					MapEntity mapEntity = Level.GetEntity(mapId) as MapEntity;
					//if (mapEntity == null)
					//{
					//	// Create new map entity
					//	// send map for that entity
					//	mapEntity = new MapEntity(Level, mapId);
					//	mapEntity.SpawnEntity();
					//}
					//else
					{
						mapEntity?.AddToMapListeners(this, mapId);
					}
				}
			}
		}

		public virtual void SendMapInfo(MapInfo mapInfo)
		{
			McpeClientboundMapItemData packet = McpeClientboundMapItemData.CreateObject();
			packet.mapinfo = mapInfo;
			SendPackage(packet);
		}

		public int ChunkRadius { get; private set; } = -1;

		public virtual void HandleMcpeRequestChunkRadius(McpeRequestChunkRadius message)
		{
			Log.Debug($"Requested chunk radius of: {message.chunkRadius}");

			ChunkRadius = Math.Max(5, Math.Min(message.chunkRadius, MaxViewDistance));

			SendChunkRadiusUpdate();

			//if (_completedStartSequence)
			{
				MiNetServer.FastThreadPool.QueueUserWorkItem(SendChunksForKnownPosition);
			}
		}


		public virtual void HandleMcpeEntityFall(McpeEntityFall message)
		{
			double damage = message.fallDistance - 3;
			if (damage > 0 && DoFallDamage)
			{
				HealthManager.TakeHit(null, (int) DamageCalculator.CalculatePlayerDamage(null, this, null, damage, DamageCause.Fall), DamageCause.Fall);
			}
		}

		/// <summary>
		///     Handles an animate packet.
		/// </summary>
		/// <param name="message">The message.</param>
		public virtual void HandleMcpeAnimate(McpeAnimate message)
		{
			if (Level == null) return;

			var itemInHand = Inventory.GetItemInHand();
			if (itemInHand != null)
			{
				bool isHandled = itemInHand.Animate(Level, this);
				if (isHandled) return; // Handled, return
			}

			McpeAnimate msg = McpeAnimate.CreateObject();
			msg.runtimeEntityId = EntityId;
			msg.actionId = message.actionId;

			Level.RelayBroadcast(this, msg);
		}

		Action _dimensionFunc;

		protected virtual void BlockBreakChanged(Block block, PlayerAction action, double breakingTime)
		{
			
		}

		protected virtual bool CanBreakBlock(Block block, Item itemInHand)
		{
			return true;
		}

		private bool _breakingBlock = false;
		/// <summary>
		///     Handles the player action.
		/// </summary>
		/// <param name="message">The message.</param>
		public virtual void HandleMcpePlayerAction(McpePlayerAction message)
		{
			var action = (PlayerAction) message.actionId;
			switch (action)
			{
				case PlayerAction.StartBreak:
				{
					var facecCoordinates = Block.GetNewCoordinatesFromFace(message.coordinates, (BlockFace)message.face);
					Block block = Level.GetBlock(facecCoordinates);
					if (block is Fire)
					{
						Level.BreakBlock(this, facecCoordinates);
					}

					if (GameMode != GameMode.Creative)
					{
						Block target = Level.GetBlock(message.coordinates);
						var breakTime = Math.Ceiling(target.GetBreakTime(Inventory.GetItemInHand())*20);

						McpeLevelEvent levelEvent = McpeLevelEvent.CreateObject();
						levelEvent.position = message.coordinates;
						levelEvent.eventId = (int) LevelEventType.BlockStartCracking;
						levelEvent.data = (int) (65535/breakTime);
						Level.RelayBroadcast(levelEvent);

						_breakingBlock = true;
						BlockBreakChanged(target, action, breakTime);
					}
				}

					break;
				case PlayerAction.AbortBreak:
				case PlayerAction.StopBreak:
				{
					Block target = Level.GetBlock(message.coordinates);

					McpeLevelEvent levelEvent = McpeLevelEvent.CreateObject();
					levelEvent.position = message.coordinates;
					levelEvent.eventId = (int) LevelEventType.BlockStopCracking;
					levelEvent.data = 0;
					Level.RelayBroadcast(levelEvent);

					_breakingBlock = false;
					BlockBreakChanged(target, action, -1);
				}
					break;
				case PlayerAction.Breaking:
				
					break;

				case PlayerAction.DropItem:
					if (_itemUseTimer <= 0) return;

					Item itemInHand = Inventory.GetItemInHand();

					if (itemInHand == null) return; // Cheat(?)

					itemInHand.Release(Level, this, new BlockCoordinates(message.coordinates.X, message.coordinates.Y, message.coordinates.Z), Level.TickTime - _itemUseTimer);

					_itemUseTimer = 0;
					break;

				case PlayerAction.StartSleeping:
					break;
				case PlayerAction.StopSleeping:
					break;

				case PlayerAction.Respawn:
					MiNetServer.FastThreadPool.QueueUserWorkItem(HandleMcpeRespawn);
					break;
				case PlayerAction.Jump:
					HungerManager.IncreaseExhaustion(IsSprinting ? 0.8f : 0.2f);
					break;

				case PlayerAction.StartSprint:
					SetSprinting(true);
					break;
				case PlayerAction.StopSprint:
					SetSprinting(false);
					break;

				case PlayerAction.StartSneak:
					SetSprinting(false);
					IsSneaking = true;
					break;
				case PlayerAction.StopSneak:
					SetSprinting(false);
					IsSneaking = false;
					break;

				case PlayerAction.DimensionChange:
					break;
				case PlayerAction.DimensionChangeAck:
					if (_dimensionFunc != null)
					{
						_dimensionFunc();
						_dimensionFunc = null;
					}
					break;

				case PlayerAction.StartGlide:
					IsGliding = true;
					Height = 0.6;

					var particle = new WhiteSmokeParticle(Level);
					particle.Position = KnownPosition.ToVector3();
					particle.Spawn();

					break;
				case PlayerAction.StopGlide:
					IsGliding = false;
					Height = 1.8;
					break;

				//case PlayerAction.DenyDestroyBlock:
				//	break;
				default:
					Log.Warn($"Unhandled action ID={message.actionId}");
					throw new ArgumentOutOfRangeException(nameof(message.actionId));
			}

			IsUsingItem = false;

			BroadcastSetEntityData();

			//MetadataDictionary metadata = new MetadataDictionary
			//{
			//	[0] = GetDataValue()
			//};

			//var setEntityData = McpeSetEntityData.CreateObject();
			//setEntityData.entityId = EntityId;
			//setEntityData.metadata = metadata;
			//Level?.RelayBroadcast(this, setEntityData);
		}

		private float _baseSpeed;
		private object _sprintLock = new object();

		public void SetSprinting(bool isSprinting)
		{
			lock (_sprintLock)
			{
				if (isSprinting == IsSprinting) return;

				if (isSprinting)
				{
					IsSprinting = true;
					_baseSpeed = MovementSpeed;
					MovementSpeed += MovementSpeed*0.3f;
				}
				else
				{
					IsSprinting = false;
					MovementSpeed = _baseSpeed;
				}

				SendUpdateAttributes();
			}
		}

		/// <summary>
		///     Handles the entity data.
		/// </summary>
		/// <param name="message">The message.</param>
		public virtual void HandleMcpeBlockEntityData(McpeBlockEntityData message)
		{
			Log.DebugFormat("x:  {0}", message.coordinates.X);
			Log.DebugFormat("y:  {0}", message.coordinates.Y);
			Log.DebugFormat("z:  {0}", message.coordinates.Z);
			Log.DebugFormat("NBT {0}", message.namedtag.NbtFile);

			var blockEntity = Level.GetBlockEntity(message.coordinates);

			if (blockEntity == null) return;

			blockEntity.SetCompound(message.namedtag.NbtFile.RootTag);
			Level.SetBlockEntity(blockEntity);
		}


		public bool IsWorldImmutable { get; set; }
		public bool IsWorldBuilder { get; set; }
		public bool IsMuted { get; set; }
		public bool IsNoPvp { get; set; }
		public bool IsNoPvm { get; set; }
		public bool IsNoMvp { get; set; }
		public bool IsNoClip { get; set; }
		public bool IsFlying { get; set; } = false;
		public bool DoFallDamage { get; set; } = true;

		public virtual void HandleMcpeAdventureSettings(McpeAdventureSettings message)
		{
			if (message.userId != EntityId)
			{
				return;
			}

			var flags = message.flags;
			IsAutoJump = (flags & 0x20) == 0x20;
			IsFlying = (flags & 0x200) == 0x200;

			var previewPermissions =  (Actionpermissions)message.actionPermissions;
			var previewPermissionLevel = (UserPermission) message.permissionLevel;

			UpdateFlags(previewPermissionLevel, previewPermissions);

			SendAdventureSettings();
		}

		private uint GetUintFlags()
		{
			uint flags = 0;

			if (IsWorldImmutable || GameMode == GameMode.Adventure) flags |= 0x01; // Immutable World (Remove hit markers client-side).
			if (IsNoPvp || IsSpectator || GameMode == GameMode.Spectator) flags |= 0x02; // No PvP (Remove hit markers client-side).
			if (IsNoPvm || IsSpectator || GameMode == GameMode.Spectator) flags |= 0x04; // No PvM (Remove hit markers client-side).
			if (IsNoMvp || IsSpectator || GameMode == GameMode.Spectator) flags |= 0x08;

			if (IsAutoJump) flags |= 0x20;

			if (AllowFly || GameMode == GameMode.Creative) flags |= 0x40;

			if (IsNoClip || IsSpectator || GameMode == GameMode.Spectator) flags |= 0x80; // No clip

			if (IsWorldBuilder) flags |= 0x100; // Worldbuilder

			if (IsFlying) flags |= 0x200;
			if (IsMuted) flags |= 0x400; // Mute

			return flags;
		}

		public virtual void SendAdventureSettings()
		{
			McpeAdventureSettings mcpeAdventureSettings = McpeAdventureSettings.CreateObject();

			mcpeAdventureSettings.flags = GetUintFlags();
			mcpeAdventureSettings.commandPermission = (uint) CommandPermission;
			mcpeAdventureSettings.actionPermissions = (uint) ActionPermissions;
			mcpeAdventureSettings.permissionLevel= (uint) PermissionLevel;
			mcpeAdventureSettings.userId = Endian.SwapInt64(EntityId);
			mcpeAdventureSettings.customStoredPermissions = 0;

			SendPackage(mcpeAdventureSettings);
		}

		protected virtual void UpdateFlags(UserPermission rank, Actionpermissions permissions)
		{
			if (rank < PermissionLevel)
			{
				PermissionLevel = rank;
			}

			if (permissions == Actionpermissions.All)
			{
				IsNoPvp = false;
				IsNoPvm = false;
				AllowFly = true;
				IsMuted = false;
				
				IsWorldBuilder = true;
			}
			else if (permissions == Actionpermissions.Default)
			{
				IsNoPvp = true;
				IsNoPvm = true;
				AllowFly = false;
				IsMuted = true;

				IsWorldBuilder = false;
			}
			else if (permissions == Actionpermissions.Default)
			{
				Log.Warn("!!! Default Permission Flags !!!");
			}
			else
			{
				IsNoPvm = !permissions.HasFlag(Actionpermissions.AttackMobs);
				IsNoPvp = !permissions.HasFlag(Actionpermissions.AttackPlayers);
				IsWorldBuilder = permissions.HasFlag(Actionpermissions.BuildAndMine);
				IsNoPvm = !permissions.HasFlag(Actionpermissions.Default);
			}

			SendAdventureSettings();
		}

		public UserPermission PermissionLevel { get; set; } = UserPermission.Operator;
		public CommandPermission CommandPermission { get; set; } = CommandPermission.Operator;
		public Actionpermissions ActionPermissions { get; set; } = Actionpermissions.All;
		public bool IsSpectator { get; set; }

		[Wired]
		public void SetSpectator(bool isSpectator)
		{
			IsSpectator = isSpectator;
			SendAdventureSettings();
		}

		public bool IsAutoJump { get; set; }

		[Wired]
		public void SetAutoJump(bool isAutoJump)
		{
			IsAutoJump = isAutoJump;
			SendAdventureSettings();
		}

		public bool AllowFly { get; set; }

		[Wired]
		public void SetAllowFly(bool allowFly)
		{
			AllowFly = allowFly;
			SendAdventureSettings();
		}

		private object _loginSyncLock = new object();

		public virtual void HandleMcpeLogin(McpeLogin message)
		{
			// Do nothing
		}

		public void Start(object o)
		{
			Stopwatch watch = new Stopwatch();
			watch.Restart();

			var serverInfo = Server.ServerInfo;

			try
			{
				Session = Server.SessionManager.CreateSession(this);

				lock (_disconnectSync)
				{
					if (!IsConnected) return;

					if (Level != null) return; // Already called this method.

					Level = Server.LevelManager.GetLevel(this, Dimension.Overworld.ToString());
				}
				if (Level == null)
				{
					Disconnect("No level assigned.");
					return;
				}

				SpawnPosition = SpawnPosition ?? Level.SpawnPoint;
				KnownPosition = SpawnPosition;

				// Check if the user already exist, that case bumpt the old one
				Level.RemoveDuplicatePlayers(Username, ClientId);

				Level.EntityManager.AddEntity(this);

				GameMode = Level.GameMode;

				//
				// Start game - spawn sequence starts here
				//

				// Vanilla 1st player list here

				//Level.AddPlayer(this, false);

				SendSetTime();

				SendStartGame();

				if (ChunkRadius == -1) ChunkRadius = 5;

				SendChunkRadiusUpdate();

				//SendSetSpawnPosition();

				SendSetTime();

				SendSetDificulty();

				SendSetCommandsEnabled();

				SendAdventureSettings();

				// Send McpeGameRulesChanged

				// Vanilla 2nd player list here

				Level.AddPlayer(this, false);

				SendAvailableCommands();

				SendUpdateAttributes();

				SendPlayerInventory();

				SendCreativeInventory();

				SendCraftingRecipes();
			}
			catch (Exception e)
			{
				Log.Error(e);
			}
			finally
			{
				Interlocked.Decrement(ref serverInfo.ConnectionsInConnectPhase);
			}

			LastUpdatedTime = DateTime.UtcNow;
			Log.InfoFormat("Login complete by: {0} from {2} in {1}ms", Username, watch.ElapsedMilliseconds, EndPoint);

			OnLoginComplete();
		}

		protected virtual void OnLoginComplete()
		{
			
		}

		public bool EnableCommands { get; set; } = Config.GetProperty("EnableCommands", false);

		protected virtual void SendSetCommandsEnabled()
		{
			McpeSetCommandsEnabled enabled = McpeSetCommandsEnabled.CreateObject();
			enabled.enabled = EnableCommands;
			SendPackage(enabled);
		}

		protected virtual void SendAvailableCommands()
		{
			McpeAvailableCommands commands = McpeAvailableCommands.CreateObject();
			commands.CommandSet = Server.PluginManager.Commands;
			SendPackage(commands);
		}

		public virtual void HandleMcpeCommandRequest(McpeCommandRequest message)
		{
			var result = Server.PluginManager.HandleCommand(this, message.command);
			if (result != null)
			{
				if (result is string)
				{
					string sRes = result as string;
					SendMessage(sRes);
					return;
				}

			
				var resultType = result.GetType();
				string bodyValue = resultType.GetProperty("Body")?.GetValue(result, null) as string;
				if (bodyValue != null)
				{
					SendMessage(bodyValue);
				}
			}
		}

		public virtual void InitializePlayer()
		{
			// Send set health

			SendPlayerStatus(3);

			//send time again
			SendSetTime();
			IsSpawned = true;

			SetPosition(SpawnPosition);

			LastUpdatedTime = DateTime.UtcNow;
			_haveJoined = true;

			OnPlayerJoin(new PlayerEventArgs(this));
		}

		public virtual void HandleMcpeRespawn()
		{
			HandleMcpeRespawn(null);
		}

		public virtual void HandleMcpeRespawn(McpeRespawn message)
		{
			HealthManager.ResetHealth();

			HungerManager.ResetHunger();

			BroadcastSetEntityData();

			SendUpdateAttributes();

			SendSetSpawnPosition();

			SendAdventureSettings();

			SendPlayerInventory();

			CleanCache();

			ForcedSendChunk(SpawnPosition);

			// send teleport to spawn
			SetPosition(SpawnPosition);

			Level.SpawnToAll(this);

			IsSpawned = true;

			Log.InfoFormat("Respawn player {0} on level {1}", Username, Level.LevelId);

			SendSetTime();

			MiNetServer.FastThreadPool.QueueUserWorkItem(() => ForcedSendChunks());

			//SendPlayerStatus(3);

			//McpeRespawn mcpeRespawn = McpeRespawn.CreateObject();
			//mcpeRespawn.x = SpawnPosition.X;
			//mcpeRespawn.y = SpawnPosition.Y;
			//mcpeRespawn.z = SpawnPosition.Z;
			//SendPackage(mcpeRespawn);

			////send time again
			//SendSetTime();
			//IsSpawned = true;
			//LastUpdatedTime = DateTime.UtcNow;
			//_haveJoined = true;
		}

		[Wired]
		public void SetPosition(PlayerLocation position, bool teleport = true)
		{
			KnownPosition = position;
			LastUpdatedTime = DateTime.UtcNow;

			var package = McpeMovePlayer.CreateObject();
			package.runtimeEntityId = EntityManager.EntityIdSelf;
			package.x = position.X;
			package.y = position.Y + 1.62f;
			package.z = position.Z;
			package.yaw = position.Yaw;
			package.headYaw = position.HeadYaw;
			package.pitch = position.Pitch;
			package.mode = (byte) (teleport ? 2 : 0);
			package.otherRuntimeEntityId = 0;
			package.Cause= McpeMovePlayer.TeleportCause.Unknown;
			package.onGround = IsOnGround;

			SendPackage(package);
		}

		private object _teleportSync = new object();

		public virtual void Teleport(PlayerLocation newPosition)
		{
			if (!Monitor.TryEnter(_teleportSync)) return;

			try
			{
				bool oldNoAi = NoAi;
				SetNoAi(true);

				if (!IsChunkInCache(newPosition))
				{
					// send teleport straight up, no chunk loading
					SetPosition(new PlayerLocation
					{
						X = KnownPosition.X,
						Y = 4000,
						Z = KnownPosition.Z,
						Yaw = 91,
						Pitch = 28,
						HeadYaw = 91,
					});

					ForcedSendChunk(newPosition);
				}

				// send teleport to spawn
				SetPosition(newPosition);

				SetNoAi(oldNoAi);
			}
			finally
			{
				Monitor.Exit(_teleportSync);
			}

			MiNetServer.FastThreadPool.QueueUserWorkItem(SendChunksForKnownPosition);
		}

		private bool IsChunkInCache(PlayerLocation position)
		{
			var chunkPosition = new ChunkCoordinates(position);

			var key = new Tuple<int, int>(chunkPosition.X, chunkPosition.Z);
			return _chunksUsed.ContainsKey(key);
		}

		public virtual void ChangeDimension(Level toLevel, PlayerLocation spawnPoint, Dimension dimension, Func<Level> levelFunc = null)
		{
			SendChangeDimension(dimension);

			Level.RemovePlayer(this);

			Dimension fromDimension = Level.Dimension;

			if (toLevel == null && levelFunc != null)
			{
				toLevel = levelFunc();
			}

			Level = toLevel; // Change level
			SpawnPosition = spawnPoint ?? Level?.SpawnPoint;

			BroadcastSetEntityData();

			SendUpdateAttributes();

			CleanCache();

			// Check if we need to generate a platform
			if (dimension == Dimension.TheEnd)
			{
				BlockCoordinates platformPosition = (BlockCoordinates) (SpawnPosition + BlockCoordinates.Down);
				if (!(Level.GetBlock(platformPosition) is Obsidian))
				{
					for (int x = 0; x < 5; x++)
					{
						for (int z = 0; z < 5; z++)
						{
							for (int y = 0; y < 5; y++)
							{
								var coordinates = new BlockCoordinates(x, y, z) + platformPosition + new BlockCoordinates(-2, 0, -2);
								if (y == 0)
								{
									Level.SetBlock(new Obsidian() {Coordinates = coordinates});
								}
								else
								{
									Level.SetAir(coordinates);
								}
							}
						}
					}
				}
			}
			else if (dimension == Dimension.Overworld && fromDimension == Dimension.TheEnd)
			{
				// Spawn on player home spawn
			}
			else if (dimension == Dimension.Nether)
			{
				// Find closes portal or spawn new
				// coordinate translation x/8

				BlockCoordinates start = (BlockCoordinates) KnownPosition;
				start /= new BlockCoordinates(8, 1, 8);

				PlayerLocation pos = FindNetherSpawn(Level, start);
				if (pos != null)
				{
					SpawnPosition = pos;
				}
				else
				{
					SpawnPosition = CreateNetherPortal(Level);
				}
			}
			else if (dimension == Dimension.Overworld && fromDimension == Dimension.Nether)
			{
				// Find closes portal or spawn new
				// coordinate translation x * 8

				BlockCoordinates start = (BlockCoordinates) KnownPosition;
				start *= new BlockCoordinates(8, 1, 8);

				PlayerLocation pos = FindNetherSpawn(Level, start);
				if (pos != null)
				{
					SpawnPosition = pos;
				}
				else
				{
					SpawnPosition = CreateNetherPortal(Level);
				}
			}

			Log.Debug($"Spawn point: {SpawnPosition}");

			SendChunkRadiusUpdate();

			ForcedSendChunk(SpawnPosition);

			// send teleport to spawn
			SetPosition(SpawnPosition);

			MiNetServer.FastThreadPool.QueueUserWorkItem(delegate
			{
				Level.AddPlayer(this, true);

				ForcedSendChunks(() =>
				{
					Log.WarnFormat("Respawn player {0} on level {1}", Username, Level.LevelId);

					SendSetTime();
				});
			});
		}

		private PlayerLocation FindNetherSpawn(Level level, BlockCoordinates start)
		{
			int width = 128;
			int height = Level.Dimension == Dimension.Overworld ? 256 : 128;


			int portalId = new Portal().Id;
			int obsidionId = new Obsidian().Id;

			Log.Debug($"Starting point: {start}");

			BlockCoordinates? closestPortal = null;
			int closestDistance = int.MaxValue;
			for (int x = start.X - width; x < start.X + width; x++)
			{
				for (int z = start.Z - width; z < start.Z + width; z++)
				{
					if (level.Dimension == Dimension.Overworld)
					{
						height = level.GetHeight(new BlockCoordinates(x, 0, z)) + 10;
					}

					for (int y = height - 1; y >= 0; y--)
					{
						var coord = new BlockCoordinates(x, y, z);
						if (coord.DistanceTo(start) > closestDistance) continue;

						bool b = level.IsBlock(coord, portalId);
						b &= level.IsBlock(coord + BlockCoordinates.Down, obsidionId);
						if (b)
						{
							Portal portal = (Portal) level.GetBlock(coord);
							if (portal.Metadata >= 2)
							{
								b &= level.IsBlock(coord + BlockCoordinates.North, portalId);
							}
							else
							{
								b &= level.IsBlock(coord + BlockCoordinates.East, portalId);
							}

							Log.Debug($"Found portal block at {coord}, direction={portal.Metadata}");
							if (b && coord.DistanceTo(start) < closestDistance)
							{
								Log.Debug($"Found a closer portal at {coord}");
								closestPortal = coord;
								closestDistance = (int) coord.DistanceTo(start);
							}
						}
					}
				}
			}

			return closestPortal;
		}

		private PlayerLocation CreateNetherPortal(Level level)
		{
			int width = 16;
			int height = Level.Dimension == Dimension.Overworld ? 256 : 128;


			BlockCoordinates start = (BlockCoordinates) KnownPosition;
			if (Level.Dimension == Dimension.Nether)
			{
				start /= new BlockCoordinates(8, 1, 8);
			}
			else
			{
				start *= new BlockCoordinates(8, 1, 8);
			}

			Log.Debug($"Starting point: {start}");

			PortalInfo closestPortal = null;
			int closestPortalDistance = int.MaxValue;
			for (int x = start.X - width; x < start.X + width; x++)
			{
				for (int z = start.Z - width; z < start.Z + width; z++)
				{
					if (level.Dimension == Dimension.Overworld)
					{
						height = level.GetHeight(new BlockCoordinates(x, 0, z)) + 10;
					}

					for (int y = height - 1; y >= 0; y--)
					{
						var coord = new BlockCoordinates(x, y, z);
						if (coord.DistanceTo(start) > closestPortalDistance) continue;

						if (!(!level.IsAir(coord) && level.IsAir(coord + BlockCoordinates.Up))) continue;

						var bbox = new BoundingBox(coord, coord + new BlockCoordinates(3, 5, 4));
						if (!SpawnAreaClear(bbox))
						{
							bbox = new BoundingBox(coord, coord + new BlockCoordinates(4, 5, 3));
							if (!SpawnAreaClear(bbox))
							{
								bbox = new BoundingBox(coord, coord + new BlockCoordinates(1, 5, 4));
								if (!SpawnAreaClear(bbox))
								{
									bbox = new BoundingBox(coord, coord + new BlockCoordinates(4, 5, 1));
									if (!SpawnAreaClear(bbox))
									{
										continue;
									}
								}
							}
						}

						//coord += BlockCoordinates.Up;

						Log.Debug($"Found portal location at {coord}");
						if (coord.DistanceTo(start) < closestPortalDistance)
						{
							Log.Debug($"Found a closer portal location at {coord}");
							closestPortal = new PortalInfo() {Coordinates = coord, Size = bbox};
							closestPortalDistance = (int) coord.DistanceTo(start);
						}
					}
				}
			}

			if (closestPortal == null)
			{
				// Force create between Y=YMAX - (10 to 70)
				int y = (int) Math.Max(Height - 70, start.Y);
				y = (int) Math.Min(Height - 10, y);
				start.Y = y;

				Log.Debug($"Force portal location at {start}");

				closestPortal = new PortalInfo();
				closestPortal.HasPlatform = true;
				closestPortal.Coordinates = start;
				closestPortal.Size = new BoundingBox(start, start + new BlockCoordinates(4, 5, 3));
			}


			if (closestPortal != null)
			{
				BuildPortal(level, closestPortal);
			}


			return closestPortal?.Coordinates;
		}

		public static void BuildPortal(Level level, PortalInfo portalInfo)
		{
			var bbox = portalInfo.Size;

			Log.Debug($"Building portal from BBOX: {bbox}");

			int minX = (int) (bbox.Min.X);
			int minZ = (int) (bbox.Min.Z);
			int width = (int) (bbox.Width);
			int depth = (int) (bbox.Depth);
			int height = (int) (bbox.Height);

			int midPoint = depth > 2 ? depth/2 : 0;

			bool haveSetCoordinate = false;
			for (int x = 0; x < width; x++)
			{
				for (int z = 0; z < depth; z++)
				{
					for (int y = 0; y < height; y++)
					{
						var coordinates = new BlockCoordinates(x + minX, (int) (y + bbox.Min.Y), z + minZ);
						Log.Debug($"Place: {coordinates}");

						if (width > depth && z == midPoint)
						{
							if ((x == 0 || x == width - 1) || (y == 0 || y == height - 1))
							{
								level.SetBlock(new Obsidian {Coordinates = coordinates});
							}
							else
							{
								level.SetBlock(new Portal {Coordinates = coordinates});
								if (!haveSetCoordinate)
								{
									haveSetCoordinate = true;
									portalInfo.Coordinates = coordinates;
								}
							}
						}
						else if (width <= depth && x == midPoint)
						{
							if ((z == 0 || z == depth - 1) || (y == 0 || y == height - 1))
							{
								level.SetBlock(new Obsidian {Coordinates = coordinates});
							}
							else
							{
								level.SetBlock(new Portal {Coordinates = coordinates, Metadata = 2});
								if (!haveSetCoordinate)
								{
									haveSetCoordinate = true;
									portalInfo.Coordinates = coordinates;
								}
							}
						}

						if (portalInfo.HasPlatform && y == 0)
						{
							level.SetBlock(new Obsidian {Coordinates = coordinates});
						}
					}
				}
			}
		}


		private bool SpawnAreaClear(BoundingBox bbox)
		{
			BlockCoordinates min = bbox.Min;
			BlockCoordinates max = bbox.Max;
			for (int x = min.X; x < max.X; x++)
			{
				for (int z = min.Z; z < max.Z; z++)
				{
					for (int y = min.Y; y < max.Y; y++)
					{
						//if (z == min.Z) if (!Level.GetBlock(new BlockCoordinates(x, y, z)).IsBuildable) return false;
						if (y == min.Y)
						{
							if (!Level.GetBlock(new BlockCoordinates(x, y, z)).IsBuildable) return false;
						}
						else
						{
							if (!Level.IsAir(new BlockCoordinates(x, y, z))) return false;
						}
					}
				}
			}

			return true;
		}


		public virtual void SpawnLevel(Level toLevel, PlayerLocation spawnPoint, bool useLoadingScreen = false, Func<Level> levelFunc = null, Action postSpawnAction = null)
		{
			bool oldNoAi = NoAi;
			SetNoAi(true);

			if (useLoadingScreen)
			{
				SendChangeDimension(Dimension.Nether);
			}

			if (toLevel == null && levelFunc != null)
			{
				toLevel = levelFunc();
			}

			SetPosition(new PlayerLocation
			{
				X = KnownPosition.X,
				Y = 4000,
				Z = KnownPosition.Z,
				Yaw = 91,
				Pitch = 28,
				HeadYaw = 91,
			});

			Action transferFunc = delegate
			{
				if (useLoadingScreen)
				{
					SendChangeDimension(Dimension.Overworld);
				}

				Level.RemovePlayer(this, true);

				Level = toLevel; // Change level
				SpawnPosition = spawnPoint ?? Level?.SpawnPoint;

				HungerManager.ResetHunger();

				HealthManager.ResetHealth();

				BroadcastSetEntityData();

				SendUpdateAttributes();

				SendSetSpawnPosition();

				SendAdventureSettings();

				SendPlayerInventory();

				CleanCache();

				ForcedSendChunk(SpawnPosition);

				// send teleport to spawn
				SetPosition(SpawnPosition);

				SetNoAi(oldNoAi);

				MiNetServer.FastThreadPool.QueueUserWorkItem(delegate
				{
					Level.AddPlayer(this, true);

					ForcedSendChunks(() =>
					{
						Log.InfoFormat("Respawn player {0} on level {1}", Username, Level.LevelId);

						SendSetTime();

						postSpawnAction?.Invoke();
					});
				});
			};


			if (useLoadingScreen)
			{
				_dimensionFunc = transferFunc;
				ForcedSendEmptyChunks();
			}
			else
			{
				transferFunc();
			}
		}

		protected virtual void SendChangeDimension(Dimension dimension, bool flag = false, Vector3 position = new Vector3())
		{
			McpeChangeDimension changeDimension = McpeChangeDimension.CreateObject();
			changeDimension.dimension = (int) dimension;
			changeDimension.position = position;
			changeDimension.unknown = flag;
			changeDimension.NoBatch = true; // This is here because the client crashes otherwise.
			SendPackage(changeDimension);
		}

		public override void BroadcastSetEntityData()
		{
			McpeSetEntityData mcpeSetEntityData = McpeSetEntityData.CreateObject();
			mcpeSetEntityData.runtimeEntityId = EntityManager.EntityIdSelf;
			mcpeSetEntityData.metadata = GetMetadata();
			mcpeSetEntityData.Encode();
			SendPackage(mcpeSetEntityData);

			base.BroadcastSetEntityData();
		}

		public void SendSetDificulty()
		{
			McpeSetDifficulty mcpeSetDifficulty = McpeSetDifficulty.CreateObject();
			mcpeSetDifficulty.difficulty = (uint) Level.Difficulty;
			SendPackage(mcpeSetDifficulty);
		}

		public virtual void SendPlayerInventory()
		{
			McpeInventoryContent strangeContent = McpeInventoryContent.CreateObject();
			strangeContent.inventoryId = (byte) 0x7b;
			strangeContent.input = new ItemStacks();
			SendPackage(strangeContent);

			McpeInventoryContent inventoryContent = McpeInventoryContent.CreateObject();
			inventoryContent.inventoryId = (byte) 0x00;
			inventoryContent.input = Inventory.GetSlots();
			SendPackage(inventoryContent);

			McpeInventoryContent armorContent = McpeInventoryContent.CreateObject();
			armorContent.inventoryId = 0x78;
			armorContent.input = Inventory.GetArmor();
			SendPackage(armorContent);

			McpeMobEquipment mobEquipment = McpeMobEquipment.CreateObject();
			mobEquipment.runtimeEntityId = EntityManager.EntityIdSelf;
			mobEquipment.item = Inventory.GetItemInHand();
			mobEquipment.slot = (byte) Inventory.InHandSlot;
			mobEquipment.selectedSlot = (byte) Inventory.SelectedHotbarSlot;
			mobEquipment.windowsId = 0;
			SendPackage(mobEquipment);
		}

		public virtual void SendCraftingRecipes()
		{
			McpeCraftingData craftingData = McpeCraftingData.CreateObject();
			craftingData.recipes = RecipeManager.Recipes;
			SendPackage(craftingData);
		}

		public virtual void SendCreativeInventory()
		{
			if (!UseCreativeInventory) return;

			McpeInventoryContent creativeContent = McpeInventoryContent.CreateObject();
			creativeContent.inventoryId = (byte) 0x79;
			creativeContent.input = InventoryUtils.GetCreativeMetadataSlots();
			SendPackage(creativeContent);
		}

		private void SendChunkRadiusUpdate()
		{
			McpeChunkRadiusUpdate package = McpeChunkRadiusUpdate.CreateObject();
			package.chunkRadius = ChunkRadius;

			SendPackage(package);
		}

		public void SendPlayerStatus(int status)
		{
			McpePlayStatus mcpePlayerStatus = McpePlayStatus.CreateObject();
			mcpePlayerStatus.status = status;
			SendPackage(mcpePlayerStatus);
		}

		[Wired]
		public void SetGameMode(GameMode gameMode)
		{
			GameMode = gameMode;

			SendSetPlayerGameType();
		}


		public void SendSetPlayerGameType()
		{
			McpeSetPlayerGameType gametype = McpeSetPlayerGameType.CreateObject();
			gametype.gamemode = (int) GameMode;
			SendPackage(gametype);
		}

		[Wired]
		public void StrikeLightning()
		{
			Lightning lightning = new Lightning(Level) {KnownPosition = KnownPosition};

			if (lightning.Level == null) return;

			lightning.SpawnEntity();
		}

		private object _disconnectSync = new object();

		private bool _haveJoined = false;

		public virtual void Disconnect(string reason, bool sendDisconnect = true)
		{
			try
			{
				lock (_disconnectSync)
				{
					if (IsConnected)
					{
						if (Level != null) OnPlayerLeave(new PlayerEventArgs(this));

						if (sendDisconnect)
						{
							McpeDisconnect disconnect = McpeDisconnect.CreateObject();
							disconnect.NoBatch = true;
							disconnect.message = reason;
							NetworkHandler.SendDirectPackage(disconnect);
						}

						NetworkHandler.Close();
						NetworkHandler = null;

						IsConnected = false;
					}

					Level?.RemovePlayer(this);

					var playerSession = Session;
					Session = null;
					if (playerSession != null)
					{
						Server.SessionManager.RemoveSession(playerSession);
						playerSession.Player = null;
					}

					string levelId = Level == null ? "Unknown" : Level.LevelId;
					if (!_haveJoined)
					{
						Log.WarnFormat("Disconnected crashed player {0}/{1} from level <{3}>, reason: {2}", Username, EndPoint.Address, reason, levelId);
					}
					else
					{
						Log.Warn(string.Format("Disconnected player {0}/{1} from level <{3}>, reason: {2}", Username, EndPoint.Address, reason, levelId));
					}

					CleanCache();
				}
			}
			catch (Exception e)
			{
				Log.Error("On disconnect player", e);
				throw;
			}
		}

		private string _prevText = null;

		public virtual void HandleMcpeText(McpeText message)
		{
			string text = message.message;

			if (string.IsNullOrEmpty(text)) return;

			Level.BroadcastMessage(text, sender: this);
		}

		private int _lastPlayerMoveSequenceNUmber;
		private int _lastOrderingIndex;
		private object _moveSyncLock = new object();

		public virtual void HandleMcpeMovePlayer(McpeMovePlayer message)
		{
			if (!IsSpawned || HealthManager.IsDead) return;

			if (Server.ServerRole != ServerRole.Node)
			{
				lock (_moveSyncLock)
				{
					if (_lastPlayerMoveSequenceNUmber > message.DatagramSequenceNumber)
					{
						return;
					}
					_lastPlayerMoveSequenceNUmber = message.DatagramSequenceNumber;

					if (_lastOrderingIndex > message.OrderingIndex)
					{
						return;
					}
					_lastOrderingIndex = message.OrderingIndex;
				}
			}

			Vector3 origin = KnownPosition.ToVector3();
			double distanceTo = Vector3.Distance(origin, new Vector3(message.x, message.y/* - 1.62f*/, message.z));

			CurrentSpeed = distanceTo/((double) (DateTime.UtcNow - LastUpdatedTime).Ticks/TimeSpan.TicksPerSecond);

			double verticalMove = message.y - KnownPosition.Y;//1.62 - KnownPosition.Y;

			bool isOnGround = IsOnGround;
			bool isFlyingHorizontally = false;
			if (Math.Abs(distanceTo) > 0.01)
			{
				isOnGround = CheckOnGround(message);
				isFlyingHorizontally = DetectSimpleFly(message, isOnGround);
			}

			if (!AcceptPlayerMove(message, isOnGround, isFlyingHorizontally)) return;

			IsFlyingHorizontally = isFlyingHorizontally;
			IsOnGround = isOnGround;

			// Hunger management
			if (!IsGliding) HungerManager.Move(Vector3.Distance(new Vector3(KnownPosition.X, 0, KnownPosition.Z), new Vector3(message.x, 0, message.z)));

			KnownPosition = new PlayerLocation
			{
				X = message.x,
				Y = message.y ,//- 1.62f,
				Z = message.z,
				Pitch = message.pitch,
				Yaw = message.yaw,
				HeadYaw = message.headYaw
			};

			IsFalling = verticalMove < 0 && !IsOnGround;

			LastUpdatedTime = DateTime.UtcNow;

			var chunkPosition = new ChunkCoordinates(KnownPosition);
			if (_currentChunkPosition != chunkPosition && _currentChunkPosition.DistanceTo(chunkPosition) >= MoveRenderDistance)
			{
				MiNetServer.FastThreadPool.QueueUserWorkItem(SendChunksForKnownPosition);
			}
		}

		public double CurrentSpeed { get; private set; } = 0;

		protected virtual bool AcceptPlayerMove(McpeMovePlayer message, bool isOnGround, bool isFlyingHorizontally)
		{
			return true;
		}

		protected virtual bool DetectSimpleFly(McpeMovePlayer message, bool isOnGround)
		{
			double d = Math.Abs(KnownPosition.Y - (message.y - 1.62f));
			return !(AllowFly || IsOnGround || isOnGround || d > 0.001);
		}

		private static readonly int[] Layers = {-1, 0};
		private static readonly int[] Arounds = {0, 1, -1};

		public bool CheckOnGround(McpeMovePlayer message)
		{
			if (Level == null)
				return true;

			BlockCoordinates pos = new Vector3(message.x, message.y - 1.62f, message.z);

			foreach (int layer in Layers)
			{
				foreach (int x in Arounds)
				{
					foreach (int z in Arounds)
					{
						var offset = new BlockCoordinates(x, layer, z);
						Block block = Level.GetBlock(pos + offset);
						if (block.IsSolid)
						{
							//Level.SetBlock(new GoldBlock() {Coordinates = block.Coordinates});
							return true;
						}
					}
				}
			}

			return false;
		}

		public void HandleMcpeLevelSoundEvent(McpeLevelSoundEvent message)
		{
			McpeLevelSoundEvent sound = McpeLevelSoundEvent.CreateObject();
			sound.soundId = message.soundId;
			sound.position = message.position;
			sound.blockId = message.blockId;
			sound.entityType = message.entityType;
			sound.isBabyMob = message.isBabyMob;
			sound.isGlobal = message.isGlobal;
			Level.RelayBroadcast(sound);
		}

		public virtual void HandleMcpeMobArmorEquipment(McpeMobArmorEquipment message)
		{
		}

		public virtual void HandleMcpeItemFrameDropItem(McpeItemFrameDropItem message)
		{
			Log.Warn($"Player {Username} drops item frame at {message.coordinates}");
		}

		public virtual void HandleMcpeMobEquipment(McpeMobEquipment message)
		{
			//Log.Warn($"!!! MOB EQUIPMENT ({message.selectedSlot}) !!!");
			if (HealthManager.IsDead) return;

			lock (_inventorySync)
			{
				Inventory.InHandSlot = message.selectedSlot;
				Inventory.SetHeldItemSlot(message.selectedSlot, false);
				/*	Item itemStack = message.item;
					if (GameMode != GameMode.Creative && itemStack != null && !VerifyItemStack(itemStack))
					{
						Log.Error($"Kicked {Username} for equipment hacking.");
						Disconnect("Error #376. Please report this error.");
					}
	
					byte selectedHotbarSlot = message.selectedSlot;
					int selectedInventorySlot = (byte) (message.slot - PlayerInventory.HotbarSize);
	
					if (Log.IsDebugEnabled) Log.Debug($"Player {Username} called set equiptment with inv slot: {selectedInventorySlot}({message.slot}) and hotbar slot {message.selectedSlot} with item {message.item}");
	
					// 255 indicates empty hmmm
					if (selectedInventorySlot < 0 || (message.slot != 255 && selectedInventorySlot >= Inventory.Slots.Length))
					{
						if (GameMode != GameMode.Creative)
						{
							Log.Error($"Player {Username} set equiptment fails with inv slot: {selectedInventorySlot}({message.slot}) and hotbar slot {selectedHotbarSlot} for inventory size: {Inventory.Slots.Length} and Item ID: {message.item?.Id}");
						}
						return;
					}
	
					if (message.slot == 255)
					{
						//Inventory.ItemHotbar[selectedHotbarSlot] = -1;
						//return;
						selectedInventorySlot = -1;
					}
					else
					{
						for (int i = 0; i < Inventory.ItemHotbar.Length; i++)
						{
							if (Inventory.ItemHotbar[i] == selectedInventorySlot)
							{
								Inventory.ItemHotbar[i] = Inventory.ItemHotbar[selectedHotbarSlot];
								break;
							}
						}
					}
	
					Inventory.ItemHotbar[selectedHotbarSlot] = selectedInventorySlot;
					Inventory.SetHeldItemSlot(selectedHotbarSlot, false);
	
					if (Log.IsDebugEnabled) Log.Debug($"Player {Username} set equiptment with inv slot: {selectedInventorySlot}({message.slot}) and hotbar slot {selectedHotbarSlot}");*/
			}
		}


		private object _inventorySync = new object();

		public void OpenInventory(BlockCoordinates inventoryCoord)
		{
			lock (_inventorySync)
			{
				if (_openInventory != null)
				{
					if (_openInventory.Coordinates.Equals(inventoryCoord)) return;
					HandleMcpeContainerClose(null);
				}

				// get inventory from coordinates
				// - get blockentity
				// - get inventory from block entity

				Inventory inventory = Level.InventoryManager.GetInventory(inventoryCoord);

				if (inventory == null)
				{
					Log.Warn($"No inventory found at {inventoryCoord}");
					return;
				}

				// get inventory # from inventory manager
				// set inventory as active on player

				_openInventory = inventory;

				if (inventory.Type == 0 && !inventory.IsOpen()) // Chest open animation
				{
					var tileEvent = McpeBlockEvent.CreateObject();
					tileEvent.coordinates = inventoryCoord;
					tileEvent.case1 = 1;
					tileEvent.case2 = 2;
					Level.RelayBroadcast(tileEvent);
				}

				// subscribe to inventory changes
				inventory.InventoryChange += OnInventoryChange;
				inventory.AddObserver(this);

				// open inventory

				var containerOpen = McpeContainerOpen.CreateObject();
				containerOpen.windowId = inventory.WindowsId;
				containerOpen.type = inventory.Type;
				containerOpen.coordinates = inventoryCoord;
				containerOpen.unknownRuntimeEntityId = 1;
				SendPackage(containerOpen);

				McpeInventoryContent containerSetContent = McpeInventoryContent.CreateObject();
				containerSetContent.inventoryId = inventory.WindowsId;
				containerSetContent.input = inventory.Slots;
				SendPackage(containerSetContent);
			}
		}

		private void OnInventoryChange(Player player, Inventory inventory, byte slot, Item itemStack)
		{
			if (player == this)
			{
				//TODO: This needs to be synced to work properly under heavy load (SG).
				//Level.SetBlockEntity(inventory.BlockEntity, false);
			}
			else
			{
				McpeInventorySlot sendSlot = McpeInventorySlot.CreateObject();
				sendSlot.inventoryId = inventory.WindowsId;
				sendSlot.slot = slot;
				sendSlot.item = itemStack;
				SendPackage(sendSlot);
			}

			//if(inventory.BlockEntity != null)
			//{
			//	Level.SetBlockEntity(inventory.BlockEntity, false);
			//}
		}


		public void HandleMcpeInventorySlot(McpeInventorySlot message)
		{
			Log.Warn("MCPEInventorySlot!");
		}

		public virtual void HandleMcpeCraftingEvent(McpeCraftingEvent message)
		{
			Log.Debug($"Player {Username} crafted item on window 0x{message.windowId:X2} on type: {message.recipeType}");
		}

		public virtual void HandleMcpeInventoryTransaction(McpeInventoryTransaction message)
		{
			if (HealthManager.IsDead) return;
			var transaction = message.transaction;
			
			McpeInventoryTransaction.TransactionTypes transactionType =
				(McpeInventoryTransaction.TransactionTypes)transaction.TransactionType;

			switch (transactionType)
			{
				case McpeInventoryTransaction.TransactionTypes.Normal:
					HandleInventoryNormal(transaction);
					break;
				case McpeInventoryTransaction.TransactionTypes.ItemUse:
					HandleItemUse(transaction);
					break;
				case McpeInventoryTransaction.TransactionTypes.ItemUseOnEntity:
					HandleItemUseOnEntity(transaction);
					break;
				case McpeInventoryTransaction.TransactionTypes.InventoryMismatch:
					SendPlayerInventory();
					break;
				case McpeInventoryTransaction.TransactionTypes.ItemRelease:
					HandleItemRelease(transaction);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected virtual void HandleItemRelease(Transaction transaction)
		{
			var itemInHand = Inventory.GetItemInHand();
			switch ((McpeInventoryTransaction.ItemReleaseActions)transaction.ActionType)
			{
				case McpeInventoryTransaction.ItemReleaseActions.Release:
					if (itemInHand == null) return;

					if (GameMode != GameMode.Creative && !VerifyItemStack(transaction.Item))
					{
						Log.Error($"Kicked {Username} for equipment hacking.");
						Disconnect("Error #376. Please report this error.");
						return;
					}

					if (_itemUseTimer <= 0) return;

					itemInHand.Release(Level, this, transaction.Position, Level.TickTime - _itemUseTimer);

					_itemUseTimer = 0;
					break;
				case McpeInventoryTransaction.ItemReleaseActions.Use:
					Log.Warn("Use item detected in Release Item... ?");
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		protected virtual void HandleItemUseOnEntity(Transaction transaction)
		{
			var entity = Level.GetEntity(transaction.EntityId);
			if (entity == null || !entity.IsSpawned || entity.HealthManager.IsDead || entity.HealthManager.IsInvulnerable)
				return;

			switch ((McpeInventoryTransaction.ItemUseOnEntityAction)transaction.ActionType)
			{
				case McpeInventoryTransaction.ItemUseOnEntityAction.Interact:
					DoInteraction((byte) transaction.ActionType, this);
					entity.DoInteraction((byte) transaction.ActionType, this);
					break;
				case McpeInventoryTransaction.ItemUseOnEntityAction.Attack:
					entity.HealthManager.TakeHit(this, this.Inventory.GetItemInHand(), CalculateDamage(entity), DamageCause.EntityAttack);
					break;
				case McpeInventoryTransaction.ItemUseOnEntityAction.Hoverover:
					DoMouseOverInteraction((byte)transaction.ActionType, this);
					entity.DoMouseOverInteraction((byte) transaction.ActionType, this);
					break;
				default:
					Log.Warn("Unknown ItemUseOnEntity: " + transaction.ActionType);
					break;
			}
		}

		private void KickEquipmentHacking()
		{
			Log.Error($"Kicked {Username} for equipment hacking.");
			Disconnect("Error #377. Please report this error.");
			return;
		}

		private long _itemUseTimer;
		private int _equipmentHackCount = 0;
		protected virtual void HandleItemUse(Transaction transaction)
		{
			var itemInHand = Inventory.GetItemInHand();
			if (itemInHand == null) return;
			if (GameMode != GameMode.Creative &&
				((transaction.Item.Id != itemInHand.Id || itemInHand.Metadata != transaction.Item.Metadata) 
				&& transaction.ActionType != (int) McpeInventoryTransaction.ItemUseAction.Destroy /*||itemInHand.Count != transaction.Item.Count*/)) 
				//If the transaction & item in-hand do not match, re-send inventory. If this happens to often, kick them for item-hacking.
			{
				Log.Warn($"Possible equipment hacking (Hand: {itemInHand} | Transaction: {transaction.Item}) ");
				_equipmentHackCount++;
				if (_equipmentHackCount == 3)
				{
					KickEquipmentHacking();
				}
				else if (_equipmentHackCount < 3)
				{
					SendPlayerInventory();
				}
				return;
			}

			_equipmentHackCount = 0;
			switch ((McpeInventoryTransaction.ItemUseAction) transaction.ActionType)
			{
				case McpeInventoryTransaction.ItemUseAction.Place:
				case McpeInventoryTransaction.ItemUseAction.Use:
					if (itemInHand.GetType() == typeof(Item))
					{
						Log.Warn(
							$"Generic item in hand when placing block. Can not complete request. Expected item {transaction.Item} and item in hand is {itemInHand}");
					}

					if (transaction.Face >= 0 && transaction.Face <= 5)
					{
						Level.Interact(this, itemInHand, transaction.Position, (BlockFace) transaction.Face, transaction.FromPosition);
					}
					else
					{
						Log.Debug($"Begin non-block action with {itemInHand}");

						// Snowballs and shit

						_itemUseTimer = Level.TickTime;

						itemInHand.UseItem(Level, this, transaction.Position);

						IsUsingItem = true;
						var metadata = new MetadataDictionary
						{
							[0] = GetDataValue()
						};

						var setEntityData = McpeSetEntityData.CreateObject();
						setEntityData.runtimeEntityId = EntityId;
						setEntityData.metadata = metadata;
						Level.RelayBroadcast(this, setEntityData);

					}
					break;
					//Level.Interact(this, itemInHand, transaction.Position, (BlockFace)transaction.Face, transaction.ClickPosition);
					break;
				case McpeInventoryTransaction.ItemUseAction.Destroy:

					Block block = Level.GetBlock(transaction.Position);
					if (!CanBreakBlock(block, itemInHand))
					{
						McpeUpdateBlock revertUpdateBlock = McpeUpdateBlock.CreateObject();
						revertUpdateBlock.blockId = block.Id;
						revertUpdateBlock.priorityAndMetadata = (uint)((8 << 4) | block.Metadata);
						revertUpdateBlock.coordinates = transaction.Position;
						SendPackage(revertUpdateBlock);
						return;
					}

					if (GameMode != GameMode.Creative)
					{
						//SendMessage($"Client: {transaction.Item.Metadata} Server: {itemInHand.Metadata}");
						Level.BreakBlock(this, transaction.Position);
						//var inHand = Inventory.GetItemInHand();

						//SendMessage("IsBreaking: " + _breakingBlock + " IsTool: " + (inHand.ItemType & ItemType.AnyTool));

						if ((itemInHand.ItemType & ItemType.AnyTool) != 0 && transaction.Item.Metadata != itemInHand.Metadata)
						{
							if (itemInHand.Metadata - 1 > transaction.Item.Metadata)
							{
							//	SendMessage($"Client metadata missmatch! Client: {transaction.Item.Metadata} Server: {itemInHand.Metadata}");
								Inventory.SendSetSlot(Inventory.InHandSlot);
							}
							else if (itemInHand.Metadata - 1 < transaction.Item.Metadata)
							{
							//	SendMessage($"Server metadata missmatch! Client: {transaction.Item.Metadata} Server: {itemInHand.Metadata}");
								Inventory.SendSetSlot(Inventory.InHandSlot);
							}
							//SendMessage($"Metadata missmatch! Client: {transaction.Item.Metadata} Server: {inHand.Metadata}");
						}
					}
					else if (GameMode == GameMode.Creative)
					{
						Level.BreakBlock(this, transaction.Position);
					}
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		private List<Item> _craftingInput = new List<Item>(new Item[9]);
		public bool UsingCraftingTable { get; set; }
		protected virtual void HandleInventoryNormal(Transaction transaction)
		{
			foreach (var trans in transaction.Transactions)
			{
				//Log.Warn($"\tSlot: {trans.Slot}/{36 + trans.Slot}\n\tOldItem: {trans.OldItem}\n\tNewItem: {trans.NewItem}\n\tTransaction slot: {transaction.Slot}\n");
				int inventoryId = 0;
				//Type transactionType = trans.GetType();
				if (trans is ContainerTransactionRecord containerrec)
				{
					inventoryId = containerrec.InventoryId;
				}
				else if (trans is CreativeTransactionRecord creativerec)
				{
					inventoryId = creativerec.InventoryId;
				}
				else if (trans is CraftTransactionRecord rec)
				{
					int invId = rec.Action;
					int slot = rec.Slot;
					Item oldItem = rec.OldItem;
					Item newItem = rec.NewItem;

					switch (invId)
					{
						case 3: //Put Slot

							break;
						case 5: //Get Slot

							break;
						case 7: //Get Result
							if (VerifyRecipe(_craftingInput, oldItem))
							{

							}
							else
							{

							}

							_craftingInput.Clear();
							_craftingInput.AddRange(new Item[9]);
							break;
						case 9: //Craft use
							_craftingInput[slot] = newItem;
							break;

						case 29: //Enchant item
							break;
						case 31: //Enchant Lapis
							break;
						case 33: //Enchant Result
							break;

						case 199: //Drop Item

							break;
						default:
							Log.Warn("Unknown CraftTransaction.Action: " + rec.Action);
							break;
					}
					continue;
				}
				else if (trans is WorldInteractionTransactionRecord interact)
				{
					Item droppedItem = Inventory.GetItemInHand();
					droppedItem.Count = interact.NewItem.Count;

					//Log.Info("New Item: " + interact.NewItem);
					//Log.Info("Old Item: " + interact.OldItem);
					DropItem(droppedItem, interact.OldItem);
					
					continue;
				}
				else if (trans is GlobalTransactionRecord i)
				{
					Log.Warn("Global transaction!");
					continue;
				}

			//	Log.Warn($"\tInventory ID: 0x{inventoryId:X}");

				//var signed = ItemSigner.DefaultItemSigner.SignItem(trans.NewItem);
				if (inventoryId == -1) //None
				{
					Log.Warn("HandleInventory: got inventory id -1");
					continue;	
				}
				else if (inventoryId == 0) //Survival
				{
					Inventory.UpdateInventorySlot(trans.Slot, trans.NewItem);
					Inventory.CursorItem = trans.OldItem;
					Inventory.Cursor = trans.Slot;

					if (trans.OldItem.Id == 0)
					{
						Inventory.Cursor = -1;
					}
					//Inventory.Cursor = trans.Slot;
					continue;
				}
				else if (inventoryId == 0x77) //OffHand
				{
					Log.Warn("HandleInventory: Got offhand");
					continue;
				}
				else if (inventoryId == 0x78) //Armor
				{
					var armorItem = trans.NewItem;
					switch (trans.Slot)
					{
						case 0:
							Inventory.Helmet = armorItem;
							break;
						case 1:
							Inventory.Chest = armorItem;
							break;
						case 2:
							Inventory.Leggings = armorItem;
							break;
						case 3:
							Inventory.Boots = armorItem;
							break;
					}

					McpeMobArmorEquipment armorEquipment = McpeMobArmorEquipment.CreateObject();
					armorEquipment.runtimeEntityId = EntityId;
					armorEquipment.helmet = Inventory.Helmet;
					armorEquipment.chestplate = Inventory.Chest;
					armorEquipment.leggings = Inventory.Leggings;
					armorEquipment.boots = Inventory.Boots;
					Level.RelayBroadcast(this, armorEquipment);
					continue;
				}
				else if (inventoryId == 0x79) //Creative
				{
					if (trans.Slot == 1)
					{
						Inventory.CursorItem = trans.OldItem;
						Inventory.Cursor = -1;
					}
					//Inventory.SetInventorySlot(trans.Slot, trans.NewItem);
					continue;
					//slot = -2;
				}
				else if (inventoryId == 0x7C) //Cursor Selected
				{
					Inventory.CursorItem = trans.NewItem;
					if (trans.NewItem.Id == 0)
					{
						Inventory.Cursor = -1;
					}
				//	Log.Warn("\n\t\tCursor selected!!");
					continue;
				}
				else
				{
					if (_openInventory != null)
					{
						if (_openInventory.WindowsId == inventoryId)
						{
							if (_openInventory.Type == 3)
							{
								Recipes recipes = new Recipes();
								recipes.Add(new EnchantingRecipe());
								McpeCraftingData crafting = McpeCraftingData.CreateObject();
								crafting.recipes = recipes;
								SendPackage(crafting);
							}

							Log.Warn("OPEN INVENTORY!!!");
							// block inventories of various kinds (chests, furnace, etc)
							_openInventory.SetSlot(this, (byte) trans.Slot, trans.NewItem);
							continue;
						}
					}
				}
			}
		}

		private bool VerifyRecipe(List<Item> craftingInput, Item result)
		{
			List<Item> shapedInput = new List<Item>();
			foreach (var item in craftingInput)
			{
				shapedInput.Add(item ?? new Item(0));
			}

			List<Item> shapelessInput = new List<Item>();
			foreach (var item in craftingInput)
			{
				if (item == null) continue;
				shapelessInput.Add(item);
			}

		//	Log.Debug($"Looking for matching recipes with the result {result}");
			var recipes = RecipeManager.Recipes.Where(r => r is ShapedRecipe).Where(r => ((ShapedRecipe)r).Result.Id == result.Id && ((ShapedRecipe)r).Result.Metadata == result.Metadata).ToList();
			recipes.AddRange(RecipeManager.Recipes.Where(r => r is ShapelessRecipe).Where(r => ((ShapelessRecipe)r).Result.Id == result.Id && ((ShapelessRecipe)r).Result.Metadata == result.Metadata).ToList());
			//Log.Debug($"Found {recipes.Count} matching recipes with the result {result}");
			foreach (var r in recipes)
			{
				if (r is ShapedRecipe)
				{
					var recipe = (ShapedRecipe)r;
					int rowOffset = -1;
					int colOffset = -1;
					int dim = UsingCraftingTable ? 3 : 2;
					for (int row = 0; row < dim; row++)
					{
						for (int col = 0; col < dim; col++)
						{
							var item = craftingInput[col + (dim * row)];
							if (item == null) continue;

							if (rowOffset == -1 && item.Id != 0)
							{
								rowOffset = row;
							}
							if (colOffset == -1 && item.Id != 0)
							{
								colOffset = col;
							}
						}
					}

					List<Item> shapedInputSmall = new List<Item>(new Item[recipe.Height * recipe.Width]);
					for (int row = 0; row < recipe.Height; row++)
					{
						for (int col = 0; col < recipe.Width; col++)
						{
							shapedInputSmall[col + (recipe.Width * row)] = craftingInput[(colOffset + col) + (dim * (row + rowOffset))];
						}
					}

				//	Log.Debug($"Items input={ToJson(craftingInput)}");
				//	Log.Debug($"Items input={ToJson(shapedInputSmall)}");
				//	Log.Debug($"Recipe input={ToJson(recipe.Input)}");

					var match = (recipe.Input.Length == shapedInputSmall.Count);
				//	Log.Debug($"ShapedRecipe Count match={match}");
					match = match && !shapedInputSmall.Except(recipe.Input, new ItemCompare()).Any();
				//	Log.Debug($"Items match={match}");
					if (match) return true;
				}
				else
				{
					var recipe = (ShapelessRecipe)r;
					var match = (recipe.Input.Count == shapelessInput.Count);
				//	Log.Debug($"ShapelessRecipe Count match={match}");
					match = match && !shapelessInput.Except(recipe.Input, new ItemCompare()).Any();
				//	Log.Debug($"Items match={match}");
					if (match) return true;
				}
			}

			return false;
		}

		private class ItemCompare : IEqualityComparer<Item>
		{
			public bool Equals(Item x, Item y)
			{
				if (ReferenceEquals(null, x)) return false;
				if (ReferenceEquals(null, y)) return false;
				if (ReferenceEquals(x, y)) return true;

				return x.Id == y.Id && (x.Metadata == y.Metadata || x.Metadata == -1 || y.Metadata == -1);
			}

			public int GetHashCode(Item obj)
			{
				return 0;
			}
		}

		protected virtual void DropItem(Item droppedItem, Item newInventoryItem)
		{
			Inventory.UpdateInventorySlot(Inventory.InHandSlot, newInventoryItem);

			ItemEntity itemEntity = new ItemEntity(Level, droppedItem)
			{
				Velocity = KnownPosition.GetDirection() * 0.7f,
				KnownPosition =
				{
					X = KnownPosition.X,
					Y = KnownPosition.Y + 1.62f,
					Z = KnownPosition.Z
				},
			};

			itemEntity.SpawnEntity();
		}

		public virtual bool VerifyItemStack(Item itemStack)
		{
			if (ItemSigner.DefaultItemSigner == null) return true;

			return ItemSigner.DefaultItemSigner.VerifyItemStack(this, itemStack);
		}

		public virtual void HandleMcpeContainerClose(McpeContainerClose message)
		{
			lock (_inventorySync)
			{
				var inventory = _openInventory;
				_openInventory = null;

				if (inventory == null) return;

				// unsubscribe to inventory changes
				inventory.InventoryChange -= OnInventoryChange;
				inventory.RemoveObserver(this);

				if (message != null && message.windowId != inventory.WindowsId) return;

				// close container 
				if (inventory.Type == 0 && !inventory.IsOpen())
				{
					var tileEvent = McpeBlockEvent.CreateObject();
					tileEvent.coordinates = inventory.Coordinates;
					tileEvent.case1 = 1;
					tileEvent.case2 = 0;
					Level.RelayBroadcast(tileEvent);
				}
			}
		}

		public virtual void HandleMcpeSetPlayerGameType(McpeSetPlayerGameType message)
		{
			if (PermissionLevel >= UserPermission.Operator || ActionPermissions.HasFlag(Actionpermissions.Operator)) //If operator, change our gamemode
			{
				var newGamemode = (GameMode) message.gamemode;

				if (newGamemode != GameMode)
				{
					//Log.Info($"Player {Username}'s gamemode has been updated from {GameMode} to {newGamemode}");
					SetGameMode(newGamemode);
				}
			}
			else
			{
				Log.Warn($"Player {Username} tried changing gamemode without the correct permissions!");
			}
		}

		public virtual void HandleMcpeSetDefaultGamemode(McpeSetDefaultGamemode message)
		{
			if (PermissionLevel >= UserPermission.Operator || ActionPermissions.HasFlag(Actionpermissions.Operator)) //If operator, change our gamemode
			{
				var newGamemode = (GameMode) message.gamemode;

				if (newGamemode != Level.GameMode)
				{
					SendMessage("Default gamemode updated to: " + newGamemode);
					Level.SetDefaultGamemode(newGamemode);
				}
			}
			else
			{
				Log.Warn($"Player {Username} tried changing the levels default gamemode without the correct permissions!");
			}
		}

		public void HandleMcpePlayerHotbar(McpePlayerHotbar message)
		{
			//Log.Error("MCPEPLAYERHOTBAR Not implemented!");
			Log.Warn($"PlayerHotbar:\n\tSelected hotbar slot: {message.selectedHotbarSlot}\n\tSelect hotbar slot: {message.selectHotbarSlot}");
			if (message.selectHotbarSlot && message.selectedHotbarSlot != Inventory.SelectedHotbarSlot)
			{
				Inventory.SetHeldItemSlot((int) message.selectedHotbarSlot);
			}
		}

		public void HandleMcpeInventoryContent(McpeInventoryContent message)
		{
			Log.Warn($"InventoryContent: InventoryID: {message.inventoryId} | Data: {message.input}");
		}

		/// <summary>
		///     Handles the interact.
		/// </summary>
		/// <param name="message">The message.</param>
		public virtual void HandleMcpeInteract(McpeInteract message)
		{
			Entity target = Level.GetEntity(message.targetRuntimeEntityId);

			if (message.actionId != 4)
			{
				Log.DebugFormat("Interact Action ID: {0}", message.actionId);
				Log.DebugFormat("Interact Target Entity ID: {0}", message.targetRuntimeEntityId);
			}

			if (target == null) return;
			switch (message.actionId)
			{
				case 1:
				{
					// Button pressed

					//McpeAnimate animate = McpeAnimate.CreateObject();
					//animate.entityId = target.EntityId;
					//animate.actionId = 4;
					//Level.RelayBroadcast(animate);

					DoInteraction(message.actionId, this);
					target.DoInteraction(message.actionId, this);
					break;
				}
				case 4:
				{
					// Mouse over
					//McpeAnimate animate = McpeAnimate.CreateObject();
					//animate.entityId = target.EntityId;
					//animate.actionId = 4;
					//Level.RelayBroadcast(animate);

					DoMouseOverInteraction(message.actionId, this);
					target.DoMouseOverInteraction(message.actionId, this);
					break;
				}
			}


			// Old code...
			if (message.actionId != 2) return;

			Item itemInHand = Inventory.GetItemInHand();

			LastAttackTarget = target;

			Player player = target as Player;
			if (player != null)
			{
				double damage = DamageCalculator.CalculateItemDamage(this, itemInHand, player);

				if (IsFalling)
				{
					damage += DamageCalculator.CalculateFallDamage(this, damage, player);
				}

				damage += DamageCalculator.CalculateEffectDamage(this, damage, player);

				if (damage < 0) damage = 0;

				damage += DamageCalculator.CalculateDamageIncreaseFromEnchantments(this, itemInHand, player);

				player.HealthManager.TakeHit(this, itemInHand, (int) DamageCalculator.CalculatePlayerDamage(this, player, itemInHand, damage, DamageCause.EntityAttack), DamageCause.EntityAttack);
				var fireAspectLevel = itemInHand.GetEnchantingLevel(EnchantingType.FireAspect);
				if (fireAspectLevel > 0)
				{
					player.HealthManager.Ignite(fireAspectLevel*80);
				}
			}
			else
			{
				// This is totally wrong. Need to merge with the above damage calculation
				target.HealthManager.TakeHit(this, itemInHand, CalculateDamage(target), DamageCause.EntityAttack);
			}

			HungerManager.IncreaseExhaustion(0.3f);
		}

		public virtual void HandleMcpeBlockPickRequest(McpeBlockPickRequest message)
		{
			if (GameMode != GameMode.Creative) return;
			Block b = Level.GetBlock(message.x, message.y, message.z);

			int availableSlot = -1;
			for (int i = 0;
				i < 9;
				i++) //Check if we already have the item in our inventory. And if not check for an available slot.
			{
				var s = Inventory.Slots[i];
				if (s is ItemAir)
				{
					availableSlot = i;
				}
				else if (s.Id == b.Id)
				{
					Inventory.SetHeldItemSlot(i, true);
					return;
				}
			}

			if (Inventory.GetItemInHand() is ItemAir || availableSlot == -1)
			{
				var s = Inventory.SelectedHotbarSlot;
				Inventory.SetInventorySlot(s, new ItemBlock(b, b.Metadata));
				Inventory.SetHeldItemSlot(s, true);
				return;
			}
			else
			{
				Inventory.SetInventorySlot(availableSlot, new ItemBlock(b, b.Metadata));
				Inventory.SetHeldItemSlot(availableSlot, true);
				return;
			}
		}

		protected virtual int CalculateDamage(Entity target)
		{
			int damage = Inventory.GetItemInHand().GetDamage(); //Item Damage.

			damage = (int) Math.Floor(damage*(1.0));

			return damage;
		}


		public virtual void HandleMcpeEntityEvent(McpeEntityEvent message)
		{
			Log.Debug("Entity Id:" + message.runtimeEntityId);
			Log.Debug("Entity Event:" + message.eventId);
			Log.Debug("Entity Event:" + message.unknown);

			//if (message.eventId != 0) return; // Should probably broadcast?!

			switch (message.eventId)
			{
				case 9:
					// Eat food

					if (GameMode == GameMode.Survival)
					{
						Item itemInHand = Inventory.GetItemInHand();

						if (itemInHand is FoodItem)
						{
							FoodItem foodItem = (FoodItem) Inventory.GetItemInHand();
							foodItem.Consume(this);
							foodItem.Count--;
							SendPlayerInventory();
						}
						else if (itemInHand is ItemPotion)
						{
							ItemPotion potion = (ItemPotion) Inventory.GetItemInHand();
							potion.Consume(this);
							potion.Count--;
							SendPlayerInventory();
						}
					}
					break;
				case 34:
					RemoveExperienceLevels(message.unknown);
					break;
			}
		}

		public void SendRespawn()
		{
			McpeRespawn mcpeRespawn = McpeRespawn.CreateObject();
			mcpeRespawn.x = SpawnPosition.X;
			mcpeRespawn.y = SpawnPosition.Y;
			mcpeRespawn.z = SpawnPosition.Z;
			SendPackage(mcpeRespawn);
		}

		public void SendStartGame()
		{
			McpeStartGame mcpeStartGame = McpeStartGame.CreateObject();
			mcpeStartGame.entityIdSelf = EntityId;
			mcpeStartGame.runtimeEntityId = EntityManager.EntityIdSelf;
			mcpeStartGame.playerGamemode = (int) GameMode;
			mcpeStartGame.spawn = SpawnPosition;
			mcpeStartGame.unknown1 = new Vector2(KnownPosition.HeadYaw, KnownPosition.Pitch);
			mcpeStartGame.seed = 12345;
			mcpeStartGame.dimension = 0;
			mcpeStartGame.generator = 1;
			mcpeStartGame.gamemode = (int) GameMode;
			mcpeStartGame.x = (int) SpawnPosition.X;
			mcpeStartGame.y = (int) (SpawnPosition.Y + Height);
			mcpeStartGame.z = (int) SpawnPosition.Z;
			//mcpeStartGame.hasAchievementsDisabled = GameMode == GameMode.Creative || EnableCommands;
			mcpeStartGame.hasAchievementsDisabled = true;
			mcpeStartGame.dayCycleStopTime = (int)Level.CurrentWorldTime;
			mcpeStartGame.eduMode = PlayerInfo.Edition == 1;
			mcpeStartGame.rainLevel = 0;
			mcpeStartGame.lightnigLevel = 0;
			mcpeStartGame.broadcastToXbl = true;
			mcpeStartGame.broadcastToLan = true;
			mcpeStartGame.enableCommands = EnableCommands;
			mcpeStartGame.isTexturepacksRequired = false;
			mcpeStartGame.gamerules = GetGameRules();
			mcpeStartGame.levelId = Guid.NewGuid().ToString();
			mcpeStartGame.worldName = Level.LevelName;
			mcpeStartGame.gamePublishSetting = 4;
			mcpeStartGame.permissionLevel = (int) PermissionLevel;
			mcpeStartGame.bonusChest = false;
			mcpeStartGame.isTrial = false;
			mcpeStartGame.currentLevelTime = Level.CurrentWorldTime;
			mcpeStartGame.enchantmentSeed = 0;
			mcpeStartGame.isMultiplayer = true;
			mcpeStartGame.premiumWorldTemplateId = "";
			//mcpeStartGame.uuid = ClientUuid.ToString();

			mcpeStartGame.startWithMap = false;
			mcpeStartGame.trustPlayers = true;//PermissionLevel == UserPermission.Operator;

			SendPackage(mcpeStartGame);
		}

		public virtual void SendGameRules()
		{
			McpeGameRulesChanged gameRulesChanged = McpeGameRulesChanged.CreateObject();
			gameRulesChanged.rules = GetGameRules();
			SendPackage(gameRulesChanged);
		}

		public virtual GameRules GetGameRules()
		{
			GameRules rules = new GameRules();
			rules.Add("drowningdamage", new GameRule<bool>(!HealthManager.IsInvulnerable));
			rules.Add("dotiledrops", new GameRule<bool>(true));
			rules.Add("commandblockoutput", new GameRule<bool>(true));
			rules.Add("domobloot", new GameRule<bool>(Level.MobLooting));
			rules.Add("dodaylightcycle", new GameRule<bool>(Level.IsWorldTimeStarted));
			rules.Add("keepinventory", new GameRule<bool>(KeepInventory));
			rules.Add("domobspawning", new GameRule<bool>(Level.EnableChunkTicking));
			rules.Add("doentitydrops", new GameRule<bool>(true));
			rules.Add("dofiretick", new GameRule<bool>(Level.EnableBlockTicking));
			rules.Add("doweathercycle", new GameRule<bool>(false));
			rules.Add("falldamage", new GameRule<bool>(DoFallDamage));
			rules.Add("pvp", new GameRule<bool>(IsNoPvp));
			rules.Add("firedamage", new GameRule<bool>(!HealthManager.IsInvulnerable));
			rules.Add("mobgriefing", new GameRule<bool>(Level.MobGriefing));
			rules.Add("sendcommandfeedback", new GameRule<bool>(true));
			rules.Add("showcoordinates", new GameRule<bool>(_showCoordinates));
			return rules;
		}

		public bool KeepInventory = Config.GetProperty("KeepInventory", false);

		[Wired]
		public void SetKeepInventory(bool value)
		{
			KeepInventory = value;
			SendGameRules();
		}

		private bool _showCoordinates = true;

		[Wired]
		public bool ShowCoordinates
		{
			get { return _showCoordinates; }
			set
			{
				_showCoordinates = value;
				SendGameRules();
			}
		}

		/// <summary>
		///     Sends the set spawn position packet.
		/// </summary>
		public void SendSetSpawnPosition()
		{
			McpeSetSpawnPosition mcpeSetSpawnPosition = McpeSetSpawnPosition.CreateObject();
			mcpeSetSpawnPosition.spawnType = 1;
			mcpeSetSpawnPosition.coordinates = (BlockCoordinates) SpawnPosition;
			SendPackage(mcpeSetSpawnPosition);
		}

		private object _sendChunkSync = new object();

		private void ForcedSendChunk(PlayerLocation position)
		{
			lock (_sendChunkSync)
			{
				var chunkPosition = new ChunkCoordinates(position);

				McpeWrapper chunk = Level.GetChunk(chunkPosition)?.GetBatch();
				var key = new Tuple<int, int>(chunkPosition.X, chunkPosition.Z);
				if (!_chunksUsed.ContainsKey(key))
				{
					_chunksUsed.Add(key, chunk);
				}

				if (chunk != null)
				{
					SendPackage(chunk);
				}
			}
		}

		private void ForcedSendEmptyChunks()
		{
			Monitor.Enter(_sendChunkSync);
			try
			{
				var chunkPosition = new ChunkCoordinates(KnownPosition);

				_currentChunkPosition = chunkPosition;

				if (Level == null) return;

				for (int x = -1; x <= 1; x++)
				{
					for (int z = -1; z <= 1; z++)
					{
						McpeFullChunkData chunk = new McpeFullChunkData();
						chunk.chunkX = chunkPosition.X + x;
						chunk.chunkZ = chunkPosition.Z + z;
						chunk.chunkData = new byte[0];
						SendPackage(chunk);
					}
				}
			}
			finally
			{
				Monitor.Exit(_sendChunkSync);
			}
		}

		public void ForcedSendChunks(Action postAction = null)
		{
			Monitor.Enter(_sendChunkSync);
			try
			{
				var chunkPosition = new ChunkCoordinates(KnownPosition);

				_currentChunkPosition = chunkPosition;

				if (Level == null) return;

				int packetCount = 0;
				foreach (McpeWrapper chunk in Level.GenerateChunks(_currentChunkPosition, _chunksUsed, ChunkRadius))
				{
					if (chunk != null) SendPackage(chunk);

					//if (packetCount > 16) Thread.Sleep(12);

					packetCount++;
				}
			}
			finally
			{
				Monitor.Exit(_sendChunkSync);
			}

			if (postAction != null)
			{
				postAction();
			}
		}

		private void SendChunksForKnownPosition()
		{
			if (!Monitor.TryEnter(_sendChunkSync)) return;

			Log.Debug($"Send chunks: {KnownPosition}");

			try
			{
				if (ChunkRadius <= 0) return;


				var chunkPosition = new ChunkCoordinates(KnownPosition);
				if (IsSpawned && _currentChunkPosition == chunkPosition) return;

				if (IsSpawned && _currentChunkPosition.DistanceTo(chunkPosition) < MoveRenderDistance)
				{
					return;
				}

				_currentChunkPosition = chunkPosition;

				int packetCount = 0;

				if (Level == null) return;

				foreach (McpeWrapper chunk in Level.GenerateChunks(_currentChunkPosition, _chunksUsed, ChunkRadius))
				{
					if (chunk != null) SendPackage(chunk);

					if (!IsSpawned)
					{
						if (packetCount++ == 56)
						{
							InitializePlayer();
						}
					}
					else
					{
						//if (packetCount++ > 56) Thread.Sleep(1);
					}
				}
			}
			finally
			{
				Monitor.Exit(_sendChunkSync);
			}
		}

		public virtual void SendUpdateAttributes()
		{
			var attributes = new PlayerAttributes();
			attributes["minecraft:attack_damage"] = new PlayerAttribute
			{
				Name = "minecraft:attack_damage",
				MinValue = 1,
				MaxValue = 1,
				Value = 1,
				Default = 1,
			};
			attributes["minecraft:absorption"] = new PlayerAttribute
			{
				Name = "minecraft:absorption",
				MinValue = 0,
				MaxValue = float.MaxValue,
				Value = HealthManager.Absorption,
				Default = 0,
			};
			attributes["minecraft:health"] = new PlayerAttribute
			{
				Name = "minecraft:health",
				MinValue = 0,
				MaxValue = 20,
				Value = HealthManager.Hearts,
				Default = 20,
			};
			attributes["minecraft:movement"] = new PlayerAttribute
			{
				Name = "minecraft:movement",
				MinValue = 0,
				MaxValue = 0.5f,
				Value = MovementSpeed,
				Default = MovementSpeed,
			};
			attributes["minecraft:knockback_resistance"] = new PlayerAttribute
			{
				Name = "minecraft:knockback_resistance",
				MinValue = 0,
				MaxValue = 1,
				Value = 0,
				Default = 0,
			};
			attributes["minecraft:luck"] = new PlayerAttribute
			{
				Name = "minecraft:luck",
				MinValue = -1025,
				MaxValue = 1024,
				Value = 0,
				Default = 0,
			};
			attributes["minecraft:fall_damage"] = new PlayerAttribute
			{
				Name = "minecraft:fall_damage",
				MinValue = 0,
				MaxValue = float.MaxValue,
				Value = 1,
				Default = 1,
			};
			attributes["minecraft:follow_range"] = new PlayerAttribute
			{
				Name = "minecraft:follow_range",
				MinValue = 0,
				MaxValue = 2048,
				Value = 16,
				Default = 16,
			};
			attributes["minecraft:player.experience"] = new PlayerAttribute
			{
				Name = "minecraft:player.experience",
				MinValue = 0,
				MaxValue = 1,
				Value = CalculateXp(),
				Default = 0,
			};
			attributes["minecraft:player.level"] = new PlayerAttribute
			{
				Name = "minecraft:player.level",
				MinValue = 0,
				MaxValue = 24791,
				Value = ExperienceLevel,
				Default = 0,
			};

			// Workaround, bad design.
			attributes = HungerManager.AddHungerAttributes(attributes);

			McpeUpdateAttributes attributesPackate = McpeUpdateAttributes.CreateObject();
			attributesPackate.runtimeEntityId = EntityManager.EntityIdSelf;
			attributesPackate.attributes = attributes;
			SendPackage(attributesPackate);
		}

		private float CalculateXp()
		{
			var xpToNextLevel = GetXpToNextLevel();

			return Experience/xpToNextLevel;
		}

		public void RemoveExperienceLevels(float levels)
		{
			var currentXp = CalculateXp();
			ExperienceLevel = Experience - Math.Abs(levels);
			var xpToNextLevel = GetXpToNextLevel();
			Experience = xpToNextLevel*currentXp;
		}

		public void AddExperience(float xp, bool send = true)
		{
			var xpToNextLevel = GetXpToNextLevel();

			if (xpToNextLevel - (xp + Experience) > 0)
			{
				Experience += xp;
			}
			else
			{
				ExperienceLevel++;
				AddExperience(Experience + xp - xpToNextLevel, false);
			}

			if (send) SendUpdateAttributes();
		}

		private float GetXpToNextLevel()
		{
			float xpToNextLevel = 0;
			if (ExperienceLevel >= 0 && ExperienceLevel <= 15)
			{
				xpToNextLevel = 2*ExperienceLevel + 7;
			}
			else if (ExperienceLevel > 15 && ExperienceLevel <= 30)
			{
				xpToNextLevel = 5*ExperienceLevel - 38;
			}
			else if (ExperienceLevel > 30)
			{
				xpToNextLevel = 9*ExperienceLevel - 158;
			}
			return xpToNextLevel;
		}

		public virtual void SendSetTime()
		{
			McpeSetTime message = McpeSetTime.CreateObject();
			message.time = (int) Level.CurrentWorldTime;
			//message.started = Level.IsWorldTimeStarted;
			SendPackage(message);
		}

		public virtual void SendMovePlayer(bool teleport = false)
		{
			var package = McpeMovePlayer.CreateObject();
			package.runtimeEntityId = EntityManager.EntityIdSelf;
			package.x = KnownPosition.X;
			package.y = KnownPosition.Y;
			package.z = KnownPosition.Z;
			package.yaw = KnownPosition.Yaw;
			package.headYaw = KnownPosition.HeadYaw;
			package.pitch = KnownPosition.Pitch;
			package.mode = (byte)(teleport ? 2 : 0);
			package.otherRuntimeEntityId = 0;
			package.Cause = McpeMovePlayer.TeleportCause.Unknown;
			package.onGround = IsOnGround;

			SendPackage(package);
		}

		public override void OnTick(Entity[] entities)
		{
			OnTicking(new PlayerEventArgs(this));

			/*if (IsUsingItem && _itemUseTimer + 3 <= Level.TickTime) //3 ticks after item use.
			{
				IsUsingItem = false;
				BroadcastSetEntityData();
			}*/

			if (DetectInPortal())
			{
				if (PortalDetected == Level.TickTime)
				{
					PortalDetected = -1;

					Dimension dimension = Level.Dimension == Dimension.Overworld ? Dimension.Nether : Dimension.Overworld;
					Log.Debug($"Dimension change to {dimension} from {Level.Dimension} initiated, Game mode={GameMode}");

					ThreadPool.QueueUserWorkItem(delegate
					{
						Level oldLevel = Level;

						ChangeDimension(null, null, dimension, delegate
						{
							Level nextLevel = dimension == Dimension.Overworld ? oldLevel.OverworldLevel : dimension == Dimension.Nether ? oldLevel.NetherLevel : oldLevel.TheEndLevel;
							return nextLevel;
						});
					});
				}
				else if (PortalDetected == 0)
				{
					PortalDetected = Level.TickTime + (GameMode == GameMode.Creative ? 1 : 4*20);
				}
			}
			else
			{
				if (PortalDetected != 0) Log.Debug($"Reset portal detected");
				PortalDetected = 0;
			}

			HungerManager.OnTick();

			base.OnTick(entities);

			if (LastAttackTarget != null && LastAttackTarget.HealthManager.IsDead)
			{
				LastAttackTarget = null;
			}

			foreach (var effect in Effects)
			{
				effect.Value.OnTick(this);
			}

			bool hasDisplayedPopup = false;
			bool hasDisplayedTip = false;
			lock (Popups)
			{
				// Code below is just pure magic and mystery. In short, it takes care of sorting a list of popups
				// based on priority, ticks and delays. And then makes sure that the most applicable popup and tip
				// is presented.
				// In the end it adjusts for the display times for tip (20ticks) and popup (10ticks) and sends it at
				// regular intervalls to make sure there is no blinking.
				foreach (var popup in Popups.OrderByDescending(p => p.Priority).ThenByDescending(p => p.CurrentTick))
				{
					if (popup.CurrentTick >= popup.Duration + popup.DisplayDelay)
					{
						Popups.Remove(popup);
						continue;
					}

					if (popup.CurrentTick >= popup.DisplayDelay)
					{
						// Tip is ontop
						if (popup.MessageType == MessageType.Tip && !hasDisplayedTip)
						{
							if (popup.CurrentTick <= popup.Duration + popup.DisplayDelay - 30)
								if (popup.CurrentTick%20 == 0 || popup.CurrentTick == popup.Duration + popup.DisplayDelay - 30) SendMessage(popup.Message, type: popup.MessageType);
							hasDisplayedTip = true;
						}

						// Popup is below
						if (popup.MessageType == MessageType.Popup && !hasDisplayedPopup)
						{
							if (popup.CurrentTick <= popup.Duration + popup.DisplayDelay - 30)
								if (popup.CurrentTick%20 == 0 || popup.CurrentTick == popup.Duration + popup.DisplayDelay - 30) SendMessage(popup.Message, type: popup.MessageType);
							hasDisplayedPopup = true;
						}
					}

					popup.CurrentTick++;
				}
			}

			OnTicked(new PlayerEventArgs(this));
		}

		public void AddPopup(Popup popup)
		{
			lock (Popups)
			{
				if (popup.Id == 0) popup.Id = popup.Message.GetHashCode();
				var exist = Popups.FirstOrDefault(pop => pop.Id == popup.Id);
				if (exist != null) Popups.Remove(exist);

				Popups.Add(popup);
			}
		}

		public void ClearPopups()
		{
			lock (Popups) Popups.Clear();
		}

		public override void Knockback(Vector3 velocity)
		{
			McpeSetEntityMotion motions = McpeSetEntityMotion.CreateObject();
			motions.runtimeEntityId = EntityManager.EntityIdSelf;
			motions.velocity = velocity;
			SendPackage(motions);
		}

		public string ButtonText { get; set; }

		public override MetadataDictionary GetMetadata()
		{
			var metadata = base.GetMetadata();
			metadata[0] = GetDataValue();
			metadata[4] = new MetadataString(NameTag ?? Username);
			metadata[40] = new MetadataString(ButtonText ?? string.Empty);

			//MetadataDictionary metadata = new MetadataDictionary();
			//metadata[0] = new MetadataLong(GetDataValue()); // 10000000000000011000000000000000
			//metadata[1] = new MetadataInt(1);
			//metadata[2] = new MetadataInt(0);
			//metadata[3] = new MetadataByte(0);
			//metadata[4] = new MetadataString(NameTag ?? Username);
			//metadata[5] = new MetadataLong(1);
			//metadata[7] = new MetadataShort(400);
			//metadata[8] = new MetadataInt(0);
			//metadata[9] = new MetadataByte(0);
			//metadata[27] = new MetadataByte(0);
			//metadata[28] = new MetadataInt(1);
			//metadata[29] = new MetadataIntCoordinates((int) SpawnPosition.X, (int) SpawnPosition.Y, (int) SpawnPosition.Z);
			//metadata[38] = new MetadataLong(0);
			//metadata[39] = new MetadataFloat(1f);
			//metadata[40] = new MetadataString(ButtonText ?? string.Empty);
			//metadata[41] = new MetadataLong(0);
			//metadata[44] = new MetadataShort(400);
			//metadata[45] = new MetadataInt(0);
			//metadata[46] = new MetadataByte(0);
			//metadata[47] = new MetadataInt(0);
			//metadata[53] = new MetadataFloat(0.8f);
			//metadata[54] = new MetadataFloat(1.8f);
			//metadata[56] = new MetadataVector3(10, 50, 10);
			//metadata[57] = new MetadataByte(0);
			//metadata[58] = new MetadataFloat(0f);
			//metadata[59] = new MetadataFloat(0f);

			return metadata;
		}

		[Wired]
		public void SetNoAi(bool noAi)
		{
			NoAi = noAi;

			BroadcastSetEntityData();
		}

		[Wired]
		public void SetHideNameTag(bool hideNameTag)
		{
			HideNameTag = hideNameTag;

			BroadcastSetEntityData();
		}

		[Wired]
		public void SetNameTag(string nameTag)
		{
			NameTag = nameTag;

			BroadcastSetEntityData();
		}

		[Wired]
		public void SetDisplayName(string displayName)
		{
			DisplayName = displayName;

			{
				McpePlayerList playerList = McpePlayerList.CreateObject();
				playerList.records = new PlayerRemoveRecords {this};
				Level.RelayBroadcast(Level.CreateMcpeBatch(playerList.Encode()));
				playerList.records = null;
				playerList.PutPool();
			}
			{
				McpePlayerList playerList = McpePlayerList.CreateObject();
				playerList.records = new PlayerAddRecords {this};
				Level.RelayBroadcast(Level.CreateMcpeBatch(playerList.Encode()));
				playerList.records = null;
				playerList.PutPool();
			}
		}

		[Wired]
		public void SetEffect(Effect effect)
		{
			if (Effects.ContainsKey(effect.EffectId))
			{
				effect.SendUpdate(this);
			}
			else
			{
				effect.SendAdd(this);
			}

			Effects[effect.EffectId] = effect;
		}

		[Wired]
		public void RemoveEffect(Effect effect)
		{
			if (Effects.ContainsKey(effect.EffectId))
			{
				effect.SendRemove(this);
				Effects.TryRemove(effect.EffectId, out effect);
			}
		}

		[Wired]
		public void RemoveAllEffects()
		{
			foreach (var effect in Effects)
			{
				RemoveEffect(effect.Value);
			}
		}

		public override void DespawnEntity()
		{
			IsSpawned = false;
			Level.DespawnFromAll(this);
		}

		public virtual void SendTitle(string text, TitleType type = TitleType.Title, int fadeIn = 6, int fadeOut = 6, int stayTime = 20, Player sender = null)
		{
			Level.BroadcastTitle(text, type, fadeIn, fadeOut, stayTime, sender, new[] {this});
		}

		public virtual void SendMessage(string text, MessageType type = MessageType.Chat, Player sender = null)
		{
			Level.BroadcastMessage(text, type, sender, new[] {this});
		}

		public override void BroadcastEntityEvent()
		{
			{
				var entityEvent = McpeEntityEvent.CreateObject();
				entityEvent.runtimeEntityId = EntityManager.EntityIdSelf;
				entityEvent.eventId = (byte) (HealthManager.Health <= 0 ? 3 : 2);
				SendPackage(entityEvent);
			}
			{
				var entityEvent = McpeEntityEvent.CreateObject();
				entityEvent.runtimeEntityId = EntityId;
				entityEvent.eventId = (byte) (HealthManager.Health <= 0 ? 3 : 2);
				Level.RelayBroadcast(this, entityEvent);
			}

			if (HealthManager.IsDead)
			{
				Player player = HealthManager.LastDamageSource as Player;
				BroadcastDeathMessage(player, HealthManager.LastDamageCause);
			}
		}

		public virtual void BroadcastDeathMessage(Player player, DamageCause lastDamageCause)
		{
			string deathMessage = string.Format(HealthManager.GetDescription(lastDamageCause), Username, player == null ? "" : player.Username);
			Level.BroadcastMessage(deathMessage, type: MessageType.Raw);
			Log.Debug(deathMessage);
		}

		/// <summary>
		///     Very important litle method. This does all the sending of packages for
		///     the player class. Treat with respect!
		/// </summary>
		public void SendPackage(Package package)
		{
			if (NetworkHandler == null)
			{
				package.PutPool();
			}
			else
			{
				NetworkHandler?.SendPackage(package);
			}
		}

		private object _sendMoveListSync = new object();
		private DateTime _lastMoveListSendTime = DateTime.UtcNow;

		public void SendMoveList(McpeWrapper batch, DateTime sendTime)
		{
			if (sendTime < _lastMoveListSendTime || !Monitor.TryEnter(_sendMoveListSync))
			{
				batch.PutPool();
				return;
			}

			_lastMoveListSendTime = sendTime;

			try
			{
				SendPackage(batch);
			}
			finally
			{
				Monitor.Exit(_sendMoveListSync);
			}
		}

		public void CleanCache()
		{
			lock (_sendChunkSync)
			{
				_chunksUsed.Clear();
			}
		}

		public virtual void DropInventory()
		{
			var slots = Inventory.Slots;

			Vector3 coordinates = KnownPosition.ToVector3();
			coordinates.Y += 0.5f;

			foreach (var stack in slots.ToArray())
			{
				Level.DropItem(coordinates, stack);
			}

			if (Inventory.Helmet.Id != 0)
			{
				Level.DropItem(coordinates, Inventory.Helmet);
				Inventory.Helmet = new ItemAir();
			}
			if (Inventory.Chest.Id != 0)
			{
				Level.DropItem(coordinates, Inventory.Chest);
				Inventory.Chest = new ItemAir();
			}
			if (Inventory.Leggings.Id != 0)
			{
				Level.DropItem(coordinates, Inventory.Leggings);
				Inventory.Leggings = new ItemAir();
			}
			if (Inventory.Boots.Id != 0)
			{
				Level.DropItem(coordinates, Inventory.Boots);
				Inventory.Boots = new ItemAir();
			}

			Inventory.Clear();
		}

		public override void SpawnToPlayers(Player[] players)
		{
			McpeAddPlayer mcpeAddPlayer = McpeAddPlayer.CreateObject();
			mcpeAddPlayer.uuid = ClientUuid;
			mcpeAddPlayer.username = Username;
			mcpeAddPlayer.entityIdSelf = EntityId;
			mcpeAddPlayer.runtimeEntityId = EntityId;
			mcpeAddPlayer.x = KnownPosition.X;
			mcpeAddPlayer.y = KnownPosition.Y;
			mcpeAddPlayer.z = KnownPosition.Z;
			mcpeAddPlayer.speedX = Velocity.X;
			mcpeAddPlayer.speedY = Velocity.Y;
			mcpeAddPlayer.speedZ = Velocity.Z;
			mcpeAddPlayer.yaw = KnownPosition.Yaw;
			mcpeAddPlayer.headYaw = KnownPosition.HeadYaw;
			mcpeAddPlayer.pitch = KnownPosition.Pitch;
			mcpeAddPlayer.metadata = GetMetadata();
			mcpeAddPlayer.permissionLevel = (uint) PermissionLevel;
			mcpeAddPlayer.actionPermissions = (uint) ActionPermissions;
			mcpeAddPlayer.flags = GetUintFlags();
			mcpeAddPlayer.userId = Endian.SwapInt64(EntityId);

			Level.RelayBroadcast(this, players, mcpeAddPlayer);

			SendEquipmentForPlayer(players);

			SendArmorForPlayer(players);
		}

		public virtual void SendEquipmentForPlayer(Player[] receivers)
		{
			McpeMobEquipment mcpePlayerEquipment = McpeMobEquipment.CreateObject();
			mcpePlayerEquipment.runtimeEntityId = EntityId;
			mcpePlayerEquipment.item = Inventory.GetItemInHand();
			mcpePlayerEquipment.slot = (byte) Inventory.SelectedHotbarSlot;
			mcpePlayerEquipment.selectedSlot = (byte) Inventory.InHandSlot;

			Level.RelayBroadcast(this, receivers, mcpePlayerEquipment);
		}

		public virtual void SendArmorForPlayer(Player[] receivers)
		{
			McpeMobArmorEquipment mcpePlayerArmorEquipment = McpeMobArmorEquipment.CreateObject();
			mcpePlayerArmorEquipment.runtimeEntityId = EntityId;
			mcpePlayerArmorEquipment.helmet = Inventory.Helmet;
			mcpePlayerArmorEquipment.chestplate = Inventory.Chest;
			mcpePlayerArmorEquipment.leggings = Inventory.Leggings;
			mcpePlayerArmorEquipment.boots = Inventory.Boots;
			Level.RelayBroadcast(this, receivers, mcpePlayerArmorEquipment);
		}

		public override void DespawnFromPlayers(Player[] players)
		{
			McpeRemoveEntity mcpeRemovePlayer = McpeRemoveEntity.CreateObject();
			mcpeRemovePlayer.entityIdSelf = EntityId;
			Level.RelayBroadcast(this, players, mcpeRemovePlayer);
		}


		// Events

		public event EventHandler<PlayerEventArgs> PlayerJoin;

		protected virtual void OnPlayerJoin(PlayerEventArgs e)
		{
			PlayerJoin?.Invoke(this, e);
		}

		public event EventHandler<PlayerEventArgs> PlayerLeave;

		protected virtual void OnPlayerLeave(PlayerEventArgs e)
		{
			PlayerLeave?.Invoke(this, e);
		}

		public event EventHandler<PlayerEventArgs> Ticking;

		protected virtual void OnTicking(PlayerEventArgs e)
		{
			Ticking?.Invoke(this, e);
		}

		public event EventHandler<PlayerEventArgs> Ticked;

		protected virtual void OnTicked(PlayerEventArgs e)
		{
			Ticked?.Invoke(this, e);
		}

	}

	public enum UserPermission : uint
	{
		Visitor = 0,
		Member = 1,
		Operator = 2,
		Custom = 3
	}

	public class PlayerEventArgs : CancelEventArgs
	{
		public Player Player { get; }
		public Level Level { get; }

		public PlayerEventArgs(Player player)
		{
			Player = player;
			Level = player?.Level;
		}
	}
}