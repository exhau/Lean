using QuantConnect.Data.Market;
using QuantConnect.Data.Custom;

namespace QuantConnect.Algorithm.Amigo
{
    public class AmigoTestAlgo : QCAlgorithm
    {
        private const string Symbol1 = "YAHOO/INDEX_SPY";
        private const string Symbol2 = "YAHOO/IBM";
        /// <summary>
        /// Initialise the data and resolution required, as well as the cash and start-end dates for your algorithm. All algorithms must initialized.
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(100000);             //Set Strategy Cash
            // Find more symbols here: http://quantconnect.com/data
            AddSecurity(SecurityType.Equity, "SPY", Resolution.Minute);
            AddSecurity(SecurityType.Equity, "IBM", Resolution.Minute);

            // request SPY data with minute resolution
            AddData<Quandl>(Symbol1, Resolution.Daily, true, true);
            // request SPY data with minute resolution
            AddData<Quandl>(Symbol2, Resolution.Daily, true, true);
        }

        /// <summary>
        /// OnData event is the primary entry point for your algorithm. Each new data point will be pumped in here.
        /// </summary>
        /// <param name="data">TradeBars IDictionary object with your stock data</param>
        public void OnData(TradeBars data)
        {
            if (!Portfolio.Invested)
            {
                SetHoldings("SPY", 1);
                Debug("Purchased Stock");
            }
        }

        public void OnData(Quandl data)
        {
            if (!Portfolio.Invested)
            {
                SetHoldings("SPY", 1);
                Debug("Purchased Stock");
            }
        }
    }
}
