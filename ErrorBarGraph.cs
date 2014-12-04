using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

namespace ArrayDACControl
{
    public partial class ErrorBarGraph : UserControl
    {
        public ErrorBarGraph()
        {
            InitializeComponent();
        }

        //label
        public String theLabel
        {
            get
            {
                return theName.Text;
            }
            set
            {
                theName.Text = value;
            }
        }

        //XData
        public double[] theXData
        {
            get
            {
                return theXData;
            }
            set
            {
                theXData = value;
            }
        }

        //YData
        public double[] theYData
        {
            get
            {
                return theYData;
            }
            set
            {
                theYData = value;
            }
        }

        //Error Vector
        public double[] theErrData
        {
            get
            {
                return theErrData;
            }
            set
            {
                theErrData = value;
            }
        }

        public void PlotY(double[] theY, double[] theErr)
        {
            theYData = theY;
            theErrData = theErr;
            //plot
            PlotYData();
        }

        public void PlotYAverage(double[] theY, double[] theErr, int index)
        {
            if (index == 0)
            {
                PlotY(theY, theErr);
            }
            else
            {
                for (int i = 0; i < theYData.Length; i++)
                {
                    theYData[i] = (theYData[i] * index + theY[i]) / (index + 1);
                    theErrData[i] = Math.Sqrt((theErrData[i] * theErrData[i] * index + theErr[i] * theErr[i]) / (index + 1));
                }
                PlotYData();
            }
        }

        public void PlotXY(double[] theX, double[] theY, double[] theErr)
        {
            theXData = theX;
            theYData = theY;
            theErrData = theErr;
            //plot
            PlotXYData();
        }

        public void PlotXYAverage(double[] theX, double[] theY, double[] theErr, int index)
        {
            if (index == 0)
            {
                PlotXY(theX, theY, theErr);
            }
            else
            {
                theXData = theX;
                for (int i = 0; i < theXData.Length; i++)
                {
                    theYData[i] = (theYData[i] * index + theY[i]) / (index + 1);
                    theErrData[i] = Math.Sqrt((theErrData[i] * theErrData[i] * index + theErr[i] * theErr[i]) / (index + 1));
                }
                PlotXYData();
            }
        }

        private void PlotYData()
        {
            thescatterPlot.ClearData();

            for (int i = 0; i < theYData.Length; i++)
            {
                thescatterPlot.PlotXYAppend((double) i, theYData[i]);
                thescatterPlot.PlotXYAppend((double) i, theYData[i] - theErrData[i]);
                thescatterPlot.PlotXYAppend((double) i, theYData[i] + theErrData[i]);
                thescatterPlot.PlotXYAppend((double) i, theYData[i]);
            }
        }

        private void PlotXYData()
        {
            thescatterPlot.ClearData();

            for (int i = 0; i < theXData.Length; i++)
            {
                thescatterPlot.PlotXYAppend(theXData[i], theYData[i]);
                thescatterPlot.PlotXYAppend(theXData[i], theYData[i] - theErrData[i]);
                thescatterPlot.PlotXYAppend(theXData[i], theYData[i] + theErrData[i]);
                thescatterPlot.PlotXYAppend(theXData[i], theYData[i]);
            }
        }

    }
}
