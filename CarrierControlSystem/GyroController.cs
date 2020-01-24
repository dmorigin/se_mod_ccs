using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRage;
using VRageMath;

namespace IngameScript
{
    partial class Program
    {
        public class GyroController
        {
            const double minGyroRpmScale_ = 0.001;
            const double gyroVelocityScale_ = 0.02;

            readonly List<IMyGyro> gyros_;
            readonly IMyShipController cockpit_;
            bool gyroOverride_;
            Vector3D reference_;
            Vector3D target_;
            double angle_;

            public bool GyroOverride => gyroOverride_;

            public GyroController(List<IMyGyro> gyros, IMyShipController cockpit)
            {
                gyros_ = gyros;
                cockpit_ = cockpit;
            }

            public void Tick()
            {
                UpdateGyroRpm();
            }

            public void SetGyroOverride(bool state)
            {
                gyroOverride_ = state;
                for (int i = 0; i < gyros_.Count; i++)
                    gyros_[i].GyroOverride = gyroOverride_;
            }

            public void ResetGyro()
            {
                foreach (var gyro in gyros_)
                {
                    gyro.GyroOverride = false;
                    gyro.Yaw = 0f;
                    gyro.Pitch = 0f;
                    gyro.Roll = 0f;
                }
            }

            public void SetTargetOrientation(Vector3D setReference, Vector3D setTarget)
            {
                reference_ = setReference;
                target_ = setTarget;
                UpdateGyroRpm();
            }

            void UpdateGyroRpm()
            {
                if (!gyroOverride_)
                    return;

                foreach (var gyro in gyros_)
                {
                    Matrix localOrientation;
                    gyro.Orientation.GetMatrix(out localOrientation);
                    var localReference = Vector3D.Transform(reference_, MatrixD.Transpose(localOrientation));
                    var localTarget = Vector3D.Transform(target_, MatrixD.Transpose(gyro.WorldMatrix.GetOrientation()));

                    var axis = Vector3D.Cross(localReference, localTarget);
                    angle_ = axis.Length();
                    angle_ = Math.Atan2(angle_, Math.Sqrt(Math.Max(0.0, 1.0 - angle_ * angle_)));
                    if (Vector3D.Dot(localReference, localTarget) < 0)
                        angle_ = Math.PI;
                    axis.Normalize();
                    axis *= Math.Max(minGyroRpmScale_, gyro.GetMaximum<float>("Roll") * (angle_ / Math.PI) * gyroVelocityScale_);

                    gyro.Pitch = (float)-axis.X;
                    gyro.Yaw = (float)-axis.Y;
                    gyro.Roll = (float)-axis.Z;
                }
            }
        }
    }
}
