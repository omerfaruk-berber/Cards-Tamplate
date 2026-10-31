Shader "Cards/Flowing Background"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _DarkColor ("Dark Green", Color) = (0.025, 0.075, 0.05, 1)
        _GreenColor ("Emerald", Color) = (0.055, 0.3, 0.18, 1)
        _MintColor ("Mint", Color) = (0.32, 0.65, 0.43, 1)
        _SwirlStrength ("Swirl Strength", Range(0, 3)) = 1
        _PatternScale ("Pattern Scale", Range(0.25, 5)) = 1
        _Contrast ("Contrast", Range(0.25, 3)) = 1.25
        _PixelSize ("Pixel Size", Range(0, 8)) = 2
        _Vignette ("Vignette", Range(0, 1)) = 0.3
        [Enum(Spiral,0,LiquidMarble,1,CellularFluid,2,SilkFlow,3,Smoke,4,LavaLamp,5,Aurora,6)] _PatternFrom ("Pattern", Float) = 0
        [HideInInspector] _PatternTo ("Next Pattern", Float) = 0
        [HideInInspector] _PatternBlend ("Pattern Blend", Float) = 0
        _PatternSeed ("Pattern Seed", Range(0, 9999)) = 0
        _MarbleGloss ("Marble Gloss", Range(0, 1)) = 0.4
        [Toggle] _CRTEnabled ("Enable CRT", Float) = 0
        _ScanlineStrength ("Scanline Strength", Range(0, 1)) = 0.12
        _GrainStrength ("Grain Strength", Range(0, 0.25)) = 0.025
        [HideInInspector] _GrainTime ("Grain Time", Float) = 0
        [HideInInspector] _FlowTime ("Flow Time", Float) = 0
        [HideInInspector] _Aspect ("Aspect", Float) = 1
        [HideInInspector] _ViewportHeight ("Viewport Height", Float) = 1080
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "CanUseSpriteAtlas" = "False"
            "DisableBatching" = "True"
        }

        Cull Off
        ZWrite Off
        ZTest LEqual

        Pass
        {
            Name "FlowingBackground"
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _DarkColor;
                half4 _GreenColor;
                half4 _MintColor;
                float _FlowTime;
                float _SwirlStrength;
                float _PatternScale;
                float _Contrast;
                float _PixelSize;
                float _Vignette;
                float _Aspect;
                float _ViewportHeight;
                float _CRTEnabled;
                float _ScanlineStrength;
                float _GrainStrength;
                float _GrainTime;
                float _PatternFrom;
                float _PatternTo;
                float _PatternBlend;
                float _PatternSeed;
                float _MarbleGloss;
            CBUFFER_END

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }

            float2 Rotate(float2 p, float angle)
            {
                float s, c;
                sincos(angle, s, c);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            float Hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                float2 blend = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
                return lerp(lerp(Hash21(cell), Hash21(cell + float2(1, 0)), blend.x),
                    lerp(Hash21(cell + float2(0, 1)), Hash21(cell + 1.0), blend.x), blend.y);
            }

            float FractalNoise(float2 p)
            {
                float value = 0.0;
                float amplitude = 0.5714286;
                [unroll]
                for (int octave = 0; octave < 3; octave++)
                {
                    value += amplitude * Noise(p);
                    p = Rotate(p, 0.61) * 2.03 + float2(5.2, 1.7);
                    amplitude *= 0.5;
                }
                return value;
            }

            half3 BandColor(float value)
            {
                float ink = saturate((value - 0.5) * _Contrast + 0.5);
                half3 color = lerp(_DarkColor.rgb, _GreenColor.rgb, smoothstep(0.18, 0.4, ink));
                return lerp(color, _MintColor.rgb, smoothstep(0.68, 0.8, ink));
            }

            half3 Spiral(float2 p, float t, float2 seed)
            {
                float2 q = (p + float2(0.12, -0.04)) * (4.0 * _PatternScale);
                float radius = length(q);
                float twist = _SwirlStrength * 9.0 / (1.0 + radius * radius * 0.35);
                q = Rotate(q, twist + t * 0.12 + seed.x * 0.2);

                // Different phase speeds deform the bands instead of rotating a fixed image.
                [unroll]
                for (int i = 0; i < 4; i++)
                {
                    float phase = (float)i;
                    float2 warp = float2(
                        sin(q.y * 1.7 + q.x * 0.45 + t * (0.37 + phase * 0.11) + phase * 1.7),
                        cos(q.x * 1.4 - q.y * 0.35 - t * (0.29 + phase * 0.07) + phase * 2.1));
                    q += warp * (0.65 * _SwirlStrength / (1.0 + phase * 0.45));
                }

                float bands = q.x * 1.35 + q.y * 0.55
                    + sin(q.y * 1.65 - t * 0.28) * 1.2 + t * 0.24;
                return BandColor(0.5 + 0.5 * sin(bands));
            }

            half3 LiquidMarble(float2 p, float t, float2 seed)
            {
                // Domain warping folds paint into islands and veins without a central vortex.
                float2 q = p * (3.4 * _PatternScale) + seed;
                float2 drift = float2(t * 0.16, -t * 0.11);
                float2 warp = float2(FractalNoise(q * 0.9 + drift),
                    FractalNoise(q * 0.9 + float2(4.7, -8.2) - drift * 0.7)) - 0.5;
                float2 r = q + warp * (4.0 + _SwirlStrength * 1.5);
                r += float2(sin(r.y * 1.4 + t * 0.16), cos(r.x * 1.3 - t * 0.19)) * 0.45;
                float field = Noise(r * 1.2 + drift * 0.6);
                float veins = field * (TWO_PI * 1.4) + r.y * 0.75 + r.x * 0.3;
                half3 color = BandColor(0.14 + 0.86 * (0.5 + 0.5 * sin(veins)));

                // Offset light and dark ridges give the folds a polished, liquid surface.
                float ridge = pow(saturate(0.5 + 0.5 * sin(veins + 0.55)), 24.0);
                float fineRidge = pow(saturate(0.5 + 0.5 * sin(veins * 3.0 + 0.9)), 48.0);
                float shadow = pow(saturate(0.5 + 0.5 * sin(veins - 0.5)), 5.0);
                color *= 1.0 - _MarbleGloss * shadow * 0.28;
                half3 sheen = lerp(_MintColor.rgb, half3(1, 1, 1), 0.8);
                return lerp(color, sheen, saturate(_MarbleGloss * (ridge * 0.9 + fineRidge * 0.25)));
            }

            half3 CellularFluid(float2 p, float t, float2 seed)
            {
                float2 q = p * (3.2 * _PatternScale) + seed;
                float2 drift = float2(t * 0.13, -t * 0.07);
                float2 warp = float2(FractalNoise(q * 0.65 + drift),
                    FractalNoise(q * 0.65 + float2(7.4, -3.1) - drift * 0.6)) - 0.5;
                q += warp * (1.5 + _SwirlStrength);
                float2 cell = floor(q);
                float2 fraction = frac(q);
                float nearest = 100.0;
                float second = 100.0;

                // The two closest moving sites form a continuous, warped cell membrane.
                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 offset = float2(x, y);
                        float2 key = cell + offset;
                        float2 phase = float2(Hash21(key + 17.1), Hash21(key + 43.7));
                        float2 site = 0.5 + 0.36 * sin(phase * TWO_PI + t * float2(0.23, -0.17));
                        float2 d = offset + site - fraction;
                        float distanceSquared = dot(d, d);
                        second = min(second, max(nearest, distanceSquared));
                        nearest = min(nearest, distanceSquared);
                    }
                }
                float gap = sqrt(second) - sqrt(nearest);
                float interior = saturate((smoothstep(0.02, 0.38, gap) - 0.5) * _Contrast + 0.5);
                float body = pow(saturate(1.0 - sqrt(nearest) * 0.9), 3.0);
                half3 color = lerp(_DarkColor.rgb, _GreenColor.rgb, 0.25 + interior * 0.75);
                color = lerp(color, _MintColor.rgb, body * 0.4);
                float membrane = exp(-gap * 22.0);
                half3 membraneColor = lerp(_MintColor.rgb, half3(1, 1, 1), 0.2);
                return lerp(color, membraneColor, membrane * 0.65);
            }

            half3 SilkFlow(float2 p, float t, float2 seed)
            {
                float2 q = Rotate(p * (2.8 * _PatternScale), -0.6) + seed;
                float2 drift = float2(t * 0.1, -t * 0.08);
                float2 warp = float2(FractalNoise(q * 0.75 + drift),
                    FractalNoise(q * 0.75 + float2(-5.1, 3.7) - drift)) - 0.5;
                float2 r = q + warp * (1.2 + _SwirlStrength * 0.5);
                r += float2(sin(q.y * 0.85 + t * 0.2), cos(q.x * 1.1 - t * 0.17)) * 0.35;
                float phase = r.x * 5.3 + r.y * 0.5 + sin(r.y * 1.2 - t * 0.2) * 1.4
                    + sin(r.x * 1.1 + r.y * 0.6 + t * 0.15) * 0.4;
                float fold = 0.5 + 0.5 * sin(phase);
                float tone = saturate((fold - 0.5) * _Contrast + 0.5);
                // Analytical fold normals retain a coherent light direction, even with pixelation.
                float3 normal = normalize(float3(-cos(phase) * 1.5,
                    cos(r.y * 0.8 + t * 0.12) * 0.25, 1.0));
                float3 light = normalize(float3(-0.55, 0.15, 1.0));
                float diffuse = saturate(dot(normal, light));
                float specular = pow(diffuse, 40.0);
                half3 cloth = lerp(_GreenColor.rgb, _MintColor.rgb, 0.15 + tone * 0.45);
                half3 color = cloth * (0.25 + diffuse * 0.75);
                color = lerp(_DarkColor.rgb, color, 0.35 + 0.65 * smoothstep(0.02, 0.65, tone));
                float lighting = 0.55 + 0.15 * sin(r.y * 0.8 + t * 0.12);
                return lerp(color, lerp(_MintColor.rgb, half3(1, 1, 1), 0.65), specular * lighting);
            }

            half3 Smoke(float2 p, float t, float2 seed)
            {
                float2 q = p * (3.2 * _PatternScale) + seed;
                float2 drift = float2(t * 0.12, t * 0.07);
                float2 warp = float2(FractalNoise(q * 0.7 + drift),
                    FractalNoise(q * 0.7 + float2(8.3, 2.8) - drift * 0.6)) - 0.5;
                float density = FractalNoise(q + warp * (1.0 + _SwirlStrength) + drift * 0.4);
                float ink = saturate((density - 0.5) * 2.8 * _Contrast + 0.5);
                half3 color = lerp(_DarkColor.rgb, _GreenColor.rgb, smoothstep(0.05, 0.9, ink));
                return lerp(color, _MintColor.rgb, ink * ink * ink * 0.7);
            }

            half3 LavaLamp(float2 p, float t, float2 seed)
            {
                float2 q = p * (3.3 * _PatternScale);
                q += float2(sin(q.y * 1.4 + t * 0.2), sin(q.x * 1.1 - t * 0.18)) * (0.12 * _SwirlStrength);
                float field = 0.0;
                float2 gradient = 0.0;
                [unroll]
                for (int i = 0; i < 6; i++)
                {
                    float index = (float)i;
                    float2 key = float2(index + 3.1, index * 1.37 + 8.4) + seed;
                    float phase = Hash21(key) * TWO_PI;
                    float radius = (0.32 + Hash21(key + 14.7) * 0.25)
                        * (1.0 + sin(t * 0.3 + phase) * 0.08);
                    float2 center = float2(
                        sin(t * (0.16 + index * 0.008) + phase) * max(0.2, _Aspect * 1.35 - radius * 0.3),
                        sin(t * (0.24 + index * 0.013) + phase + 1.3) * 1.45);
                    float2 d = q - center;
                    float denominator = dot(d, d) + 0.04;
                    float weight = radius * radius / denominator;
                    field += weight;
                    gradient -= 2.0 * weight * d / denominator;
                }
                field = 1.0 + (field - 1.0) * _Contrast;
                gradient *= _Contrast;
                float edge = smoothstep(0.88, 1.12, field);
                float3 normal = normalize(float3(-gradient * 0.12, 1.0));
                float3 light = normalize(float3(-0.5, 0.6, 1.0));
                float diffuse = 0.55 + 0.45 * saturate(dot(normal, light));
                float specular = pow(saturate(dot(normal, light)), 20.0);
                half3 lava = lerp(_GreenColor.rgb, _MintColor.rgb, saturate((field - 1.0) * 0.55));
                lava = lerp(lava * diffuse, lerp(_MintColor.rgb, half3(1, 1, 1), 0.5), specular * 0.5);
                half3 color = lerp(_DarkColor.rgb * 0.8, lava, edge);
                float rim = exp(-abs(field - 1.0) * 12.0);
                return lerp(color, _MintColor.rgb, rim * 0.25);
            }

            half3 Aurora(float2 p, float t, float2 seed)
            {
                float2 q = p * (3.4 * _PatternScale);
                half3 color = _DarkColor.rgb * 0.65;
                [unroll]
                for (int i = 0; i < 3; i++)
                {
                    float phase = (float)i;
                    float center = (phase - 1.0) * _Aspect * 0.9
                        + sin(q.y * 0.85 + t * 0.18 + phase * 1.9 + seed.x) * 0.42
                        + sin(q.y * 1.9 - t * 0.1 + phase * 2.3 + seed.y) * 0.16;
                    float width = 0.13 + sin(q.y * 0.7 - t * 0.15 + phase + seed.x) * 0.04;
                    float distanceSquared = (q.x - center) * (q.x - center) / (width * width);
                    float core = exp(-distanceSquared);
                    float halo = exp(-distanceSquared * 0.1);
                    float strands = 0.7 + 0.3 * sin(q.x * 32.0 + sin(q.y * 0.5 + t * 0.1) * 3.0 + phase);
                    float fade = 0.55 + 0.45 * smoothstep(-1.4, 1.4, q.y);
                    float breath = 0.75 + 0.25 * sin(t * 0.3 + phase + seed.y);
                    float intensity = (core * 0.8 + halo * 0.3) * strands * fade * breath * _Contrast;
                    half3 light = lerp(_GreenColor.rgb, _MintColor.rgb, saturate(core * 0.8 + phase * 0.1));
                    color += light * intensity + lerp(_MintColor.rgb, half3(1, 1, 1), 0.5) * pow(core, 5.0) * 0.12;
                }
                return saturate(color);
            }

            half3 RenderPattern(float id, float2 p, float t, float2 seed)
            {
                // Uniform branches keep inactive patterns out of the normal rendering path.
                [branch] if (id < 0.5) return Spiral(p, t, seed);
                [branch] if (id < 1.5) return LiquidMarble(p, t, seed);
                [branch] if (id < 2.5) return CellularFluid(p, t, seed);
                [branch] if (id < 3.5) return SilkFlow(p, t, seed);
                [branch] if (id < 4.5) return Smoke(p, t, seed);
                [branch] if (id < 5.5) return LavaLamp(p, t, seed);
                return Aurora(p, t, seed);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Square pixels and undistorted patterns at any viewport aspect ratio.
                float2 p = (input.uv - 0.5) * float2(_Aspect, 1.0);
                if (_PixelSize > 0.0)
                {
                    float stepSize = _PixelSize / max(_ViewportHeight, 1.0);
                    p = (floor(p / stepSize) + 0.5) * stepSize;
                }
                float2 seed = 0;
                if (_PatternSeed > 0.0)
                    seed = (float2(Hash21(float2(_PatternSeed, 11)), Hash21(float2(23, _PatternSeed))) - 0.5) * 14.0;
                half3 color = RenderPattern(_PatternFrom, p, _FlowTime, seed);
                [branch] if (_PatternBlend > 0.0 && _PatternFrom != _PatternTo)
                    color = lerp(color, RenderPattern(_PatternTo, p, _FlowTime, seed), _PatternBlend);

                float2 edge = (input.uv - 0.5) * 2.0;
                color *= 1.0 - _Vignette * smoothstep(0.15, 1.6, dot(edge, edge));
                if (_CRTEnabled > 0.5)
                {
                    // CRT uses physical screen pixels, independently of pattern pixelation.
                    float2 pixel = floor(input.uv * float2(_Aspect, 1.0) * _ViewportHeight);
                    float scanline = 0.5 + 0.5 * cos(pixel.y * PI);
                    color *= 1.0 - _ScanlineStrength * scanline;

                    // Stable for each 1/24 second frame; freezing _GrainTime freezes the noise.
                    float3 noise = frac(float3(pixel, floor(_GrainTime * 24.0)) * 0.1031);
                    noise += dot(noise, noise.yzx + 33.33);
                    float grain = frac((noise.x + noise.y) * noise.z) - 0.5;
                    color = saturate(color + grain * _GrainStrength);
                }
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
