using System;
using System.Linq;
using Excel = Microsoft.Office.Interop.Excel;
using Office = Microsoft.Office.Core;

using QuantConnect;
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
        const int PnLStartColumn = 7;
        const int StatisticsStartColumn = 3;

        private Engine _engine;

        //Form Business Logic:
        private Timer _polling;
        private IResultHandler _resultsHandler;
        private static Thread _leanEngineThread;

        private Excel.ChartObject _pnLchart;

        private int _numOfChartSeries;

        private void Sheet1_Startup(object sender, System.EventArgs e)
        {
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
            Config.Set("result-handler", "QuantConnect.Lean.Engine.Results.ConsoleResultHandler");

            // Reset charting and performance area
            ResetResultArea();

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
            _polling = new Timer {Interval = 1000};
            _polling.Tick += PollingOnTick;
            _polling.Start();
        }

        private static Engine LaunchLean()
        {
            //Launch the Lean Engine in another thread: this will run the algorithm specified above.
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
            if (_resultsHandler == null) return;

            if (!_resultsHandler.IsActive)
            {
                DisplayStatistics();
                DoPlot();
                AlgoEnd();
            }
            
        }

        private void DisplayStatistics()
        {
            var result = _resultsHandler as ConsoleResultHandler;
            if (result != null && !result.FinalStatistics.Any())
                return;

            int i = 2;
            Cells[1, StatisticsStartColumn] = "Statistics: ";
            foreach(var stat in result.FinalStatistics)
            {
                Cells[i, StatisticsStartColumn] = stat.Key;
                Cells[i, StatisticsStartColumn + 1] = stat.Value;
                ++i;
            }
        }

        private void DoPlot()
        {
            var result = _resultsHandler as ConsoleResultHandler;

            var chart = _pnLchart.Chart;
            var oSeriesCollection = (Excel.SeriesCollection)chart.SeriesCollection();

            //// Set chart range.

            // Set chart properties.
            chart.ChartType = Microsoft.Office.Interop.Excel.XlChartType.xlLine;

            int j = PnLStartColumn;
            _numOfChartSeries = 0;
            foreach (var qcChart in result.Charts)
            {
                var series = qcChart.Value.Series;
                if (!series.ContainsKey("Daily Performance"))
                    continue;

                decimal portfolioValue = 10000;
                Excel.Series oSeries = oSeriesCollection.NewSeries();
                oSeries.Name = qcChart.Key;

                int i = 1;
                Cells[i, j] = "Date";
                Cells[i, j + 1] = qcChart.Key;
                foreach (var chartpoint in series["Daily Performance"].Values)
                {
                    i++;
                    portfolioValue *= (1 + chartpoint.y / 100);

                    // Only daily performance is needed.
                    Cells[i, j] = Time.UnixTimeStampToDateTime(chartpoint.x).Date;
                    Cells[i, j + 1] = portfolioValue;
                }

                oSeries.Values = (Excel.Range)this.get_Range(Cells[2, j + 1], Cells[i, j + 1]);
                oSeries.XValues = (Excel.Range)this.get_Range(Cells[2, j], Cells[i, j]);

                j += 2;
                _numOfChartSeries++;
            }
        }

        private void button_BackTest_Stop_Click(object sender, EventArgs e)
        {
            AlgoEnd();
        }

        private void AlgoEnd()
        {
            _polling.Stop();
            _engine.Dispose();

            Composer.Instance.Reset();
            button_Start_BackTest.Enabled = true;
            button_BackTest_Stop.Enabled = false;
        }

        private void ResetResultArea()
        {
            // Delete existing PnL chart.
            if (_pnLchart != null)
                _pnLchart.Delete();

            var charts = this.ChartObjects() as Excel.ChartObjects;
            _pnLchart = charts.Add(0, 280, 900, 300);

            // Clear the PnL area
            Excel.Range range = (Excel.Range)this.get_Range(Cells[2, StatisticsStartColumn], Cells[2, PnLStartColumn + 2 * _numOfChartSeries - 1]);
            range.EntireColumn.Delete();
            System.Runtime.InteropServices.Marshal.ReleaseComObject(range);
        }
    }
}
