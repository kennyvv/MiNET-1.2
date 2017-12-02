using System;
using System.Net;
using MiNET.Utils;

namespace MiNET
{
	public class MotdProvider
	{
		public const int ProtocolVersion = 140;
		public const string ClientVersion = "1.2.6.2";

		public string Motd { get; set; }

		public string SecondLine { get; set; }

		public int MaxNumberOfPlayers { get; set; }

		public int NumberOfPlayers { get; set; }

		public MotdProvider()
		{
			Motd = Config.GetProperty("motd", "MiNET: MCPE Server");
			SecondLine = Config.GetProperty("motd-2nd", "MiNET");
		}

		public virtual string GetMotd(ServerInfo serverInfo, IPEndPoint caller, bool eduMotd = false)
		{
			NumberOfPlayers = serverInfo.NumberOfPlayers;
			MaxNumberOfPlayers = serverInfo.MaxNumberOfPlayers;

			var protocolVersion = ProtocolVersion;
			var clientVersion = ClientVersion;
			var edition = "MCPE";

			if (eduMotd)
			{
				protocolVersion = 111;
				clientVersion = "1.0.17";
				edition = "MCEE";
			}

			return string.Format($"{edition};{Motd};{protocolVersion};{clientVersion};{NumberOfPlayers};{MaxNumberOfPlayers};{Motd.GetHashCode() + caller.Address.Address + caller.Port};{SecondLine};Survival;");
		}
	}
}