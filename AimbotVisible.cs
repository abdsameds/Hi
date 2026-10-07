using ImGuiNET;
using System;
using System.Numerics;
using System.Threading;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.TaskbarClock;

namespace AotForms
{
    internal static class AimbotAi
    {


        private static CancellationTokenSource cts = new();
        private static bool isRunning = false;

        internal static void Work()
        {
            while (Config.AimbotVisible)
            {
                if (Core.Width == -1 || Core.Height == -1 || !Core.HaveMatrix)
                {
                    Thread.Sleep(1);
                    continue;
                }

                Entity target = FindBestTarget();
                if (target != null)
                {
                    AimAtTarget(target);
                }
            }
        }

        private static Entity FindBestTarget()
        {
            Entity bestTarget = null;
            float closestDistance = float.MaxValue;
            var screenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);

            foreach (var entity in Core.Entities.Values)
            {
                if (entity.IsDead) continue;

                if (Config.IgnoreKnocked && entity.IsKnocked)
                    continue;

                var head2D = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                if (head2D.X < 1 || head2D.Y < 1) continue;

                float playerDistance = Vector3.Distance(Core.LocalMainCamera, entity.Head);
                if (playerDistance > Config.AimBotMaxDistance) continue;

                float crosshairDistance = Vector2.Distance(screenCenter, head2D);
                if (crosshairDistance < closestDistance && crosshairDistance <= Config.AimBotFov)
                {
                    closestDistance = crosshairDistance;
                    bestTarget = entity;
                    Thread.Sleep(Config.AimbotSmoothness);
                }
            }

            return bestTarget;
        }

        private static void AimAtTarget(Entity target)
        {
            if (target == null || target.Address == 0) return;

            uint m_HeadCollider;
            var rHeadCollider = InternalMemory.Read<uint>(target.Address + 0x4a4, out m_HeadCollider);
            if (!rHeadCollider || m_HeadCollider == 0) return;

            const int repeatCount = 10;

            for (int i = 0; i < repeatCount; i++)
            {
                InternalMemory.Write(target.Address + Offsets.adarshcheat, m_HeadCollider);
            }
            InternalMemory.Write(target.Address + Offsets.adarshcheat, m_HeadCollider);
        }

        internal static void Render()
        {
            if (!Config.AimbotVisible || Core.Width == -1 || Core.Height == -1)
                return;

            var screenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);

            ImGui.GetBackgroundDrawList().AddCircle(
                screenCenter,
                Config.AimBotFov,
                ImGui.GetColorU32(new Vector4(1f, 0f, 0f, 0.8f)), // Red color
                64, // Number of segments
                2f  // Thickness
            );
        }

        internal static void Stop()
        {
            if (!isRunning) return;

            cts.Cancel();
            cts = new CancellationTokenSource();
            isRunning = false;
        }


    }
}