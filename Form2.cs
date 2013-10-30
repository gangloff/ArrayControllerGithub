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

        private void SaveImageButton_Click(object sender, EventArgs e)
        {
            double[,] data = this.intensityPlot1.GetZData();

            try
            {
                System.IO.StreamWriter tw = new System.IO.StreamWriter(SaveImagePath.Text);

                tw.WriteLine(DateTime.Now);

                for (int i = 0; i < data.GetLength(0); i++)
                {
                    for (int j = 0; j < data.GetLength(1) - 1; j++)
                    {
                        tw.Write(data[i, j].ToString() + ",");
                    }
                    tw.WriteLine(data[i, data.GetLength(1) - 1]);
                }

                tw.Close();
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void PMTcountGraphClearButton_Click(object sender, EventArgs e)
        {
            PMTcountGraph.ClearData();
        }

        private void clearScanButton_Click(object sender, EventArgs e)
        {
            scatterGraph3.ClearData();
        }

        private void testlbl_Click(object sender, EventArgs e)
        {

        }

        private void label7_Click(object sender, EventArgs e)
        {

        }

        private void IonFocusGraphsClearButton_Click(object sender, EventArgs e)
        {
            xBalanceGraph.ClearData();
            xSpreadGraph.ClearData();
            yBalanceGraph.ClearData();
            ySpreadGraph.ClearData();
        }

    }
}