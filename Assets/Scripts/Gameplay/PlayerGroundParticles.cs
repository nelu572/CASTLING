using UnityEngine;

public sealed class PlayerGroundParticles : MonoBehaviour
{
    [Header("한 번 재생할 파티클 시스템 연결")]
    [SerializeField] private ParticleSystem walkEffect;
    [SerializeField] private ParticleSystem jumpEffect;
    [SerializeField] private ParticleSystem landingEffect;
    [SerializeField, Min(0.1f)] private float footstepInterval = 0.32f;

    private float nextFootstepAt;

    private void OnDisable()
    {
        StopEffect(walkEffect);
        StopEffect(jumpEffect);
        StopEffect(landingEffect);
    }

    public void SetWalking(bool isWalking)
    {
        if (!isWalking)
        {
            nextFootstepAt = Time.time;
            return;
        }

        if (Time.time >= nextFootstepAt)
        {
            PlayEffect(walkEffect);
            nextFootstepAt = Time.time + footstepInterval;
        }
    }

    public void PlayJump()
    {
        PlayEffect(jumpEffect);
    }

    public void PlayLanding()
    {
        PlayEffect(landingEffect);
    }

    private static void PlayEffect(ParticleSystem effect)
    {
        if (effect == null)
        {
            return;
        }

        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.Play(true);
    }

    private static void StopEffect(ParticleSystem effect)
    {
        if (effect != null)
        {
            effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}
