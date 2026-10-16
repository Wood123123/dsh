namespace FromHell.TacticalSprint;

public sealed class SprintGate
{
	private float last = float.NegativeInfinity;

	public bool Active { get; private set; }

	public bool Press(float now, float window, bool sprinting)
	{
		if (Active)
		{
			Reset();
			return false;
		}
		if (now >= last && now - last <= window)
		{
			Active = true;
			last = float.NegativeInfinity;
			return true;
		}
		last = now;
		return false;
	}

	public void Stop()
	{
		Active = false;
	}

	public void Reset()
	{
		Active = false;
		last = float.NegativeInfinity;
	}
}
