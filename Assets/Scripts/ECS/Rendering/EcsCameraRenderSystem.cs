using ECS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;

namespace ECS.Rendering
{
    [BurstCompile]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial class EcsCameraRenderSystem : SystemBase
    {
        [BurstCompile]
        protected override void OnCreate()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCameraRendering;
        }

        [BurstCompile]
        protected override void OnDestroy()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRendering;
        }

        [BurstCompile]
        protected override void OnUpdate() { }

        [BurstCompile]
        private void OnBeginCameraRendering(ScriptableRenderContext context, UnityEngine.Camera camera)
        {
            if (camera.cameraType != CameraType.Game) return;

            foreach (var (ecsCamera, transform) 
                     in SystemAPI.Query<RefRO<EcsCamera>, RefRO<LocalTransform>>())
            {
                camera.transform.SetPositionAndRotation(transform.ValueRO.Position, transform.ValueRO.Rotation);
                
                camera.fieldOfView = ecsCamera.ValueRO.FoV;
                camera.nearClipPlane = ecsCamera.ValueRO.Near;
                camera.farClipPlane = ecsCamera.ValueRO.Far;
                
                break;
            }
        }
    }
}