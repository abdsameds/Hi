using AotForms;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;

namespace Client
{
    internal class EnemyPullRaycast
    {
        private static readonly Dictionary<uint, Vector3> _originalPositions = new();
        private static bool _wasFiring;

        internal static void Work()
        {
            while (Config.EnemyPullRaycast)
            {
                if (_originalPositions.Count > 0)
                    RestoreAllPositions();
                _wasFiring = false;

                if (Core.Width == -1 || Core.Height == -1 || !Core.HaveMatrix)
                {
                    Thread.Sleep(1);
                    continue;
                }

                InternalMemory.Read<bool>(Core.LocalPlayer + Offsets.sAim1, out bool isShooting);

                if (isShooting)
                {
                    Entity? target = FindNearest();
                    if (target != null)
                        PullToOpenSpace(target);
                }
                else
                {
                    if (_wasFiring && _originalPositions.Count > 0)
                        RestoreAllPositions();
                }

                _wasFiring = isShooting;
                Thread.Sleep(1);
            }
        }

        private static Entity? FindNearest()
        {
            Entity? best = null;
            float closest = float.MaxValue;
            Vector3 camPos = Core.LocalMainCamera;

            foreach (var entity in Core.Entities.Values)
            {
                if (!entity.IsKnown || entity.IsDead) continue;
                if (Config.IgnoreKnocked && entity.IsKnocked) continue;

                float d = Vector3.Distance(camPos, entity.Head);
                if (d > (Config.EnemyPullMaxDistance > 0 ? Config.EnemyPullMaxDistance : 300f) || d <= 2f)
                    continue;

                if (d < closest)
                {
                    closest = d;
                    best = entity;
                }
            }
            return best;
        }

        private static void PullToOpenSpace(Entity entity)
        {
            try
            {
                if (!InternalMemory.Read(entity.Address + (uint)Bones.Root, out uint rootBonePtr)) return;
                if (!InternalMemory.Read(rootBonePtr + 0x8, out uint transformValue)) return;
                if (!InternalMemory.Read(transformValue + 0x8, out uint transformObjPtr)) return;
                if (!InternalMemory.Read(transformObjPtr + 0x20, out uint matrixPtr)) return;

                if (!Transform.GetNodePosition(rootBonePtr, out var currentPos)) return;

                if (!_originalPositions.ContainsKey(entity.Address))
                    _originalPositions[entity.Address] = currentPos;

                Vector3 camPos = Core.LocalMainCamera;

                // Direction FROM enemy TOWARD player = open space
                Vector3 toPlayer = camPos - currentPos;
                float dist = toPlayer.Length();
                if (dist < 1f) return;
                toPlayer /= dist;

                // Pull enemy forward toward player (out of cover into open)
                float pullForward = MathF.Min(dist * 0.5f, 8f);
                if (pullForward < 2f) pullForward = 2f;

                // Slight side offset (alternates per entity address)
                float sideOffset = ((entity.Address & 1) == 0) ? 2.5f : -2.5f;
                Vector3 right = Vector3.Normalize(Vector3.Cross(toPlayer, new Vector3(0, 1, 0)));
                if (right.LengthSquared() < 0.5f) right = new Vector3(1, 0, 0);

                Vector3 targetPos = currentPos + toPlayer * pullForward + right * sideOffset;
                targetPos.Y = currentPos.Y;

                InternalMemory.Write(matrixPtr + 0x60, targetPos);
            }
            catch { }
        }

        private static void RestoreAllPositions()
        {
            foreach (var entry in _originalPositions)
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
            _originalPositions.Clear();
        }
    }
}
