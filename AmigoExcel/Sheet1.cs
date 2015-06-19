using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Xml.Linq;
using Microsoft.Office.Tools.Excel;
using Microsoft.VisualStudio.Tools.Applications.Runtime;
using Excel = Microsoft.Office.Interop.Excel;
using Office = Microsoft.Office.Core;

using QuantConnect.Configuration;
using QuantConnect.Lean.Engine;
using QuantConnect.Lean.Engine.Results;
using QuantConnect.Packets;
using QuantConnect.Util;
using Timer = System.Windows.Forms.Timer;
using System.Threading;

namespace AmigoExcel
{
    public partial class Sheet1
    {
        private Engine _engine;

        //Form Business Logic:
        private Timer _polling;
        private IResultHandler _resultsHandler;
        private static Thread _leanEngineThread;
        private void Sheet1_Startup(object sender, System.EventArgs e)
        {
            this.Cells[3, 3] = "Result: ";
        }

        private void Sheet1_Shutdown(object sender, System.EventArgs e)
        {
        }

        #region VSTO Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InternalStartup()
        {
            this.button_Start_BackTest.Click += new System.EventHandler(this.button_Start_BackTest_Click);
            this.button_BackTest_Stop.Click += new System.EventHandler(this.button_BackTest_Stop_Click);
            this.Startup += new System.EventHandler(this.Sheet1_Startup);
            this.Shutdown += new System.EventHandler(this.Sheet1_Shutdown);

        }

        #endregion

        private void button_Start_BackTest_Click(object sender, EventArgs e)
        {
            string algorithm = ((Excel.Range)this.Cells[2, 1]).Value2.ToString();

            Console.WriteLine("Running " + algorithm + "...");

            // Setup the configuration, since the UX is not in the 
            // lean directory we write a new config in the UX output directory.
            // TODO > Most of this should be configured through a helper form in the UX.
            Config.Set("algorithm-type-name", algorithm);
            Config.Set("local", "true");
            Config.Set("live-mode", "false");
            Config.Set("messaging-handler", "QuantConnect.Messaging.Messaging");
            Config.Set("job-queue-handler", "QuantConnect.Queues.JobQueue");
            Config.Set("api-handler", "QuantConnect.Api.Api");
            Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.DesktopResultHandler");

            //Start default backtest.
            var engine = LaunchLean();
            button_Start_BackTest.Enabled = false;
            button_BackTest_Stop.Enabled = true;
            
            RunAlgo(engine);
        }

        private void RunAlgo(Engine engine)
        {
            _engine = engine;
            _resultsHandler = engine.AlgorithmHandlers.Results;

            //Setup Polling Events:
            _polling = new Timer();
            _polling.Interval = 1000;
            _polling.Tick += PollingOnTick;
            _polling.Start();
        }

        private static Engine LaunchLean()
        {
            //Launch the Lean Engine in another thread: this will run the algorithm specified above.
            // TODO > This should only be launched when clicking a backtest/trade live button provided in the UX.

            var systemHandlers = LeanEngineSystemHandlers.FromConfiguration(Composer.Instance);
            var algorithmHandlers = LeanEngineAlgorithmHandlers.FromConfiguration(Composer.Instance);
            var engine = new Engine(systemHandlers, algorithmHandlers, Config.GetBool("live-mode"));
            _leanEngineThread = new Thread(() =>
            {
                engine.Run();
            });
            _leanEngineThread.Start();

            return engine;
        }

        private void PollingOnTick(object sender, EventArgs eventArgs)
        {
            Packet message;
            if (_resultsHandler == null) return;
            DisplayStatistics();

            while (_resultsHandler.Messages.TryDequeue(out message))
            {
                //Process the packet request:
                switch (message.Type)
                {
                    case PacketType.BacktestResult:
                        //Draw chart
                        break;

                    case PacketType.LiveResult:
                        //Draw streaming chart
                        break;

                    case PacketType.AlgorithmStatus:
                        //Algorithm status update
                        break;

                    case PacketType.RuntimeError:
                        var runError = message as RuntimeErrorPacket;
                        if (runError != null) AppendMessage(runError.Message);
                        break;

                    case PacketType.HandledError:
                        var handledError = message as HandledErrorPacket;
                        if (handledError != null) AppendMessage(handledError.Message);
                        break;

                    case PacketType.Log:
                        var log = message as LogPacket;
                        if (log != null) AppendMessage(log.Message);
                        break;

                    case PacketType.Debug:
                        var debug = message as DebugPacket;
                        if (debug != null) AppendMessage(debug.Message);
                        break;

                    case PacketType.OrderEvent:
                        //New order event.
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        /// <summary>
        /// Write to the console in specific font color.
        /// </summary>
        /// <param name="message">String to append</param>
        /// <param name="color">Defaults to black</param>
        private void AppendMessage(string message)
        {
            message = DateTime.Now.ToString("u") + " " + message + Environment.NewLine;

            this.Cells[3, 3] += message;
        }

        private void DisplayStatistics()
        {
            var result = _resultsHandler as ConsoleResultHandler;
            if (!result.FinalStatistics.Any())
                return;

            int i = 4;
            foreach(var stat in result.FinalStatistics)
            {
                this.Cells[i, 3] = stat.Key;
                this.Cells[i, 4] = stat.Value;
                ++i;
            }

            AlgoEnd();
        }

        private void button_BackTest_Stop_Click(object sender, EventArgs e)
        {
            AlgoEnd();
        }

        private void AlgoEnd()
        {
            _polling.Stop();
            _engine.Dispose();
            button_Start_BackTest.Enabled = true;
            button_BackTest_Stop.Enabled = false;
        }
    }
}
