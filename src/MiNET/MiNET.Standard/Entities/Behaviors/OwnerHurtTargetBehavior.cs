using MiNET.Entities.Passive;

namespace MiNET.Entities.Behaviors
{
	public class OwnerHurtTargetBehavior : IBehavior
	{
		private readonly Mob _mob;

		public OwnerHurtTargetBehavior(Mob mob)
		{
			_mob = mob;
		}

		public bool ShouldStart()
		{
			if (!_mob.IsTamed) return false;

			Player owner = _mob.Owner as Player;

			if (owner == null || owner.LastAttackTarget == null) return false;

			_mob.SetTarget(owner.LastAttackTarget);

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