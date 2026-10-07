using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using AotForms;

namespace Client
{
    internal static class fprceaim
    {
        private static Task moveTask;
        private static CancellationTokenSource cts;
        private static bool isRunning = false;

        // Settings
        private static float maxDistance = 200f;       // max target distance in world units
        private static int pullIntervalMs = 5;         // update interval in ms
        private static float screenPullRange = 140f;   // max distance in pixels from crosshair to pull

        internal static void Start()
        {
            if (isRunning) return;
            isRunning = true;

            cts = new CancellationTokenSource();
            moveTask = Task.Run(() => LoopAsync(cts.Token), cts.Token);
        }

        internal static void Stop()
        {
            if (!isRunning) return;
            cts.Cancel();
            try { moveTask.Wait(); } catch { }
            isRunning = false;
        }

        private static async Task LoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (!Config.forceaim || Core.Width == -1 || Core.Height == -1 || !Core.HaveMatrix)
                    {
                        await Task.Delay(pullIntervalMs, token);
                        continue;
                    }

                    if ((!InternalMemory.Read(Core.LocalPlayer + 0x4a0, out bool firing) || !firing))
                    {
                        await Task.Delay(pullIntervalMs, token);
                        continue;
                    }

                    Entity target = FindBestTarget();
                    if (target != null)
                        MoveTargetToCrosshair(target);
                }
                catch
                {
                    // keep loop running
                }

                await Task.Delay(pullIntervalMs, token);
            }
        }

        private static void MoveTargetToCrosshair(Entity target)
        {
            try
            {
                if (!InternalMemory.Read(target.Address + (uint)Bones.Root, out uint enemyRootBonePtr)) return;
                if (!InternalMemory.Read(enemyRootBonePtr + 0x8, out uint enemyTransformValue)) return;
                if (!InternalMemory.Read(enemyTransformValue + 0x8, out uint enemyTransformObjPtr)) return;
                if (!InternalMemory.Read(enemyTransformObjPtr + 0x20, out uint enemyMatrixValue)) return;
                if (!InternalMemory.Read(enemyMatrixValue + 0x60, out Vector3 currentRootPos)) return;

                // Get current head position and screen center
                Vector3 currentHeadPos = target.Head;
                Vector2 screenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);

                // Only pull if enemy HEAD is close enough to crosshair on screen
                Vector2 headScreen = W2S.WorldToScreen(Core.CameraMatrix, currentHeadPos, Core.Width, Core.Height);
                float screenDistance = Vector2.Distance(headScreen, screenCenter);
                if (screenDistance > screenPullRange) return;

                // Calculate where head should be (on crosshair at same depth)
                float headDepth = Vector3.Distance(Core.LocalMainCamera, currentHeadPos);
                Vector3 targetHeadPos = ScreenToWorld(screenCenter, headDepth);

                // Calculate offset and apply to root
                Vector3 offset = targetHeadPos - currentHeadPos;
                Vector3 newRootPos = currentRootPos + offset;

                InternalMemory.Write(enemyMatrixValue + 0x60, newRootPos);
            }
            catch { }
        }

        private static Entity FindBestTarget()
        {
            Entity bestTarget = null;
            float closestDistance = float.MaxValue;
            Vector2 screenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);

            foreach (var entity in Core.Entities.Values)
            {
                if (entity.IsDead || (Config.IgnoreKnocked && entity.IsKnocked))
                    continue;

                var head2D = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                if (head2D.X < 0 || head2D.Y < 0) continue;

                float playerDistance = Vector3.Distance(Core.LocalMainCamera, entity.Head);
                if (playerDistance > maxDistance) continue;

                if (playerDistance <= 10f) continue;  // Skip too-close enemies

                float crosshairDistance = Vector2.Distance(screenCenter, head2D);
                if (crosshairDistance < closestDistance)
                {
                    closestDistance = crosshairDistance;
                    bestTarget = entity;
                }
            }

            return bestTarget;
        }

        private static Vector3 ScreenToWorld(Vector2 screen, float depth)
        {
            float x = (2f * screen.X) / Core.Width - 1f;
            float y = 1f - (2f * screen.Y) / Core.Height;

            Vector4 ndc = new Vector4(x, y, 1f, 1f);

            if (!Matrix4x4.Invert(Core.CameraMatrix, out Matrix4x4 inv))
                return Core.LocalMainCamera;

            Vector4 worldPos = Vector4.Transform(ndc, inv);
            if (worldPos.W != 0f)
                worldPos /= worldPos.W;

            Vector3 camPos = Core.LocalMainCamera;
            Vector3 dir = Vector3.Normalize(new Vector3(worldPos.X, worldPos.Y, worldPos.Z) - camPos);

            return camPos + dir * depth;
        }
    }
}
