using Unity.Cinemachine;
using UnityEngine;

[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CinemachineTargetGroup))]
public sealed class CameraFramingFloor : MonoBehaviour
{
    [SerializeField] private Collider2D cameraBounds;
    [SerializeField, Min(0f)] private float reentryMargin = 0.5f;

    private CinemachineTargetGroup targetGroup;
    private float[] originalWeights;
    private bool[] excluded;

    private void Awake()
    {
        targetGroup = GetComponent<CinemachineTargetGroup>();
    }

    private void OnEnable()
    {
        if (targetGroup == null)
        {
            targetGroup = GetComponent<CinemachineTargetGroup>();
        }

        RememberWeights();
    }

    private void LateUpdate()
    {
        if (cameraBounds == null || !cameraBounds.enabled || targetGroup.Targets.Count != originalWeights.Length)
        {
            return;
        }

        float bottomY = cameraBounds.bounds.min.y;
        bool changed = false;

        for (int i = 0; i < targetGroup.Targets.Count; i++)
        {
            CinemachineTargetGroup.Target target = targetGroup.Targets[i];
            if (target.Object == null || originalWeights[i] <= 0f)
            {
                continue;
            }

            float threshold = excluded[i] ? bottomY + reentryMargin : bottomY;
            bool shouldExclude = target.Object.position.y < threshold;
            if (shouldExclude == excluded[i])
            {
                continue;
            }

            excluded[i] = shouldExclude;
            target.Weight = shouldExclude ? 0f : originalWeights[i];
            changed = true;
        }

        if (changed)
        {
            targetGroup.DoUpdate();
        }
    }

    private void OnDisable()
    {
        if (targetGroup == null || originalWeights == null)
        {
            return;
        }

        for (int i = 0; i < Mathf.Min(targetGroup.Targets.Count, originalWeights.Length); i++)
        {
            targetGroup.Targets[i].Weight = originalWeights[i];
        }

        targetGroup.DoUpdate();
    }

    private void RememberWeights()
    {
        int count = targetGroup.Targets.Count;
        originalWeights = new float[count];
        excluded = new bool[count];
        for (int i = 0; i < count; i++)
        {
            originalWeights[i] = targetGroup.Targets[i].Weight;
        }
    }
}
