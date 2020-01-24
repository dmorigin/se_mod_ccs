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
        public class LCDMessages
        {
            StringBuilder lcdMsgBuffer_ = new StringBuilder();
            List<IMyTextSurface> surfaces_ = new List<IMyTextSurface>();

            public LCDMessages(Program app)
            {
                App = app;
            }

            Program App
            {
                get;
                set;
            }

            public void Initalize()
            {
                surfaces_.Clear();
                surfaces_.Add(App.Me.GetSurface(0));

                // ToDo: Implement a more customizeable version
                App.GridTerminalSystem.GetBlocksOfType<IMyTextSurfaceProvider>(null, block =>
                {
                    if (block.CustomName.Contains(App.lcdTag_))
                    {
                        IMyTextSurfaceProvider provider = block as IMyTextSurfaceProvider;
                        surfaces_.Add(provider.GetSurface(0));
                    }

                    return false;
                });
            }

            public void Flush()
            {
                // do nothing if no panel is active
                if (surfaces_.Count == 0)
                    return;

                lcdMsgBuffer_.AppendLine("    FLIGHT ASSIST V" + VERSION);
                lcdMsgBuffer_.AppendLine("----------------------------------------");

                if (App.disabled_ == false)
                {
                    // Status
                    if (App.landingInProgress_)
                        lcdMsgBuffer_.AppendLine("  MODE: In landing mode");
                    else if (!App.naturalGravity_)
                        lcdMsgBuffer_.AppendLine("  Mode: No natural Gravity");
                    else
                        lcdMsgBuffer_.AppendLine("  MODE: In flight mode");

                    if (App.setSpeed_ > 0)
                        lcdMsgBuffer_.AppendLine($"  SET SPEED: {App.setSpeed_}m/s");
                    else
                        lcdMsgBuffer_.AppendLine("");

                    // velocity
                    string velocityString = $" X:{App.worldSpeedForward_.ToString("+000;\u2013000")}";
                    velocityString += $" Y:{App.worldSpeedRight_.ToString("+000;\u2013000")}";
                    velocityString += $" Z:{App.worldSpeedUp_.ToString("+000;\u2013000")}";
                    lcdMsgBuffer_.AppendLine($"\n Velocity (m/s)+\n{velocityString}");

                    // orientation
                    lcdMsgBuffer_.AppendLine("\n Orientation");
                    lcdMsgBuffer_.AppendLine($" Pitch: {(90 - App.pitch_).ToString("+00;\u201300")}° | Roll: {((90 - App.roll_) * -1).ToString("+00;\u201300")}°");
                }
                else
                    lcdMsgBuffer_.AppendLine("<= SYSTEM DISABLED =>");

                // print to lcd panel
                foreach (var surface in surfaces_)
                {
                    surface.ContentType = ContentType.TEXT_AND_IMAGE;
                    surface.Alignment = TextAlignment.LEFT;
                    surface.TextPadding = 0f;
                    surface.Font = "monospace";
                    surface.FontSize = 0.8f;
                    surface.WriteText(lcdMsgBuffer_.ToString());
                }

                lcdMsgBuffer_.Clear();
            }
        }
    }
}
