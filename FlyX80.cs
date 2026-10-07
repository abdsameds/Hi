using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace AotForms
{
    /// <summary>
    /// Premium Fly Hack implementation using Bone Transform Matrix locking.
    /// Locks player height at exactly 80 units above the current reference.
    /// This version is optimized for stability and avoids all external physics offsets.
    /// </summary>
    internal static class FlyHack80x
    {
        private static Task _flyTask;
        private static CancellationTokenSource _cts = new();
        private static bool _isRunning = false;

        // State tracking
        private static Vector3 _restorePos = Vector3.Zero;
        private static bool _isLocked = false;
        private static Vector3 _lockedPos = Vector3.Zero;

        internal static void SetState(bool enable)
        {
            if (enable)
            {
                Start();
            }
            else
            {
                Stop();
            }
        }

        internal static void Start()
        {
            if (_isRunning) return;
            _isRunning = true;
            _cts = new CancellationTokenSource();
            _flyTask = Task.Run(() => FlyLoop(_cts.Token), _cts.Token);
        }

        internal static void Stop()
        {
            _cts?.Cancel();
            _isRunning = false;
        }

        private static async Task FlyLoop(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (Config.FlyHack80xx)
                    {
                        // Mutual Exclusion: If any teleport feature is active, let it handle the height
                        if (Config.TeleportAnywhere || Config.teleportmap || Config.directteleport)
                        {
                            await Task.Delay(10, token);
                            continue;
                        }

                        if (Config.FlyHack50xx) Config.FlyHack50xx = false;
                        if (Config.ClimbUpV2Enabled) Config.ClimbUpV2Enabled = false;

                        ApplyHeightLock();
                    }
                    else if (_isLocked)
                    {
                        RestoreState();
                    }
                }
                catch { /* Suppress memory access exceptions */ }

                await Task.Delay(5, token);
            }

            if (_isLocked) RestoreState();
        }

        private static void ApplyHeightLock()
        {
            ulong localPlayer = Core.LocalPlayer;
            if (localPlayer == 0)
            {
                if (InternalMemory.Read<uint>(Offsets.Il2Cpp + Offsets.LocalPlayer, out var lpRva))
                    localPlayer = lpRva;
            }
            if (localPlayer == 0) return;

            // Bone Root Transform Hierarchy
            if (!InternalMemory.Read<uint>(localPlayer + (uint)Bones.Root, out uint rootNode)) return;
            if (!InternalMemory.Read<uint>(rootNode + 0x8, out uint transformValue)) return;
            if (!InternalMemory.Read<uint>(transformValue + 0x8, out uint transformObj)) return;
            if (!InternalMemory.Read<uint>(transformObj + 0x24, out uint index)) return;
            if (!InternalMemory.Read<uint>(transformObj + 0x20, out uint matrixData)) return;
            if (!InternalMemory.Read<uint>(matrixData + 0x18, out uint matrixList)) return;
            if (matrixList == 0) return;

            ulong targetAddr = (ulong)(matrixList + (index * 0x30));

            if (InternalMemory.Read<Vector3>(targetAddr, out Vector3 currentPos))
            {
                if (!_isLocked)
                {
                    _restorePos = currentPos;
                    _lockedPos = currentPos;
                    _lockedPos.Y += 12.0f; // Height Lock X80 (reduced from 80.0f to avoid anti-cheat kicks)
                    _isLocked = true;
                }

                // Preserve real-time horizontal coordinates from currentPos, only modify Y
                Vector3 targetPos = currentPos;
                targetPos.Y = _restorePos.Y + 12.0f;

                InternalMemory.Write<Vector3>(targetAddr, targetPos);
                InternalMemory.Write<float>(targetAddr + 0xC, 1.0f); // W = 1
            }
        }

        private static void RestoreState()
        {
            if (!_isLocked) return;

            ulong localPlayer = Core.LocalPlayer;
            if (localPlayer != 0)
            {
                if (InternalMemory.Read<uint>(localPlayer + (uint)Bones.Root, out uint rootNode) &&
                    InternalMemory.Read<uint>(rootNode + 0x8, out uint transformValue) &&
                    InternalMemory.Read<uint>(transformValue + 0x8, out uint transformObj) &&
                    InternalMemory.Read<uint>(transformObj + 0x24, out uint index) &&
                    InternalMemory.Read<uint>(transformObj + 0x20, out uint matrixData) &&
                    InternalMemory.Read<uint>(matrixData + 0x18, out uint matrixList))
                {
                    if (matrixList != 0)
                    {
                        ulong targetAddr = (ulong)(matrixList + (index * 0x30));
                        InternalMemory.Write<Vector3>(targetAddr, _restorePos);
                        InternalMemory.Write<float>(targetAddr + 0xC, 1.0f);
                    }
                }
            }
            _isLocked = false;
        }
    }
}