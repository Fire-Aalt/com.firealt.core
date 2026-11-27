Shader "Hidden/FireAlt/ReconstructionMotionBlur"
{
    HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Random.hlsl"

        TEXTURE2D(_MotionVectors);
        TEXTURE2D_FLOAT(_SceneDepth);
        TEXTURE2D(_Source);
        TEXTURE2D(_Tiles);

        float _CameraBlur;
        float4 _BlurScreen; // width, height, tile columns, tile rows
        float4 _BlurParams; // radius per unit of motion (intensity / 2), tile size, sample count

        struct Attributes
        {
            uint vertexID : SV_VertexID;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
            output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
            return output;
        }

        // Motion vectors are in UV per frame; blur radii are in pixels, capped at one tile.
        float2 RadiusPx(float2 motion)
        {
            float2 radius = motion * _BlurScreen.xy * _BlurParams.x;
            float length2 = dot(radius, radius);
            float maxRadius = _BlurParams.y;
            return length2 > maxRadius * maxRadius ? radius * (maxRadius * rsqrt(length2)) : radius;
        }

        float2 LoadRadius(int2 pixel)
        {
            return RadiusPx(LOAD_TEXTURE2D(_MotionVectors, pixel).xy);
        }

        // Longest radius in xy, shortest radius length in z.
        float4 MergeTile(float4 a, float4 b)
        {
            float4 longest = dot(a.xy, a.xy) >= dot(b.xy, b.xy) ? a : b;
            return float4(longest.xy, min(a.z, b.z), 0);
        }
    ENDHLSL

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "CameraSplit"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            // Same reconstruction as URP's CameraMotionVectors, so the camera's share matches the stored vectors.
            float2 Frag(Varyings input) : SV_Target
            {
                int2 pixel = int2(input.positionCS.xy);
                float2 motion = LOAD_TEXTURE2D(_MotionVectors, pixel).xy;
                float depth = LOAD_TEXTURE2D(_SceneDepth, pixel).r;
            #if !UNITY_REVERSED_Z
                depth = lerp(UNITY_NEAR_CLIP_VALUE, 1, depth);
            #endif
                float3 positionWS = ComputeWorldSpacePosition(input.uv, depth, UNITY_MATRIX_I_VP);
                float4 current = mul(_NonJitteredViewProjMatrix, float4(positionWS, 1.0));
                float4 previous = mul(_PrevViewProjMatrix, float4(positionWS, 1.0));
                float2 camera = current.xy / current.w - previous.xy / previous.w;
            #if UNITY_UV_STARTS_AT_TOP
                camera.y = -camera.y;
            #endif
                camera *= 0.5;
                return motion - camera * (1.0 - _CameraBlur);
            }
            ENDHLSL
        }

        Pass
        {
            Name "TileMaxX"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                int2 tile = int2(input.positionCS.xy);
                int size = (int)_BlurParams.y;
                int start = tile.x * size;
                int end = min(start + size, (int)_BlurScreen.x);
                float4 result = float4(0, 0, 1e6, 0);
                for (int x = start; x < end; x++)
                {
                    float2 radius = LoadRadius(int2(x, tile.y));
                    result = MergeTile(result, float4(radius, length(radius), 0));
                }
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Name "TileMaxY"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                int2 tile = int2(input.positionCS.xy);
                int size = (int)_BlurParams.y;
                int start = tile.y * size;
                int end = min(start + size, (int)_BlurScreen.y);
                float4 result = float4(0, 0, 1e6, 0);
                for (int y = start; y < end; y++)
                {
                    result = MergeTile(result, LOAD_TEXTURE2D(_Tiles, int2(tile.x, y)));
                }
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Name "NeighborMax"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            float4 Frag(Varyings input) : SV_Target
            {
                int2 tile = int2(input.positionCS.xy);
                int2 last = int2(_BlurScreen.zw) - 1;
                float4 result = float4(0, 0, 1e6, 0);
                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        result = MergeTile(result, LOAD_TEXTURE2D(_Tiles, clamp(tile + int2(x, y), 0, last)));
                    }
                }
                return result;
            }
            ENDHLSL
        }

        Pass
        {
            Name "Reconstruct"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            struct Tap
            {
                float depth;
                float radius;
            };

            Tap LoadTap(int2 pixel)
            {
                Tap tap;
                tap.depth = LinearEyeDepth(LOAD_TEXTURE2D(_SceneDepth, pixel).r, _ZBufferParams);
                tap.radius = max(length(LoadRadius(pixel)), 0.5);
                return tap;
            }

            // Jimenez 2014: x weighs the centre's blur reaching a tap behind it, y a tap's blur reaching the centre
            // from in front, with a one-pixel soft edge on each blur.
            float TapWeight(Tap center, Tap tap, float distance)
            {
                float soft = 0.05 * min(center.depth, tap.depth) + 0.01;
                float2 depthCompare = saturate(0.5 + float2(tap.depth - center.depth, center.depth - tap.depth) / soft);
                float2 spreadCompare = saturate(float2(center.radius, tap.radius) - distance + 1.0);
                return dot(depthCompare, spreadCompare);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                float2 position = input.positionCS.xy;
                int2 pixel = int2(position);
                float4 center = LOAD_TEXTURE2D(_Source, pixel);
                float noise = InterleavedGradientNoise(position, 0);
                float tileSize = _BlurParams.y;
                int2 tile = clamp(int2((position + (noise - 0.5) * tileSize * 0.5) / tileSize), 0, int2(_BlurScreen.zw) - 1);
                float4 neighborhood = LOAD_TEXTURE2D(_Tiles, tile);
                float2 radiusN = neighborhood.xy;
                float lengthN = length(radiusN);
                if (lengthN < 0.5)
                {
                    return center;
                }

                int count = (int)_BlurParams.z;
                float2 texel = rcp(_BlurScreen.xy);
                if (lengthN - neighborhood.z < max(1.0, 0.25 * lengthN))
                {
                    float4 sum = center;
                    for (int i = 0; i < count; i++)
                    {
                        float t = (i + noise) / count * 2.0 - 1.0;
                        sum += SAMPLE_TEXTURE2D_LOD(_Source, sampler_PointClamp, (position + radiusN * t) * texel, 0);
                    }
                    return sum / (count + 1);
                }

                int2 lastPixel = int2(_BlurScreen.xy) - 1;
                Tap centerTap = LoadTap(pixel);
                int pairs = max(1, count >> 1);
                float weightC = pairs / centerTap.radius;
                float4 sum = center * weightC;
                float total = weightC;
                for (int j = 0; j < pairs; j++)
                {
                    float2 offset = radiusN * ((j + noise) / pairs);
                    float distance = length(offset);
                    int2 pixelA = clamp(int2(position + offset), 0, lastPixel);
                    int2 pixelB = clamp(int2(position - offset), 0, lastPixel);
                    Tap a = LoadTap(pixelA);
                    Tap b = LoadTap(pixelB);
                    float weightA = TapWeight(centerTap, a, distance);
                    float weightB = TapWeight(centerTap, b, distance);
                    // Mirrored fill: a background tap opposite a faster foreground tap takes the foreground's weight,
                    // so a moving edge fades inward as far as it streaks outward.
                    bool2 mirror = bool2(a.depth > b.depth, b.radius > a.radius);
                    weightA = all(mirror) ? weightB : weightA;
                    weightB = any(mirror) ? weightB : weightA;
                    sum += LOAD_TEXTURE2D(_Source, pixelA) * weightA + LOAD_TEXTURE2D(_Source, pixelB) * weightB;
                    total += weightA + weightB;
                }
                return sum / total;
            }
            ENDHLSL
        }
    }
}
