using AotForms;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace AotForms
{
    internal static class EnemyPull360
    {
        private static Thread pullThread;
        private static CancellationTokenSource cts;
        private static bool isRunning = false;

        private static readonly Dictionary<uint, Vector3> originalPositions = new();
        private static Entity currentTarget = null;

        private static uint _lockedEntityId;
        private static Vector3 _lockedPullPos;
        private static bool _hasLockedPos;

        private const float MaxSide = 4.7f;
        private const float MaxForward = 150f;
        private const float MaxUp = 0.55f;
        private const float PullYOffset = 0.010f;
        private const float MinProjDistance = 2.5f;
        private const int LockWritesPerTick = 4;

        internal static void Start()
        {
            if (isRunning) return;

            cts = new CancellationTokenSource();
            pullThread = new Thread(() => Work(cts.Token))
            {
                IsBackground = true,
                Priority = ThreadPriority.AboveNormal
            };
            pullThread.Start();
            isRunning = true;
        }

        internal static void Stop()
        {
            if (!isRunning) return;

            cts?.Cancel();
            ClearLockState();
            RestoreAllPositions();
            isRunning = false;
        }

        private static void Work(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                bool pullingThisFrame = false;
                try
                {
                    if (!Config.EnemyPullEnabled)
                    {
                        if (originalPositions.Count > 0 || _hasLockedPos)
                            RestoreAllPositions();
                        currentTarget = null;
                        Thread.Sleep(1);
                        continue;
                    }

                    if (Core.Width == -1 || Core.Height == -1 || !Core.HaveMatrix)
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    if (EnemyPullHelpers.IsLocalFiring())
                    {
                        pullingThisFrame = true;

                        if (currentTarget == null || !IsTargetStillValid(currentTarget))
                            currentTarget = FindBestTarget();

                        if (currentTarget != null && IsTargetStillValid(currentTarget))
                            ApplyStableLock(currentTarget);
                        else
                        {
                            currentTarget = null;
                            ClearLockState();
                        }
                    }
                    else
                    {
                        RestoreAllPositions();
                        currentTarget = null;
                    }
                }
                catch { }

                int delay = Config.EnemyPullTickMs > 0 ? Config.EnemyPullTickMs : 2;
                if (pullingThisFrame)
                    delay = 0;
                if (delay > 0)
                    Thread.Sleep(delay);
            }
        }

        private static bool IsTargetStillValid(Entity entity)
        {
            if (entity == null || entity.IsDead) return false;
            if (!entity.IsKnown) return false;
            if (Config.IgnoreKnocked && entity.IsKnocked) return false;
            if (!Core.Entities.ContainsKey(entity.Address)) return false;

            float dist3D = Vector3.Distance(Core.LocalMainCamera, entity.Head);
            if (dist3D > Config.EnemyPullMaxDistance) return false;

            return true;
        }

        private static Entity FindBestTarget()
        {
            Entity bestTarget = null;
            float closestDist = float.MaxValue;
            var screenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);

            foreach (var entity in Core.Entities.Values)
            {
                if (!entity.IsKnown || entity.IsDead) continue;
                if (Config.IgnoreKnocked && entity.IsKnocked) continue;

                var head2D = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                if (head2D.X < 1 || head2D.Y < 1) continue;

                float dist3D = Vector3.Distance(Core.LocalMainCamera, entity.Head);
                if (dist3D > Config.EnemyPullMaxDistance) continue;

                float crosshairDist = Vector2.Distance(screenCenter, head2D);
                if (crosshairDist > EnemyPullHelpers.GetPullFovRadius()) continue;

                // HackerDetect priority: if marked hacker is valid, always pick it first
                if (Config.HackerDetect && ESP.selectedHackerAddress != 0 && entity.Address == ESP.selectedHackerAddress)
                    return entity;

                if (crosshairDist < closestDist)
                {
                    closestDist = crosshairDist;
                    bestTarget = entity;
                }
            }
            return bestTarget;
        }

        private static Vector3 ComputeLockPosition(Vector3 currentPos, Vector3 originalPos)
        {
            Vector3 camPos = Core.LocalMainCamera;

            Vector3 fireDirection = new Vector3(
                Core.CameraMatrix.M13,
                Core.CameraMatrix.M23,
                Core.CameraMatrix.M33
            );
            if (fireDirection.LengthSquared() < 1e-8f)
                return currentPos;
            fireDirection = Vector3.Normalize(fireDirection);

            Vector3 toEnemy = currentPos - camPos;
            float projLength = Vector3.Dot(toEnemy, fireDirection);
            if (projLength < MinProjDistance)
                projLength = MinProjDistance;

            Vector3 targetPos = camPos + fireDirection * projLength;
            targetPos.Y += PullYOffset;

            Vector3 delta = targetPos - originalPos;
            Vector3 forwardXZ = new Vector3(fireDirection.X, 0f, fireDirection.Z);
            if (forwardXZ.LengthSquared() > 1e-8f)
            {
                forwardXZ = Vector3.Normalize(forwardXZ);
                Vector3 sideXZ = new Vector3(-forwardXZ.Z, 0f, forwardXZ.X);

                float forward = Vector3.Dot(delta, forwardXZ);
                float side = Vector3.Dot(delta, sideXZ);

                if (Math.Abs(forward) > MaxForward)
                    forward = Math.Sign(forward) * MaxForward;
                if (Math.Abs(side) > MaxSide)
                    side = Math.Sign(side) * MaxSide;

                Vector3 clamped = forwardXZ * forward + sideXZ * side;
                targetPos.X = originalPos.X + clamped.X;
                targetPos.Z = originalPos.Z + clamped.Z;
            }
            else
            {
                float horizontalDist = new Vector2(delta.X, delta.Z).Length();
                if (horizontalDist > MaxSide)
                {
                    Vector2 clamped2D = Vector2.Normalize(new Vector2(delta.X, delta.Z)) * MaxSide;
                    targetPos.X = originalPos.X + clamped2D.X;
                    targetPos.Z = originalPos.Z + clamped2D.Y;
                }
            }

            if (targetPos.Y < originalPos.Y)
                targetPos.Y = originalPos.Y;
            else if (targetPos.Y - originalPos.Y > MaxUp)
                targetPos.Y = originalPos.Y + MaxUp;

            return targetPos;
        }

        private static void ApplyStableLock(Entity entity)
        {
            try
            {
                if (!InternalMemory.Read(entity.Address + (uint)Bones.Root, out uint rootBonePtr)) return;
                if (!InternalMemory.Read(rootBonePtr + 0x8, out uint transformValue)) return;
                if (!InternalMemory.Read(transformValue + 0x8, out uint transformObjPtr)) return;
                if (!InternalMemory.Read(transformObjPtr + 0x20, out uint matrixPtr)) return;

                if (!Transform.GetNodePosition(rootBonePtr, out var currentPos)) return;

                if (!originalPositions.ContainsKey(entity.Address))
                    originalPositions[entity.Address] = currentPos;

                Vector3 originalPos = originalPositions[entity.Address];
                uint eid = entity.Address;

                if (!_hasLockedPos || _lockedEntityId != eid)
                {
                    _lockedPullPos = ComputeLockPosition(currentPos, originalPos);
                    _lockedEntityId = eid;
                    _hasLockedPos = true;
                }

                ulong writeAddr = (ulong)(matrixPtr + 0x60);
                for (int i = 0; i < LockWritesPerTick; i++)
                    InternalMemory.Write(writeAddr, _lockedPullPos);
            }
            catch { }
        }

        private static void ClearLockState()
        {
            _hasLockedPos = false;
            _lockedEntityId = 0;
            _lockedPullPos = Vector3.Zero;
        }

        private static void RestoreAllPositions()
        {
            foreach (var entry in originalPositions)
            {
                try
                {
                    if (!InternalMemory.Read(entry.Key + (uint)Bones.Root, out uint rootBonePtr)) continue;
                    if (!InternalMemory.Read(rootBonePtr + 0x8, out uint transformValue)) continue;
                    if (!InternalMemory.Read(transformValue + 0x8, out uint transformObjPtr)) continue;
                    if (!InternalMemory.Read(transformObjPtr + 0x20, out uint matrixPtr)) continue;

                    InternalMemory.Write((ulong)(matrixPtr + 0x60), entry.Value);
                }
                catch { }
            }
            originalPositions.Clear();
            ClearLockState();
        }
    }
}