using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;

namespace AotForms
{
    internal class DownPlayer
    {
        private static Task tpTask;
        private static CancellationTokenSource cts = new();
        private static bool isFrozen = false;
        private static Vector3 frozenPos = Vector3.Zero;
        private static float teleportDownDistance = 0.9f;
        private static int teleportDelay = 1;
        private static bool _isRunning = false;
        private static Thread _DownPlayerThread;
        
        // Enemy down lock system
        private static readonly Dictionary<uint, Vector3> _enemyOriginalPositions = new();
        private static Entity currentEnemyTarget = null;
        private const float enemyDownLockDistance = 1.7f;
        private const float undergroundEnemyDownDistance = 2f;
        private const int enemyLockUpdateInterval = 1; // Fast update for stability

        internal static void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            Config.DownPlayer = true;
            _DownPlayerThread = new Thread(Work)
            {
                IsBackground = true
            };
            _DownPlayerThread.Start();
            StartTeleportTask();
        }

        internal static void Stop()
        {
            _isRunning = false;
            Config.DownPlayer = false;
            StopTeleportTask();
            RestoreAllEnemyPositions();
            currentEnemyTarget = null;
        }

        private static void StartTeleportTask()
        {
            if (cts != null && cts.IsCancellationRequested)
                cts = new CancellationTokenSource();
            else if (cts == null)
                cts = new CancellationTokenSource();

            tpTask = Task.Run(async () =>
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    if (!Config.DownPlayer)
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
                    catch { }

                    await Task.Delay(teleportDelay, cts.Token);
                }
            }, cts.Token);
        }

        private static void StopTeleportTask()
        {
            try { cts?.Cancel(); } catch { }
            isFrozen = false;
        }

        internal static void SetTeleportDown(float distance) => teleportDownDistance = distance;
        internal static void SetTeleportDelay(int delay) => teleportDelay = delay;

        private static bool IsLocalFiring()
        {
            try
            {
                bool value;
                if (InternalMemory.Read<bool>(Core.LocalPlayer + Offsets.IsFiring, out value))
                    return value;
                return false;
            }
            catch { return false; }
        }

        private static Entity FindBestEnemyTarget()
        {
            Entity bestTarget = null;
            float closestDist = float.MaxValue;
            var screenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);

            foreach (var entity in Core.Entities.Values)
            {
                if (!entity.IsKnown || entity.IsDead) continue;
                if (entity.IsKnocked) continue;

                var head2D = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                if (head2D.X < 1 || head2D.Y < 1 || head2D.X > Core.Width || head2D.Y > Core.Height) continue;

                float dist3D = Vector3.Distance(Core.LocalMainCamera, entity.Head);
                if (dist3D > 200f) continue;

                float crosshairDist = Vector2.Distance(screenCenter, head2D);
                if (crosshairDist > Config.AimFovCircle) continue;

                if (crosshairDist < closestDist)
                {
                    closestDist = crosshairDist;
                    bestTarget = entity;
                }
            }

            return bestTarget;
        }

        private static void ApplyEnemyDownLock(Entity entity)
        {
            try
            {
                if (!InternalMemory.Read<uint>(entity.Address + (uint)Bones.Root, out uint rootBonePtr) || rootBonePtr == 0) return;
                if (!InternalMemory.Read<uint>(rootBonePtr + 0x8, out uint transformValue) || transformValue == 0) return;
                if (!InternalMemory.Read<uint>(transformValue + 0x8, out uint transformObjPtr) || transformObjPtr == 0) return;
                if (!InternalMemory.Read<uint>(transformObjPtr + 0x20, out uint matrixPtr) || matrixPtr == 0) return;

                // Read current position
                if (!InternalMemory.Read<Vector3>(matrixPtr + 0x60, out Vector3 currentPos)) return;

                // Store original position on first lock
                if (!_enemyOriginalPositions.ContainsKey(entity.Address))
                {
                    _enemyOriginalPositions[entity.Address] = currentPos;
                }

                Vector3 originalPos = _enemyOriginalPositions[entity.Address];

                float downDist = Config.undergroundkill ? undergroundEnemyDownDistance : enemyDownLockDistance;
                Vector3 lockedPos = originalPos;
                lockedPos.Y = originalPos.Y - downDist;

                InternalMemory.Write<Vector3>(matrixPtr + 0x60, lockedPos);
            }
            catch { }
        }

        private static void RestoreEnemyPosition(uint entityAddress)
        {
            if (!_enemyOriginalPositions.ContainsKey(entityAddress)) return;

            try
            {
                if (!InternalMemory.Read<uint>(entityAddress + (uint)Bones.Root, out uint rootBonePtr) || rootBonePtr == 0) return;
                if (!InternalMemory.Read<uint>(rootBonePtr + 0x8, out uint transformValue) || transformValue == 0) return;
                if (!InternalMemory.Read<uint>(transformValue + 0x8, out uint transformObjPtr) || transformObjPtr == 0) return;
                if (!InternalMemory.Read<uint>(transformObjPtr + 0x20, out uint matrixPtr) || matrixPtr == 0) return;

                InternalMemory.Write<Vector3>(matrixPtr + 0x60, _enemyOriginalPositions[entityAddress]);
                _enemyOriginalPositions.Remove(entityAddress);
            }
            catch { }
        }

        private static void RestoreAllEnemyPositions()
        {
            foreach (var entry in _enemyOriginalPositions)
            {
                try
                {
                    if (!InternalMemory.Read<uint>(entry.Key + (uint)Bones.Root, out uint rootBonePtr) || rootBonePtr == 0) continue;
                    if (!InternalMemory.Read<uint>(rootBonePtr + 0x8, out uint transformValue) || transformValue == 0) continue;
                    if (!InternalMemory.Read<uint>(transformValue + 0x8, out uint transformObjPtr) || transformObjPtr == 0) continue;
                    if (!InternalMemory.Read<uint>(transformObjPtr + 0x20, out uint matrixPtr) || matrixPtr == 0) continue;

                    InternalMemory.Write<Vector3>(matrixPtr + 0x60, entry.Value);
                }
                catch { }
            }

            _enemyOriginalPositions.Clear();
        }

        internal static void Work()
        {
            while (Config.DownPlayer || Config.undergroundkill)
            {
                try
                {
                    if (Core.Width == -1 || Core.Height == -1 || !Core.HaveMatrix)
                    {
                        Thread.Sleep(enemyLockUpdateInterval);
                        continue;
                    }

                    if (!IsLocalFiring())
                    {
                        // Fire released - restore all enemy positions
                        if (currentEnemyTarget != null)
                        {
                            RestoreEnemyPosition(currentEnemyTarget.Address);
                            currentEnemyTarget = null;
                        }
                        RestoreAllEnemyPositions();
                        Thread.Sleep(enemyLockUpdateInterval);
                        continue;
                    }

                    // Maintain lock if valid
                    if (currentEnemyTarget != null)
                    {
                        if (currentEnemyTarget.IsDead || currentEnemyTarget.IsKnocked || !currentEnemyTarget.IsEnemy)
                        {
                            RestoreEnemyPosition(currentEnemyTarget.Address);
                            currentEnemyTarget = null;
                        }
                        else
                        {
                            float dist3D = Vector3.Distance(Core.LocalMainCamera, currentEnemyTarget.Head);
                            if (dist3D > 200f)
                            {
                                RestoreEnemyPosition(currentEnemyTarget.Address);
                                currentEnemyTarget = null;
                            }
                        }
                    }

                    // Find new target if not locked
                    if (currentEnemyTarget == null)
                        currentEnemyTarget = FindBestEnemyTarget();

                    // Apply down lock to enemy
                    if (currentEnemyTarget != null)
                        ApplyEnemyDownLock(currentEnemyTarget);
                }
                catch { }

                Thread.Sleep(enemyLockUpdateInterval);
            }

            // Restore all positions when stopping
            RestoreAllEnemyPositions();
        }
    }
}
