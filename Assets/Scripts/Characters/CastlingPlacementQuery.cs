using System;
using System.Collections.Generic;
using UnityEngine;

internal sealed class CastlingPlacementQuery : IDisposable
{
    private readonly List<Collider2D> kingColliders = new();
    private readonly List<Collider2D> rookColliders = new();
    private readonly List<Collider2D> overlaps = new();
    private readonly List<Collider2D> kingSupports = new();
    private readonly List<Collider2D> rookSupports = new();
    private readonly List<ContactPoint2D> contacts = new();
    private readonly ContactFilter2D solidFilter = new() { useTriggers = false };
    private QueryShape firstShape;
    private QueryShape secondShape;

    public CastlingAbility.CastlingResult Evaluate(Rigidbody2D king, Rigidbody2D rook)
    {
        Vector2 kingOrigin = king.position;
        Vector2 rookOrigin = rook.position;
        Vector2 kingDestination = rookOrigin;
        Vector2 rookDestination = kingOrigin;
        CollectBodyColliders(king, kingColliders);
        CollectBodyColliders(rook, rookColliders);

        // Contact generation has a skin on both shapes. Keep this tolerance tied
        // to physics settings instead of giving CASTLING its own clearance radius.
        float tolerance = Mathf.Max(Physics2D.defaultContactOffset * 2f, Mathf.Epsilon);
        CollectSupports(king, rook, kingSupports, tolerance);
        CollectSupports(rook, king, rookSupports, tolerance);

        firstShape ??= new QueryShape("CASTLING placement A");
        secondShape ??= new QueryShape("CASTLING placement B");

        float kingLift = FootDistance(king, kingColliders) - FootDistance(rook, rookColliders);
        float rookLift = -kingLift;
        bool kingBlocked = EnvironmentBlocked(
            king, rook, kingColliders, kingDestination, rookSupports, tolerance,
            out bool kingBlockedBySupport);
        bool rookBlocked = EnvironmentBlocked(
            rook, king, rookColliders, rookDestination, kingSupports, tolerance,
            out bool rookBlockedBySupport);

        // Only replace the small floor correction the ordinary contact solver
        // already makes for different foot origins. This never searches for room.
        if (kingBlocked && kingBlockedBySupport && kingLift > 0f)
        {
            kingDestination.y += kingLift;
        }
        if (rookBlocked && rookBlockedBySupport && rookLift > 0f)
        {
            rookDestination.y += rookLift;
        }

        kingBlocked = kingColliders.Count == 0 || EnvironmentBlocked(
            king, rook, kingColliders, kingDestination, null, tolerance, out _);
        rookBlocked = rookColliders.Count == 0 || EnvironmentBlocked(
            rook, king, rookColliders, rookDestination, null, tolerance, out _);

        if (FinalBodiesOverlap(king, rook, kingDestination, rookDestination, tolerance))
        {
            kingBlocked = true;
            rookBlocked = true;
        }

        return new CastlingAbility.CastlingResult(
            kingOrigin, rookOrigin, kingDestination, rookDestination, kingBlocked, rookBlocked);
    }

    public void Dispose()
    {
        firstShape?.Dispose();
        secondShape?.Dispose();
        firstShape = null;
        secondShape = null;
    }

    private static void CollectBodyColliders(Rigidbody2D body, List<Collider2D> results)
    {
        results.Clear();
        body.GetComponentsInChildren(false, results);
        for (int index = results.Count - 1; index >= 0; index--)
        {
            Collider2D collider = results[index];
            if (collider.attachedRigidbody != body || !IsActiveSolid(collider))
            {
                results.RemoveAt(index);
            }
        }
    }

    private static bool IsActiveSolid(Collider2D collider)
    {
        return collider != null && collider.enabled && collider.gameObject.activeInHierarchy
            && !collider.isTrigger && collider.shapeCount > 0 && collider.composite == null
            && (collider.attachedRigidbody == null || collider.attachedRigidbody.simulated);
    }

    private static float FootDistance(Rigidbody2D body, List<Collider2D> colliders)
    {
        if (colliders.Count == 0)
        {
            return 0f;
        }
        float foot = float.PositiveInfinity;
        for (int index = 0; index < colliders.Count; index++)
        {
            foot = Mathf.Min(foot, colliders[index].bounds.min.y);
        }
        return body.position.y - foot;
    }

    private void CollectSupports(
        Rigidbody2D body, Rigidbody2D exchangedBody, List<Collider2D> results, float tolerance)
    {
        results.Clear();
        contacts.Clear();
        body.GetContacts(contacts);
        for (int index = 0; index < contacts.Count; index++)
        {
            ContactPoint2D contact = contacts[index];
            bool bodyIsOther = contact.otherCollider.attachedRigidbody == body;
            Collider2D own = bodyIsOther ? contact.otherCollider : contact.collider;
            Collider2D support = bodyIsOther ? contact.collider : contact.otherCollider;
            Vector2 normal = bodyIsOther ? contact.normal : -contact.normal;
            if (normal.y <= 0f || !IsActiveSolid(own) || !IsActiveSolid(support)
                || support.attachedRigidbody == body || support.attachedRigidbody == exchangedBody
                || !ShouldCollide(own, support))
            {
                continue;
            }

            // Recheck geometry because contacts describe the last simulation step.
            ColliderDistance2D distance = Physics2D.Distance(own, support);
            if (distance.isValid && distance.distance >= -tolerance
                && distance.distance <= tolerance * 2f && !results.Contains(support))
            {
                results.Add(support);
            }
        }
    }

    private bool EnvironmentBlocked(
        Rigidbody2D body, Rigidbody2D exchangedBody, List<Collider2D> colliders,
        Vector2 destination, List<Collider2D> destinationSupports, float tolerance,
        out bool blockedBySupport)
    {
        bool blocked = false;
        blockedBySupport = false;
        Vector2 delta = destination - body.position;
        for (int index = 0; index < colliders.Count; index++)
        {
            Collider2D source = colliders[index];
            firstShape.Copy(source, destination, body.rotation);
            Bounds bounds = source.bounds;
            Vector2 center = (Vector2)bounds.center + delta;
            Vector2 size = (Vector2)bounds.size + Vector2.one * (tolerance * 2f);
            overlaps.Clear();
            Physics2D.OverlapBox(center, size, 0f, solidFilter, overlaps);
            for (int obstacleIndex = 0; obstacleIndex < overlaps.Count; obstacleIndex++)
            {
                Collider2D obstacle = overlaps[obstacleIndex];
                if (!IsActiveSolid(obstacle) || obstacle.attachedRigidbody == body
                    || obstacle.attachedRigidbody == exchangedBody || !ShouldCollide(source, obstacle))
                {
                    continue;
                }

                ColliderDistance2D distance = Physics2D.Distance(firstShape.Collider, obstacle);
                if (!distance.isValid || distance.distance < -tolerance)
                {
                    blocked = true;
                    if (distance.isValid && distance.normal.y < 0f
                        && destinationSupports != null && destinationSupports.Contains(obstacle))
                    {
                        blockedBySupport = true;
                    }
                }
            }
        }
        return blocked;
    }

    private bool FinalBodiesOverlap(
        Rigidbody2D king, Rigidbody2D rook, Vector2 kingDestination, Vector2 rookDestination,
        float tolerance)
    {
        for (int kingIndex = 0; kingIndex < kingColliders.Count; kingIndex++)
        {
            Collider2D kingCollider = kingColliders[kingIndex];
            firstShape.Copy(kingCollider, kingDestination, king.rotation);
            for (int rookIndex = 0; rookIndex < rookColliders.Count; rookIndex++)
            {
                Collider2D rookCollider = rookColliders[rookIndex];
                if (!ShouldCollide(kingCollider, rookCollider))
                {
                    continue;
                }
                secondShape.Copy(rookCollider, rookDestination, rook.rotation);
                ColliderDistance2D distance = Physics2D.Distance(firstShape.Collider, secondShape.Collider);
                if (!distance.isValid || distance.distance < -tolerance)
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static bool ShouldCollide(Collider2D first, Collider2D second)
    {
        if (Physics2D.GetIgnoreCollision(first, second))
        {
            return false;
        }
        bool layerCollision = !Physics2D.GetIgnoreLayerCollision(first.gameObject.layer, second.gameObject.layer);
        bool firstDecision = LayerDecision(first, second.gameObject.layer, layerCollision, out bool firstOverride);
        bool secondDecision = LayerDecision(second, first.gameObject.layer, layerCollision, out bool secondOverride);
        if (firstDecision == secondDecision)
        {
            return firstDecision;
        }
        if (!firstOverride)
        {
            return secondDecision;
        }
        if (!secondOverride)
        {
            return firstDecision;
        }
        if (first.layerOverridePriority == second.layerOverridePriority)
        {
            return false;
        }
        return first.layerOverridePriority > second.layerOverridePriority ? firstDecision : secondDecision;
    }

    private static bool LayerDecision(Collider2D collider, int otherLayer, bool baseDecision, out bool hasOverride)
    {
        int include = collider.includeLayers;
        int exclude = collider.excludeLayers;
        Rigidbody2D body = collider.attachedRigidbody;
        if (body != null)
        {
            include |= body.includeLayers;
            exclude |= body.excludeLayers;
        }
        int otherLayerBit = 1 << otherLayer;
        hasOverride = ((include | exclude) & otherLayerBit) != 0;
        return (baseDecision || (include & otherLayerBit) != 0) && (exclude & otherLayerBit) == 0;
    }

    private sealed class QueryShape : IDisposable
    {
        private readonly GameObject gameObject;
        private readonly Rigidbody2D body;
        private readonly PhysicsShapeGroup2D shapes = new();
        public CustomCollider2D Collider { get; }

        public QueryShape(string name)
        {
            gameObject = new GameObject(name) { hideFlags = HideFlags.HideAndDontSave };
            body = gameObject.AddComponent<Rigidbody2D>();
            body.simulated = false;
            Collider = gameObject.AddComponent<CustomCollider2D>();
            Collider.isTrigger = true;
        }

        public void Copy(Collider2D source, Vector2 position, float rotation)
        {
            // Native shapes already contain offset, scale and child transforms in
            // the source Rigidbody's local space. Neither real body is moved.
            shapes.Clear();
            source.GetShapes(shapes);
            shapes.localToWorldMatrix = Matrix4x4.identity;
            Collider.SetCustomShapes(shapes);
            body.position = position;
            body.rotation = rotation;
        }

        public void Dispose()
        {
            if (gameObject != null)
            {
                UnityEngine.Object.Destroy(gameObject);
            }
        }
    }
}
