using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class PixilationRenderFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class PixilationSettings
    {
        public bool isEnabled = true;
        public bool enableInSceneView = false;
        public LayerMask layerMask;
        public RenderPassEvent renderPass = RenderPassEvent.AfterRendering;
        public Material pixilationMaterial; //has the effect pass and the final blit pass
        [Range(0, 5)] public int LOD = 3;
    }

    public PixilationSettings settings = new PixilationSettings();

    const string _profilerTagDownSamplePixilation = "DownSamplePixilationPass";
    const string _profilerTagBlitPixilation = "BlitPixilationPass";

    const int MESH_DRAW_PASS = 0;
    const int VERTICAL_BLUR_PASS = 1;
    const int HORIZONTAL_BLUR_PASS = 2;
    const int DOWNSAMPLE_PASS = 3;
    const int RESOLVE_DOWNSAMPLE_PASS = 4;
    const int PIXILATION_TO_RT_PASS = 5;


    RTHandle drawMeshRTHandle;
    int meshBufferID = Shader.PropertyToID("_MeshRenderBuffer");
    //int verticalBlurBufferID = Shader.PropertyToID("_VerticalBlurBuffer");
    //int horizontalBlurBufferID = Shader.PropertyToID("_HorizontalBlurBuffer");
    int downSampleBufferID = Shader.PropertyToID("_PixilationDownSampleBuffer");
    int blitToBufferID = Shader.PropertyToID("_PixilationBlitToBuffer");

    string downSampleName = "_DownSampleBufferID_";

    DrawMeshPass drawMeshPass;
    PixilationPass pixilationPass;



    class DrawMeshPass : ScriptableRenderPass
    {
        //int meshBufferID;
        RTHandle drawRenderersRTHandle;

        readonly ProfilingSampler _profilingSampler;

        PixilationSettings settings;

        //boiler plate for selective objects
        readonly List<ShaderTagId> _shaderTagIds = new List<ShaderTagId>();
        FilteringSettings _filteringSettings;
        RenderStateBlock _renderStateBlock;

        public DrawMeshPass(LayerMask layerMask, PixilationSettings settings)
        {
            this.settings = settings;

            this.renderPassEvent = settings.renderPass;

            _profilingSampler = new ProfilingSampler(_profilerTagDownSamplePixilation);

            //Boilerplate code for setting up draw renderers to draw only objects on layer
            _filteringSettings = new FilteringSettings(null, layerMask);

            _shaderTagIds.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIds.Add(new ShaderTagId("UniversalForwardOnly"));
            _shaderTagIds.Add(new ShaderTagId("LightweightForward"));

            _renderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
        }

        public void ReleaseHandles()
        {
            drawRenderersRTHandle?.Release();
        }

        public void SetupDrawRenderersRTHandle(RTHandle drawRenderersRTHandle)
        {
            this.drawRenderersRTHandle = drawRenderersRTHandle;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            //in a render feature we do not want to call CommandBuffer.SetRenderTarget/ClearRenderTarget
            //it just isnt how it works. You have to use these methods
            ConfigureTarget(drawRenderersRTHandle, renderingData.cameraData.renderer.cameraDepthTargetHandle);
            //ConfigureTarget(meshBufferID);
            ConfigureClear(ClearFlag.Color, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //for drawRenderers we need these drawingSettings, so we create them
            SortingCriteria sortingCriteria = SortingCriteria.CommonOpaque;
            DrawingSettings drawingSettings = CreateDrawingSettings(
                _shaderTagIds, ref renderingData, sortingCriteria);
            drawingSettings.overrideMaterial = settings.pixilationMaterial;
            drawingSettings.overrideMaterialPassIndex = MESH_DRAW_PASS;


            //this buffer is really only used to use the Profiling scope so it is easy to frame debug
            //kind of hacky but w/e
            CommandBuffer cmd = CommandBufferPool.Get(_profilingSampler.name);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                context.DrawRenderers(
                    renderingData.cullResults, ref drawingSettings,
                    ref _filteringSettings, ref _renderStateBlock);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }

    class PixilationPass : ScriptableRenderPass
    {
        int downSampleBufferID;
        int blitToBufferID;

        //int meshBufferID;
        RTHandle drawRenderersRTHandle;

        int realBufferCount = -1;

        List<int> downSampleIDs = new List<int>();

        readonly ProfilingSampler _profilingSampler;

        PixilationSettings settings;

        public PixilationPass(PixilationSettings settings, int blitToBufferID, int downSampleID, string downSampleName)
        {
            this.settings = settings;

            renderPassEvent = settings.renderPass;

            downSampleBufferID = downSampleID;
            this.blitToBufferID = blitToBufferID;

            for(int i = 0; i < settings.LOD; i++)
            {
                downSampleIDs.Add(Shader.PropertyToID(downSampleName + i));
            }

            _profilingSampler = new ProfilingSampler(_profilerTagBlitPixilation);
        }

        public void SetDrawRenderersRTHandle(RTHandle drawRenderersRTHandle)
        {
            this.drawRenderersRTHandle = drawRenderersRTHandle;
        }

        public void ReleaseHandles()
        {
            drawRenderersRTHandle?.Release();
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            RenderTextureDescriptor targetDesc = renderingData.cameraData.cameraTargetDescriptor;

            RenderTextureDescriptor downSampleDesc = targetDesc;

            //downSampleDesc.width = Mathf.CeilToInt(Mathf.Max(1, downSampleDesc.width / Mathf.Pow(2, settings.LOD)));
            //downSampleDesc.height = Mathf.CeilToInt(Mathf.Max(1, downSampleDesc.height / Mathf.Pow(2, settings.LOD)));   
            
            for(int i = 0; i < downSampleIDs.Count; i++)
            {
                downSampleDesc.width /= 2;
                downSampleDesc.height /= 2;

                if(downSampleDesc.width < 2 || downSampleDesc.height < 2)
                {
                    break;
                }

                realBufferCount = i + 1;

                cmd.GetTemporaryRT(downSampleIDs[i], downSampleDesc, FilterMode.Bilinear);
            }

            cmd.GetTemporaryRT(downSampleBufferID, downSampleDesc);

            cmd.SetGlobalVector("_PixilationDownSampleResolution", new Vector4(1f / downSampleDesc.width, 1f / downSampleDesc.height, downSampleDesc.width, downSampleDesc.height));

            cmd.GetTemporaryRT(blitToBufferID, targetDesc);
        }

        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            cmd.ReleaseTemporaryRT(downSampleBufferID);
            cmd.ReleaseTemporaryRT(blitToBufferID);

            for(int i = 0; i < realBufferCount; i++)
            {
                cmd.ReleaseTemporaryRT(downSampleIDs[i]);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            //this buffer is really only used to use the Profiling scope so it is easy to frame debug
            //kind of hacky but w/e
            CommandBuffer cmd = CommandBufferPool.Get(_profilingSampler.name);

            using (new ProfilingScope(cmd, _profilingSampler))
            {
                if (realBufferCount > 0)
                {
                    cmd.Blit(drawRenderersRTHandle, downSampleIDs[0]);

                    //down sample
                    for (int i = 1; i < realBufferCount; i++)
                    {
                        cmd.Blit(downSampleIDs[i - 1], downSampleIDs[i]);
                    }

                    //up sample
                    for (int i = realBufferCount - 1; i > 0; i--)
                    {
                        cmd.Blit(downSampleIDs[i], downSampleIDs[i - 1]);
                    }

                    //to global set texture
                    cmd.Blit(downSampleIDs[0], downSampleBufferID);
                }
                else
                {
                    //to global set texture
                    cmd.Blit(drawRenderersRTHandle, downSampleBufferID);
                }


                //using the id worked here, using the handle on its own caused issues with how it is cast to a texture.
                cmd.Blit(renderingData.cameraData.renderer.cameraColorTargetHandle.nameID, blitToBufferID, settings.pixilationMaterial, PIXILATION_TO_RT_PASS);
                cmd.Blit(blitToBufferID, renderingData.cameraData.renderer.cameraColorTargetHandle);
            }

            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        RenderTextureDescriptor targetDesc = renderingData.cameraData.cameraTargetDescriptor;
        targetDesc.depthBufferBits = 0;
        RenderingUtils.ReAllocateIfNeeded(ref drawMeshRTHandle, targetDesc);

        drawMeshPass.SetupDrawRenderersRTHandle(drawMeshRTHandle);
        pixilationPass.SetDrawRenderersRTHandle(drawMeshRTHandle);

        pixilationPass.ConfigureInput(ScriptableRenderPassInput.Color);
    }


    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (drawMeshPass == null)
        {
            return;
        }

        renderer.EnqueuePass(drawMeshPass);
        renderer.EnqueuePass(pixilationPass);
    }

    public override void Create()
    {
        if (!settings.isEnabled)
        {
            return;
        }

        drawMeshPass = new DrawMeshPass(settings.layerMask, settings);
        pixilationPass = new PixilationPass(settings, blitToBufferID, downSampleBufferID, downSampleName);
    }

    protected override void Dispose(bool disposing)
    {
        drawMeshPass.ReleaseHandles();
        pixilationPass.ReleaseHandles();
    }
}
