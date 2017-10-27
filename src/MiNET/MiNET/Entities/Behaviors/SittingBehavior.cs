﻿using System.Numerics;
using MiNET.Entities.Passive;

namespace MiNET.Entities.Behaviors
{
	public class SittingBehavior : IBehavior
	{
		private readonly Mob _entity;

		public SittingBehavior(Mob entity)
		{
			this._entity = entity;
		}

		public bool ShouldStart()
		{
			if (!_entity.IsTamed) return false;
			if (_entity.IsInWater) return false;

			Entity owner = _entity.Owner;

			var shouldStart = owner == null || ((!(_entity.KnownPosition.DistanceTo(owner.KnownPosition) < 144.0) || _entity.HealthManager.LastDamageSource == null) && _entity.IsSitting);
			if (!shouldStart) return false;

			_entity.Velocity *= new Vector3(0, 1, 0);

			return true;
		}

		public bool CanContinue()
		{
			return _entity.IsSitting;
		}


		public void OnTick(Entity[] entities)
		{
			if (_entity.Owner != null)
				_entity.Controller.LookAt(_entity.Owner);
		}

		public void OnEnd()
		{
			_entity.IsSitting = false;
			_entity.BroadcastSetEntityData();
		}
	}
}