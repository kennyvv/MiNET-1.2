#region LICENSE

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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Xsl;
using fNbt;
using Jose;
using log4net;
using MiNET.Net;
using MiNET.Utils;
using Newtonsoft.Json.Linq;

namespace MiNET
{
	public class LoginMessageHandler : IMcpeMessageHandler
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (LoginMessageHandler));

		private readonly PlayerNetworkSession _session;

		private object _loginSyncLock = new object();
		private PlayerInfo _playerInfo = new PlayerInfo();

		public LoginMessageHandler(PlayerNetworkSession session)
		{
			_session = session;
		}

		public void Disconnect(string reason, bool sendDisconnect = true)
		{
		}

		public virtual void HandleMcpeLogin(McpeLogin message)
		{
			// Only one login!
			lock (_loginSyncLock)
			{
				if (_session.Username != null)
				{
					Log.Info($"Player {_session.Username} doing multiple logins");
					return; // Already doing login
				}

				_session.Username = string.Empty;
			}

			_playerInfo.ProtocolVersion = message.protocolVersion;

			DecodeCert(message);
		}

		protected void DecodeCert(McpeLogin message)
		{
			byte[] buffer = message.payload;

			if (message.payload.Length != buffer.Length)
			{
				Log.Debug($"Wrong lenght {message.payload.Length} != {message.payload.Length}");
				throw new Exception($"Wrong lenght {message.payload.Length} != {message.payload.Length}");
			}

			if (Log.IsDebugEnabled) Log.Debug("Lenght: " + message.payload.Length + ", Message: " + Convert.ToBase64String(buffer));

			string certificateChain;
			string skinData;

			try
			{
				var destination = new MemoryStream(buffer);
				destination.Position = 0;
				NbtBinaryReader reader = new NbtBinaryReader(destination, false);

				var countCertData = reader.ReadInt32();
				certificateChain = Encoding.UTF8.GetString(reader.ReadBytes(countCertData));
				if (Log.IsDebugEnabled) Log.Debug($"Certificate Chain (Lenght={countCertData})\n{certificateChain}");

				var countSkinData = reader.ReadInt32();
				skinData = Encoding.UTF8.GetString(reader.ReadBytes(countSkinData));
				if (Log.IsDebugEnabled) Log.Debug($"Skin data (Lenght={countSkinData})\n{skinData}");
			}
			catch (Exception e)
			{
				Log.Error("Parsing login", e);
				return;
			}

			try
			{
				{
					IDictionary<string, dynamic> headers = JWT.Headers(skinData);
					dynamic payload = JObject.Parse(JWT.Payload(skinData));

					if (Log.IsDebugEnabled) Log.Debug($"Skin JWT Header: {string.Join(";", headers)}");
					if (Log.IsDebugEnabled) Log.Debug($"Skin JWT Payload:\n{payload.ToString()}");

					try
					{
						_playerInfo.ClientId = payload.ClientRandomId;
						_playerInfo.CurrentInputMode = payload.CurrentInputMode;
						_playerInfo.DefaultInputMode = payload.DefaultInputMode;
						_playerInfo.DeviceModel = payload.DeviceModel;
						_playerInfo.DeviceOS = payload.DeviceOS;
						_playerInfo.GameVersion = payload.GameVersion;
						_playerInfo.GuiScale = payload.GuiScale;
						_playerInfo.LanguageCode = payload.LanguageCode;
						_playerInfo.ServerAddress = payload.ServerAddress;
						_playerInfo.UIProfile = payload.UIProfile;

						_playerInfo.Skin = new Skin()
						{
							CapeData = Convert.FromBase64String((string)payload.CapeData),
							SkinId = payload.SkinId,
							SkinData = Convert.FromBase64String((string)payload.SkinData),
							SkinGeometryName = payload.SkinGeometryName,
							SkinGeometry = Convert.FromBase64String((string)payload.SkinGeometry),
						};
						Log.Warn($"Cape data lenght={_playerInfo.Skin.CapeData.Length}");
					}
					catch (Exception e)
					{
						Log.Error("Parsing skin data", e);
					}
				}

				{
					dynamic json = JObject.Parse(certificateChain);

					if (Log.IsDebugEnabled) Log.Debug($"Certificate JSON:\n{json}");

					JArray chain = json.chain;
					//var chainArray = chain.ToArray();

					string validationKey = null;
					string identityPublicKey = null;

					foreach (JToken token in chain)
					{
						IDictionary<string, dynamic> headers = JWT.Headers(token.ToString());

						if (Log.IsDebugEnabled)
						{
							Log.Debug("Raw chain element:\n" + token.ToString());
							Log.Debug($"JWT Header: {string.Join(";", headers)}");

							dynamic jsonPayload = JObject.Parse(JWT.Payload(token.ToString()));
							Log.Debug($"JWT Payload:\n{jsonPayload}");
						}

						// Mojang root x5u cert (string): MHYwEAYHKoZIzj0CAQYFK4EEACIDYgAE8ELkixyLcwlZryUQcu1TvPOmI2B7vX83ndnWRUaXm74wFfa5f/lwQNTfrLVHa2PmenpGI6JhIMUJaWZrjmMj90NoKNFSNBuKdm8rYiXsfaz3K36x/1U26HpG0ZxK/V1V

						if (!headers.ContainsKey("x5u")) continue;

						string x5u = headers["x5u"];

						if (identityPublicKey == null)
						{
							if (CertificateData.MojangRootKey.Equals(x5u, StringComparison.InvariantCultureIgnoreCase))
							{
								Log.Debug("Key is ok, and got Mojang root");
							}
							else if (chain.Count > 1)
							{
								Log.Debug("Got client cert (client root)");
								continue;
							}
							else if (chain.Count == 1)
							{
								Log.Debug("Selfsigned chain");
							}
						}
						else if (identityPublicKey.Equals(x5u))
						{
							Log.Debug("Derived Key is ok");
						}

						if (Log.IsDebugEnabled)
						{
							Log.Debug($"x5u cert (string): {x5u}");
							ECDiffieHellmanPublicKey publicKey = CryptoUtils.CreateEcDiffieHellmanPublicKey(x5u);
							Log.Debug($"Cert:\n{publicKey.ToXmlString()}");
						}

						// Validate
						CngKey newKey = CryptoUtils.ImportECDsaCngKeyFromString(x5u);
						CertificateData data = JWT.Decode<CertificateData>(token.ToString(), newKey);

						if (data != null)
						{
							identityPublicKey = data.IdentityPublicKey;

							if (Log.IsDebugEnabled) Log.Debug("Decoded token success");

							if (CertificateData.MojangRootKey.Equals(x5u, StringComparison.InvariantCultureIgnoreCase))
							{
								Log.Debug("Got Mojang key. Is valid = " + data.CertificateAuthority);
								validationKey = data.IdentityPublicKey;
							}
							else if (validationKey != null && validationKey.Equals(x5u, StringComparison.InvariantCultureIgnoreCase))
							{
								_playerInfo.CertificateData = data;
							}
							else
							{
								if (data.ExtraData == null) continue;

								// Self signed, make sure they don't fake XUID
								if (data.ExtraData.Xuid != null)
								{
									Log.Warn("Received fake XUID from " + data.ExtraData.DisplayName);
									data.ExtraData.Xuid = null;
								}

								_playerInfo.CertificateData = data;
							}
						}
						else
						{
							Log.Error("Not a valid Identity Public Key for decoding");
						}
					}

					//TODO: Implement disconnect here

					{
						_playerInfo.Username = _playerInfo.CertificateData.ExtraData.DisplayName;
						_session.Username = _playerInfo.Username;
						string identity = _playerInfo.CertificateData.ExtraData.Identity;

						if (Log.IsDebugEnabled) Log.Debug($"Connecting user {_playerInfo.Username} with identity={identity}");
						_playerInfo.ClientUuid = new UUID(identity);

						_session.CryptoContext = new CryptoContext
						{
							UseEncryption = Config.GetProperty("UseEncryptionForAll", false) || (Config.GetProperty("UseEncryption", true) && !string.IsNullOrWhiteSpace(_playerInfo.CertificateData.ExtraData.Xuid)),
						};

						if (_session.CryptoContext.UseEncryption)
						{
							ECDiffieHellmanPublicKey publicKey = CryptoUtils.CreateEcDiffieHellmanPublicKey(_playerInfo.CertificateData.IdentityPublicKey);
							if (Log.IsDebugEnabled) Log.Debug($"Cert:\n{publicKey.ToXmlString()}");

							// Create shared shared secret
							ECDiffieHellmanCng ecKey = new ECDiffieHellmanCng(384);
							ecKey.HashAlgorithm = CngAlgorithm.Sha256;
							ecKey.KeyDerivationFunction = ECDiffieHellmanKeyDerivationFunction.Hash;
							ecKey.SecretPrepend = Encoding.UTF8.GetBytes("RANDOM SECRET"); // Server token

							byte[] secret = ecKey.DeriveKeyMaterial(publicKey);

							if (Log.IsDebugEnabled) Log.Debug($"SECRET KEY (b64):\n{Convert.ToBase64String(secret)}");

							{
								RijndaelManaged rijAlg = new RijndaelManaged
								{
									BlockSize = 128,
									Padding = PaddingMode.None,
									Mode = CipherMode.CFB,
									FeedbackSize = 8,
									Key = secret,
									IV = secret.Take(16).ToArray(),
								};

								// Create a decrytor to perform the stream transform.
								ICryptoTransform decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);
								MemoryStream inputStream = new MemoryStream();
								CryptoStream cryptoStreamIn = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read);

								ICryptoTransform encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);
								MemoryStream outputStream = new MemoryStream();
								CryptoStream cryptoStreamOut = new CryptoStream(outputStream, encryptor, CryptoStreamMode.Write);

								_session.CryptoContext.Algorithm = rijAlg;
								_session.CryptoContext.Decryptor = decryptor;
								_session.CryptoContext.Encryptor = encryptor;
								_session.CryptoContext.InputStream = inputStream;
								_session.CryptoContext.OutputStream = outputStream;
								_session.CryptoContext.CryptoStreamIn = cryptoStreamIn;
								_session.CryptoContext.CryptoStreamOut = cryptoStreamOut;
							}

							//TODO: JSON now.
							throw new Exception("JSON!!!");
							//var response = McpeServerToClientHandshake.CreateObject();
						//	response.NoBatch = true;
						//	response.ForceClear = true;

							//response.token = 
								//	response.serverPublicKey = Convert.ToBase64String(ecKey.PublicKey.GetDerEncoded());
								//	response.tokenLength = (short) ecKey.SecretPrepend.Length;
								//	response.token = ecKey.SecretPrepend;

								//_session.SendPackage(response);

							if (Log.IsDebugEnabled) Log.Warn($"Encryption enabled for {_session.Username}");
						}
					}
				}

				if (!_session.CryptoContext.UseEncryption)
				{
					_session.MessageHandler.HandleMcpeClientToServerHandshake(null);
				}
			}
			catch (Exception e)
			{
				Log.Error("Decrypt", e);
			}
		}

		public void HandleMcpeClientToServerHandshake(McpeClientToServerHandshake message)
		{
			IServerManager serverManager = _session.Server.ServerManager;
			IServer server = serverManager.GetServer();

			IMcpeMessageHandler messageHandler = server.CreatePlayer(_session, _playerInfo);
			_session.MessageHandler = messageHandler; // Replace current message handler with real one.

			if (_playerInfo.ProtocolVersion < MotdProvider.ProtocolVersion)
			{
				Log.Warn($"Wrong version ({_playerInfo.ProtocolVersion}) of Minecraft. Upgrade to join this server.");
				_session.Disconnect($"Wrong version ({_playerInfo.ProtocolVersion}) of Minecraft. Upgrade to join this server.");
				return;
			}
			else if (_playerInfo.ProtocolVersion > MotdProvider.ProtocolVersion)
			{
				Log.Warn($"Wrong version ({_playerInfo.ProtocolVersion}) of Minecraft. Downgrade to join this server. ({MotdProvider.ProtocolVersion})");
				_session.Disconnect($"Wrong version ({_playerInfo.ProtocolVersion}) of Minecraft. Downgrade to join this server. ({MotdProvider.ProtocolVersion})");
				return;
			}

			if (Config.GetProperty("ForceXBLAuthentication", false) && _playerInfo.CertificateData.ExtraData.Xuid == null)
			{
				Log.Warn($"You must authenticate to XBOX Live to join this server.");
				_session.Disconnect(Config.GetProperty("ForceXBLLogin", "You must authenticate to XBOX Live to join this server."));

				return;
			}

			_session.MessageHandler.HandleMcpeClientToServerHandshake(null);
		}

		public void HandleMcpeResourcePackClientResponse(McpeResourcePackClientResponse message)
		{
		}

		public void HandleMcpeText(McpeText message)
		{
		}

		public void HandleMcpeMovePlayer(McpeMovePlayer message)
		{
		}

		public void HandleMcpeLevelSoundEvent(McpeLevelSoundEvent message)
		{
		}

		public void HandleMcpeEntityEvent(McpeEntityEvent message)
		{
		}

		public void HandleMcpeInventoryTransaction(McpeInventoryTransaction message)
		{
		}

		public void HandleMcpeMobEquipment(McpeMobEquipment message)
		{
		}

		public void HandleMcpeMobArmorEquipment(McpeMobArmorEquipment message)
		{
		}

		public void HandleMcpeInteract(McpeInteract message)
		{
		}

		public void HandleMcpeBlockPickRequest(McpeBlockPickRequest message)
		{
		}

		public void HandleMcpePlayerAction(McpePlayerAction message)
		{
		}

		public void HandleMcpeEntityFall(McpeEntityFall message)
		{
		}

		public void HandleMcpeAnimate(McpeAnimate message)
		{
		}

		public void HandleMcpeRespawn(McpeRespawn message)
		{
		}

	//	public void HandleMcpeDropItem(McpeDropItem message)
	//	{
	//	}

		public void HandleMcpeContainerClose(McpeContainerClose message)
		{
		}

		public void HandleMcpePlayerHotbar(McpePlayerHotbar message)
		{
			
		}

		public void HandleMcpeInventoryContent(McpeInventoryContent message)
		{
			
		}

		//	public void HandleMcpeContainerSetSlot(McpeContainerSetSlot message)
	//	{
	//	}

		public void HandleMcpeInventorySlot(McpeInventorySlot message)
		{ 
		}

		public void HandleMcpeCraftingEvent(McpeCraftingEvent message)
		{
		}

		public void HandleMcpeAdventureSettings(McpeAdventureSettings message)
		{
		}

		public void HandleMcpeBlockEntityData(McpeBlockEntityData message)
		{
		}

		public void HandleMcpePlayerInput(McpePlayerInput message)
		{
		}

		public void HandleMcpeSimpleEvent(McpeSimpleEvent message)
		{
			
		}

		public void HandleMcpeMapInfoRequest(McpeMapInfoRequest message)
		{
		}

		public void HandleMcpeRequestChunkRadius(McpeRequestChunkRadius message)
		{
		}

		public void HandleMcpeItemFrameDropItem(McpeItemFrameDropItem message)
		{
		}

		public void HandleMcpeCommandRequest(McpeCommandRequest message)
		{
		}

		public void HandleMcpeCommandBlockUpdate(McpeCommandBlockUpdate message)
		{
		}

		public void HandleMcpeResourcePackChunkRequest(McpeResourcePackChunkRequest message)
		{
		}

		public void HandleMcpePurchaseReceipt(McpePurchaseReceipt message)
		{
		}

		public void HandleMcpeServerSettingsRequest(McpeServerSettingsRequest message)
		{
			
		}

		public void HandleMcpeSetPlayerGameType(McpeSetPlayerGameType message)
		{
			
		}

		public void HandleMcpeModalFormResponse(McpeModalFormResponse message)
		{
			
		}

		public void HandleMcpeSetDifficulty(McpeSetDifficulty message)
		{
			
		}

		public void HandleMcpeSetDefaultGamemode(McpeSetDefaultGamemode message)
		{
			
		}
	}

	public interface IServerManager
	{
		IServer GetServer();
	}

	public interface IServer
	{
		IMcpeMessageHandler CreatePlayer(INetworkHandler session, PlayerInfo playerInfo);
	}

	public class DefaultServerManager : IServerManager
	{
		private readonly MiNetServer _miNetServer;
		private IServer _getServer;

		protected DefaultServerManager()
		{
		}

		public DefaultServerManager(MiNetServer miNetServer)
		{
			_miNetServer = miNetServer;
			_getServer = new DefaultServer(miNetServer);
		}

		public virtual IServer GetServer()
		{
			return _getServer;
		}
	}

	public class DefaultServer : IServer
	{
		private readonly MiNetServer _server;

		protected DefaultServer()
		{
		}

		public DefaultServer(MiNetServer server)
		{
			_server = server;
		}

		public virtual IMcpeMessageHandler CreatePlayer(INetworkHandler session, PlayerInfo playerInfo)
		{
			Player player = _server.PlayerFactory.CreatePlayer(_server, session.GetClientEndPoint(), playerInfo);
			player.NetworkHandler = session;
			player.CertificateData = playerInfo.CertificateData;
			player.Username = playerInfo.Username;
			player.ClientUuid = playerInfo.ClientUuid;
			player.ServerAddress = playerInfo.ServerAddress;
			player.ClientId = playerInfo.ClientId;
			player.Skin = playerInfo.Skin;
			player.PlayerInfo = playerInfo;

			return player;
		}
	}
}