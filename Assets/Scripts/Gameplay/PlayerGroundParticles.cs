using UnityEngine;
using UnityEngine.Tilemaps;

[RequireComponent(typeof(PolygonCollider2D))]
public sealed class PlayerGroundParticles : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float footstepDistance = 0.9f;

    private const int PoolSize = 12;
    private static readonly Color OutlineColor = new Color(0.13f, 0.16f, 0.19f, 0.85f);
    private static readonly Color CoreColor = new Color(1f, 0.84f, 0.55f, 0.95f);
    private static readonly Color DarkPlatformColor = new Color(101f / 255f, 103f / 255f, 106f / 255f, 0.72f);
    private static readonly Color LightPlatformColor = new Color(1f, 248f / 255f, 238f / 255f, 0.72f);

    private readonly DustParticle[] particles = new DustParticle[PoolSize];
    private GameObject particleRoot;
    private Sprite dustSprite;
    private float footY;
    private float footCenterX;
    private float footHalfWidth;
    private float lastFootstepX;
    private float distanceSinceFootstep;
    private bool wasWalking;
    private int nextParticle;

    private struct DustParticle
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public SpriteRenderer coreRenderer;
        public Vector2 velocity;
        public float angularVelocity;
        public float lifetime;
        public float age;
        public float size;
        public float heightRatio;
        public bool isBurst;
        public Color outlineColor;
        public Color coreColor;
    }

    private void Awake()
    {
        PolygonCollider2D bodyCollider = GetComponent<PolygonCollider2D>();
        Vector2[] points = bodyCollider.points;
        footY = points[0].y + bodyCollider.offset.y;
        float left = points[0].x;
        float right = points[0].x;
        for (int index = 1; index < points.Length; index++)
        {
            footY = Mathf.Min(footY, points[index].y + bodyCollider.offset.y);
            left = Mathf.Min(left, points[index].x);
            right = Mathf.Max(right, points[index].x);
        }

        footCenterX = (left + right) * 0.5f + bodyCollider.offset.x;
        footHalfWidth = (right - left) * 0.5f;

        dustSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f), 1f);

        SpriteRenderer[] bodyRenderers = GetComponentsInChildren<SpriteRenderer>();
        int sortingLayerId = bodyRenderers.Length > 0 ? bodyRenderers[0].sortingLayerID : 0;
        int sortingOrder = 0;
        foreach (SpriteRenderer bodyRenderer in bodyRenderers)
        {
            sortingOrder = Mathf.Max(sortingOrder, bodyRenderer.sortingOrder);
        }

        particleRoot = new GameObject($"{name} Ground Particles");
        for (int index = 0; index < particles.Length; index++)
        {
            GameObject particleObject = new GameObject("FootDust");
            particleObject.transform.SetParent(particleRoot.transform, false);
            SpriteRenderer renderer = particleObject.AddComponent<SpriteRenderer>();
            renderer.sprite = dustSprite;
            renderer.sortingLayerID = sortingLayerId;
            renderer.sortingOrder = sortingOrder + 1;
            renderer.enabled = false;

            GameObject coreObject = new GameObject("Core");
            coreObject.transform.SetParent(particleObject.transform, false);
            coreObject.transform.localScale = Vector3.one * 0.55f;
            SpriteRenderer coreRenderer = coreObject.AddComponent<SpriteRenderer>();
            coreRenderer.sprite = dustSprite;
            coreRenderer.sortingLayerID = sortingLayerId;
            coreRenderer.sortingOrder = sortingOrder + 2;
            coreRenderer.enabled = false;

            particles[index].transform = particleObject.transform;
            particles[index].renderer = renderer;
            particles[index].coreRenderer = coreRenderer;
        }
    }

    private void Update()
    {
        for (int index = 0; index < particles.Length; index++)
        {
            DustParticle particle = particles[index];
            if (!particle.renderer.enabled)
            {
                continue;
            }

            particle.age += Time.deltaTime;
            if (particle.age >= particle.lifetime)
            {
                particle.renderer.enabled = false;
                particle.coreRenderer.enabled = false;
                particles[index] = particle;
                continue;
            }

            particle.transform.position += (Vector3)(particle.velocity * Time.deltaTime);
            particle.transform.Rotate(0f, 0f, particle.angularVelocity * Time.deltaTime);
            particle.velocity.y -= 1.5f * Time.deltaTime;
            float remaining = 1f - particle.age / particle.lifetime;
            float scale = particle.size * Mathf.Lerp(0.45f, 1f, remaining);
            particle.transform.localScale = new Vector3(scale, scale * particle.heightRatio, 1f);
            float opacity = particle.isBurst ? Mathf.Clamp01(remaining * 1.5f) : remaining;
            Color outline = particle.outlineColor;
            outline.a *= opacity;
            particle.renderer.color = outline;
            Color core = particle.coreColor;
            core.a *= opacity;
            particle.coreRenderer.color = core;
            particles[index] = particle;
        }
    }

    private void OnDisable()
    {
        wasWalking = false;
        distanceSinceFootstep = 0f;
        foreach (DustParticle particle in particles)
        {
            if (particle.renderer != null)
            {
                particle.renderer.enabled = false;
                particle.coreRenderer.enabled = false;
            }
        }
    }

    private void OnDestroy()
    {
        if (particleRoot != null)
        {
            Destroy(particleRoot);
        }

        if (dustSprite != null)
        {
            Destroy(dustSprite);
        }

    }

    public bool SetWalking(bool isWalking)
    {
        if (!isWalking)
        {
            wasWalking = false;
            distanceSinceFootstep = 0f;
            return false;
        }

        float currentX = transform.position.x;
        if (!wasWalking)
        {
            lastFootstepX = currentX;
            wasWalking = true;
            return false;
        }

        float movement = currentX - lastFootstepX;
        distanceSinceFootstep += Mathf.Abs(movement);
        lastFootstepX = currentX;
        if (distanceSinceFootstep < footstepDistance)
        {
            return false;
        }

        distanceSinceFootstep %= footstepDistance;
        Emit(2, 0.22f, -Mathf.Sign(movement));
        return true;
    }

    public void PlayJump()
    {
        EmitBurst(3, 0.28f, true);
    }

    public void PlayLanding()
    {
        EmitBurst(4, 0.26f, false);
    }

    private void Emit(int count, float lifetime, float trailingSide)
    {
        for (int index = 0; index < count; index++)
        {
            int slot = nextParticle++ % particles.Length;
            DustParticle particle = particles[slot];
            float side = trailingSide == 0f ? (index % 2 == 0 ? -1f : 1f) : trailingSide;
            Vector3 origin = transform.TransformPoint(new Vector3(
                footCenterX + side * (footHalfWidth + Random.Range(-0.04f, 0.08f)),
                footY + 0.06f, 0f));
            origin.z -= 0.1f;
            particle.transform.position = origin;
            particle.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-35f, 35f));
            particle.velocity = new Vector2(side * Random.Range(0.6f, 1.4f),
                Random.Range(0.45f, 1.1f));
            particle.angularVelocity = 0f;
            particle.lifetime = lifetime;
            particle.age = 0f;
            particle.size = Random.Range(0.1f, 0.15f);
            particle.heightRatio = 1f;
            particle.isBurst = false;
            particle.outlineColor = OutlineColor;
            particle.coreColor = CoreColor;
            particle.transform.localScale = Vector3.one * particle.size;
            particle.renderer.sprite = dustSprite;
            particle.coreRenderer.sprite = dustSprite;
            particle.renderer.color = OutlineColor;
            particle.renderer.enabled = true;
            particle.coreRenderer.color = CoreColor;
            particle.coreRenderer.enabled = true;
            particles[slot] = particle;
        }
    }

    private void EmitBurst(int count, float lifetime, bool isJump)
    {
        Color puffColor = isJump ? OutlineColor : ResolveLandingPuffColor();
        for (int index = 0; index < count; index++)
        {
            int slot = nextParticle++ % particles.Length;
            DustParticle particle = particles[slot];
            float side = index % 2 == 0 ? -1f : 1f;
            Vector3 origin = transform.TransformPoint(new Vector3(
                footCenterX + side * Random.Range(0.07f, 0.28f),
                footY + Random.Range(0.04f, 0.14f), 0f));
            origin.z -= 0.1f;
            particle.transform.position = origin;
            particle.transform.rotation = Quaternion.Euler(0f, 0f, Random.Range(-45f, 45f));
            particle.velocity = new Vector2(
                side * Random.Range(isJump ? 0.5f : 1.1f, isJump ? 1.1f : 1.9f),
                Random.Range(isJump ? 0.45f : 0.25f, isJump ? 0.85f : 0.55f));
            particle.angularVelocity = Random.Range(-360f, 360f);
            particle.lifetime = lifetime;
            particle.age = 0f;
            particle.size = Random.Range(0.11f, 0.18f);
            particle.heightRatio = Random.Range(0.55f, 0.9f);
            particle.isBurst = true;
            particle.outlineColor = puffColor;
            particle.coreColor = isJump ? CoreColor : Color.clear;
            particle.transform.localScale = new Vector3(
                particle.size, particle.size * particle.heightRatio, 1f);
            particle.renderer.sprite = dustSprite;
            particle.coreRenderer.sprite = dustSprite;
            particle.renderer.color = particle.outlineColor;
            particle.renderer.enabled = true;
            particle.coreRenderer.color = particle.coreColor;
            particle.coreRenderer.enabled = isJump;
            particles[slot] = particle;
        }
    }

    private Color ResolveLandingPuffColor()
    {
        Vector3 footPosition = transform.TransformPoint(new Vector3(footCenterX, footY + 0.18f, 0f));
        RaycastHit2D groundHit = Physics2D.Raycast(footPosition, Vector2.down, 0.45f,
            LayerMask.GetMask(Layers.Environment));
        if (groundHit.collider == null)
        {
            return DarkPlatformColor;
        }

        Tilemap tilemap = groundHit.collider.GetComponent<Tilemap>();
        if (tilemap == null)
        {
            return DarkPlatformColor;
        }

        Vector3Int cell = tilemap.WorldToCell(groundHit.point - groundHit.normal * 0.08f);
        Sprite surfaceSprite = tilemap.GetSprite(cell);
        if (surfaceSprite == null || surfaceSprite.texture.name != "TileMap_Main")
        {
            return DarkPlatformColor;
        }

        bool isDarkTile = surfaceSprite.textureRect.center.y > surfaceSprite.texture.height * 0.5f;
        return (isDarkTile ? DarkPlatformColor : LightPlatformColor)
            * tilemap.color * tilemap.GetColor(cell);
    }
}
