using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuantConnect.Data.Custom;
using QuantConnect.Indicators;

namespace QuantConnect.Algorithm.Amigo
{
    public class QCUMovingAverageCross : QCAlgorithm
    {
        private const string Symbol = "YAHOO/INDEX_SPY";

        private ExponentialMovingAverage fast;
        private ExponentialMovingAverage slow;
        private MovingAverageConvergenceDivergence macd;
        private SimpleMovingAverage[] ribbon;

        public override void Initialize()
        {
            // set up our analysis span
            SetStartDate(2011, 01, 01);
            SetEndDate(2015, 06, 10);
            SetCash(50000000);

            // request SPY data with minute resolution
            AddData<Quandl>(Symbol, Resolution.Daily, true, true);

            // create a 15 day exponential moving average
            fast = EMA(Symbol, 15, Resolution.Daily);
            // create a 12 26 9 MACD
            macd = MACD(Symbol, 12, 26, 9, MovingAverageType.Simple, Resolution.Daily);
            // create a 30 day exponential moving average
            slow = EMA(Symbol, 30, Resolution.Daily);

            // the following lines produce a simple moving average ribbon, this isn't
            // actually used in the algorithm's logic, but shows how easy it is to make
            // indicators and plot them!

            // note how we can easily define these indicators to receive hourly data
            int ribbonCount = 7;
            int ribbonInterval = 15 * 8;
            ribbon = new SimpleMovingAverage[ribbonCount];

            for (int i = 0; i < ribbonCount; i++)
            {
                ribbon[i] = SMA(Symbol, (i + 1) * ribbonInterval, Resolution.Daily); // 5-day SMA, 10-day SMA, 15-day SMA....
            }
        }

        private DateTime previous;
        public void OnData(Quandl data)
        {
            // a couple things to notice in this method:
            //  1. We never need to 'update' our indicators with the data, the engine takes care of this for us
            //  2. We can use indicators directly in math expressions
            //  3. We can easily plot many indicators at the same time

            // wait for our slow ema to fully initialize
            if (!slow.IsReady) return;

            // only once per day
            if (previous.Date == data.Time.Date) return;

            // define a small tolerance on our checks to avoid bouncing
            const decimal tolerance = 0.00015m;
            var holdings = Portfolio[Symbol].Quantity;

            // we only want to go long if we're currently short or flat
            if (holdings <= 0)
            {
                // if the fast is greater than the slow, we'll go long
                if (fast > slow * (1 + tolerance) && macd.Signal > (macd.Fast - macd.Slow))
                {
                    Log("BUY  >> " + Securities[Symbol].Price);
                    SetHoldings(Symbol, 1.0);
                }
            }

            // we only want to liquidate if we're currently long
            // if the fast is less than the slow we'll liquidate our long
            if (holdings > 0 && fast < slow)
            {
                Log("SELL >> " + Securities[Symbol].Price);
                Liquidate(Symbol);
            }

            Plot(Symbol, "Price", data.Price);
            Plot("Ribbon", "Price", data.Price);

            // easily plot indicators, the series name will be the name of the indicator
            Plot(Symbol, fast, slow, macd.Signal, macd.Fast, macd.Slow);
            Plot("Ribbon", ribbon);

            previous = data.Time;
        }
    }
}
