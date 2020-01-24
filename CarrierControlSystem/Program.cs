using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System;
using VRage;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game;
using VRageMath;

namespace IngameScript
{
    partial class Program : MyGridProgram
    {
        const string VERSION = "0.2-beta";

        // Constances
        const double HalfPi = Math.PI / 2;
        const double RadToDeg = 180 / Math.PI;
        const double DegToRad = Math.PI / 180;

        // processing parameter
        List<IMyLandingGear> gears_;
        GyroController gyroController_;
        IMyShipController cockpit_;
        Statistics statistics_ = new Statistics();
        LCDMessages messages_ = null;
        double pitch_ = 0.0;
        double roll_ = 0.0;
        double worldSpeedForward_ = 0.0;
        double worldSpeedRight_ = 0.0;
        double worldSpeedUp_ = 0.0;
        double desiredPitch_ = 0.0;
        double desiredRoll_ = 0.0;

        // disable system
        bool disabled_ = true;
        bool landingInProgress_ = false;
        bool naturalGravity_ = false;

        // configuration parameter
        double maxPitch_ = 0.0;
        double maxRoll_ = 0.0;
        int gyroResponsiveness_ = 8;
        double setSpeed_ = 0.01;
        string lcdTag_ = "[FA]";
        UpdateFrequency updateFrequency_ = UpdateFrequency.Update10;


        void UpdateOrientationParameters()
        {
            Vector3D linearVelocity = Vector3D.Normalize(cockpit_.GetShipVelocities().LinearVelocity);
            Vector3D gravity = -Vector3D.Normalize(cockpit_.GetNaturalGravity());

            pitch_ = Math.Acos(Vector3D.Dot(cockpit_.WorldMatrix.Forward, gravity)) * RadToDeg;
            roll_ = Math.Acos(Vector3D.Dot(cockpit_.WorldMatrix.Right, gravity)) * RadToDeg;

            worldSpeedForward_ = Vector3D.Dot(linearVelocity, Vector3D.Cross(gravity, cockpit_.WorldMatrix.Right)) * cockpit_.GetShipSpeed();
            worldSpeedRight_ = Vector3D.Dot(linearVelocity, Vector3D.Cross(gravity, cockpit_.WorldMatrix.Forward)) * cockpit_.GetShipSpeed();
            worldSpeedUp_ = Vector3D.Dot(linearVelocity, gravity) * cockpit_.GetShipSpeed();
        }

        void ExecuteManeuver()
        {
            Matrix cockpitOrientation;
            cockpit_.Orientation.GetMatrix(out cockpitOrientation);
            var quatPitch = Quaternion.CreateFromAxisAngle(cockpitOrientation.Left, (float)(desiredPitch_ * DegToRad));
            var quatRoll = Quaternion.CreateFromAxisAngle(cockpitOrientation.Backward, (float)(desiredRoll_ * DegToRad));
            var reference = Vector3D.Transform(cockpitOrientation.Down, quatPitch * quatRoll);
            gyroController_.SetTargetOrientation(reference, cockpit_.GetNaturalGravity());
        }

        bool initialize()
        {
            // setup cockpit
            List<IMyShipController> cockpits = new List<IMyShipController>();
            GridTerminalSystem.GetBlocksOfType<IMyShipController>(cockpits, cockpit =>
            {
                if (cockpit.IsMainCockpit || cockpit.CanControlShip)
                    return true;
                return false;
            });

            if (cockpits.Count > 0)
            {
                // use first cockpit
                cockpit_ = cockpits[0];

                // setup gyros
                List<IMyGyro> gyroscopes = new List<IMyGyro>();
                GridTerminalSystem.GetBlocksOfType<IMyGyro>(gyroscopes);
                gyroController_ = new GyroController(gyroscopes, cockpit_);
            }

            // search landing gears
            gears_ = new List<IMyLandingGear>();
            GridTerminalSystem.GetBlocksOfType<IMyLandingGear>(gears_);

            messages_ = new LCDMessages(this);
            return true;
        }


        #region SE Implementation
        public Program()
        {
            initialize();

            // Update frequency
            Runtime.UpdateFrequency = updateFrequency_;
            statistics_.setSensitivity(updateFrequency_);
        }

        public void Save()
        {
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // execute user interaction
            if ((updateSource & UpdateType.Trigger) != 0 || (updateSource & UpdateType.Terminal) != 0)
            {
                if (argument.Contains("enable"))
                    disabled_ = false;
                else if (argument.Contains("disable"))
                {
                    gyroController_.ResetGyro();
                    disabled_ = true;
                }
            }

            // check landing
            landingInProgress_ = false;
            foreach (var gear in gears_)
            {
                if (gear.LockMode == LandingGearMode.Locked || gear.LockMode == LandingGearMode.ReadyToLock)
                {
                    landingInProgress_ = true;
                    break;
                }
            }

            // run normal iteration
            if ((updateSource & UpdateType.Update10) != 0)
            {
                // check natural gravity
                if (cockpit_.GetNaturalGravity().Length() == 0.0)
                {
                    naturalGravity_ = false;
                    gyroController_.SetGyroOverride(false);
                    return;
                }
                else
                    naturalGravity_ = true;


                if (landingInProgress_ == false && disabled_ == false)
                {
                    // update all orientation parameters
                    UpdateOrientationParameters();

                    // update gyroController
                    gyroController_.Tick();


                    if (cockpit_.MoveIndicator.Length() > 0.0 || cockpit_.RotationIndicator.Length() > 0.0)
                    {
                        desiredPitch_ = -(pitch_ - 90);
                        desiredRoll_ = (roll_ - 90);
                        gyroController_.SetGyroOverride(false);
                    }
                    else
                    {
                        gyroController_.SetGyroOverride(true);
                        desiredPitch_ = Math.Atan((worldSpeedForward_ - setSpeed_) / gyroResponsiveness_) / HalfPi * maxPitch_;
                        desiredRoll_ = Math.Atan(worldSpeedRight_ / gyroResponsiveness_) / HalfPi * maxRoll_;
                    }


                    if (gyroController_.GyroOverride)
                        ExecuteManeuver();
                }
            }

            // generate lcd output and print them on
            messages_.Flush();
            statistics_.tick(this);
        }
        #endregion // SE Implementation
    }
}
