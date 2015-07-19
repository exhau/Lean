using System;
using System.Collections.Generic;
using System.Globalization;
using QuantConnect.Algorithm;
using QuantConnect.Data;
using QuantConnect.Data.Custom;
using QuantConnect.Data.Market;
using QuantConnect.Orders;

namespace QuantConnect.Algorithm.CSharp.Amigo
{
    public class SPArbitrageDaily : QCAlgorithm
    {
        const string _indexFuture = "CHRIS/CME_ES2";
        const string _index = "SPY";
        const int  _multiplier = 10;

        int _unit = 10;

        enum CurrentPosition { LongIndex = 1, ShortIndex = -1, None = 0 };

        private DateTime _lastAction;
        private CurrentPosition _currentPosition = CurrentPosition.None;

        /// <summary>
        /// Initialize the data and resolution you require for your strategy
        /// </summary>
        public override void Initialize()
        {
            SetStartDate(2013, 10, 07);  //Set Start Date
            SetEndDate(2013, 10, 11);    //Set End Date
            SetCash(250000);
            AddData<QuandlFuture>(_indexFuture, Resolution.Daily);
            AddSecurity(SecurityType.Equity, _index, Resolution.Minute);
        }

        /// <summary>
        /// Data Event Handler: New data arrives here. "TradeBars" type is a dictionary of strings so you can access it by symbol.
        /// </summary>
        /// <param name="data">Data.</param>
        public void OnData(QuandlFuture data)
        {
            ExecutePair(DiscoverArbitrage(0.02));
        }

        public void OnData(TradeBars data)
        {
            if (_lastAction.Date == Time.Date) return;

            ExecutePair(DiscoverArbitrage(0.02));
        }

        private int DiscoverArbitrage(double thresholdPercentage)
        {
            if (thresholdPercentage < 0)
                throw new Exception("thresholdPercentage must not be negative.");

            double indexPrice = System.Convert.ToDouble(Securities[_index].Close) * _multiplier;
            double indexPriceForward = indexPrice * ForwardFactor(0.0, 0.0, 1.0); // TODO: interest, dividend and time to maturity
            double indexFuturesPrice = System.Convert.ToDouble(Securities[_indexFuture].Close);

            int result = 0;

            switch (_currentPosition)
	        {
		        case CurrentPosition.LongIndex:
                    if (indexFuturesPrice < indexPriceForward)
                        result = -1;
 
                    break;
                case CurrentPosition.ShortIndex:
                    if (indexFuturesPrice > indexPriceForward)
                        result = 1;

                    break;
                default:
                    break;
	        }

            if (_currentPosition == CurrentPosition.None || result != 0)
            {
                if (indexFuturesPrice - Fees(indexPrice, indexFuturesPrice, OrderDirection.Buy) > indexPriceForward * (1.0 + thresholdPercentage))
                    return ++result;

                if (indexFuturesPrice + Fees(indexPrice, indexFuturesPrice, OrderDirection.Sell) < indexPriceForward * (1.0 - thresholdPercentage))
                    return --result;
            }

            return result;
        }

        private void ExecutePair(int arbitrageDirection)
        {
            if (arbitrageDirection == 0)
                return;

            Order(_index, arbitrageDirection * _unit * _multiplier);
            Order(_indexFuture, -arbitrageDirection * _unit);

            _lastAction = Time;

            _currentPosition = _currentPosition + arbitrageDirection;

            Debug(Time.ToString("u") + " Current Position: " + _currentPosition.ToString());
        }

        private double ForwardFactor(double interestRate, double dividendYield, double timeToMaturity)
        {
            return Math.Exp((interestRate - dividendYield) * timeToMaturity);
        }

        private double Fees(double indexPrice, double indexFuturesPrice, OrderDirection direction)
        {
            return 0.0;
        }
    }

    /// <summary>
    /// Custom quandl data type for setting customized value column name. Value column is used for the primary trading calculations and charting.
    /// </summary>
    public class QuandlFuture : Quandl
    {
        private bool _isInitialized;
        private readonly List<string> _propertyNames = new List<string>();

        public decimal Low;
        public decimal High;

        /// <summary>
        /// Initializes a new instance of the <see cref="QuantConnect.QuandlFuture"/> class.
        /// </summary>
        public QuandlFuture()
            : base(valueColumnName: "Settle")
        {
        }

        public override BaseData Reader(SubscriptionDataConfig config, string line, DateTime date, bool isLiveMode)
        {
            var data = new QuandlFuture {Symbol = config.Symbol};
            var csv = line.Split(',');

            if (!_isInitialized)
            {
                _isInitialized = true;
                foreach (var propertyName in csv)
                {
                    var property = propertyName.TrimStart().TrimEnd();
                    // should we remove property names like Time?
                    // do we need to alias the Time??
                    data.SetProperty(property, 0m);
                    _propertyNames.Add(property);
                }
                return data;
            }

            data.Time = DateTime.ParseExact(csv[0], "yyyy-MM-dd", CultureInfo.InvariantCulture);

            for (var i = 1; i < csv.Length; i++)
            {
                var value = csv[i].ToDecimal();
                data.SetProperty(_propertyNames[i], value);
            }

            // we know that there is a close property, we want to set that to 'Value'
            data.Value = (decimal)data.GetProperty("Settle");
            data.Low = (decimal)data.GetProperty("Low");
            data.High = (decimal)data.GetProperty("High");

            return data;
        }
    }
}
