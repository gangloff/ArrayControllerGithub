using System;
namespace ArrayDACControl
{
    partial class ErrorBarGraph
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.thescatterGraph = new NationalInstruments.UI.WindowsForms.ScatterGraph();
            this.thescatterPlot = new NationalInstruments.UI.ScatterPlot();
            this.xAxis12 = new NationalInstruments.UI.XAxis();
            this.yAxis12 = new NationalInstruments.UI.YAxis();
            this.theName = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.thescatterGraph)).BeginInit();
            this.SuspendLayout();
            // 
            // thescatterGraph
            // 
            this.thescatterGraph.Location = new System.Drawing.Point(11, 29);
            this.thescatterGraph.Name = "thescatterGraph";
            this.thescatterGraph.Plots.AddRange(new NationalInstruments.UI.ScatterPlot[] {
            this.thescatterPlot});
            this.thescatterGraph.Size = new System.Drawing.Size(707, 413);
            this.thescatterGraph.TabIndex = 188;
            this.thescatterGraph.UseColorGenerator = true;
            this.thescatterGraph.XAxes.AddRange(new NationalInstruments.UI.XAxis[] {
            this.xAxis12});
            this.thescatterGraph.YAxes.AddRange(new NationalInstruments.UI.YAxis[] {
            this.yAxis12});
            // 
            // thescatterPlot
            // 
            this.thescatterPlot.LineColor = System.Drawing.Color.White;
            this.thescatterPlot.LineColorPrecedence = NationalInstruments.UI.ColorPrecedence.UserDefinedColor;
            this.thescatterPlot.LineWidth = 3F;
            this.thescatterPlot.PointColor = System.Drawing.Color.FromArgb(((int)(((byte)(255)))), ((int)(((byte)(128)))), ((int)(((byte)(0)))));
            this.thescatterPlot.PointSize = new System.Drawing.Size(15, 3);
            this.thescatterPlot.PointStyle = NationalInstruments.UI.PointStyle.SolidSquare;
            this.thescatterPlot.XAxis = this.xAxis12;
            this.thescatterPlot.YAxis = this.yAxis12;
            // 
            // theName
            // 
            this.theName.AutoSize = true;
            this.theName.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.theName.ForeColor = System.Drawing.Color.DarkRed;
            this.theName.Location = new System.Drawing.Point(7, 6);
            this.theName.Name = "theName";
            this.theName.Size = new System.Drawing.Size(81, 20);
            this.theName.TabIndex = 189;
            this.theName.Text = "theName";
            // 
            // ErrorBarGraph
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.theName);
            this.Controls.Add(this.thescatterGraph);
            this.Name = "ErrorBarGraph";
            this.Size = new System.Drawing.Size(729, 453);
            ((System.ComponentModel.ISupportInitialize)(this.thescatterGraph)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private NationalInstruments.UI.ScatterPlot thescatterPlot;
        private NationalInstruments.UI.XAxis xAxis12;
        private NationalInstruments.UI.YAxis yAxis12;
        private System.Windows.Forms.Label theName;
        private NationalInstruments.UI.WindowsForms.ScatterGraph thescatterGraph;
    }
}
