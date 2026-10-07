using System;
using System.Numerics;
using System.Threading;

namespace AotForms
{
    internal static class KCBRUTALSILENTBODY
    {
        static Entity? PickTarget(Vector2 screenCenter, Matrix4x4 view, int drawW, int drawH, Vector3 camWorld)
        {
            Entity? best = null;
            float bestScore = float.MaxValue;
            float fovLimit = MathF.Max(8f, Config.AimFov * Config.BrutalSilentFovScale);

            foreach (var entity in Core.Entities.Values)
            {
                if (entity == null || !entity.IsKnown || entity.IsDead)
                    continue;
                if (Config.IgnoreKnocked && entity.IsKnocked)
                    continue;

                if (Config.HackerDetect && ESP.selectedHackerAddress != 0 && entity.Address == ESP.selectedHackerAddress)
                    return entity;

                Vector3 aimWorld = entity.Hip;
                if (aimWorld == Vector3.Zero)
                {
                    if (entity.Neck != Vector3.Zero) aimWorld = entity.Neck;
                    else if (entity.Groin != Vector3.Zero) aimWorld = entity.Groin;
                    else continue;
                }

                float worldDist = Vector3.Distance(camWorld, aimWorld);
                if (worldDist > Config.AimBotMaxDistance)
                    continue;

                var head2D = W2S.WorldToScreen(view, aimWorld, drawW, drawH);
                if (head2D.X == -1f && head2D.Y == -1f)
                    continue;

                float crossDist = Vector2.Distance(screenCenter, head2D);
                if (crossDist > fovLimit)
                    continue;

                // Prefer crosshair proximity, lightly weight closer world distance.
                float score = crossDist + worldDist * 0.015f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = entity;
                }
            }

            return best;
        }

        static bool TryWriteSilentAim(Entity target, Vector3 camWorld)
        {
            if (Core.LocalPlayer == 0)
                return false;

            if (!InternalMemory.Read<bool>(Core.LocalPlayer + Offsets.sAim1, out bool isShooting) || !isShooting)
                return false;

            if (!InternalMemory.Read<uint>(Core.LocalPlayer + Offsets.sAim2, out uint weaponData) || weaponData == 0)
                return false;

            if (!InternalMemory.Read<Vector3>(weaponData + Offsets.sAim3, out Vector3 startPos))
                return false;

            // --- CHANGED: Use Hip instead of Head ---
            Vector3 aimPoint = target.Hip;
            if (aimPoint == Vector3.Zero)
            {
                if (target.Groin != Vector3.Zero) aimPoint = target.Groin;
                else if (target.Neck != Vector3.Zero) aimPoint = target.Neck;
                else return false;
            }

            // Optional: remove or reduce head bias for body aim
            float bias = Math.Clamp(Config.BrutalSilentHeadBias, 0f, 0.25f);
            aimPoint += new Vector3(0f, bias, 0f);

            Vector3 dir = aimPoint - startPos;
            if (dir.LengthSquared() < 0.0001f)
                return false;

            InternalMemory.Write<Vector3>(weaponData + Offsets.sAim4, dir);
            InternalMemory.Write<Vector3>(weaponData + Offsets.sAim4, dir);
            return true;
        }
        internal static void Work()
        {
            while (Config.kcbrutasilnetaimBODY)
            {
                if (!Core.CanDrawEspWorld || Core.EspDrawWidth < 8 || Core.EspDrawHeight < 8)
                {
                    Thread.Sleep(2);
                    continue;
                }

                Core.GetEspDrawingSnapshot(out var view, out var camWorld);
                var screenCenter = new Vector2(Core.EspDrawWidth / 2f, Core.EspDrawHeight / 2f);
                Entity? target = PickTarget(screenCenter, view, Core.EspDrawWidth, Core.EspDrawHeight, camWorld);

                if (target != null)
                    TryWriteSilentAim(target, camWorld);

                Thread.Sleep(0);
            }
        }
    }
}
