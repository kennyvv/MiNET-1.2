using MiNET.Entities.Passive;

namespace MiNET.Entities.Behaviors
{
	public class HurtByTargetBehavior : IBehavior
	{
		private Mob _mob;

		public HurtByTargetBehavior(Mob wolf)
		{
			_mob = wolf;
		}

		public bool ShouldStart()
		{
			if (_mob.HealthManager.LastDamageSource == null) return false;

			_mob.SetTarget(_mob.HealthManager.LastDamageSource);

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