using UnityEngine;

public static class BulletEffects
{
    private const float TracerLifetime = 0.04f;
    private const float TracerWidth = 0.03f;
    private const float BulletHoleLifetime = 30f;
    private const float BulletHoleSize = 0.06f;

    private static Material tracerMat;
    private static Material bulletHoleMat;

    public static void SpawnTracer(Vector3 from, Vector3 to)
    {
        GameObject go = new GameObject("Tracer");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = GetTracerMat();
        lr.startWidth = TracerWidth;
        lr.endWidth = TracerWidth;
        lr.positionCount = 2;
        lr.SetPosition(0, from);
        lr.SetPosition(1, to);
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        Object.Destroy(go, TracerLifetime);
    }

    public static void SpawnKnifeHit(RaycastHit hit, bool isFleshHit)
    {
        // Quick burst of small lines radiating outward from the impact point —
        // a simple "spark" that reads visually as a slash effect.
        for (int i = 0; i < 6; i++)
        {
            GameObject go = new GameObject("KnifeSpark");
            go.transform.position = hit.point;
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.material = GetTracerMat();
            lr.startWidth = 0.02f;
            lr.endWidth = 0.005f;
            lr.positionCount = 2;

            Vector3 randomDir = Quaternion.LookRotation(hit.normal) * Random.insideUnitSphere * 0.25f;
            randomDir.z = Mathf.Abs(randomDir.z); // bias outward from surface
            lr.SetPosition(0, hit.point);
            lr.SetPosition(1, hit.point + randomDir);
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            // Red sparks for flesh hits, white-ish for hard surfaces
            if (lr.material.HasProperty("_EmissiveColor"))
            {
                Color c = isFleshHit ? new Color(2f, 0.2f, 0.2f) : new Color(1.5f, 1.5f, 1.5f);
                lr.material.SetColor("_EmissiveColor", c);
            }

            Object.Destroy(go, 0.12f);
        }
    }

    public static void SpawnBulletHole(RaycastHit hit)
    {
        // Skip bullet holes on dynamic things (players, bots): the marker would float
        // around with the rig. Only stick to static-looking colliders.
        if (hit.collider.GetComponentInParent<PlayerHealth>() != null) return;
        if (hit.rigidbody != null) return;

        GameObject hole = GameObject.CreatePrimitive(PrimitiveType.Quad);
        hole.name = "BulletHole";
        Object.Destroy(hole.GetComponent<Collider>());

        // Position slightly off the surface to avoid z-fighting
        hole.transform.position = hit.point + hit.normal * 0.005f;
        hole.transform.rotation = Quaternion.LookRotation(-hit.normal);
        hole.transform.localScale = Vector3.one * BulletHoleSize;
        hole.transform.SetParent(hit.collider.transform, true);

        Renderer rend = hole.GetComponent<Renderer>();
        rend.material = GetBulletHoleMat();
        rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Object.Destroy(hole, BulletHoleLifetime);
    }

    private static Material GetTracerMat()
    {
        if (tracerMat == null)
        {
            Shader unlit = Shader.Find("HDRP/Unlit") ?? Shader.Find("Unlit/Color");
            tracerMat = new Material(unlit);
            Color c = new Color(1f, 0.85f, 0.3f, 1f);
            if (tracerMat.HasProperty("_UnlitColor")) tracerMat.SetColor("_UnlitColor", c);
            if (tracerMat.HasProperty("_EmissiveColor"))
            {
                tracerMat.SetColor("_EmissiveColor", new Color(2f, 1.6f, 0.4f));
                tracerMat.EnableKeyword("_EMISSION");
            }
            if (tracerMat.HasProperty("_Color")) tracerMat.SetColor("_Color", c);
        }
        return tracerMat;
    }

    private static Material GetBulletHoleMat()
    {
        if (bulletHoleMat == null)
        {
            Shader hdrp = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
            bulletHoleMat = new Material(hdrp);
            Color dark = new Color(0.05f, 0.05f, 0.05f, 1f);
            if (bulletHoleMat.HasProperty("_BaseColor")) bulletHoleMat.SetColor("_BaseColor", dark);
            if (bulletHoleMat.HasProperty("_Color")) bulletHoleMat.SetColor("_Color", dark);
            if (bulletHoleMat.HasProperty("_Metallic")) bulletHoleMat.SetFloat("_Metallic", 0f);
            if (bulletHoleMat.HasProperty("_Smoothness")) bulletHoleMat.SetFloat("_Smoothness", 0.1f);
        }
        return bulletHoleMat;
    }
}
