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
        delegate void MyDelegateThreadHelper(ThreadHelperClass theThreadHelper);

        public ErrorBarGraph ExpSeqErrorBarGraph;

        public Form2()
        {
            InitializeComponent();

            ExpSeqErrorBarGraph = new ErrorBarGraph();
            ExpSeqErrorBarGraph.theLabel = "Experimental Sequencer Averages";
            ExpSeqErrorBarGraph.Size = new System.Drawing.Size(729, 453);
            ExpSeqErrorBarGraph.Location = new System.Drawing.Point(191, 560);
            this.Controls.Add(ExpSeqErrorBarGraph);
            this.tabPage6.Controls.Add(ExpSeqErrorBarGraph);
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
            ScanResultsGraph.ClearData();
        }

        private void IonFocusGraphsClearButton_Click(object sender, EventArgs e)
        {
            xBalanceGraph.ClearData();
            xSpreadGraph.ClearData();
            yBalanceGraph.ClearData();
            ySpreadGraph.ClearData();
        }

        private void corrRecToggle_StateChanged(object sender, NationalInstruments.UI.ActionEventArgs e)
        {
            if (corrRecToggle.Value == false)
            {
                if (Form1.Self.LockinFrequencySwitch.Value == true)
                {
                    for (int i = 0; i < Form1.Self.ncorrbins; i++)
                    {
                        Array.Clear(Form1.Self.corrampCh1history[i], 0, Form1.Self.corrampCh1history[i].Length);
                        Array.Clear(Form1.Self.corrampCh2history[i], 0, Form1.Self.corrampCh2history[i].Length);
                    }
                    Form1.Self.historyCounter = 0;

                    Form1.Self.counterAmp = 0;
                    int nextx = corrAmpLog.Plots[0].HistoryCount;
                    double[] xnew = new double[4] { nextx, nextx, nextx, nextx };
                    double[] ynew = new double[4] { 0, 0, 0, 0 };
                    corrAmpLog.PlotXYAppend(xnew, ynew);
                }
                else
                {
                    Form1.Self.counterMu = 0;
                    int nextx = corrMuLog.Plots[0].HistoryCount;
                    double[] xnew = new double[4] { nextx, nextx, nextx, nextx };
                    double[] ynew = new double[4] { 0, 0, 0, 0 };
                    corrMuLog.PlotXYAppend(xnew, ynew);
                }

            }

        }

        private void clrCorrLog_Click(object sender, EventArgs e)
        {
            if (Form1.Self.LockinFrequencySwitch.Value == true)
                corrAmpLog.ClearData();
            else
                corrMuLog.ClearData();
        }

        private void ExpSeqViewScanIndex_TextChanged(object sender, EventArgs e)
        {
            try
            {
                this.Invoke(new MyDelegateThreadHelper(Form1.Self.ExpSeqViewScatterGraphUpdateCallbackFn), Form1.Self.ExperimentalSequencerThreadHelper);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
        }

        private void ExpSeqViewPMTConfig_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                this.Invoke(new MyDelegateThreadHelper(Form1.Self.ExpSeqViewScatterGraphUpdateCallbackFn), Form1.Self.ExperimentalSequencerThreadHelper);
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }

        }

        private void toolStripPropertyEditor25_SourceValueChanged(object sender, EventArgs e)
        {

        }

        private void CorrelatorDisplayMode_SelectedIndexChanged(object sender, EventArgs e)
        {
            for (int i = 0; i < scatterGraphNormCorrSig.Plots.Count; i++)
            {
                scatterGraphNormCorrSig.Plots[i].Visible = false;
                if (i < CorrelatorGraph.Plots.Count) CorrelatorGraph.Plots[i].Visible = false;
            }

            int[] indices = new int[CorrelatorDisplayMode.SelectedIndices.Count];
            CorrelatorDisplayMode.SelectedIndices.CopyTo(indices, 0);

            for( int i=0; i < indices.Length; i++)
            {
                scatterGraphNormCorrSig.Plots[indices[i]].Visible = true;
                if (indices[i] < CorrelatorGraph.Plots.Count) CorrelatorGraph.Plots[indices[i]].Visible = true;
            }
        }

    }
}