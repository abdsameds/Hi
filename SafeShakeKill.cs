using AotForms;
using Memory;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    internal static class ShakeKill
    {
        private static Task upTask;
        private static CancellationTokenSource cts = new();
        private static bool isRunning = false;

        internal static void Work()
        {
            if (isRunning) return;
            isRunning = true;

            upTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (!Config.ShakeKill)
                    {
                        await Task.Delay(1, cts.Token);
                        continue;
                    }

                    if (Core.Width == -1 || Core.Height == -1 || !Core.HaveMatrix)
                    {
                        await Task.Delay(1, cts.Token);
                        continue;
                    }

                    //BY ADARSH
                    if (!InternalMemory.Read(Core.LocalPlayer + Offsets.pomba, out bool isFiring) || !isFiring)
                    {
                        await Task.Delay(1, cts.Token);
                        continue;
                    }

                    foreach (var entity in Core.Entities.Values)
                    {
                        if (entity.IsDead || entity.IsKnocked || !entity.IsKnown)
                            continue;

                        var head2D = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                        if (head2D.X < 1 || head2D.Y < 1)
                            continue;

                        if (!IsInsideFOV((int)head2D.X, (int)head2D.Y))
                            continue;

                        try
                        {

                            if (!InternalMemory.Read(entity.Address + (uint)Bones.Root, out uint enemyRootBonePtr)) continue;
                            if (!InternalMemory.Read(enemyRootBonePtr + 0x8, out uint enemyTransformValue)) continue;
                            if (!InternalMemory.Read(enemyTransformValue + 0x8, out uint enemyTransformObjPtr)) continue;
                            if (!InternalMemory.Read(enemyTransformObjPtr + 0x20, out uint enemyMatrixValue)) continue;

                            if (!InternalMemory.Read(enemyMatrixValue + 0x60, out Vector3 enemyPos)) continue;

                            enemyPos.Y += 0.0400f;
                            InternalMemory.Write(enemyMatrixValue + 0x60, enemyPos);
                        }
                        catch
                        {

                        }
                    }

                    await Task.Delay(1, cts.Token);
                }
            }, cts.Token);
        }

        private static bool IsInsideFOV(int x, int y)
        {
            int fov = 1500;
            int centerX = Core.Width / 2;
            int centerY = Core.Height / 2;
            return (x - centerX) * (x - centerX) + (y - centerY) * (y - centerY) <= fov * fov;
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
