using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CastlingFeedback : MonoBehaviour
{
    [SerializeField, Min(0.05f)] private float successDuration = 0.24f;
    [SerializeField, Min(0.05f)] private float failureDuration = 0.32f;
    [SerializeField, Range(0f, 1f)] private float silhouetteOpacity = 0.32f;
    [SerializeField, Min(0.01f)] private float strokeWidth = 0.045f;

    private const int ChargeEffect = 4;
    private const int EffectCount = 5;
    private const int PulseSegments = 32;
    private readonly Effect[] effects = new Effect[EffectCount];
    private CastlingAbility ability;
    private Material strokeMaterial;

    private sealed class Stroke
    {
        internal LineRenderer outside;
        internal LineRenderer inside;
    }

    private sealed class Effect
    {
        internal GameObject root;
        internal SpriteRenderer silhouette;
        internal readonly List<Stroke> strokes = new List<Stroke>();
        internal float age;
        internal float duration;
        internal bool failed;
        internal bool pulse;
        internal bool charging;
    }

    private void OnEnable()
    {
        ability = GetComponent<CastlingAbility>();
        if (ability != null)
        {
            ability.Attempted += OnAttempted;
        }
    }

    private void OnDisable()
    {
        if (ability != null)
        {
            ability.Attempted -= OnAttempted;
        }

        HideEffects();
    }

    private void OnDestroy()
    {
        foreach (Effect effect in effects)
        {
            if (effect != null && effect.root != null)
            {
                Destroy(effect.root);
            }
        }

        if (strokeMaterial != null)
        {
            Destroy(strokeMaterial);
        }
    }

    private void OnAttempted(CastlingAbility.CastlingResult result)
    {
        HideEffects();
        if (result.Succeeded)
        {
            ShowSilhouette(0, ability.King, result.KingOrigin, false);
            ShowSilhouette(1, ability.Rook, result.RookOrigin, false);
            ShowPulse(2, ability.King, result.KingDestination, false);
            ShowPulse(3, ability.Rook, result.RookDestination, false);
        }
        else
        {
            ShowPulse(0, ability.King, result.KingOrigin, true);
            if (result.KingBlocked)
            {
                ShowSilhouette(1, ability.King, result.KingDestination, true);
            }

            if (result.RookBlocked)
            {
                ShowSilhouette(2, ability.Rook, result.RookDestination, true);
            }
        }
    }

    private void Update()
    {
        foreach (Effect effect in effects)
        {
            if (effect == null || effect.root == null || !effect.root.activeSelf || effect.charging)
            {
                continue;
            }

            effect.age += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(effect.age / effect.duration);
            if (progress >= 1f)
            {
                effect.root.SetActive(false);
                continue;
            }

            // Interrupted pulses distinguish rejection without moving or squashing the body.
            float interrupted = effect.failed && progress > 0.25f && progress < 0.4f ? 0.15f : 1f;
            float opacity = (1f - progress) * interrupted;
            float expansion = effect.pulse ? 1f + progress * 0.3f : 1f;
            effect.root.transform.localScale = Vector3.one * expansion;
            Color ghostColor = Color.white;
            ghostColor.a = silhouetteOpacity * opacity;
            effect.silhouette.color = ghostColor;
            foreach (Stroke stroke in effect.strokes)
            {
                SetStrokeOpacity(stroke, opacity);
            }
        }
    }

    private void LateUpdate()
    {
        if (ability == null || !ability.IsCharging)
        {
            if (effects[ChargeEffect] != null)
            {
                effects[ChargeEffect].root.SetActive(false);
            }
            return;
        }

        Effect charge = effects[ChargeEffect];
        if (charge == null || !charge.root.activeSelf)
        {
            HideEffects();
            charge = BeginEffect(ChargeEffect, ability.King, ability.King.position, false, false);
            charge.charging = true;
            ShowStroke(charge, 0, new Vector3[PulseSegments + 1], false);
        }

        Rigidbody2D source = ability.King;
        charge.root.transform.position = source.transform.position;
        Bounds bounds = source.GetComponent<Collider2D>().bounds;
        Vector3 center = bounds.center - source.transform.position;
        float radius = Mathf.Max(0.6f, bounds.size.x * 0.65f);
        float filledSegments = Mathf.Max(0.02f, ability.ChargeProgress) * PulseSegments;
        int count = Mathf.CeilToInt(filledSegments) + 1;
        Stroke stroke = charge.strokes[0];
        stroke.outside.positionCount = stroke.inside.positionCount = count;
        for (int point = 0; point < count; point++)
        {
            float angle = Mathf.PI * 0.5f - Mathf.Min(point, filledSegments) * Mathf.PI * 2f / PulseSegments;
            Vector3 vertex = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            stroke.outside.SetPosition(point, vertex);
            stroke.inside.SetPosition(point, vertex);
        }
    }

    private Effect BeginEffect(int index, Rigidbody2D source, Vector2 position, bool failed, bool pulse)
    {
        Effect effect = effects[index];
        if (effect == null)
        {
            effect = new Effect { root = new GameObject("CASTLING Effect") };
            effect.root.hideFlags = HideFlags.DontSave;
            GameObject silhouetteObject = new GameObject("Silhouette");
            silhouetteObject.transform.SetParent(effect.root.transform, false);
            effect.silhouette = silhouetteObject.AddComponent<SpriteRenderer>();
            effects[index] = effect;
        }

        effect.root.transform.position = new Vector3(position.x, position.y, source.transform.position.z);
        effect.root.transform.rotation = Quaternion.identity;
        effect.root.transform.localScale = Vector3.one;
        effect.age = 0f;
        effect.duration = failed ? failureDuration : successDuration;
        effect.failed = failed;
        effect.pulse = pulse;
        effect.charging = false;
        effect.silhouette.enabled = false;
        foreach (Stroke stroke in effect.strokes)
        {
            stroke.outside.enabled = false;
            stroke.inside.enabled = false;
        }

        effect.root.SetActive(true);
        return effect;
    }

    private void ShowSilhouette(int index, Rigidbody2D source, Vector2 position, bool failed)
    {
        Effect effect = BeginEffect(index, source, position, failed, false);
        SpriteRenderer bodySprite = FindBodySprite(source);
        if (bodySprite != null)
        {
            SpriteRenderer ghost = effect.silhouette;
            ghost.sprite = bodySprite.sprite;
            ghost.sharedMaterial = bodySprite.sharedMaterial;
            ghost.flipX = bodySprite.flipX;
            ghost.flipY = bodySprite.flipY;
            ghost.transform.localPosition = bodySprite.transform.position - source.transform.position;
            ghost.transform.localRotation = bodySprite.transform.rotation;
            ghost.transform.localScale = bodySprite.transform.lossyScale;
            SetSorting(ghost, 20);
            ghost.color = new Color(1f, 1f, 1f, silhouetteOpacity);
            ghost.enabled = true;
        }

        int strokeIndex = 0;
        foreach (PolygonCollider2D polygon in source.GetComponentsInChildren<PolygonCollider2D>())
        {
            if (!polygon.enabled || polygon.isTrigger || polygon.attachedRigidbody != source)
            {
                continue;
            }

            for (int path = 0; path < polygon.pathCount; path++)
            {
                Vector2[] points = polygon.GetPath(path);
                Vector3[] vertices = new Vector3[points.Length];
                for (int point = 0; point < points.Length; point++)
                {
                    vertices[point] = polygon.transform.TransformPoint(points[point] + polygon.offset)
                        - source.transform.position;
                }

                ShowStroke(effect, strokeIndex++, vertices, true);
            }
        }
    }

    private void ShowPulse(int index, Rigidbody2D source, Vector2 position, bool failed)
    {
        Effect effect = BeginEffect(index, source, position, failed, true);
        Collider2D bodyCollider = source.GetComponent<Collider2D>();
        Bounds bounds = bodyCollider.bounds;
        Vector3 center = bounds.center - source.transform.position;
        float radius = Mathf.Max(0.6f, bounds.size.x * 0.65f);
        int segments = failed ? PulseSegments * 3 / 4 : PulseSegments;
        Vector3[] vertices = new Vector3[segments];
        for (int point = 0; point < segments; point++)
        {
            float angle = Mathf.PI * 0.5f - point * Mathf.PI * 2f / PulseSegments;
            vertices[point] = center + new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        ShowStroke(effect, 0, vertices, !failed);
    }

    private void ShowStroke(Effect effect, int index, Vector3[] vertices, bool loop)
    {
        while (effect.strokes.Count <= index)
        {
            effect.strokes.Add(new Stroke
            {
                outside = CreateLine(effect.root.transform, "Light Stroke", strokeWidth * 2.5f, 21),
                inside = CreateLine(effect.root.transform, "Dark Stroke", strokeWidth, 22)
            });
        }

        Stroke stroke = effect.strokes[index];
        foreach (LineRenderer line in new[] { stroke.outside, stroke.inside })
        {
            line.loop = loop;
            line.positionCount = vertices.Length;
            line.SetPositions(vertices);
            line.enabled = true;
        }

        SetStrokeOpacity(stroke, 1f);
    }

    private LineRenderer CreateLine(Transform parent, string objectName, float width, int sortingOrder)
    {
        if (strokeMaterial == null)
        {
            strokeMaterial = new Material(Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default"));
            strokeMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        GameObject lineObject = new GameObject(objectName);
        lineObject.transform.SetParent(parent, false);
        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.sharedMaterial = strokeMaterial;
        line.useWorldSpace = false;
        line.widthMultiplier = width;
        line.numCornerVertices = 2;
        line.numCapVertices = 2;
        SetSorting(line, sortingOrder);
        return line;
    }

    private void SetSorting(Renderer renderer, int order)
    {
        SpriteRenderer kingSprite = FindBodySprite(ability.King);
        SpriteRenderer rookSprite = FindBodySprite(ability.Rook);
        SpriteRenderer front = kingSprite;
        if (front == null || (rookSprite != null && SortingLayer.GetLayerValueFromID(rookSprite.sortingLayerID)
            > SortingLayer.GetLayerValueFromID(front.sortingLayerID)))
        {
            front = rookSprite;
        }

        renderer.sortingLayerID = front != null ? front.sortingLayerID : 0;
        renderer.sortingOrder = (front != null ? front.sortingOrder : 0) + order;
    }

    private static SpriteRenderer FindBodySprite(Rigidbody2D source)
    {
        SpriteRenderer largest = null;
        float largestArea = 0f;
        foreach (SpriteRenderer renderer in source.GetComponentsInChildren<SpriteRenderer>())
        {
            float area = renderer.bounds.size.x * renderer.bounds.size.y;
            if (renderer.enabled && renderer.sprite != null && area > largestArea)
            {
                largest = renderer;
                largestArea = area;
            }
        }

        return largest;
    }

    private static void SetStrokeOpacity(Stroke stroke, float opacity)
    {
        Color light = new Color(1f, 1f, 1f, opacity * 0.9f);
        Color dark = new Color(0.08f, 0.08f, 0.08f, opacity * 0.9f);
        stroke.outside.startColor = stroke.outside.endColor = light;
        stroke.inside.startColor = stroke.inside.endColor = dark;
    }

    private void HideEffects()
    {
        foreach (Effect effect in effects)
        {
            if (effect != null && effect.root != null)
            {
                effect.root.SetActive(false);
            }
        }
    }
}
