using AotForms;
using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace Client
{
    internal static class undergroundkill
    {
        private static Task tpTask;
        private static CancellationTokenSource cts = new();
        private static bool isRunning = false;

        private static bool isFrozen = false;
        private static Vector3 frozenPos = Vector3.Zero;

        private static float teleportDownDistance = 0.9f;
        private static int teleportDelay = 1;

        internal static void Work()
        {
            if (isRunning) return;
            isRunning = true;

            if (cts != null && cts.IsCancellationRequested)
                cts = new CancellationTokenSource();
            else if (cts == null)
                cts = new CancellationTokenSource();

            tpTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (!Config.undergroundkill)
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

                        if (!InternalMemory.Read<Vector3>(matrixPtr + 0x60, out Vector3 currentPos))
                            continue;

                        if (!isFrozen)
                        {

                            Vector3 newPos = currentPos;
                            newPos.Y -= teleportDownDistance;


                            for (int i = 0; i < 50; i++)
                            {
                                InternalMemory.Write<Vector3>(matrixPtr + 0x60, newPos);
                                await Task.Delay(teleportDelay, cts.Token);
                            }

                            frozenPos = newPos;
                            isFrozen = true;
                        }
                        else
                        {

                            if (InternalMemory.Read<Vector3>(matrixPtr + 0x60, out Vector3 posNow))
                            {
                                float delta = Vector3.Distance(posNow, frozenPos);
                                if (delta > 0.5f)
                                {
                                    isFrozen = false;
                                    await Task.Delay(teleportDelay, cts.Token);
                                    continue;
                                }
                            }

                            InternalMemory.Write<Vector3>(matrixPtr + 0x60, frozenPos);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {

                    }

                    await Task.Delay(teleportDelay, cts.Token);
                }
            }, cts.Token);
        }

        internal static void Stop()
        {
            if (!isRunning) return;

            try { cts.Cancel(); } catch { }
            isRunning = false;
            isFrozen = false;
        }


        internal static void SetTeleportDown(float distance) => teleportDownDistance = distance;
        internal static void SetTeleportDelay(int delay) => teleportDelay = delay;
    }
}
