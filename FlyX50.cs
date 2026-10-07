using System;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace AotForms
{
    /// <summary>
    /// Premium Fly Hack implementation using Bone Transform Matrix locking.
    /// Locks player height at exactly 50 units above the current reference.
    /// </summary>
    internal static class FlyHack50x
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
                    if (Config.FlyHack50xx)
                    {
                        // Mutual Exclusion: If this is on, the other must be off
                        if (Config.FlyHack80x) Config.FlyHack80x = false;

                        ApplyHeightLock();
                    }
                    else if (_isLocked)
                    {
                        RestoreState();
                    }
                }
                catch { /* Optimized: Ignore transient memory errors */ }

                await Task.Delay(5, token); // High frequency for stability
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

            // Step 1: Access the Root Bone Node
            if (!InternalMemory.Read<uint>(localPlayer + (uint)Bones.Root, out uint rootNode)) return;

            // Step 2: Traverse Unity Transform hierarchy to reach the Matrix List
            // Reference: Unity Transform -> transformValue (0x8) -> transformObj (0x8)
            if (!InternalMemory.Read<uint>(rootNode + 0x8, out uint transformValue)) return;
            if (!InternalMemory.Read<uint>(transformValue + 0x8, out uint transformObj)) return;

            // Step 3: Extract Matrix Index and List Base
            // transformObj + 0x24 = index, + 0x20 = matrixDataPtr
            if (!InternalMemory.Read<uint>(transformObj + 0x24, out uint index)) return;
            if (!InternalMemory.Read<uint>(transformObj + 0x20, out uint matrixData)) return;
            
            // matrixData + 0x18 = actual Matrix List
            if (!InternalMemory.Read<uint>(matrixData + 0x18, out uint matrixList)) return;
            if (matrixList == 0) return;

            // Step 4: Calculate Target Matrix Address (each entry is 0x30 bytes)
            ulong targetAddr = (ulong)(matrixList + (index * 0x30));

            // Step 5: Read and Manipulate Position
            if (InternalMemory.Read<Vector3>(targetAddr, out Vector3 currentPos))
            {
                if (!_isLocked)
                {
                    _restorePos = currentPos;
                    _lockedPos = currentPos;
                    _lockedPos.Y += 9.0f; // Lock at +9 Height (reduced from 50.0f to avoid anti-cheat kicks)
                    _isLocked = true;
                }

                // Step 6: Force Vertical Position into the Matrix
                // We preserve real-time horizontal coordinates from currentPos, only modify Y
                Vector3 targetPos = currentPos;
                targetPos.Y = _restorePos.Y + 9.0f;

                InternalMemory.Write<Vector3>(targetAddr, targetPos);
                
                // Ensure W-component of the matrix column is 1.0f (homogenous coordinates)
                InternalMemory.Write<float>(targetAddr + 0xC, 1.0f);
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
