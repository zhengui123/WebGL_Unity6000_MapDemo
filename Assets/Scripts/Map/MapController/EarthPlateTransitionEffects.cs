using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 过渡阶段：前半加浓/覆盖，后半消散；中点切换地球与板块。
/// </summary>
public static class EarthPlateTransitionPhase
{
    public static void Evaluate(float progress, float coverPhaseEnd, out bool isCovering, out float coverT, out float revealT)
    {
        float end = Mathf.Clamp(coverPhaseEnd, 0.35f, 0.65f);
        isCovering = progress < end;
        coverT = isCovering ? progress / Mathf.Max(end, 1e-4f) : 1f;
        revealT = isCovering ? 0f : 1f - (progress - end) / Mathf.Max(1f - end, 1e-4f);
    }
}

public abstract class EarthPlateTransitionEffectBase
{
    protected Camera Camera;
    protected Transform WorldAnchor;
    protected EarthPlateTransitionConfig Config;
    protected Color MainColor;
    protected Color AccentColor;
    protected float CoverPhaseEnd;
    protected readonly List<GameObject> SpawnedObjects = new List<GameObject>();
    protected readonly List<Material> MaterialInstances = new List<Material>();
    protected readonly List<ParticleSystem> ParticleSystems = new List<ParticleSystem>();

    public virtual void Setup(
        Camera camera,
        Transform worldAnchor,
        EarthPlateTransitionConfig config,
        Color mainColor,
        Color accentColor,
        float coverPhaseEnd)
    {
        Camera = camera;
        WorldAnchor = worldAnchor != null ? worldAnchor : camera != null ? camera.transform : null;
        Config = config;
        MainColor = mainColor;
        AccentColor = accentColor;
        CoverPhaseEnd = coverPhaseEnd;
    }

    public abstract void Show();
    public abstract void SetProgress(float progress);

    public virtual void Hide()
    {
        for (int i = 0; i < SpawnedObjects.Count; i++)
        {
            if (SpawnedObjects[i] != null)
            {
                SpawnedObjects[i].SetActive(false);
            }
        }
    }

    public virtual void Dispose()
    {
        Hide();
        for (int i = 0; i < SpawnedObjects.Count; i++)
        {
            if (SpawnedObjects[i] != null)
            {
                Object.Destroy(SpawnedObjects[i]);
            }
        }

        SpawnedObjects.Clear();
        ParticleSystems.Clear();

        for (int i = 0; i < MaterialInstances.Count; i++)
        {
            if (MaterialInstances[i] != null)
            {
                Object.Destroy(MaterialInstances[i]);
            }
        }

        MaterialInstances.Clear();
    }

    protected GameObject InstantiatePrefab(GameObject prefab, Transform parent, string fallbackName)
    {
        GameObject instance;
        if (prefab != null)
        {
            instance = Object.Instantiate(prefab, parent);
            instance.name = prefab.name;
        }
        else
        {
            instance = new GameObject(fallbackName);
            if (parent != null)
            {
                instance.transform.SetParent(parent, false);
            }
        }

        SpawnedObjects.Add(instance);
        return instance;
    }

    protected static ParticleSystem FindParticle(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        Transform child = root.Find(childName);
        return child != null ? child.GetComponent<ParticleSystem>() : null;
    }

    protected Vector3 GetFocusPosition()
    {
        if (Camera == null)
        {
            return WorldAnchor != null ? WorldAnchor.position : Vector3.zero;
        }

        if (WorldAnchor != null)
        {
            return Vector3.Lerp(Camera.transform.position, WorldAnchor.position, 0.55f);
        }

        return Camera.transform.position + Camera.transform.forward * 800f;
    }

    protected ParticleSystem CreateFogParticleSystem(
        Transform parent,
        string name,
        float shapeRadius,
        float startSize,
        float startSpeed,
        float maxRate,
        Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = Vector3.zero;
        SpawnedObjects.Add(go);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ParticleSystems.Add(ps);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 5f;
        main.startSpeed = startSpeed;
        main.startSize = startSize;
        main.startColor = color;
        main.maxParticles = 800;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = shapeRadius;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.Distance;
        renderer.minParticleSize = 0f;
        renderer.maxParticleSize = 2.5f;
        Material cloudMat = Config != null && Config.ParticleCloudMaterial != null
            ? new Material(Config.ParticleCloudMaterial)
            : EarthPlateParticleMaterials.CreateSoftCloudMaterial(color);
        if (cloudMat.HasProperty("_TintColor"))
        {
            cloudMat.SetColor("_TintColor", color);
        }

        if (cloudMat.HasProperty("_Color"))
        {
            cloudMat.SetColor("_Color", color);
        }

        renderer.material = cloudMat;
        MaterialInstances.Add(renderer.material);
        return ps;
    }

    protected static void SetEmissionRate(ParticleSystem ps, float rate)
    {
        if (ps == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = rate;
    }

    protected static void SetParticleAlpha(ParticleSystem ps, float alphaMul)
    {
        if (ps == null)
        {
            return;
        }

        ParticleSystem.MainModule main = ps.main;
        Color c = main.startColor.color;
        c.a *= Mathf.Clamp01(alphaMul);
        main.startColor = c;
    }
}

/// <summary>
/// 云雾：相机前方世界空间粒子云逐渐加浓，穿出云层后露出板块。
/// </summary>
public sealed class CloudFogPlateTransition : EarthPlateTransitionEffectBase
{
    private Transform _fogRoot;
    private ParticleSystem _mistNear;
    private ParticleSystem _cloudMid;
    private ParticleSystem _hazeFar;

    public override void Show()
    {
        if (Camera == null)
        {
            return;
        }

        GameObject root = InstantiatePrefab(Config != null ? Config.CloudFogPrefab : null, null, "CloudFogTransition");
        root.transform.position = Camera.transform.position;
        root.transform.rotation = Camera.transform.rotation;
        _fogRoot = root.transform;

        _mistNear = FindParticle(_fogRoot, "MistNear");
        _cloudMid = FindParticle(_fogRoot, "CloudMid");
        _hazeFar = FindParticle(_fogRoot, "HazeFar");

        if (_mistNear == null)
        {
            Color nearC = MainColor;
            Color midC = Color.Lerp(MainColor, AccentColor, 0.35f);
            Color farC = AccentColor;
            _mistNear = CreateFogParticleSystem(_fogRoot, "MistNear", 180f, 120f, 35f, 90f, nearC);
            _cloudMid = CreateFogParticleSystem(_fogRoot, "CloudMid", 420f, 220f, 18f, 45f, midC);
            _hazeFar = CreateFogParticleSystem(_fogRoot, "HazeFar", 900f, 380f, 8f, 22f, farC);
        }
        else
        {
            ParticleSystems.Add(_mistNear);
            ParticleSystems.Add(_cloudMid);
            ParticleSystems.Add(_hazeFar);
        }

        SetEmissionRate(_mistNear, 0f);
        SetEmissionRate(_cloudMid, 0f);
        SetEmissionRate(_hazeFar, 0f);
    }

    public override void SetProgress(float progress)
    {
        EarthPlateTransitionPhase.Evaluate(progress, CoverPhaseEnd, out bool covering, out float coverT, out float revealT);

        if (_fogRoot != null && Camera != null)
        {
            _fogRoot.position = Vector3.Lerp(Camera.transform.position, GetFocusPosition(), 0.25f);
            _fogRoot.rotation = Camera.transform.rotation;
        }

        if (covering)
        {
            float density = Mathf.SmoothStep(0f, 1f, coverT);
            SetEmissionRate(_mistNear, density * 95f);
            SetEmissionRate(_cloudMid, density * 48f);
            SetEmissionRate(_hazeFar, density * 20f);
            SetParticleAlpha(_mistNear, 0.45f + density * 0.55f);
            SetParticleAlpha(_cloudMid, 0.35f + density * 0.5f);
            SetParticleAlpha(_hazeFar, 0.25f + density * 0.4f);
        }
        else
        {
            float fade = 1f - Mathf.SmoothStep(0f, 1f, revealT);
            SetEmissionRate(_mistNear, fade * 30f);
            SetEmissionRate(_cloudMid, fade * 15f);
            SetEmissionRate(_hazeFar, fade * 6f);
            SetParticleAlpha(_mistNear, fade * 0.5f);
            SetParticleAlpha(_cloudMid, fade * 0.4f);
            SetParticleAlpha(_hazeFar, fade * 0.3f);
        }
    }
}

/// <summary>
/// 科技扫描：世界空间水平扫描波掠过场景，网格数字化覆盖地球区域。
/// </summary>
public sealed class TechScanPlateTransition : EarthPlateTransitionEffectBase
{
    private static readonly int ScanLineId = Shader.PropertyToID("_ScanLine");
    private static readonly int FillAmountId = Shader.PropertyToID("_FillAmount");
    private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

    private Material _scanMaterial;
    private Transform _scanPlane;
    private LineRenderer _ringLine;
    private float _planeBaseY;

    public override void Show()
    {
        Vector3 focus = GetFocusPosition();
        _planeBaseY = focus.y - 1200f;

        _scanMaterial = EarthPlateParticleMaterials.CreateTechScanWaveMaterial(MainColor, AccentColor);
        MaterialInstances.Add(_scanMaterial);

        GameObject root = InstantiatePrefab(Config != null ? Config.TechScanPrefab : null, null, "TechScanTransition");
        root.transform.position = new Vector3(focus.x, _planeBaseY, focus.z);

        Transform planeT = root.transform.Find("ScanPlane");
        if (planeT != null)
        {
            _scanPlane = planeT;
            MeshRenderer mr = planeT.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = _scanMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
        else
        {
            GameObject plane = GameObject.CreatePrimitive(PrimitiveType.Quad);
            plane.name = "ScanPlane";
            Object.Destroy(plane.GetComponent<Collider>());
            plane.transform.SetParent(root.transform, false);
            plane.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            plane.transform.localScale = new Vector3(6000f, 6000f, 1f);
            plane.GetComponent<MeshRenderer>().sharedMaterial = _scanMaterial;
            _scanPlane = plane.transform;
            SpawnedObjects.Add(plane);
        }

        Transform ringT = root.transform.Find("ScanRing");
        _ringLine = ringT != null ? ringT.GetComponent<LineRenderer>() : null;
        if (_ringLine == null)
        {
            GameObject ringGo = new GameObject("ScanRing");
            ringGo.transform.SetParent(root.transform, false);
            ringGo.transform.position = focus;
            _ringLine = ringGo.AddComponent<LineRenderer>();
            _ringLine.useWorldSpace = true;
            _ringLine.loop = true;
            _ringLine.widthMultiplier = 8f;
            _ringLine.positionCount = 64;
            SpawnedObjects.Add(ringGo);
        }

        Material lineMat = EarthPlateParticleMaterials.CreateTechScanLineMaterial(AccentColor);
        _ringLine.material = lineMat;
        MaterialInstances.Add(lineMat);
        _ringLine.startColor = AccentColor;
        _ringLine.endColor = AccentColor;

        float radius = 400f;
        for (int i = 0; i < _ringLine.positionCount; i++)
        {
            float a = (float)i / _ringLine.positionCount * Mathf.PI * 2f;
            _ringLine.SetPosition(i, focus + new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
    }

    public override void SetProgress(float progress)
    {
        EarthPlateTransitionPhase.Evaluate(progress, CoverPhaseEnd, out bool covering, out float coverT, out float revealT);

        if (_scanPlane != null)
        {
            float ySpan = 2400f;
            if (covering)
            {
                _scanPlane.position = new Vector3(
                    _scanPlane.position.x,
                    _planeBaseY + coverT * ySpan,
                    _scanPlane.position.z);
            }
            else
            {
                float t = 1f - revealT;
                _scanPlane.position = new Vector3(
                    _scanPlane.position.x,
                    _planeBaseY + t * ySpan,
                    _scanPlane.position.z);
            }
        }

        if (_scanMaterial != null)
        {
            float scanLine = covering ? coverT : 1f - revealT;
            float fill = covering ? coverT * 0.85f : (1f - revealT) * 0.85f;
            float intensity = covering
                ? Mathf.Lerp(0.2f, 1.2f, coverT)
                : Mathf.Lerp(1.2f, 0f, revealT);

            _scanMaterial.SetFloat(ScanLineId, scanLine);
            _scanMaterial.SetFloat(FillAmountId, fill);
            _scanMaterial.SetFloat(IntensityId, intensity);
        }

        if (_ringLine != null)
        {
            float ringAlpha = covering ? coverT : 1f - revealT;
            Color c = AccentColor;
            c.a *= ringAlpha;
            _ringLine.startColor = c;
            _ringLine.endColor = c;
            float scale = 400f + ringAlpha * 600f;
            Vector3 focus = GetFocusPosition();
            for (int i = 0; i < _ringLine.positionCount; i++)
            {
                float a = (float)i / _ringLine.positionCount * Mathf.PI * 2f;
                _ringLine.SetPosition(i, focus + new Vector3(Mathf.Cos(a) * scale, focus.y + 50f, Mathf.Sin(a) * scale));
            }
        }
    }
}

/// <summary>
/// 轨道俯冲：相机前推 + 迎面速度线粒子，穿过视角后露出板块。
/// </summary>
public sealed class DiveRevealPlateTransition : EarthPlateTransitionEffectBase
{
    private Transform _cameraTransform;
    private Vector3 _baseLocalPos;
    private ParticleSystem _streakPs;
    private Transform _streakRoot;
    private float _startCamLocalY;

    public override void Setup(
        Camera camera,
        Transform worldAnchor,
        EarthPlateTransitionConfig config,
        Color mainColor,
        Color accentColor,
        float coverPhaseEnd)
    {
        base.Setup(camera, worldAnchor, config, mainColor, accentColor, coverPhaseEnd);
        _cameraTransform = camera != null ? camera.transform : null;
        if (_cameraTransform != null)
        {
            _baseLocalPos = _cameraTransform.localPosition;
            _startCamLocalY = _baseLocalPos.y;
        }
    }

    public override void Show()
    {
        if (Camera == null || _cameraTransform == null)
        {
            return;
        }

        _cameraTransform.localPosition = _baseLocalPos;
        _startCamLocalY = _baseLocalPos.y;

        GameObject root = InstantiatePrefab(
            Config != null ? Config.DiveRevealPrefab : null,
            _cameraTransform,
            "DiveRevealTransition");
        root.transform.localPosition = Vector3.zero;
        root.transform.localRotation = Quaternion.identity;
        _streakRoot = root.transform;

        Transform streakT = _streakRoot.Find("DiveStreaks");
        _streakPs = streakT != null ? streakT.GetComponent<ParticleSystem>() : _streakRoot.GetComponentInChildren<ParticleSystem>();

        if (_streakPs == null)
        {
            GameObject psGo = new GameObject("DiveStreaks");
            psGo.transform.SetParent(_streakRoot, false);
            _streakPs = psGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = _streakPs.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = 0.6f;
            main.startSpeed = 0f;
            main.startSize = 25f;
            main.startColor = AccentColor;
            main.maxParticles = 1200;
            ParticleSystem.ShapeModule shape = _streakPs.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.15f;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            ParticleSystem.VelocityOverLifetimeModule vel = _streakPs.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.z = new ParticleSystem.MinMaxCurve(280f);
            ParticleSystemRenderer renderer = _streakPs.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 3.5f;
            renderer.velocityScale = 0.2f;
            renderer.material = EarthPlateParticleMaterials.CreateStreakMaterial(AccentColor);
            MaterialInstances.Add(renderer.material);
        }
        else
        {
            ParticleSystems.Add(_streakPs);
            Material streakMat = EarthPlateParticleMaterials.CreateStreakMaterial(AccentColor);
            _streakPs.GetComponent<ParticleSystemRenderer>().material = streakMat;
            MaterialInstances.Add(streakMat);
        }

        SetEmissionRate(_streakPs, 0f);
    }

    public override void SetProgress(float progress)
    {
        EarthPlateTransitionPhase.Evaluate(progress, CoverPhaseEnd, out bool covering, out float coverT, out float revealT);

        if (_cameraTransform == null)
        {
            return;
        }

        if (covering)
        {
            float dive = Mathf.SmoothStep(0f, 1f, coverT);
            float targetY = Mathf.Lerp(_startCamLocalY, _startCamLocalY * 0.52f, dive);
            _cameraTransform.localPosition = new Vector3(_baseLocalPos.x, targetY, _baseLocalPos.z);

            if (_streakPs != null)
            {
                SetEmissionRate(_streakPs, dive * 420f);
                ParticleSystem.MainModule main = _streakPs.main;
                main.startSize = Mathf.Lerp(12f, 55f, dive);
                ParticleSystem.VelocityOverLifetimeModule vel = _streakPs.velocityOverLifetime;
                vel.z = new ParticleSystem.MinMaxCurve(Mathf.Lerp(120f, 520f, dive));
            }
        }
        else
        {
            float ease = 1f - Mathf.SmoothStep(0f, 1f, revealT);
            float targetY = Mathf.Lerp(_startCamLocalY * 0.52f, _startCamLocalY, revealT);
            _cameraTransform.localPosition = new Vector3(_baseLocalPos.x, targetY, _baseLocalPos.z);

            if (_streakPs != null)
            {
                SetEmissionRate(_streakPs, ease * 80f);
                SetParticleAlpha(_streakPs, ease);
            }
        }
    }

    public override void Hide()
    {
        base.Hide();
        RestoreCamera();
    }

    public override void Dispose()
    {
        RestoreCamera();
        base.Dispose();
    }

    private void RestoreCamera()
    {
        if (_cameraTransform != null)
        {
            _cameraTransform.localPosition = _baseLocalPos;
        }
    }
}
