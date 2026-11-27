using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace FireAlt.Core.URP
{
    /// <summary>
    /// Reconstruction motion blur (McGuire 2012, Jimenez 2014), driven by the <see cref="ReconstructionMotionBlur"/>
    /// volume override. Fast objects streak past their outline, nothing smears onto pixels in front of it, and the
    /// camera's own motion can be blurred less than object motion.
    /// </summary>
    [DisallowMultipleRendererFeature("Reconstruction Motion Blur")]
    public sealed class ReconstructionMotionBlurFeature : ScriptableRendererFeature
    {
        private Material _material;
        private CameraSplitPass _splitPass;
        private BlurPass _blurPass;

        public override void Create()
        {
            CoreUtils.Destroy(_material);
            if (!GraphicsSettings.TryGetRenderPipelineSettings<ReconstructionMotionBlurResources>(out var resources)
                || resources.Shader == null)
            {
                if (isActive)
                {
                    Debug.LogError($"{nameof(ReconstructionMotionBlurFeature)} can't find its shader in the URP Global Settings.");
                }

                return;
            }

            _material = CoreUtils.CreateEngineMaterial(resources.Shader);
            _splitPass = new CameraSplitPass(_material) { renderPassEvent = RenderPassEvent.AfterRenderingTransparents };
            _splitPass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Motion);
            // Late in the slot, so passes adding motion vectors at BeforeRenderingPostProcessing come first.
            _blurPass = new BlurPass(_material) { renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing + 10 };
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            var volume = VolumeManager.instance.stack.GetComponent<ReconstructionMotionBlur>();
            if (_material == null || !camera.postProcessEnabled || camera.cameraType != CameraType.Game
                || volume == null || !volume.IsActive())
            {
                return;
            }

            var settings = new BlurSettings(volume);
            _splitPass.Settings = settings;
            _blurPass.Settings = settings;
            renderer.EnqueuePass(_splitPass);
            renderer.EnqueuePass(_blurPass);
        }

        protected override void Dispose(bool disposing)
        {
            CoreUtils.Destroy(_material);
        }

        internal static bool Blurs(UniversalCameraData cameraData, UniversalResourceData resources)
        {
            return resources.motionVectorColor.IsValid() && !cameraData.xr.enabled;
        }
    }

    internal readonly struct BlurSettings
    {
        public readonly float Intensity;
        public readonly float Clamp;
        public readonly float CameraBlur;
        public readonly int SampleCount;

        public BlurSettings(ReconstructionMotionBlur volume)
        {
            Intensity = volume.intensity.value;
            Clamp = volume.clamp.value;
            CameraBlur = volume.cameraBlur.value;
            SampleCount = volume.SampleCount;
        }
    }

    internal static class MotionBlurShader
    {
        public const int CAMERA_SPLIT = 0;
        public const int TILE_MAX_X = 1;
        public const int TILE_MAX_Y = 2;
        public const int NEIGHBOR_MAX = 3;
        public const int RECONSTRUCT = 4;

        public static readonly int MOTION_VECTORS = Shader.PropertyToID("_MotionVectors");
        public static readonly int SCENE_DEPTH = Shader.PropertyToID("_SceneDepth");
        public static readonly int SOURCE = Shader.PropertyToID("_Source");
        public static readonly int TILES = Shader.PropertyToID("_Tiles");
        public static readonly int CAMERA_BLUR = Shader.PropertyToID("_CameraBlur");
        public static readonly int SCREEN = Shader.PropertyToID("_BlurScreen");
        public static readonly int PARAMS = Shader.PropertyToID("_BlurParams");

        public static void Draw(RasterCommandBuffer cmd, Material material, int pass, MaterialPropertyBlock properties)
        {
            cmd.DrawProcedural(Matrix4x4.identity, material, pass, MeshTopology.Triangles, 3, 1, properties);
        }
    }

    internal sealed class CameraSplitPass : ScriptableRenderPass
    {
        private readonly Material _material;

        private sealed class PassData
        {
            public readonly MaterialPropertyBlock Properties = new();
            public Material Material;
            public TextureHandle MotionVectors;
            public TextureHandle Depth;
            public float CameraBlur;
        }

        public CameraSplitPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler("Reconstruction Motion Blur Camera Split");
        }

        public BlurSettings Settings { get; set; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            if (!ReconstructionMotionBlurFeature.Blurs(frameData.Get<UniversalCameraData>(), resources))
            {
                return;
            }

            var velocity = renderGraph.CreateTexture(resources.motionVectorColor, "_MotionBlurVelocity");
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var data, profilingSampler))
            {
                data.Material = _material;
                data.MotionVectors = resources.motionVectorColor;
                data.Depth = resources.cameraDepthTexture;
                data.CameraBlur = Settings.CameraBlur;
                builder.UseTexture(data.MotionVectors);
                builder.UseTexture(data.Depth);
                builder.SetRenderAttachment(velocity, 0, AccessFlags.WriteAll);
                builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
                {
                    pass.Properties.Clear();
                    pass.Properties.SetTexture(MotionBlurShader.MOTION_VECTORS, (RTHandle)pass.MotionVectors);
                    pass.Properties.SetTexture(MotionBlurShader.SCENE_DEPTH, (RTHandle)pass.Depth);
                    pass.Properties.SetFloat(MotionBlurShader.CAMERA_BLUR, pass.CameraBlur);
                    MotionBlurShader.Draw(context.cmd, pass.Material, MotionBlurShader.CAMERA_SPLIT, pass.Properties);
                });
            }

            resources.motionVectorColor = velocity;
        }
    }

    internal sealed class BlurPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private readonly ProfilingSampler _tileSampler = new("Reconstruction Motion Blur Tiles");

        private sealed class PassData
        {
            public readonly MaterialPropertyBlock Properties = new();
            public Material Material;
            public int ShaderPass;
            public TextureHandle Source;
            public TextureHandle MotionVectors;
            public TextureHandle Depth;
            public TextureHandle Tiles;
            public Vector4 Screen;
            public Vector4 Params;
        }

        public BlurPass(Material material)
        {
            _material = material;
            profilingSampler = new ProfilingSampler("Reconstruction Motion Blur");
        }

        public BlurSettings Settings { get; set; }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var cameraData = frameData.Get<UniversalCameraData>();
            if (!ReconstructionMotionBlurFeature.Blurs(cameraData, resources))
            {
                return;
            }

            var width = cameraData.cameraTargetDescriptor.width;
            var height = cameraData.cameraTargetDescriptor.height;
            var tileSize = Mathf.Max(1, Mathf.CeilToInt(Settings.Clamp * height));
            var tilesX = (width + tileSize - 1) / tileSize;
            var tilesY = (height + tileSize - 1) / tileSize;
            var shape = new BlurShape
            {
                Screen = new Vector4(width, height, tilesX, tilesY),
                Params = new Vector4(Settings.Intensity * 0.5f, tileSize, Settings.SampleCount, 0f),
            };

            var rows = CreateTiles(renderGraph, tilesX, height, "_MotionBlurTileRows");
            var tiles = CreateTiles(renderGraph, tilesX, tilesY, "_MotionBlurTiles");
            var neighbors = CreateTiles(renderGraph, tilesX, tilesY, "_MotionBlurNeighbors");
            var output = renderGraph.CreateTexture(resources.cameraColor, "_MotionBlurOutput");

            AddPass(renderGraph, MotionBlurShader.TILE_MAX_X, _tileSampler, rows, shape,
                new BlurInputs { MotionVectors = resources.motionVectorColor });
            AddPass(renderGraph, MotionBlurShader.TILE_MAX_Y, _tileSampler, tiles, shape, new BlurInputs { Tiles = rows });
            AddPass(renderGraph, MotionBlurShader.NEIGHBOR_MAX, _tileSampler, neighbors, shape, new BlurInputs { Tiles = tiles });
            AddPass(renderGraph, MotionBlurShader.RECONSTRUCT, profilingSampler, output, shape, new BlurInputs
            {
                Source = resources.cameraColor,
                MotionVectors = resources.motionVectorColor,
                Depth = resources.activeDepthTexture,
                Tiles = neighbors,
            });
            resources.cameraColor = output;
        }

        private struct BlurShape
        {
            public Vector4 Screen;
            public Vector4 Params;
        }

        private struct BlurInputs
        {
            public TextureHandle Source;
            public TextureHandle MotionVectors;
            public TextureHandle Depth;
            public TextureHandle Tiles;
        }

        private static TextureHandle CreateTiles(RenderGraph renderGraph, int width, int height, string name)
        {
            return renderGraph.CreateTexture(new TextureDesc(width, height)
            {
                format = GraphicsFormat.R16G16B16A16_SFloat,
                filterMode = FilterMode.Point,
                name = name,
            });
        }

        private void AddPass(RenderGraph renderGraph, int shaderPass, ProfilingSampler sampler, TextureHandle target,
            in BlurShape shape, in BlurInputs inputs)
        {
            using var builder = renderGraph.AddRasterRenderPass<PassData>(sampler.name, out var data, sampler);
            data.Material = _material;
            data.ShaderPass = shaderPass;
            data.Source = UseIfValid(builder, inputs.Source);
            data.MotionVectors = UseIfValid(builder, inputs.MotionVectors);
            data.Depth = UseIfValid(builder, inputs.Depth);
            data.Tiles = UseIfValid(builder, inputs.Tiles);
            data.Screen = shape.Screen;
            data.Params = shape.Params;
            builder.SetRenderAttachment(target, 0, AccessFlags.WriteAll);
            builder.SetRenderFunc(static (PassData pass, RasterGraphContext context) =>
            {
                var properties = pass.Properties;
                properties.Clear();
                SetIfValid(properties, MotionBlurShader.SOURCE, pass.Source);
                SetIfValid(properties, MotionBlurShader.MOTION_VECTORS, pass.MotionVectors);
                SetIfValid(properties, MotionBlurShader.SCENE_DEPTH, pass.Depth);
                SetIfValid(properties, MotionBlurShader.TILES, pass.Tiles);
                properties.SetVector(MotionBlurShader.SCREEN, pass.Screen);
                properties.SetVector(MotionBlurShader.PARAMS, pass.Params);
                MotionBlurShader.Draw(context.cmd, pass.Material, pass.ShaderPass, properties);
            });
        }

        private static TextureHandle UseIfValid(IRasterRenderGraphBuilder builder, TextureHandle texture)
        {
            if (texture.IsValid())
            {
                builder.UseTexture(texture);
            }

            return texture;
        }

        private static void SetIfValid(MaterialPropertyBlock properties, int id, TextureHandle texture)
        {
            if (texture.IsValid())
            {
                properties.SetTexture(id, (RTHandle)texture);
            }
        }
    }
}
