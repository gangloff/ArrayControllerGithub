using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;

namespace ArrayDACControl
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        private void SaveFluorLogButton_Click(object sender, EventArgs e)
        {
            System.IO.StreamWriter tw;

            //create writer with filename and date & time
            try
            {
                tw = new System.IO.StreamWriter(FluorLogPathName.Text + DateTime.Now.ToString("HHmmss") + ".txt");

                tw.WriteLine(DateTime.Now);

                //figure out number of plots to save
                int NumPlot = FluorescenceGraph.Plots.Count;

                double[] data;

                for (int i = 0; i < NumPlot; i++)
                {
                    data = FluorescenceGraph.Plots[i].GetYData();
                    for (int j = 0; j < data.Length; j++)
                    {
                        tw.Write(data[j] + ",");
                    }
                    tw.Write("\n");
                }

                tw.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void FluorescenceLogReset_Click(object sender, EventArgs e)
        {
            FluorescenceGraph.ClearData();
        }

        private void PositionLogReset_Click(object sender, EventArgs e)
        {
            PositionGraph.ClearData();
        }

        private void SavePositionLogButton_Click(object sender, EventArgs e)
        {
            System.IO.StreamWriter tw;

            //create writer with filename and date & time
            try
            {
                tw = new System.IO.StreamWriter(PositionLogPathName.Text + DateTime.Now.ToString("HHmmss") + ".txt");

                tw.WriteLine(DateTime.Now);

                //figure out number of plots to save
                int NumPlot = PositionGraph.Plots.Count;

                double[] data;

                for (int i = 0; i < NumPlot; i++)
                {
                    data = PositionGraph.Plots[i].GetYData();
                    for (int j = 0; j < data.Length; j++)
                    {
                        tw.Write(data[j] + ",");
                    }
                    tw.Write("\n");
                }

                tw.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

        }

    }
}