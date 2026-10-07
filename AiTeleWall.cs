using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using AotForms;

namespace Client
{
    internal static class Teleportwall
    {
        private static Task tpTask;
        private static CancellationTokenSource cts = new();
        private static bool isRunning = false;
        private static int teleportDelay = 15;
        private static float teleportDistance = 1.0f;

        internal static void Work()
        {
            if (isRunning) return;
            isRunning = true;

            if (cts == null || cts.IsCancellationRequested)
                cts = new CancellationTokenSource();

            tpTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (!Config.teliportwall)
                    {
                        await Task.Delay(5, cts.Token);
                        continue;
                    }

                    try
                    {
                        ulong rawLocalPlayer = Core.LocalPlayer;
                        if (rawLocalPlayer == 0)
                        {
                            await Task.Delay(teleportDelay, cts.Token);
                            continue;
                        }

                        uint localPlayer = (uint)rawLocalPlayer;

                        if (!InternalMemory.Read<uint>(localPlayer + (uint)Bones.Root, out uint rootPtr) || rootPtr == 0)
                            continue;
                        if (!InternalMemory.Read<uint>(rootPtr + 0x8, out uint t1) || t1 == 0)
                            continue;
                        if (!InternalMemory.Read<uint>(t1 + 0x8, out uint t2) || t2 == 0)
                            continue;
                        if (!InternalMemory.Read<uint>(t2 + 0x20, out uint matrixPtr) || matrixPtr == 0)
                            continue;
                        if (!InternalMemory.Read<Vector3>(matrixPtr + 0x80, out Vector3 currentPos))
                            continue;
                        if (!Core.HaveMatrix)
                        {
                            await Task.Delay(teleportDelay, cts.Token);
                            continue;
                        }

                        Matrix4x4 viewMatrix = Core.CameraMatrix;
                        Vector3 cameraForward = new Vector3(-viewMatrix.M31, -viewMatrix.M32, -viewMatrix.M33);
                        cameraForward = Vector3.Normalize(cameraForward);

                        Vector3 newPos = new Vector3(
                            currentPos.X + cameraForward.X * teleportDistance,
                            currentPos.Y + cameraForward.Y * teleportDistance,
                            currentPos.Z + cameraForward.Z * teleportDistance
                        );

                        InternalMemory.Write<Vector3>(matrixPtr + 0x80, newPos);
                        Config.teliportwall = false;

                        await Task.Delay(teleportDelay, cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch { }

                    await Task.Delay(teleportDelay, cts.Token);
                }
            }, cts.Token);
        }

        internal static void Stop()
        {
            if (!isRunning) return;
            try { cts.Cancel(); } catch { }
            isRunning = false;
        }

        internal static void SetTeleportDistance(float dist) => teleportDistance = dist;
        internal static void SetTeleportDelay(int delay) => teleportDelay = delay;
    }
}
