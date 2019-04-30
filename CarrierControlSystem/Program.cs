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
    /*!
     * This script is a small version of the Flight Assist
     * You find this script here: https://steamcommunity.com/sharedfiles/filedetails/?id=557293234
     */
    partial class Program : MyGridProgram
    {
        /*!
         * Constances
         */
        public const double halfPi = Math.PI / 2;
        public const double radToDeg = 180 / Math.PI;
        public const double degToRad = Math.PI / 180;


        /*!
         * Class: GyroController
         * 
         * Description:
         * Over this class it is possible to controll all gyroscopes in one way.
         */
        public class GyroController
        {
            const double minGyroRpmScale = 0.001;
            const double gyroVelocityScale = 0.02;

            private readonly List<IMyGyro> gyros;
            private readonly IMyShipController cockpit;
            public bool gyroOverride;
            private Vector3D reference;
            private Vector3D target;
            public double angle;

            public GyroController(List<IMyGyro> gyros, IMyShipController cockpit)
            {
                this.gyros = gyros;
                this.cockpit = cockpit;
            }

            public void Tick()
            {
                UpdateGyroRpm();
            }

            public void SetGyroOverride(bool state)
            {
                gyroOverride = state;
                for (int i = 0; i < gyros.Count; i++)
                    gyros[i].GyroOverride = gyroOverride;
            }

            public void resetGyro()
            {
                foreach (var gyro in gyros)
                {
                    gyro.GyroOverride = false;
                    gyro.Yaw = 0f;
                    gyro.Pitch = 0f;
                    gyro.Roll = 0f;
                }
            }

            public void SetTargetOrientation(Vector3D setReference, Vector3D setTarget)
            {
                reference = setReference;
                target = setTarget;
                UpdateGyroRpm();
            }

            private void UpdateGyroRpm()
            {
                if (!gyroOverride) return;

                foreach (var gyro in gyros)
                {
                    Matrix localOrientation;
                    gyro.Orientation.GetMatrix(out localOrientation);
                    var localReference = Vector3D.Transform(reference, MatrixD.Transpose(localOrientation));
                    var localTarget = Vector3D.Transform(target, MatrixD.Transpose(gyro.WorldMatrix.GetOrientation()));

                    var axis = Vector3D.Cross(localReference, localTarget);
                    angle = axis.Length();
                    angle = Math.Atan2(angle, Math.Sqrt(Math.Max(0.0, 1.0 - angle * angle)));
                    if (Vector3D.Dot(localReference, localTarget) < 0)
                        angle = Math.PI;
                    axis.Normalize();
                    axis *= Math.Max(minGyroRpmScale, gyro.GetMaximum<float>("Roll") * (angle / Math.PI) * gyroVelocityScale);

                    gyro.Pitch = (float)-axis.X;
                    gyro.Yaw = (float)-axis.Y;
                    gyro.Roll = (float)-axis.Z;
                }
            }
        }


        // processing parameter
        private List<IMyLandingGear> gears_;
        private GyroController gyroController_;
        private IMyShipController cockpit_;
        private List<IMyTextPanel> textPanels_;
        private double pitch_ = 0.0;
        private double roll_ = 0.0;
        private double worldSpeedForward_ = 0.0;
        private double worldSpeedRight_ = 0.0;
        private double worldSpeedUp_ = 0.0;
        private double desiredPitch_ = 0.0;
        private double desiredRoll_ = 0.0;

        // disable system
        private bool disabled = true;
        private bool landingInProgress_ = false;

        // output buffer
        private string outputLCDBuffer_ = "";

        // configuration parameter
        private double maxPitch_ = 0.0;
        private double maxRoll_ = 0.0;
        private int gyroResponsiveness_ = 8;
        private double setSpeed_ = 0.01;
        private string lcdTag_ = "[FA]";
        private string version_ = "0.1";


        public Program()
        {
            // find output lcd panels
            textPanels_ = new List<IMyTextPanel>();
            GridTerminalSystem.GetBlocksOfType<IMyTextPanel>(textPanels_, block =>
            {
                if (block.CustomName.Contains(lcdTag_))
                    return true;
                return false;
            });

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

            // Update frequency
            Runtime.UpdateFrequency = UpdateFrequency.Update10;
        }

        public void Save()
        {
        }


        private void UpdateOrientationParameters()
        {
            Vector3D linearVelocity = Vector3D.Normalize(cockpit_.GetShipVelocities().LinearVelocity);
            Vector3D gravity = -Vector3D.Normalize(cockpit_.GetNaturalGravity());

            pitch_ = Math.Acos(Vector3D.Dot(cockpit_.WorldMatrix.Forward, gravity)) * radToDeg;
            roll_ = Math.Acos(Vector3D.Dot(cockpit_.WorldMatrix.Right, gravity)) * radToDeg;

            worldSpeedForward_ = Vector3D.Dot(linearVelocity, Vector3D.Cross(gravity, cockpit_.WorldMatrix.Right)) * cockpit_.GetShipSpeed();
            worldSpeedRight_ = Vector3D.Dot(linearVelocity, Vector3D.Cross(gravity, cockpit_.WorldMatrix.Forward)) * cockpit_.GetShipSpeed();
            worldSpeedUp_ = Vector3D.Dot(linearVelocity, gravity) * cockpit_.GetShipSpeed();
        }


        private void PrintLCDLine(string line)
        {
            outputLCDBuffer_ += line + "\n";
        }


        private void ExecuteManeuver()
        {
            Matrix cockpitOrientation;
            cockpit_.Orientation.GetMatrix(out cockpitOrientation);
            var quatPitch = Quaternion.CreateFromAxisAngle(cockpitOrientation.Left, (float)(desiredPitch_ * degToRad));
            var quatRoll = Quaternion.CreateFromAxisAngle(cockpitOrientation.Backward, (float)(desiredRoll_ * degToRad));
            var reference = Vector3D.Transform(cockpitOrientation.Down, quatPitch * quatRoll);
            gyroController_.SetTargetOrientation(reference, cockpit_.GetNaturalGravity());
        }


        private void PrintLCDString()
        {
            // do nothing if no panel is active
            if (textPanels_.Count == 0)
                return;

            PrintLCDLine("    FLIGHT ASSIST V" + version_);
            PrintLCDLine("----------------------------------------\n");

            if (disabled == false)
            {
                // Status
                if (landingInProgress_)
                    PrintLCDLine("  MODE: In landing mode");
                else
                    PrintLCDLine("  MODE: In flight mode");

                if (setSpeed_ > 0)
                    PrintLCDLine("  SET SPEED: " + setSpeed_ + "m/s");
                else
                    PrintLCDLine("");

                // velocity
                string velocityString = " X:" + worldSpeedForward_.ToString("+000;\u2013000");
                velocityString += " Y:" + worldSpeedRight_.ToString("+000;\u2013000");
                velocityString += " Z:" + worldSpeedUp_.ToString("+000;\u2013000");
                PrintLCDLine("\n Velocity (m/s)+\n" + velocityString);

                // orientation
                PrintLCDLine("\n Orientation");
                PrintLCDLine(" Pitch: " + (90 - pitch_).ToString("+00;\u201300") + "° | Roll: " + ((90 - roll_) * -1).ToString("+00;\u201300") + "°");
            }
            else
            {
                PrintLCDLine("<= SYSTEM DISABLED =>");
            }

            // print to lcd panel
            foreach (var panel in textPanels_)
            {
                panel.ShowPublicTextOnScreen();
                panel.WritePublicText(outputLCDBuffer_, false);
            }

            outputLCDBuffer_ = "";
        }


        public void Main(string argument, UpdateType updateSource)
        {
            // execute user interaction
            if ((updateSource & UpdateType.Trigger) != 0 || (updateSource & UpdateType.Terminal) != 0)
            {
                if (argument.Contains("enable"))
                    disabled = false;
                else if (argument.Contains("disable"))
                {
                    gyroController_.resetGyro();
                    disabled = true;
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
                    Echo("No natural gravity found. Execute disabled!");
                    gyroController_.SetGyroOverride(false);
                    return;
                }

                if (landingInProgress_ == false && disabled == false)
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
                        desiredPitch_ = Math.Atan((worldSpeedForward_ - setSpeed_) / gyroResponsiveness_) / halfPi * maxPitch_;
                        desiredRoll_ = Math.Atan(worldSpeedRight_ / gyroResponsiveness_) / halfPi * maxRoll_;
                    }


                    if (gyroController_.gyroOverride)
                        ExecuteManeuver();
                }
            }

            // generate lcd output and print them on
            PrintLCDString();
        }
    }
}
