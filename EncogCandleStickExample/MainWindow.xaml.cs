// Encog Simple Candlestick Example
// Copyright 2010 by Jeff Heaton (http://www.jeffheaton.com)
// See the copyright.txt in the distribution for a full listing of 
// individual contributors.
//
// This is free software; you can redistribute it and/or modify it
// under the terms of the GNU Lesser General Public License as
// published by the Free Software Foundation; either version 2.1 of
// the License, or (at your option) any later version.
//
// This software is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this software; if not, write to the Free
// Software Foundation, Inc., 51 Franklin St, Fifth Floor, Boston, MA
// 02110-1301 USA, or see the FSF site: http://www.fsf.org.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Encog.ML.Data;
using Encog.ML.Data.Market;
using Encog.ML.Data.Market.Loader;
using Encog.Neural.Networks;
using Encog.Neural.NeuralData;
using Encog.Util;
using Encog.Util.NetworkUtil;
using Encog.Util.Simple;
using Microsoft.Win32;

namespace EncogCandleStickExample
{
    /// <summary>
    ///     Interaction logic for Window1.xaml
    /// </summary>
    public partial class MainWindow
    {
        public const double FirstDayOffset = 32;
        public const double DayWidth = 10;
        public const double StickWidth = DayWidth*0.8;

        /// <summary>
        ///     The Bottom margin(where the months are displayed)
        /// </summary>
        private readonly int marginBottom;

        /// <summary>
        ///     Is the chart currently active.
        /// </summary>
        private bool chartActive;

        /// <summary>
        ///     The gather data dialog.
        /// </summary>
        private GatherData gather;

        /// <summary>
        ///     The loaded market data.
        /// </summary>
        private List<LoadedMarketData> marketData;

        /// <summary>
        ///     The number of days currently displayed.
        /// </summary>
        private int numberOfDays;

        /// <summary>
        ///     Maxmimum price displayed on the screen.
        /// </summary>
        private double priceMax;

        /// <summary>
        ///     Minimum price displayed on the screen.
        /// </summary>
        private double priceMin;

        /// <summary>
        ///     The starting day.
        /// </summary>
        private DateTime starting;

        public MainWindow()
        {
            InitializeComponent();
            priceMin = 0;
            priceMax = 300;
            marginBottom = 32;
            Util = new GatherUtil();
        }

        /// <summary>
        ///     Utility to gather data.
        /// </summary>
        public GatherUtil Util { get; set; }

        /// <summary>
        ///     The network to train.
        /// </summary>
        public BasicNetwork Network { get; set; }


        /// <summary>
        ///     The training data.
        /// </summary>
        public IMLDataSet Training { get; set; }

        /// <summary>
        ///     Convert a price to a y-location.
        /// </summary>
        /// <param name="price"> </param>
        /// <returns> The y-location of the price. </returns>
        private double ConvertPrice(double price)
        {
            price -= priceMin;
            double chartHeight = ChartCanvas.ActualHeight - marginBottom;
            double heightRatio = chartHeight/(priceMax - priceMin);
            double location = (price*heightRatio);
            location = (chartHeight - location);
            return location;
        }

        /// <summary>
        ///     Convert a day into an x-coordinate.
        /// </summary>
        /// <param name="index"> The zero-based index of the day. </param>
        /// <returns> The x-coordinate for the specified day. </returns>
        private double ConvertDay(int index)
        {
            return FirstDayOffset + (DayWidth/2) + (index*DayWidth);
        }

        /// <summary>
        ///     Draw a candle.
        /// </summary>
        /// <param name="dayIndex"> The day to draw it on. </param>
        /// <param name="open"> The opening price. </param>
        /// <param name="close"> The closing price. </param>
        /// <param name="dayHigh"> The day high. </param>
        /// <param name="dayLow"> The day low. </param>
        private void DrawCandle(int dayIndex, double open, double close, double dayHigh, double dayLow)
        {
            double chartHeight = ChartCanvas.ActualHeight;
            double heightRatio = chartHeight/(priceMax - priceMin);

            var l = new Line();
            double x = ConvertDay(dayIndex);
            l.X1 = x;
            l.X2 = x;
            l.Y1 = ConvertPrice(dayLow);
            l.Y2 = ConvertPrice(dayHigh);
            l.Stroke = Brushes.Black;
            ChartCanvas.Children.Add(l);

            var r = new Rectangle();
            double stickSize = Math.Abs(open - close)*heightRatio;
            double stickStart = Math.Max(open, close);
            r.Fill = open < close ? Brushes.White : Brushes.Black;

            if (stickSize < 1.0)
                stickSize = 1.0;

            r.Width = StickWidth;
            r.Height = stickSize;
            r.Stroke = Brushes.Black;
            r.SetValue(Canvas.LeftProperty, x - (StickWidth/2.0));
            r.SetValue(Canvas.TopProperty, ConvertPrice(stickStart));
            ChartCanvas.Children.Add(r);
        }

        /// <summary>
        ///     Load the market data.
        /// </summary>
        /// <returns> True if the data was loaded. </returns>
        private bool LoadMarketData()
        {
            try
            {
                IMarketLoader loader = new YahooFinanceLoader();
                var ticker = new TickerSymbol(Company.Text);
                IList<MarketDataType> needed = new List<MarketDataType>();
                needed.Add(MarketDataType.AdjustedClose);
                needed.Add(MarketDataType.Close);
                needed.Add(MarketDataType.Open);
                needed.Add(MarketDataType.High);
                needed.Add(MarketDataType.Low);
                DateTime from = starting - TimeSpan.FromDays(365);
                DateTime to = starting + TimeSpan.FromDays(365*2);
                marketData = (List<LoadedMarketData>) loader.Load(ticker, needed, from, to);
                marketData.Sort();

                numberOfDays = (int) ((ActualWidth - FirstDayOffset)/DayWidth);
                numberOfDays = Math.Min(numberOfDays, marketData.Count);
                return true;
            }
            catch (Exception e)
            {
                MessageBox.Show("Ticker symbol likely invalid.\n" + e.Message, "Error Loading Data");
                return false;
            }
        }

        /// <summary>
        ///     Draw the guide, days and prices.
        /// </summary>
        private void DrawGuide()
        {
            // price guide
            double breakPoint = priceMax - priceMin;
            breakPoint /= 10;

            for (int i = 0; i < 10; i++)
            {
                double price = priceMin + (i*breakPoint);
                var l = new Line
                            {
                                X1 = 0,
                                X2 = ChartCanvas.ActualWidth,
                                Y1 = ConvertPrice(price),
                                Y2 = ConvertPrice(price),
                                Stroke = Brushes.LightGray
                            };
                ChartCanvas.Children.Add(l);
                var label = new Label {Content = "" + (int) price};
                label.SetValue(Canvas.TopProperty, ConvertPrice(price) - 13);
                label.SetValue(Canvas.LeftProperty, 0.0);
                ChartCanvas.Children.Add(label);
            }

            int lastMonth = marketData[0].When.Month;

            // day guide
            int count = 0;
            foreach (LoadedMarketData data in marketData.Where(data => data.When.CompareTo(starting) > 0))
            {
                if (data.When.Month != lastMonth)
                {
                    double x = ConvertDay(count);
                    lastMonth = data.When.Month;
                    var l = new Line {X1 = x, X2 = x, Y1 = 0, Y2 = ActualHeight, Stroke = Brushes.LightGray};
                    ChartCanvas.Children.Add(l);

                    var label = new Label {Content = "" + data.When.Month + "/" + data.When.Year};
                    label.SetValue(Canvas.TopProperty, ChartCanvas.ActualHeight - marginBottom);
                    label.SetValue(Canvas.LeftProperty, x - 25);
                    ChartCanvas.Children.Add(label);
                }

                count++;
                if (count > numberOfDays)
                    break;
            }
        }

        /// <summary>
        ///     Auto-scale and calculate the price range.
        /// </summary>
        private void CalculatePriceRange()
        {
            double min = double.MaxValue;
            double max = double.MinValue;

            int count = 0;

            foreach (LoadedMarketData data in marketData)
            {
                if (data.When.CompareTo(starting) > 0)
                {
                    double low = data.GetData(MarketDataType.Low);
                    double high = data.GetData(MarketDataType.High);
                    min = Math.Min(min, low);
                    max = Math.Max(max, high);
                    count++;
                    if (count > numberOfDays)
                        break;
                }
            }

            double range = max - min;

            // adjust for small range
            if (range < 5)
            {
                max = min + 2;
                min = min - 2;
            }


            priceMax = max + (range*0.1);
            priceMin = min - (range*0.1);
        }

        /// <summary>
        ///     Draw the candle-chart.
        /// </summary>
        private void UpdateChart()
        {
            if (chartActive)
            {
                // obtain date
                string theStart = Start.Text;

                try
                {
                    starting = DateTime.Parse(theStart);
                }
                catch (Exception)
                {
                    MessageBox.Show("Please enter a valid date.");
                    return;
                }

                // plot it
                if (LoadMarketData())
                {
                    CalculatePriceRange();
                    ChartCanvas.Children.Clear();
                    DrawGuide();

                    int count = 0;
                    int i = 0;
                    double lastRatio = 0;
                    bool lastRatioDefined = false;

                    foreach (LoadedMarketData data in marketData)
                    {
                        if (data.When.CompareTo(starting) > 0)
                        {
                            // predict for this day
                            if (Network != null)
                            {
                                INeuralData input = Util.CreateData(marketData, i);
                                if (input != null)
                                {
                                    IMLData output = Network.Compute(input);
                                    double d = output[0];


                                    if (d < 0.2 || d > 0.8)
                                    {
                                        var r = new Rectangle();

                                        if (d < 0.5)
                                        {
                                            r.Fill = Brushes.Pink;
                                            r.Stroke = Brushes.Pink;
                                        }
                                        else
                                        {
                                            r.Fill = Brushes.LightGreen;
                                            r.Stroke = Brushes.LightGreen;
                                        }

                                        r.Width = StickWidth;
                                        r.Height = ConvertPrice(priceMin);
                                        r.SetValue(Canvas.LeftProperty, ConvertDay(count));
                                        r.SetValue(Canvas.TopProperty, 0.0);
                                        ChartCanvas.Children.Add(r);
                                    }
                                }
                            }

                            // draw the candle
                            DrawCandle(count, data.GetData(MarketDataType.Open),
                                       data.GetData(MarketDataType.Close),
                                       data.GetData(MarketDataType.High),
                                       data.GetData(MarketDataType.Low));

                            // was this a stock split?
                            double ratio = data.GetData(MarketDataType.Close)/data.GetData(MarketDataType.AdjustedClose);
                            if (!lastRatioDefined)
                            {
                                lastRatioDefined = true;
                                lastRatio = ratio;
                            }
                            else
                            {
                                if (Math.Abs(ratio - lastRatio) > 0.01)
                                {
                                    var line = new Line {X1 = ConvertDay(count)};
                                    line.X2 = line.X1;
                                    line.Y1 = 0;
                                    line.Y2 = ConvertPrice(priceMin);
                                    line.Stroke = Brushes.Yellow;
                                    ChartCanvas.Children.Add(line);
                                }
                                lastRatio = ratio;
                            }

                            count++;

                            if (count > numberOfDays)
                                break;
                        }
                        i++;
                    }
                }
            }
        }

        private void ChartClick(object sender, RoutedEventArgs e)
        {
            chartActive = true;
            UpdateChart();
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateChart();
        }

        private void MenuAboutClick(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "This example was created with the Encog AI Framework.\nFor more information visit: http://www.heatonresearch.com/encog/",
                "About Encog");
        }

        private void MenuFileQuitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void MenuNetObtainClick(object sender, RoutedEventArgs e)
        {
            if (gather == null)
                gather = new GatherData(this);
            gather.Show();
        }

        private void MenuNetTrainClick(object sender, RoutedEventArgs e)
        {
            if (Training == null)
            {
                MessageBox.Show("Can't train yet.  Obtain some data first.");
                return;
            }

            Network = EncogUtility.SimpleFeedForward(14, 100, 0, 1, false);
            EncogUtility.TrainDialog(Network, Training);
        }

        private void WindowClose(object sender, EventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void MenuFileOpenClick(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog {DefaultExt = ".eg", Filter = "Encog EG Files (.EG)|*.eg"};

            bool? result = dlg.ShowDialog();

            if (result != true) return;
            var inf = new FileInfo(dlg.FileName);
            if (inf.Directory != null)
            {
                BasicNetwork tempn = NetworkUtility.LoadNetwork(inf.Directory.ToString(), dlg.FileName);

                Network = tempn;
            }

            if (Network == null)
            {
                MessageBox.Show("This does not appear to be an EG file created for this example.");
                return;
            }


            Util = new GatherUtil();
            var xpa = new ParamsHolder(Network.Properties);

            Util.EvalWindow = xpa.GetInt("eval", true, 1);
            Util.PredictWindow = xpa.GetInt("predict", true, 1);

            Util.EvalWindow = xpa.GetInt("eval", true, 1);
            Util.PredictWindow = xpa.GetInt("predict", true, 1);
        }

        private void MenuFileSaveClick(object sender, RoutedEventArgs e)
        {
            if (Network == null)
            {
                MessageBox.Show("You must create a network before you save it.", "Error");
                return;
            }

            var dlg = new SaveFileDialog {DefaultExt = ".eg", Filter = "Encog EG Files (.EG)|*.eg"};

            bool? result = dlg.ShowDialog();

            if (result != true) return;
            //If we already have the keys ....we will update them only...
            if (!Network.Properties.ContainsKey("eval"))
                Network.Properties.Add("eval", Util.EvalWindow.ToString(CultureInfo.InvariantCulture));
                /*Update it then */
            else
                Network.Properties["eval"] = Util.EvalWindow.ToString(CultureInfo.InvariantCulture);

            /*Check for predict key */
            if (!Network.Properties.ContainsKey("predict"))
                Network.Properties.Add("predict", Util.PredictWindow.ToString(CultureInfo.InvariantCulture));
                //lets update it if it's already there.
            else
                Network.Properties["predict"] = Util.PredictWindow.ToString(CultureInfo.InvariantCulture);

            //Lets save....
            var inf = new FileInfo(dlg.FileName);
            if (inf.Directory != null)
            {
                BasicNetwork tempn = NetworkUtility.SaveNetwork(inf.Directory.ToString(), dlg.FileName,
                                                                Network);

                Network = tempn;
            }
        }
    }
}