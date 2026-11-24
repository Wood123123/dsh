using EFT;
using System.Collections;

namespace Fika.Core.Main.ClientClasses;

public class FikaClientGrenade : ObservedGrenade
{
    private const float _smoothSpeed = 10f;

    private GrenadeSyncPacket _packet;
    private bool _hasPacket;
    private float _packetReceivedAt;

    private Coroutine _interpolationRoutine;

    public override void ApplyNetPacket(GrenadeSyncPacket packet)
    {
        _packet = packet;
        _hasPacket = true;
        _packetReceivedAt = Time.unscaledTime;

        CollisionNumber = _packet.CollisionNumber;

        _interpolationRoutine ??= StartCoroutine(SmoothingCoroutine());

        if (_packet.Done)
        {
            transform.SetPositionAndRotation(_packet.Position, _packet.Rotation);
            SetVelocity(_packet);
            OnDoneFromNet();

            _hasPacket = false;
        }
    }

    private IEnumerator SmoothingCoroutine()
    {
        var wait = new WaitForFixedUpdate();
        while (_hasPacket)
        {
            yield return wait;

            var t = 1f - Mathf.Exp(-_smoothSpeed * Time.unscaledDeltaTime);
            var elapsed = Mathf.Min(Time.unscaledTime - _packetReceivedAt, 0.5f);
            var predictedPosition = _packet.Position + (_packet.Velocity * elapsed)
                + (0.5f * Physics.gravity * elapsed * elapsed);
            var predictedRotation = _packet.Rotation
                * Quaternion.Euler(_packet.AngularVelocity * Mathf.Rad2Deg * elapsed);

            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, predictedPosition, t),
                Quaternion.Lerp(transform.rotation, predictedRotation, t));

            if (Rigidbody != null)
            {
                if (CollisionNumber == _packet.CollisionNumber)
                {
                    var predictedVelocity = _packet.Velocity + (Physics.gravity * elapsed);
                    Rigidbody.velocity = Vector3.Lerp(Rigidbody.velocity, predictedVelocity, t);

                    Rigidbody.angularVelocity = Vector3.Lerp(Rigidbody.angularVelocity, _packet.AngularVelocity, t);
                }
                else
                {
                    SetVelocity(_packet);
                }
            }
        }
    }
}
