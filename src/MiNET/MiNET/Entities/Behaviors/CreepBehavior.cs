using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AStarNavigator;
using log4net;
using MiNET.Entities.Hostile;
using MiNET.Entities.Passive;
using MiNET.Utils;

namespace MiNET.Entities.Behaviors
{
	public class CreepBehavior : IBehavior
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof (CreepBehavior));
		
		private double _speedMultiplier;
		private readonly double _followRange;
		private Mob _entity;

		private int _fuse = 30;
		private List<ImprovedTile> _currentPath;
		private Vector3 _lastPlayerPos;
		private bool _damageWorld;
		public CreepBehavior(Mob creeper, double speedMultiplier, double followRange = 16, bool damageWorld = true)
		{
			_entity = creeper;
			_speedMultiplier = speedMultiplier;
			_followRange = followRange;
			_damageWorld = damageWorld;
		}

		public bool ShouldStart()
		{
			if (_entity.Target == null) return false;
			if (_entity.Target.HealthManager.IsDead) return false;

			//var currentPath = new PathFinder().FindPath(_entity, _entity.Target, _followRange);
			//if (currentPath.Count == 0)
			//{
			//	return false;
			//}

			_lastPlayerPos = _entity.Target.KnownPosition;

			return true;
		}

		public bool CanContinue()
		{
			if (_fuse <= 0) return false;
			if (_entity.Target == null) return false;
			if (_entity.Target.HealthManager.IsDead) return false;

			if (_entity.DistanceTo(_entity.Target) > _followRange * _followRange || Math.Abs(_entity.KnownPosition.Y - _entity.Target.KnownPosition.Y) > _entity.Height + 1)
			{
				_entity.SetTarget(null);
				return false;
			}

			return true;
		}

		public void OnTick(Entity[] entities)
		{
			if (_fuse <= 0) return;

			Mob entity = _entity;
			Entity target = _entity.Target;

			if (target == null) return;

			var distanceToPlayer = entity.KnownPosition.DistanceTo(target.KnownPosition);

			var haveNoPath = (_currentPath == null || _currentPath.Count == 0);
			if (haveNoPath || Vector3.Distance(_lastPlayerPos, target.KnownPosition) > 0.01)
			{
				Log.Debug($"Search new solution. Have no path={haveNoPath}");
				var pathFinder = new Pathfinder();
				_currentPath = pathFinder.FindPath(entity, target, distanceToPlayer + 1);
				if (_currentPath.Count == 0)
				{
					Log.Debug($"Found no solution, trying a search at full distance");
					_currentPath = pathFinder.FindPath(entity, target, _followRange);
				}
			}

			if (distanceToPlayer <= 7)
			{
				_fuse--;

				if (_fuse == 0)
				{
					if (_damageWorld)
					{
						Explosion explosion = new Explosion(entity.Level, entity.KnownPosition.GetCoordinates3D(), 3);
						explosion.Explode();
					}

					var playerVictims = _entity.Level.Players
						.OrderBy(p => Vector3.Distance(_entity.KnownPosition, p.Value.KnownPosition))
						.Where(p => p.Value.IsSpawned && _entity.DistanceTo(p.Value) < 7).Select(x => x.Value).ToArray();
					var victims = new Entity[entities.Length + playerVictims.Length];
					entities.CopyTo(victims, 0);
					playerVictims.CopyTo(victims, entities.Length);
					foreach (var victim in victims)
					{
						var dist = entity.KnownPosition.DistanceTo(victim.KnownPosition);
						if (dist <= 7)
						{
							victim.HealthManager.TakeHit(_entity, (int)((1D / Math.Max(dist, 1D)) * 49D), DamageCause.EntityExplosion);
						}
					}

					_entity.DespawnEntity();
				}
			}
			else
			{
				_fuse = 30; //
				_lastPlayerPos = target.KnownPosition;

				if (_currentPath.Count > 0)
				{
					ImprovedTile next;
					if (GetNextTile(out next))
					{
						entity.Controller.RotateTowards(new Vector3((float)next.X + 0.5f, entity.KnownPosition.Y, (float)next.Y + 0.5f));
						entity.Controller.MoveForward(_speedMultiplier, entities);
					}
				}
				else
				{
					Log.Debug($"Found no path solution");
					entity.Velocity = Vector3.Zero;
					_currentPath = null;
				}
				entity.Controller.LookAt(target, true);
			}

			if (_entity is Creeper c && c.Fuse != _fuse)
			{
				c.Fuse = _fuse;
				if (_fuse < 30 && _fuse >= 0)
				{
					c.IsIgnited = true;
				}
				else
				{
					c.IsIgnited = false;
				}
				c.BroadcastSetEntityData();
			}
		}
		private bool GetNextTile(out ImprovedTile next)
		{
			next = new ImprovedTile(0,0);
			if (_currentPath.Count == 0) return false;

			next = _currentPath.First();

			BlockCoordinates currPos = (BlockCoordinates)_entity.KnownPosition;
			if ((int)next.X == currPos.X && (int)next.Y == currPos.Z)
			{
				_currentPath.Remove(next);

				if (!GetNextTile(out next)) return false;
			}

			return true;
		}

		public void OnEnd()
		{
			if (_entity.Target == null) return;
			if (_entity.Target.HealthManager.IsDead) _entity.SetTarget(null);
			_entity.Velocity = Vector3.Zero;
			_entity.KnownPosition.Pitch = 0;
		}
	}
}
