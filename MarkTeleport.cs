using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace AotForms
{
    internal class KCMap_Teleport
    {
        private static Task _teleTask;
        private static CancellationTokenSource _cts;
        private static volatile bool _isRunning = false;
        private static uint _staticGameFacade = 0;

        internal static void Work()
        {
            if (_isRunning) return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            _isRunning = true;

            _teleTask = Task.Run(async () =>
            {
                while (!_cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        if (!Config.teleportmap || Core.LocalPlayer == 0)
                        {
                            await Task.Delay(10, _cts.Token);
                            continue;
                        }

                        Vector3 markedPos = GetMarkedPosition();
                        if (markedPos == Vector3.Zero)
                        {
                            await Task.Delay(10, _cts.Token);
                            continue;
                        }

                        // Fix: Check if Y is suspiciously high (marker Y is often 500-1000)
                        if (markedPos.Y > 150.0f)
                        {
                            markedPos.Y = 1.5f; // Set to a safe near-ground level
                        }

                        TeleportToPosition(markedPos);
                        await Task.Delay(5, _cts.Token); // Slightly more delay for stability
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        await Task.Delay(10, _cts.Token);
                    }
                }
                _isRunning = false;
            }, _cts.Token);
        }

        internal static void Stop()
        {
            if (!_isRunning) return;
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
            _isRunning = false;
            _staticGameFacade = 0;
        }

        private static void TeleportToPosition(Vector3 pos)
        {
            try
            {
                if (Core.LocalPlayer == 0) return;
                if (!InternalMemory.Read<uint>(Core.LocalPlayer + (uint)Bones.Root, out uint root) || root == 0)
                    return;
                if (!InternalMemory.Read<uint>(root + 0x8, out uint transform) || transform == 0)
                    return;
                if (!InternalMemory.Read<uint>(transform + 0x8, out uint obj) || obj == 0)
                    return;

                if (!InternalMemory.Read<uint>(obj + 0x20, out uint matrix) || matrix == 0)
                    return;

                InternalMemory.Write(matrix + 0x60, pos);
                InternalMemory.Write<float>(matrix + 0x6C, 1.0f); // Set W = 1 for matrix stability
            }
            catch { }
        }

        private static Vector3 GetMarkedPosition()
        {
            try
            {
                // Re-read facade if lost
                if (_staticGameFacade == 0)
                {
                    if (!InternalMemory.Read<uint>(Offsets.Il2Cpp + Offsets.InitBase, out var baseGameFacade))
                        return Vector3.Zero;
                    if (!InternalMemory.Read<uint>(baseGameFacade, out var gameFacade))
                        return Vector3.Zero;
                    if (!InternalMemory.Read<uint>(gameFacade + Offsets.StaticClass, out _staticGameFacade))
                        return Vector3.Zero;
                }

                if (!InternalMemory.Read<uint>(_staticGameFacade + 0x0, out var currentGame))
                {
                    _staticGameFacade = 0; // force re-read next time
                    return Vector3.Zero;
                }
                if (!InternalMemory.Read<uint>(currentGame + 0x8, out uint UIInGameScene))
                    return Vector3.Zero;
                if (!InternalMemory.Read<uint>(UIInGameScene + 0x1F4, out uint m_BigMapCtrl))
                    return Vector3.Zero;
                if (!InternalMemory.Read<uint>(m_BigMapCtrl + 0x54, out uint m_MapContentCtrl))
                    return Vector3.Zero;
                if (!InternalMemory.Read<uint>(m_MapContentCtrl + 0x90, out uint m_LocalMapMarkController))
                    return Vector3.Zero;
                if (!InternalMemory.Read(m_LocalMapMarkController + 0x58, out Vector3 selectedPos))
                    return Vector3.Zero;

                return selectedPos;
            }
            catch
            {
                _staticGameFacade = 0;
                return Vector3.Zero;
            }
        }
    }
}
