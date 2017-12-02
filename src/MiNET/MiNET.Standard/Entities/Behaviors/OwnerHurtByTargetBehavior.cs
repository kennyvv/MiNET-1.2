using MiNET.Entities.Passive;

namespace MiNET.Entities.Behaviors
{
	public class OwnerHurtByTargetBehavior : IBehavior
	{
		private Mob _mob;

		public OwnerHurtByTargetBehavior(Mob mob)
		{
			_mob = mob;
		}

		public bool ShouldStart()
		{
			if (!_mob.IsTamed) return false;

			Player owner = _mob.Owner as Player;

			if (owner?.HealthManager.LastDamageSource == null) return false;

			_mob.SetTarget(owner.HealthManager.LastDamageSource);

			return true;
		}

		public bool CanContinue()
		{
			return false;
		}

		public void OnTick(Entity[] entities)
		{
		}

		public void OnEnd()
		{
		}
	}
}