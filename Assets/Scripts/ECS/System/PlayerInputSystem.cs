using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = ECS.Components.PlayerInput;

namespace ECS.System
{
    [BurstCompile]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(WorldBootstrapSystem))]
    public partial struct PlayerInputSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            
            state.RequireForUpdate<PlayerInput>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }
        
        public void OnUpdate(ref SystemState state)
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            
            if (keyboard is null || mouse is null) 
                return;

            foreach (var input in SystemAPI.Query<RefRW<PlayerInput>>())
            {
                var mouseDelta = mouse.delta.ReadValue();
                
                input.ValueRW = new PlayerInput
                {
                    Move = new float2(
                        (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
                        (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f)
                    ),
                    Look = mouse.wasUpdatedThisFrame ? new float2(mouseDelta.x, mouseDelta.y) : float2.zero,
                    Jump = keyboard.spaceKey.isPressed,
                    Run = keyboard.leftShiftKey.isPressed,
                };
            }
        }
    }
}