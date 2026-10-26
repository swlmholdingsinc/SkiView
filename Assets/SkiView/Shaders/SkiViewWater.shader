// SKI VIEW lake water (URP, self-written, no paid assets) - realism pass v5.
// Calm, dark, glassy Pacific-NW lake: planar mirror reflection (PlanarReflection.cs, reduced-res RT) with Schlick
// Fresnel, reflection-probe (HDRI cubemap) fallback, tiny gentle swell, small capillary ripples gathered into drifting
// breeze patches (cat's-paws) with glassy lanes between them, sun glint. Boat wake: Kelvin V crests with real slope
// normals (they bend the mirror image like in the photos), turbulent prop-wash foam that breaks into lace and fades,
// and a slowly calming ripple strip behind the boat. Ski spray trail foam. Depth tint from the camera depth texture.
Shader "SkiView/Water"
{
    Properties
    {
        _ShallowColor ("Shallow", Color) = (0.10, 0.16, 0.12, 1)
        _DeepColor ("Deep", Color) = (0.010, 0.026, 0.030, 1)
        _DepthFade ("Depth fade (m)", Float) = 1.6
        _FoamColor ("Foam", Color) = (0.92, 0.95, 0.97, 1)
        _WaveA ("Wave A (dir.xy, steep, length)", Vector) = (1, 0.3, 0.010, 9.0)
        _WaveB ("Wave B", Vector) = (-0.35, 1, 0.008, 5.3)
        _WaveC ("Wave C", Vector) = (0.8, -0.55, 0.006, 3.1)
        _WaveD ("Wave D", Vector) = (-0.95, -0.25, 0.005, 1.9)
        _RippleStrength ("Ripple normal strength", Float) = 0.08
        _GlassyRipple ("Ripple strength in glassy lanes (x)", Range(0,1)) = 0.12
        _BreezeScale ("Breeze patch scale (1/m)", Float) = 0.010
        _ReflDistort ("Reflection distortion", Float) = 0.30
        _ReflTint ("Reflection tint", Color) = (0.92, 0.95, 0.96, 1)
        _SunSharpness ("Sun glint sharpness", Float) = 1400
        _SunSpec ("Sun glint strength (per lake)", Float) = 1
        _WakeHeight ("Wake crest slope", Float) = 1.0
        _FoamTex ("Foam texture (R density, tileable)", 2D) = "gray" {}
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-10" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "Water"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _ShallowColor, _DeepColor, _FoamColor, _ReflTint;
            float4 _WaveA, _WaveB, _WaveC, _WaveD;
            float _DepthFade, _RippleStrength, _GlassyRipple, _BreezeScale, _ReflDistort, _SunSharpness, _WakeHeight, _SunSpec;
            CBUFFER_END

            TEXTURE2D(_FoamTex); SAMPLER(sampler_FoamTex);
            TEXTURE2D(_PlanarReflectionTex); SAMPLER(sampler_PlanarReflectionTex);
            float _PlanarReflectionOn, _PlanarReflectionFlipY;
            float _SVTime;
            float _SVWaterDebug;
            float4 _WakePts[40];   // xyz: stern x, boat speed, stern z ; w: spawn time
            float _WakeCount;
            float4 _SkiPts[24];    // x, speed01, z, time
            float _SkiCount;
            float4 _SVSplash[6];   // pass 12: x, z, start time, strength (> 0 crash/patch: foam + ring ripples; < 0 continuous ripples)
            float4 _BoatPos, _BoatFwd; // _BoatPos.w speed ; _BoatFwd.w WakeEmitter local z (stern)

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float fogFactor : TEXCOORD2;
            };

            float3 Gerstner(float4 w, float3 p, inout float3 tangent, inout float3 binormal, float t)
            {
                float k = 2 * PI / w.w;
                float c = sqrt(9.8 / k);
                float2 d = normalize(w.xy);
                float f = k * (dot(d, p.xz) - c * t);
                float a = w.z / k;
                tangent += float3(-d.x * d.x * (w.z * sin(f)), d.x * (w.z * cos(f)), -d.x * d.y * (w.z * sin(f)));
                binormal += float3(-d.x * d.y * (w.z * sin(f)), d.y * (w.z * cos(f)), -d.y * d.y * (w.z * sin(f)));
                return float3(d.x * (a * cos(f)), a * sin(f), d.y * (a * cos(f)));
            }
            float3 Waves(float3 p, out float3 n, float t)
            {
                float3 tg = float3(1, 0, 0), bn = float3(0, 0, 1);
                float3 o = p;
                o += Gerstner(_WaveA, p, tg, bn, t);
                o += Gerstner(_WaveB, p, tg, bn, t);
                o += Gerstner(_WaveC, p, tg, bn, t);
                o += Gerstner(_WaveD, p, tg, bn, t);
                n = normalize(cross(bn, tg));
                return o;
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float3 n;
                ws = Waves(ws, n, _SVTime);
                o.positionWS = ws;
                o.positionCS = TransformWorldToHClip(ws);
                o.screenPos = ComputeScreenPos(o.positionCS);
                o.fogFactor = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float hash21(float2 p) { p = frac(p * float2(123.34, 456.21)); p += dot(p, p + 45.32); return frac(p.x * p.y); }
            float vnoise(float2 p)
            {
                float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(hash21(i), hash21(i + float2(1, 0)), f.x), lerp(hash21(i + float2(0, 1)), hash21(i + float2(1, 1)), f.x), f.y);
            }
            // value-noise gradient (central differences) - cheap isotropic ripple slope
            float2 vgrad(float2 p) { const float e = 0.25; return float2(vnoise(p + float2(e, 0)) - vnoise(p - float2(e, 0)), vnoise(p + float2(0, e)) - vnoise(p - float2(0, e))) / (2 * e); }

            // small capillary ripples: a few short directional sines + two octaves of drifting noise
            float2 RippleSlope(float2 p, float t)
            {
                float2 s = 0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float ang = i * 2.39996 + 0.4;
                    float2 d = float2(cos(ang), sin(ang));
                    float len = 0.42 * pow(0.68, i) + 0.08;
                    float k = 2 * PI / len;
                    float w = sqrt(9.8 * k + 0.074 * k * k * k / 1000.0);
                    s += d * (k * 0.010 * len * cos(k * dot(d, p) - w * t + i * 1.7));
                }
                s += vgrad(p * 2.3 + float2(t * 0.35, t * 0.21)) * 0.050;
                s += vgrad(p * 6.1 - float2(t * 0.6, -t * 0.45)) * 0.022;
                return s;
            }

            float SegDist(float2 p, float2 a, float2 b, out float h, out float2 dir)
            {
                float2 pa = p - a, ba = b - a;
                h = saturate(dot(pa, ba) / max(dot(ba, ba), 1e-4));
                float2 q = pa - ba * h;
                float d = length(q);
                dir = d > 1e-4 ? q / d : float2(0, 0);
                return d;
            }

            // pass 10 wake model. Out: x foam coverage (ski spray), y turbulence (ripple boost), zw wake slope (world xz);
            // washOut = prop-wash trough mask, capOut = crest-cap foam (before break-up), slickOut = glassy centre lane,
            // trackOut = (signed lateral offset m, path distance behind the transom m, water-anchored along-track coordinate m)
            float4 Wake(float2 p, out float bubblesOut, out float washOut, out float capOut, out float slickOut, out float3 trackOut)
            {
                float foam = 0, turb = 0, bubbles = 0, wash = 0, caps = 0, slick = 0; float2 slope = 0; trackOut = 0;
                int n = (int)_WakeCount;
                float2 stern = _BoatPos.xz + _BoatFwd.xz * _BoatFwd.w;   // WakeEmitter (transom, waterline)
                // nearest point on the wake path (polyline transom -> oldest point): one (distance, age, speed, path length) per
                // pixel. Evaluating every segment separately drew a ring around each path point (capsule end caps).
                float2 prev = stern; float prevAge = 0, prevV = _BoatPos.w, cum = 0;
                float bd = 1e6, ba = 0, bv = 0, bs = 0, bside = 1; float2 bdir = 0;
                [loop] for (int i = 0; i < n; i++)
                {
                    float2 q = _WakePts[i].xz;
                    float age = max(0, _SVTime - _WakePts[i].w);
                    float h; float2 dir;
                    float d = SegDist(p, prev, q, h, dir);
                    float2 sg = q - prev; float L = length(sg);
                    if (d < bd)
                    {
                        bd = d; ba = lerp(prevAge, age, h); bv = lerp(prevV, _WakePts[i].y, h); bdir = dir; bs = cum + h * L;
                        bside = (sg.x * dir.y - sg.y * dir.x) >= 0 ? 1 : -1;
                    }
                    cum += L; prev = q; prevAge = age; prevV = _WakePts[i].y;
                }
                if (n > 0)
                {
                    float d = bd, a = ba, v = bv, s = bs; float2 dir = bdir;
                    float spd = saturate(v / 15.0);
                    // every term decays with distance behind the transom AND with age, so old path segments die out even when
                    // the boat idles, and nothing survives past ~60 m (no lane lines across the lake)
                    float life = exp(-a / 6.0) * smoothstep(75.0, 40.0, s);
                    // water-anchored along-track coordinate: the time the boat passed this spot x a fixed speed (foam stays put on
                    // the water instead of sliding with the boat)
                    trackOut = float3(d * bside, s, (_SVTime - a) * 15.0);
                    // prop wash / trough: boat-wide churn from the transom (half width 1.1 m -> ~4 m), most violent in the first
                    // 10 m, fading over 40-60 m; the glassy "slick" lane opens down its centre after ~6 m
                    float ww = 1.1 + s * 0.05;
                    float xw = d / ww;
                    float band = exp(-xw * xw * xw * xw * xw * xw);
                    wash = band * exp(-s / 24.0) * smoothstep(70.0, 35.0, s) * life * spd;
                    float xs = d / (ww * 0.24);
                    slick = exp(-xs * xs) * smoothstep(8.0, 20.0, s) * exp(-s / 55.0) * life * spd;
                    bubbles = exp(-(d * d) / (ww * ww * 1.6)) * exp(-s / 20.0) * life * spd * 0.6;   // aerated jade trail after the churn calms
                    // V-wake on the Kelvin angle (19.5 deg half-angle, tan = 0.354): crests diverge, wander a little (they are
                    // not rulers), widen and flatten with distance
                    float wob = (vnoise(float2(s * 0.035, bside * 7.3 + 1.7)) - 0.5) * s * 0.07;
                    float arm = 0.9 + s * 0.354 + wob;
                    float cw = 0.45 + s * 0.045;
                    float decay = exp(-s / 22.0) * life * spd;
                    float x1 = (d - arm) / cw, x2 = (d - arm * 0.78) / (cw * 0.8);
                    float g1 = exp(-x1 * x1), g2 = exp(-x2 * x2);
                    float amp = decay * smoothstep(0.0, 4.0, s) * _WakeHeight;
                    // asymmetric rolling crest: steep outer face, long gentle back
                    float x1s = x1 > 0 ? x1 * 1.8 : x1 * 0.7;
                    float g1s = exp(-x1s * x1s);
                    float dh = amp * (-2 * x1s * g1s / cw * 0.30 - 2 * x2 * g2 / (cw * 0.8) * 0.12);
                    slope = dir * dh;
                    // foam caps on the crest lips: white in the first ~15 m, breaking into patches (frag) and gone by ~45 m
                    float cb = (d - arm - cw * 0.1) / (0.3 + s * 0.015);   // a narrow lip, not the whole crest
                    float capI = exp(-cb * cb) * smoothstep(1.0, 4.0, s) * exp(-s / 18.0) * smoothstep(48.0, 25.0, s) * life * spd;
                    caps = capI + g2 * exp(-s / 6.0) * life * spd * 0.3;
                    // disturbed (rougher) water between and around the arms
                    // hull zone: right behind the transom the whole boat width is stirred up, so no clean mirror (the white
                    // stern used to reflect there as two big soft blobs either side of the trough in the boat cam)
                    float hz = exp(-s / 7.0) * saturate(1.4 - d / (2.0 + s * 0.4)) * life * spd;
                    turb = hz * 0.7 + saturate(1.2 - d / (arm + 1.5)) * exp(-s / 50.0) * life * spd * 0.35 + g1 * decay * 0.45 + wash * 0.8 * (1.0 - slick);
                }
                int m = (int)_SkiCount;
                float2 sp = _SkiPts[0].xz; float sa = 0;
                [loop] for (int j = 1; j < m; j++)
                {
                    float2 q = _SkiPts[j].xz;
                    float age = max(0, _SVTime - _SkiPts[j].w);
                    float h; float2 dir;
                    float d = SegDist(p, sp, q, h, dir);
                    float a = lerp(sa, age, h);
                    float w = 0.16 + a * 0.20;
                    float g = exp(-(d * d) / (w * w));
                    foam += g * exp(-a / 1.1) * (0.3 + _SkiPts[j].y) * 0.55;
                    turb += g * exp(-a / 4.0) * 0.5;
                    sp = q; sa = age;
                }
                // pass 12: splash sources (falls, the floating ski, deep-water starts)
                [unroll] for (int k = 0; k < 6; k++)
                {
                    float4 sv = _SVSplash[k];
                    if (sv.w == 0) continue;
                    float age = max(0, _SVTime - sv.z);
                    float2 dv = p - sv.xy; float r = length(dv); float2 dn = r > 1e-3 ? dv / r : float2(0, 0);
                    float st = abs(sv.w);
                    if (sv.w > 0)
                    {
                        // churned foam patch (white, broken by the lace threshold) that spreads and dies in ~3-4 s
                        float pr = 0.7 + 1.1 * sqrt(age) * st;
                        float brk = 0.55 + 0.45 * sin(dot(dv, float2(5.3, 3.1)) + 2.0 * sin(dv.y * 4.1 - dv.x * 2.7 + age));   // break the disc up
                        foam += smoothstep(pr, pr * 0.2, r) * brk * exp(-age / 1.5) * st * 1.0;
                        turb += saturate(1.3 - r / (pr * 1.4)) * exp(-age / 4.0) * st;
                        // two expanding rings
                        float r1 = 0.5 + 1.7 * age, r2 = 0.3 + 1.05 * age;
                        float x1r = (r - r1) / (0.22 + 0.06 * age), x2r = (r - r2) / (0.2 + 0.05 * age);
                        float rf = exp(-age / 2.6) * st;
                        slope += dn * (-2 * x1r * exp(-x1r * x1r) * 0.30 - 2 * x2r * exp(-x2r * x2r) * 0.18) * rf;
                        foam += exp(-x1r * x1r) * exp(-age / 0.9) * st * 0.5;
                    }
                    else
                    {
                        // someone floating: small rings radiating continuously, a thin foam collar
                        float ph = (r - 0.9 * age) / 0.55;
                        slope += dn * sin(ph * 6.2832) * exp(-r / 1.6) * smoothstep(0.25, 0.55, r) * st * 0.16;
                        float xc = (r - 0.42) / 0.14;
                        foam += exp(-xc * xc) * 0.45 * st;
                        turb += saturate(1.0 - r / 2.5) * 0.5 * st;
                    }
                }
                bubblesOut = saturate(bubbles);
                washOut = saturate(wash);
                capOut = saturate(caps);
                slickOut = saturate(slick);
                return float4(foam, saturate(turb), slope);
            }

            half4 frag(Varyings i) : SV_Target
            {
                float t = _SVTime;
                float3 P = i.positionWS;
                float3 V = normalize(GetWorldSpaceViewDir(P));
                float dist = length(_WorldSpaceCameraPos - P);
                float3 n;
                Waves(P, n, t);
                float wakeBubbles, wash, caps, slick; float3 track;
                float4 wk = Wake(P.xz, wakeBubbles, wash, caps, slick, track);
                float tur10 = exp(-track.y / 9.0) * saturate(wash * 3.0);   // first ~10 m behind the transom: the violent churn

                // breeze patches: cat's-paws drifting slowly; glassy lanes between them
                float2 bp = P.xz * _BreezeScale + float2(t * 0.004, t * 0.0025);
                float breeze = smoothstep(0.42, 0.78, vnoise(bp) * 0.65 + vnoise(bp * 3.1 + 7.3) * 0.35);
                float rough = lerp(_GlassyRipple, 1.0, breeze);
                rough = max(rough, wk.y);
                float2 rs = RippleSlope(P.xz, t) * _RippleStrength * rough;
                rs *= 1.0 - saturate(dist / 350.0) * 0.85;               // distance: avoid sparkle / aliasing
                rs += wk.zw;
                // prop wash: boiling, lumpy surface (lifted, chaotic normals) that drifts back and churns
                float2 boil = vgrad(P.xz * 1.3 + float2(t * 0.9, -t * 0.7)) * 0.55 + vgrad(P.xz * 3.7 - float2(t * 1.6, t * 1.1)) * 0.30;
                rs += boil * wash * 0.9 * (0.55 + 0.45 * tur10) * (1.0 - 0.85 * slick);   // the slick lane stays glassy
                // pass 10: the water inside the V is chopped up by the hull and the crests, so the mirror breaks into jittery
                // patches there instead of reflecting whole clouds
                float chop = saturate(wk.y * 1.6) * (1.0 - 0.85 * slick);
                rs += (vgrad(P.xz * 2.1 + float2(t * 1.1, -t * 0.6)) * 0.6 + vgrad(P.xz * 5.3 - t * 1.4) * 0.4) * chop * 0.55;
                n = normalize(n + float3(-rs.x, 0, -rs.y));
                // T4: aerial / high views only (camera > 60 m, never a gameplay camera): where a pixel covers more than ~0.15 m of
                // water the wave + ripple normals alias into a hatched moire, so flatten them toward a calm, smooth surface
                // Phase 1 review fix: the gate is a hard uniform branch, so below 60 m n is untouched (bit-exact), and the
                // flattening is clamped (normal y >= 0.05, spec faded out) so a flat mirror can't blow out to a white sun disc
                float fp = length(fwidth(P.xz));
                float aerialT4 = 0.0;
                if (_WorldSpaceCameraPos.y > 60.0)
                {
                    aerialT4 = saturate((_WorldSpaceCameraPos.y - 60.0) / 60.0) * smoothstep(0.15, 0.6, fp);
                    float3 nf = lerp(n, float3(0, 1, 0), aerialT4); nf.y = max(nf.y, 0.05);
                    n = normalize(nf);
                }

                float2 suv = i.screenPos.xy / i.screenPos.w;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float thick = max(0, sceneEye - i.screenPos.w);
                float vdepth = thick * saturate(abs(V.y) + 0.15);

                float4 shadowCoord = TransformWorldToShadowCoord(P);
                Light L = GetMainLight(shadowCoord);
                float shadow = L.shadowAttenuation;
                // body: almost black-green at depth, a little olive in the shallows
                float3 body = lerp(_ShallowColor.rgb, _DeepColor.rgb, 1 - exp(-vdepth / _DepthFade));
                body *= (0.6 + 0.4 * saturate(L.direction.y)) * lerp(0.8, 1.0, shadow);
                body += _GlossyEnvironmentColor.rgb * 0.015;

                // reflection: planar mirror (flipped-x RT, see PlanarReflection.cs), distorted by the surface normal
                float3 R = reflect(-V, n); R.y = abs(R.y);
                float3 env = GlossyEnvironmentReflection(R, 0.02, 1.0);
                float3 refl = env;
                if (_PlanarReflectionOn > 0.5)
                {
                    float2 ruv = float2(1.0 - suv.x, suv.y);
                    // Vertical-flip safeguard (pass 6). The reflection RT is always rendered off-screen, so on top-left-origin
                    // APIs (D3D, Vulkan, Metal) Unity stores it with the RT-flipped projection. suv follows the *current* target:
                    // matching when the water draws into URP's intermediate colour texture (the normal case with post FX), but
                    // inverted when it draws straight to the back buffer (_ProjectionParams.x > 0), so flip v there.
                    // GL/GLES/WebGL never flip. _PlanarReflectionFlipY is a manual on-device override (PlanarReflection.forceFlipY).
                    // UNTESTED ON A DEVICE: verified only in GLCore editor captures.
                    #if UNITY_UV_STARTS_AT_TOP
                    if (_ProjectionParams.x > 0) ruv.y = 1.0 - ruv.y;
                    #endif
                    if (_PlanarReflectionFlipY > 0.5) ruv.y = 1.0 - ruv.y;
                    ruv += float2(-n.x, n.z) * _ReflDistort * 0.25 * (1.0 + 1.5 * chop);   // ripples / wake crests bend the mirror image
                    ruv = clamp(ruv, 0.001, 0.999);
                    float3 pr = SAMPLE_TEXTURE2D(_PlanarReflectionTex, sampler_PlanarReflectionTex, ruv).rgb;
                    refl = pr;
                }
                refl *= _ReflTint.rgb;
                float NoV = saturate(dot(n, V));
                float fres = 0.02 + 0.98 * pow(1 - NoV, 5);
                body += float3(0.030, 0.034, 0.026) * wakeBubbles * smoothstep(32.0, 10.0, track.y);   // churned water scatters a little light: faint grey-green trail
                // trough (pass 11): no luminous cyan. Close to the boat the water is aerated (bright grey-white, mostly hidden by foam);
                // the gaps show darker, slightly green-brown lake water, and the colour fades back to the normal lake body
                float aer15 = exp(-track.y / 15.0) * saturate(wash * 2.0);   // first ~15 m: white aerated water
                float lightAmt = (0.55 + 0.45 * saturate(L.direction.y)) * lerp(0.75, 1.0, shadow);
                float3 gapCol = float3(0.105, 0.125, 0.095) * lightAmt + _GlossyEnvironmentColor.rgb * 0.04;
                float3 aerCol = float3(0.50, 0.53, 0.50) * lightAmt + _GlossyEnvironmentColor.rgb * 0.10;
                float3 churn = lerp(gapCol, aerCol, aer15 * 0.65);
                body = lerp(body, churn, saturate(wash * 1.6 - 0.12) * (1.0 - 0.6 * slick) * smoothstep(32.0, 10.0, track.y));   // pass 11: colour gone by ~30 m
                body *= 1.0 - 0.3 * slick;   // slick: darker, smoother water down the centre of the trough
                float3 col = lerp(body, refl, saturate(fres * (1.0 - 0.35 * wakeBubbles) * (1.0 - 0.95 * saturate(wash * 1.6) * (1.0 - 0.7 * slick)) * (1.0 - 0.6 * chop)));

                float3 H = normalize(L.direction + V);
                float NoH = saturate(dot(n, H));
                float spec = pow(NoH, _SunSharpness) * 30.0 * (0.3 + 0.7 * rough) + pow(NoH, 220) * 0.25 * rough;
                spec *= _SunSpec;
                col += L.color * spec * shadow * saturate(fres * 4.0);
                if (aerialT4 > 0.0)   // aerial only: fade the sun mirror and clamp to [0,1] so the course centre can't blow out
                {
                    col -= L.color * spec * shadow * saturate(fres * 4.0) * (0.9 * aerialT4);
                    col = lerp(col, saturate(col), aerialT4);
                }

                // foam: coverage thresholded against lacy noise so it breaks into streaks/patches as it fades
                float lace = vnoise(P.xz * 1.6 - t * 0.25) * 0.55 + vnoise(P.xz * 4.7 + t * 0.4) * 0.30 + vnoise(P.xz * 11.0) * 0.15;
                float foam = saturate((wk.x * 1.1 - lace * 0.9) * 2.0);
                // pass 10 trough foam: fine, high-contrast streaks stretched along the travel direction (lateral x along-track
                // anisotropic noise + the foam texture squashed 4:1 along the track), turquoise bubbles between the streaks,
                // isotropic boils in the first ~10 m, thinning with distance; the slick lane stays mostly clear
                float u = track.x, al = track.z;
                float fineW = saturate(1.0 - fwidth(u) * 5.0);          // fade the finest octave out where it would alias
                float uw = u + (vnoise(float2(u * 0.7, al * 0.12)) - 0.5) * 0.7;   // streaks meander a little
                float streak = vnoise(float2(uw * 2.4, al * 0.085)) * 0.30 + vnoise(float2(uw * 6.0 + 3.1, al * 0.21)) * 0.25;
                streak += lerp(0.5, vnoise(float2(uw * 14.0, al * 0.5)), fineW) * 0.15;
                // bubbly lace from the foam texture, squashed ~4:1 along the track, so streak edges are intricate, not airbrushed
                float ftS = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(uw / 1.6, al / 6.0)).r;
                streak += ftS * 0.30;
                // first ~10 m: fine boils churned up by the prop, already drawn out ~3:1 along the track (never big soft blobs)
                float boilN = vnoise(float2(uw * 4.5, al * 1.5 + t * 2.0)) * 0.5 + SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(uw / 1.4, al / 4.2) + t * 0.03).r * 0.5;
                streak = lerp(streak, boilN, 0.3 * tur10);
                streak = saturate((streak - 0.5) * 2.0 + 0.5);   // stretch the contrast so the threshold carves streaks, not one mass
                float cover = saturate(wash * (0.7 + 0.3 * tur10) + 0.15 * aer15) * smoothstep(60.0, 20.0, track.y) * (1.0 - 0.8 * slick);
                float washFoam = smoothstep(1.0 - cover - 0.22, 1.0 - cover + 0.22, streak) * smoothstep(0.02, 0.15, wash) * (0.45 + 0.55 * ftS) * smoothstep(55.0, 8.0, track.y);   // b9 (Scott Oct 23): soft, bubbly, fading; no flat white sheets
                // crest caps: patchy foam that breaks up along the crest and thins out as the caps decay
                float pc = vnoise(float2(al * 0.3, u * 0.5)) * 0.55 + vnoise(float2(al * 0.9 + 5.3, u * 1.6)) * 0.30;
                pc += SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(u / 3.0, al / 6.0)).r * 0.15;
                // ...and frothy edges from the foam texture (stretched along the crest), so the curling lips read as foam, not paint
                float capLace = SAMPLE_TEXTURE2D(_FoamTex, sampler_FoamTex, float2(u / 0.9, al / 3.5) + t * 0.02).r;
                float capThr = 0.30 + 0.22 * saturate(track.y / 35.0);   // the lips break up more the older they are
                float capFoam = smoothstep(capThr, capThr + 0.25, caps * (0.25 + 0.7 * pc + 0.6 * capLace)) * (0.35 + 0.65 * capLace) * smoothstep(40.0, 4.0, track.y);   // b9: soft lacy lips that fade with age
                foam = saturate(max(foam, max(washFoam, capFoam)));
                float shoreFoam = saturate(1 - thick / 0.25) * smoothstep(0.55, 0.85, vnoise(P.xz * 2.0 + t * 0.5)) * 0.35;
                foam = saturate(foam + shoreFoam);
                float3 foamCol = _FoamColor.rgb * (0.50 + 0.35 * shadow) * (0.55 + 0.45 * saturate(L.direction.y)) + _GlossyEnvironmentColor.rgb * 0.15;
                // pass 9: fresh churn and crest lips are brighter, denser white than old lace (lots of sunlit bubbles)
                float fresh = saturate(max(washFoam * wash, capFoam) * 1.3);
                foamCol *= 1.0 + 0.32 * fresh;
                foamCol *= lerp(1.0, 0.68 + 0.42 * ftS, saturate(wash * 2.0));   // pass 10: bubbly shading inside the trough foam
                col = lerp(col, foamCol, foam * lerp(0.50, 0.72, fresh));   // b9: translucent foam, the water shows through

                float alpha = max(saturate(thick / 0.35), max(foam, wash));
                if (_SVWaterDebug > 0.5) return half4(wash, caps, slick, 1);   // capture diagnostic (SV_WATER_DEBUG=1)
                col = MixFog(col, i.fogFactor);
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
