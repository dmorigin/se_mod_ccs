﻿using Sandbox.Game.EntityComponents;
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
        public class Statistics
        {
            char[] runSymbol_ = { '-', '\\', '|', '/' };
            int runSymbolIndex_ = 0;
            char getRunSymbol()
            {
                char sym = runSymbol_[runSymbolIndex_++];
                runSymbolIndex_ %= 4;
                return sym;
            }

            double sensitivity_ = 0.01;
            public void setSensitivity(UpdateFrequency uf)
            {
                switch (uf)
                {
                    case UpdateFrequency.Update1:
                        sensitivity_ = 1;
                        return;
                    case UpdateFrequency.Update10:
                        sensitivity_ = 0.1;
                        return;
                    case UpdateFrequency.Update100:
                        sensitivity_ = 0.01;
                        return;
                }
            }

            string exception_ = "";
            public void registerException(Exception exp)
            {
                exception_ = exp.ToString();
            }

            TimeSpan nextUpdate_ = new TimeSpan(0);
            TimeSpan updateInterval_ = TimeSpan.FromSeconds(1.0);
            TimeSpan ticks_ = new TimeSpan(0);

            StringBuilder sb_ = new StringBuilder();

            long instructionCountLastUpdate_ = 0;
            long ticksSinceLastUpdate_ = 0;
            double timeSinceLastUpdate_ = 0.0;

            public void tick(Program app)
            {
                ticks_ += app.Runtime.TimeSinceLastRun;

                // update
                instructionCountLastUpdate_ += app.Runtime.CurrentInstructionCount;
                timeSinceLastUpdate_ += app.Runtime.LastRunTimeMs * sensitivity_;
                ticksSinceLastUpdate_++;

                if (nextUpdate_ <= ticks_)
                {
                    string mode = app.landingInProgress_ ? "Landing" : (app.naturalGravity_ ? "Flight" : "No Gravity");

                    // print statistic
                    sb_.Clear();
                    sb_.AppendLine($"Carrier Control System ({Program.VERSION})\n=============================");
                    sb_.AppendLine($"Running: {getRunSymbol()}");
                    sb_.AppendLine($"Time: {ticks_}");
                    sb_.AppendLine($"Ticks: {ticksSinceLastUpdate_}");
                    sb_.AppendLine($"Avg Time/tick: {(timeSinceLastUpdate_ / ticksSinceLastUpdate_).ToString("#0.0#####")}ms");
                    sb_.AppendLine($"Avg Inst/tick: {(instructionCountLastUpdate_ / (double)ticksSinceLastUpdate_).ToString("#0.00")}/{app.Runtime.MaxInstructionCount}");
                    sb_.AppendLine($"Enabled: {!app.disabled_}");
                    sb_.AppendLine($"Mode: {mode}");

                    if (exception_ != "")
                        sb_.Append($"\nException:\n{exception_}\n");

                    app.Echo(sb_.ToString());

                    nextUpdate_ = ticks_ + updateInterval_;
                    instructionCountLastUpdate_ = 0;
                    ticksSinceLastUpdate_ = 0;
                    timeSinceLastUpdate_ = 0.0;
                }
            }
        }
    }
}
