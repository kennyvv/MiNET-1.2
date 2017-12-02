using System.Collections.Concurrent;
using System.Collections.Generic;
using MiNET.Net;

namespace MiNET
{
	public class SessionManager
	{
		protected ConcurrentDictionary<UUID, Session> Sessions = new ConcurrentDictionary<UUID, Session>();

		public virtual Session FindSession(Player player)
		{
			Session session;
			Sessions.TryGetValue(player.ClientUuid, out session);

			return session;
		}

		public virtual Session CreateSession(Player player)
		{
			Sessions.TryAdd(player.ClientUuid, new Session(player));

			return FindSession(player);
		}

		public virtual void SaveSession(Session session)
		{
		}

		public virtual void RemoveSession(Session session)
		{
			if (session.Player == null) return;
			if (session.Player.ClientUuid == null) return;

			Sessions.TryRemove(session.Player.ClientUuid, out session);
		}
	}

	public class Session : Dictionary<string, object>
	{
		public Player Player { get; set; }

		public Session(Player player)
		{
			Player = player;
		}
	}
}