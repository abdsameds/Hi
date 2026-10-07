using AotForms;
using System;
using System.Numerics;
using System.Threading;
using System.Windows.Forms;

namespace Client
{
    internal class KCBRUTALSILENT
    {

        internal static void Work()
        {
            while (true)
            {
                if (!Config.kcbrutasilnetaim)
                {
                    Thread.SpinWait(1); // controled threadspinwait added
                    continue;

                }
                Entity target = null;
                float distance = float.MaxValue;

                if (Core.Width == -1 || Core.Height == -1 || !Core.HaveMatrix)
                {
                    Thread.SpinWait(1);
                    continue;
                }
                var screenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);
                foreach (var entity in Core.Entities.Values)
                {


                    if (!entity.IsKnown || entity.IsDead || (Config.IgnoreKnocked && entity.IsKnocked)) continue;
                    var head2D = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                    Vector2 headScreenPos2 = new Vector2(head2D.X, head2D.Y);
                    var playerDistance = Vector2.Distance(screenCenter, headScreenPos2);
                    if (playerDistance < distance)
                    {
                        distance = playerDistance;
                        target = entity;
                    }
                    float dist = Vector3.Distance(Core.LocalMainCamera, entity.Root);// delay fixed in aim silent
                }
                if (target != null)
                {
                    var isShooting = InternalMemory.Read<bool>(Core.LocalPlayer + Offsets.sAim1, out var readSuccess);
                    if (readSuccess && isShooting)
                    {
                        var weaponData = InternalMemory.Read<uint>(Core.LocalPlayer + Offsets.sAim2, out var weaponSuccess);
                        if (weaponSuccess != 0)
                        {
                            // instant bullet connect -- vector using
                            Vector3 adjustedTauko = target.Head + new Vector3(0, 0.1f, 0);
                            InternalMemory.Read<Vector3>(weaponSuccess + Offsets.sAim3, out var startPos);
                            Vector3 aimPosition = adjustedTauko - startPos;
                            InternalMemory.Write<Vector3>(weaponSuccess + Offsets.sAim4, aimPosition);
                        }
                    }
                }
                Thread.SpinWait(1);


            }

        }
        private static Entity FindBestTarget()
        {
            Entity bestTarget = null;

            var screenCenter = new Vector2(Core.Width / 2f, Core.Height / 2f);

            foreach (var entity in Core.Entities.Values)
            {
                if (entity.IsDead) continue;
                if (entity.IsKnocked) continue;

                var head2D = W2S.WorldToScreen(Core.CameraMatrix, entity.Head, Core.Width, Core.Height);
                if (head2D.X < 1 || head2D.Y < 1) continue;

                float playerDistance = Vector3.Distance(Core.LocalMainCamera, entity.Head);
                if (playerDistance > 10) continue;

                var crosshairDistance = Vector2.Distance(screenCenter, head2D);


                bestTarget = entity;

            }

            return bestTarget;
        }

    
    }
}
